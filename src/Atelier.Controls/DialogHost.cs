using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// A container control placed in the visual tree that hosts child content and can display
/// modal dialogs on top while darkening the underlying content and blocking all input to it.
/// </summary>
/// <remarks>
/// Dialogs stack: showing a dialog while another is open (for example from one of its button handlers) puts the new one
/// on top, with the scrim between it and the dialogs below; closing it restores the previous dialog and its focus. Each
/// open dialog is a <see cref="FocusManager"/> modal scope. Every way a dialog leaves the host completes its
/// <see cref="Controls.Dialog.ShowAsync(DialogHost)"/> task: <see cref="Controls.Dialog.Close"/>, setting
/// <see cref="Dialog"/> or <see cref="IsOpen"/>, a scrim click, or the host being removed from a displayed tree (those
/// complete with <see cref="DialogResult.None"/> unless noted otherwise).
/// </remarks>
public class DialogHost : Control
{
    // Weak: hosts must not be kept alive after their window or page is gone. Dead entries are pruned whenever the list
    // reaches s_pruneThreshold, which then doubles relative to the survivors (amortized O(1) per host).
    private static readonly List<WeakReference<DialogHost>> _hostRefs = [];
    private static int s_pruneThreshold = 16;

    /// <summary>
    /// Optional delegate that returns the root visual node of the active window, searched by <see cref="FindNearestHost"/>
    /// when the start node has no host ancestor.
    /// </summary>
    public static Func<VisualNode?>? RootVisualProvider { get; set; }

    #region Bindable Properties

    /// <summary>Identifies the <see cref="Content"/> property.</summary>
    public static readonly BindableProperty<UIElement?> ContentProperty =
        BindableProperty.Register<DialogHost, UIElement?>(
            nameof(Content),
            null,
            (s, o, n) => ((DialogHost)s).OnContentChanged(o, n)
        );

    /// <summary>Identifies the <see cref="Dialog"/> property.</summary>
    public static readonly BindableProperty<UIElement?> DialogProperty =
        BindableProperty.Register<DialogHost, UIElement?>(
            nameof(Dialog),
            null,
            (s, o, n) => ((DialogHost)s).OnDialogChanged(o, n)
        );

    /// <summary>Identifies the <see cref="IsOpen"/> property.</summary>
    public static readonly BindableProperty<bool> IsOpenProperty =
        BindableProperty.Register<DialogHost, bool>(
            nameof(IsOpen),
            false,
            (s, o, n) => ((DialogHost)s).OnIsOpenChanged(o, n),
            coerceValue: static (s, v) => v && ((DialogHost)s)._dialogStack.Count > 0
        );

    /// <summary>Identifies the <see cref="CloseOnClickAway"/> property.</summary>
    public static readonly BindableProperty<bool> CloseOnClickAwayProperty =
        BindableProperty.Register<DialogHost, bool>(
            nameof(CloseOnClickAway),
            false
        );

    /// <summary>Identifies the <see cref="OverlayColor"/> property.</summary>
    public static readonly BindableProperty<Color> OverlayColorProperty =
        BindableProperty.Register<DialogHost, Color>(
            nameof(OverlayColor),
            Color.FromArgb(128, 0, 0, 0),
            (s, o, n) => ((DialogHost)s).OnOverlayColorChanged(o, n)
        );

    /// <summary>Identifies the <see cref="Identifier"/> property.</summary>
    public static readonly BindableProperty<string?> IdentifierProperty =
        BindableProperty.Register<DialogHost, string?>(
            nameof(Identifier),
            null
        );

    #endregion

    #region Property Accessors

    /// <summary>
    /// Gets or sets the primary child content displayed underneath any active dialog.
    /// </summary>
    public UIElement? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the topmost open dialog element (a <see cref="Controls.Dialog"/> or any other element).
    /// </summary>
    /// <remarks>
    /// Setting it replaces all open dialogs with the new one; setting <c>null</c> closes them all. Removed
    /// <see cref="Controls.Dialog"/>s complete with <see cref="DialogResult.None"/>. Use
    /// <see cref="Controls.Dialog.ShowAsync(DialogHost)"/> to stack a dialog on top of the open ones instead.
    /// </remarks>
    public UIElement? Dialog
    {
        get => GetValue(DialogProperty);
        set => SetValue(DialogProperty, value);
    }

    /// <summary>
    /// Gets or sets whether a dialog is open. Setting <c>false</c> closes all open dialogs (completing them with
    /// <see cref="DialogResult.None"/>); setting <c>true</c> has no effect unless a dialog is open.
    /// </summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Gets or sets whether clicking the darkened overlay dismisses the topmost dialog: a <see cref="Controls.Dialog"/>
    /// closes with <see cref="DialogResult.Cancel"/>, other content is removed. With it set, Escape also removes non-dialog
    /// content (a <see cref="Controls.Dialog"/> handles Escape itself, see <see cref="Controls.Dialog.CloseOnEscape"/>).
    /// The default is <c>false</c>.
    /// </summary>
    public bool CloseOnClickAway
    {
        get => GetValue(CloseOnClickAwayProperty);
        set => SetValue(CloseOnClickAwayProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the semi-transparent darkening scrim. The default is 50% black.
    /// </summary>
    public Color OverlayColor
    {
        get => GetValue(OverlayColorProperty);
        set => SetValue(OverlayColorProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional unique identifier used to locate this specific host in multi-host scenarios.
    /// </summary>
    public string? Identifier
    {
        get => GetValue(IdentifierProperty);
        set => SetValue(IdentifierProperty, value);
    }

    #endregion

    /// <summary>Gets the open dialogs, bottom to top; the last one is <see cref="Dialog"/>.</summary>
    public IReadOnlyList<UIElement> OpenDialogs => _dialogStack;

    /// <summary>Occurs after a dialog has been shown in this host.</summary>
    public event EventHandler<DialogHostEventArgs>? DialogOpened;

    /// <summary>Occurs after a dialog has been removed from this host, with the response it closed with.</summary>
    public event EventHandler<DialogHostEventArgs>? DialogClosed;

    private readonly DialogScrim _scrim;
    private readonly List<UIElement> _dialogStack = [];
    private readonly EventHandler<KeyEventArgs> _contentKeyDownHandler;

    // Set while the host itself writes Dialog/IsOpen to mirror the stack, so the change callbacks ignore those writes.
    private bool _syncingState;

    private static readonly DialogResponse NoneResponse = new(DialogResult.None);

    /// <summary>Initializes a new <see cref="DialogHost"/> without content or dialogs.</summary>
    public DialogHost()
    {
        _scrim = new DialogScrim(this)
        {
            Background = OverlayColor
        };
        _contentKeyDownHandler = OnContentKeyDown;
        RegisterHost(this);
    }

    private static void RegisterHost(DialogHost host)
    {
        if (_hostRefs.Count >= s_pruneThreshold)
        {
            _hostRefs.RemoveAll(static r => !r.TryGetTarget(out _));
            s_pruneThreshold = Math.Max(16, _hostRefs.Count * 2);
        }
        _hostRefs.Add(new WeakReference<DialogHost>(host));
    }

    private void OnContentChanged(UIElement? oldVal, UIElement? newVal)
    {
        if (oldVal != null)
        {
            RemoveChild(oldVal);
        }

        if (newVal != null)
        {
            InsertChild(0, newVal);
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnDialogChanged(UIElement? oldVal, UIElement? newVal)
    {
        if (_syncingState)
        {
            return;
        }

        // Set from outside: the new value replaces every open dialog.
        for (int i = _dialogStack.Count - 1; i >= 0; i--)
        {
            if (i < _dialogStack.Count && _dialogStack[i] != newVal)
            {
                RemoveAt(i, NoneResponse);
            }
        }

        if (newVal != null && !_dialogStack.Contains(newVal))
        {
            PushCore(newVal);
        }

        SyncState();
        RaiseOpenedIfTop(newVal);
    }

    private void OnIsOpenChanged(bool oldVal, bool newVal)
    {
        if (!_syncingState && !newVal)
        {
            CloseAllDialogs();
        }
    }

    private void OnOverlayColorChanged(Color oldVal, Color newVal)
    {
        _scrim.Background = newVal;
        _scrim.InvalidateVisual();
    }

    /// <summary>
    /// Closes all open dialogs, topmost first, completing <see cref="Controls.Dialog"/>s with <see cref="DialogResult.None"/>.
    /// </summary>
    public void CloseAllDialogs()
    {
        if (_dialogStack.Count == 0)
        {
            return;
        }

        while (_dialogStack.Count > 0)
        {
            RemoveAt(_dialogStack.Count - 1, NoneResponse);
        }
        SyncState();
    }

    /// <summary>Closes the open dialogs when the host leaves the displayed tree, so their tasks don't stay pending.</summary>
    protected override void OnDetachedFromVisualTree()
    {
        CloseAllDialogs();
        base.OnDetachedFromVisualTree();
    }

    // Shows a dialog on top of the open ones. Called by Dialog.ShowAsync.
    internal void PushDialog(UIElement dialog)
    {
        if (_dialogStack.Contains(dialog))
        {
            throw new InvalidOperationException("The dialog is already open in this DialogHost.");
        }

        PushCore(dialog);
        SyncState();
        RaiseOpenedIfTop(dialog);
    }

    // Removes a dialog (and the dialogs stacked above it) and completes it with the response. Called by Dialog.Close.
    internal bool RemoveDialog(UIElement dialog, DialogResponse response)
    {
        int index = _dialogStack.IndexOf(dialog);
        if (index < 0)
        {
            return false;
        }

        while (_dialogStack.Count - 1 > index)
        {
            RemoveAt(_dialogStack.Count - 1, NoneResponse);
        }
        RemoveAt(index, response);
        SyncState();
        return true;
    }

    private void PushCore(UIElement dialog)
    {
        _dialogStack.Add(dialog);
        _scrim.Background = OverlayColor;

        // Children: Content, lower dialogs..., scrim, topmost dialog. AddChild moves an existing child to the end.
        AddChild(_scrim);
        AddChild(dialog);

        if (dialog is Controls.Dialog d)
        {
            d.OnShownIn(this);
        }
        else
        {
            dialog.KeyDown += _contentKeyDownHandler;
        }

        FocusManager.PushModal(dialog);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void RaiseOpenedIfTop(UIElement? dialog)
    {
        if (dialog == null || _dialogStack.Count == 0 || _dialogStack[^1] != dialog)
        {
            return;
        }

        (dialog as Controls.Dialog)?.RaiseOpened();
        DialogOpened?.Invoke(this, new DialogHostEventArgs(dialog, null));
    }

    private void RemoveAt(int index, DialogResponse response)
    {
        var dialog = _dialogStack[index];
        _dialogStack.RemoveAt(index);

        FocusManager.PopModal(dialog);
        if (dialog is not Controls.Dialog)
        {
            dialog.KeyDown -= _contentKeyDownHandler;
        }
        RemoveChild(dialog);

        if (_dialogStack.Count > 0)
        {
            // Keep the scrim directly below the (new) topmost dialog. InsertChild removes the scrim before inserting.
            int topIndex = IndexOfChild(_dialogStack[^1]);
            int scrimIndex = IndexOfChild(_scrim);
            if (scrimIndex != topIndex - 1)
            {
                InsertChild(scrimIndex >= 0 && scrimIndex < topIndex ? topIndex - 1 : topIndex, _scrim);
            }
        }
        else
        {
            RemoveChild(_scrim);
        }

        InvalidateMeasure();
        InvalidateVisual();

        (dialog as Controls.Dialog)?.OnRemovedFromHost(this, response);
        DialogClosed?.Invoke(this, new DialogHostEventArgs(dialog, response));
    }

    private int IndexOfChild(VisualNode child)
    {
        var children = Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] == child)
            {
                return i;
            }
        }
        return -1;
    }

    // Mirrors the stack into the Dialog and IsOpen properties.
    private void SyncState()
    {
        bool wasSyncing = _syncingState;
        _syncingState = true;
        try
        {
            Dialog = _dialogStack.Count > 0 ? _dialogStack[^1] : null;
            IsOpen = _dialogStack.Count > 0;
        }
        finally
        {
            _syncingState = wasSyncing;
        }
    }

    // Escape on non-Dialog content that did not handle it itself.
    private void OnContentKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !e.Handled && CloseOnClickAway && sender is UIElement dialog
            && _dialogStack.Count > 0 && _dialogStack[^1] == dialog)
        {
            e.Handled = true;
            RemoveDialog(dialog, NoneResponse);
        }
    }

    private void DismissTopmost()
    {
        if (_dialogStack.Count == 0)
        {
            return;
        }

        var top = _dialogStack[^1];
        if (top is Controls.Dialog dlg)
        {
            dlg.Close(DialogResult.Cancel);
        }
        else
        {
            RemoveDialog(top, NoneResponse);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var content = Content;
        content?.Measure(availableSize);

        float width = content?.DesiredSize.Width ?? 0f;
        float height = content?.DesiredSize.Height ?? 0f;

        if (_dialogStack.Count > 0)
        {
            _scrim.Measure(availableSize);
            for (int i = 0; i < _dialogStack.Count; i++)
            {
                var dialog = _dialogStack[i];
                dialog.Measure(availableSize);
                width = Math.Max(width, dialog.DesiredSize.Width);
                height = Math.Max(height, dialog.DesiredSize.Height);
            }
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    /// <remarks>The content fills the host; every open dialog is centered at its desired size, capped to the host.</remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        Content?.Arrange(new Rect(Point.Zero, finalSize));

        if (_dialogStack.Count > 0)
        {
            _scrim.Arrange(new Rect(Point.Zero, finalSize));

            for (int i = 0; i < _dialogStack.Count; i++)
            {
                var dialog = _dialogStack[i];
                var dSize = dialog.DesiredSize;
                float w = Math.Min(dSize.Width, finalSize.Width);
                float h = Math.Min(dSize.Height, finalSize.Height);
                float x = Math.Max(0, (finalSize.Width - w) * 0.5f);
                float y = Math.Max(0, (finalSize.Height - h) * 0.5f);

                dialog.Arrange(new Rect(x, y, w, h));
            }
        }

        return finalSize;
    }

    /// <summary>
    /// Locates the <see cref="DialogHost"/> to show a dialog in: the nearest ancestor of <paramref name="startNode"/>
    /// (with the requested identifier), else one in the active window's tree (<see cref="RootVisualProvider"/>), else the
    /// most recently created host that is displayed in a window.
    /// </summary>
    /// <remarks>Hosts that are not part of a displayed tree are only found as ancestors of <paramref name="startNode"/>.</remarks>
    /// <param name="startNode">The node to start from, or <c>null</c> to skip the ancestor search.</param>
    /// <param name="hostIdentifier">The required <see cref="Identifier"/>, or <c>null</c> for any host.</param>
    /// <returns>The host, or <c>null</c> if none was found.</returns>
    public static DialogHost? FindNearestHost(VisualNode? startNode, string? hostIdentifier = null)
    {
        VisualNode? current = startNode;
        while (current != null)
        {
            if (current is DialogHost host)
            {
                if (hostIdentifier == null || host.Identifier == hostIdentifier)
                {
                    return host;
                }
            }
            current = current.Parent;
        }

        // Next: the active window's tree, so that with several windows a dialog opens where the user is working
        var root = RootVisualProvider?.Invoke();
        if (root != null)
        {
            var found = FindHostInSubtree(root, hostIdentifier);
            if (found != null) return found;
        }

        // Fallback: any registered host displayed in a window (with the requested identifier), most recent first.
        for (int i = _hostRefs.Count - 1; i >= 0; i--)
        {
            if (!_hostRefs[i].TryGetTarget(out var registered))
            {
                _hostRefs.RemoveAt(i);
                continue;
            }

            if ((hostIdentifier == null || registered.Identifier == hostIdentifier) && registered.IsAttachedToVisualTree)
            {
                return registered;
            }
        }

        return null;
    }

    private static DialogHost? FindHostInSubtree(VisualNode node, string? hostIdentifier)
    {
        if (node is DialogHost host && (hostIdentifier == null || host.Identifier == hostIdentifier))
        {
            return host;
        }

        for (int i = 0; i < node.Children.Count; i++)
        {
            var found = FindHostInSubtree(node.Children[i], hostIdentifier);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>
    /// Displays a dialog within the nearest <see cref="DialogHost"/> found from the specified <paramref name="visualContext"/>.
    /// </summary>
    /// <param name="dialog">The dialog to show.</param>
    /// <param name="visualContext">An element inside (or in the window of) the target host.</param>
    /// <returns>A task completing with the dialog's response when it closes.</returns>
    /// <exception cref="InvalidOperationException">No host was found, or the dialog is already open.</exception>
    public static Task<DialogResponse> ShowAsync(Dialog dialog, UIElement visualContext)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(visualContext);
        return dialog.ShowAsync(visualContext);
    }

    /// <summary>
    /// Displays a dialog within the displayed host with the given <paramref name="hostIdentifier"/>, or in the active
    /// window's host when it is <c>null</c> (see <see cref="FindNearestHost"/>).
    /// </summary>
    /// <param name="dialog">The dialog to show.</param>
    /// <param name="hostIdentifier">The <see cref="Identifier"/> of the target host, or <c>null</c>.</param>
    /// <returns>A task completing with the dialog's response when it closes.</returns>
    /// <exception cref="InvalidOperationException">No displayed host was found, or the dialog is already open.</exception>
    public static Task<DialogResponse> ShowAsync(Dialog dialog, string? hostIdentifier = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        var host = FindNearestHost(null, hostIdentifier);
        if (host == null)
        {
            throw new InvalidOperationException(
                string.IsNullOrEmpty(hostIdentifier)
                    ? "No DialogHost is displayed in a window. Add a DialogHost to the window's content, or pass the host explicitly."
                    : $"No displayed DialogHost with Identifier '{hostIdentifier}' was found.");
        }
        return dialog.ShowAsync(host);
    }

    /// <summary>
    /// Internal scrim element that darkens the background content and intercepts all pointer/wheel events.
    /// </summary>
    private class DialogScrim : Border
    {
        private readonly DialogHost _host;

        public DialogScrim(DialogHost host)
        {
            _host = host;
            IsHitTestVisible = true;
        }

        public override void OnPointerPressed(PointerEventArgs e)
        {
            e.Handled = true;
            if (_host.CloseOnClickAway)
            {
                _host.DismissTopmost();
            }
        }

        public override void OnPointerReleased(PointerEventArgs e)
        {
            e.Handled = true;
        }

        public override void OnPointerMoved(PointerEventArgs e)
        {
            e.Handled = true;
        }

        public override void OnPointerWheel(PointerWheelEventArgs e)
        {
            e.Handled = true;
        }

        public override UIElement? HitTest(Point point)
        {
            if (Visibility != Visibility.Visible || !IsHitTestVisible || !Bounds.Contains(point))
                return null;
            return this;
        }
    }
}
