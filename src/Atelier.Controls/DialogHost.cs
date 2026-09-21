using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Threading;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// A container control placed in the visual tree that hosts child content and can display
/// modal dialogs on top while darkening the underlying content and blocking all input to it.
/// </summary>
public class DialogHost : Control
{
    private static readonly List<DialogHost> _activeHosts = [];

    /// <summary>
    /// Optional delegate that returns the root visual node of the window for fallback host resolution.
    /// </summary>
    public static Func<VisualNode?>? RootVisualProvider { get; set; }

    #region Bindable Properties

    public static readonly BindableProperty<UIElement?> ContentProperty =
        BindableProperty.Register<DialogHost, UIElement?>(
            nameof(Content),
            null,
            (s, o, n) => ((DialogHost)s).OnContentChanged(o, n)
        );

    public static readonly BindableProperty<UIElement?> DialogProperty =
        BindableProperty.Register<DialogHost, UIElement?>(
            nameof(Dialog),
            null,
            (s, o, n) => ((DialogHost)s).OnDialogChanged(o, n)
        );

    public static readonly BindableProperty<bool> IsOpenProperty =
        BindableProperty.Register<DialogHost, bool>(
            nameof(IsOpen),
            false,
            (s, o, n) => ((DialogHost)s).OnIsOpenChanged(o, n)
        );

    public static readonly BindableProperty<bool> CloseOnClickAwayProperty =
        BindableProperty.Register<DialogHost, bool>(
            nameof(CloseOnClickAway),
            false
        );

    public static readonly BindableProperty<Color> OverlayColorProperty =
        BindableProperty.Register<DialogHost, Color>(
            nameof(OverlayColor),
            Color.FromArgb(128, 0, 0, 0),
            (s, o, n) => ((DialogHost)s).OnOverlayColorChanged(o, n)
        );

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
    /// Gets or sets the currently active modal dialog element. Setting to null closes the dialog.
    /// </summary>
    public UIElement? Dialog
    {
        get => GetValue(DialogProperty);
        set => SetValue(DialogProperty, value);
    }

    /// <summary>
    /// Gets or sets whether a dialog is currently open.
    /// </summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Gets or sets whether clicking the darkened overlay area dismisses the active dialog.
    /// </summary>
    public bool CloseOnClickAway
    {
        get => GetValue(CloseOnClickAwayProperty);
        set => SetValue(CloseOnClickAwayProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the semi-transparent darkening scrim.
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

    private readonly DialogScrim _scrim;
    private UIElement? _previousFocused;

    public DialogHost()
    {
        _scrim = new DialogScrim(this)
        {
            Background = OverlayColor
        };
        _activeHosts.Add(this);
    }

    private void OnContentChanged(UIElement? oldVal, UIElement? newVal)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Post(() => OnContentChanged(oldVal, newVal));
            return;
        }

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
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Post(() => OnDialogChanged(oldVal, newVal));
            return;
        }

        if (oldVal != null)
        {
            FocusManager.PopModal(oldVal);
            RemoveChild(oldVal);
        }

        if (newVal != null)
        {
            IsOpen = true;
            _scrim.Background = OverlayColor;

            // Ensure _scrim is placed right after Content
            if (!Children.Contains(_scrim))
            {
                AddChild(_scrim);
            }

            // Ensure Dialog is added on top of _scrim
            if (!Children.Contains(newVal))
            {
                AddChild(newVal);
            }

            _previousFocused = FocusManager.CurrentFocused;
            FocusManager.PushModal(newVal);
        }
        else
        {
            IsOpen = false;
            if (Children.Contains(_scrim))
            {
                RemoveChild(_scrim);
            }

            if (_previousFocused != null)
            {
                FocusManager.SetFocus(_previousFocused);
                _previousFocused = null;
            }
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnIsOpenChanged(bool oldVal, bool newVal)
    {
        if (!newVal && Dialog != null)
        {
            Dialog = null;
        }
    }

    private void OnOverlayColorChanged(Color oldVal, Color newVal)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Post(() => OnOverlayColorChanged(oldVal, newVal));
            return;
        }

        _scrim.Background = newVal;
        _scrim.InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Content?.Measure(availableSize);

        float width = Content?.DesiredSize.Width ?? 0f;
        float height = Content?.DesiredSize.Height ?? 0f;

        if (Dialog != null)
        {
            _scrim.Measure(availableSize);
            Dialog.Measure(availableSize);

            width = Math.Max(width, Dialog.DesiredSize.Width);
            height = Math.Max(height, Dialog.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Content?.Arrange(new Rect(Point.Zero, finalSize));

        if (Dialog != null)
        {
            _scrim.Arrange(new Rect(Point.Zero, finalSize));

            var dSize = Dialog.DesiredSize;
            float w = Math.Min(dSize.Width, finalSize.Width);
            float h = Math.Min(dSize.Height, finalSize.Height);
            float x = Math.Max(0, (finalSize.Width - w) * 0.5f);
            float y = Math.Max(0, (finalSize.Height - h) * 0.5f);

            Dialog.Arrange(new Rect(x, y, w, h));
        }

        return finalSize;
    }

    /// <summary>
    /// Traverses up the visual tree starting at <paramref name="startNode"/> to locate the nearest <see cref="DialogHost"/>.
    /// </summary>
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

        // Search active hosts if identifier was explicitly specified
        if (!string.IsNullOrEmpty(hostIdentifier))
        {
            for (int i = _activeHosts.Count - 1; i >= 0; i--)
            {
                if (_activeHosts[i].Identifier == hostIdentifier)
                {
                    return _activeHosts[i];
                }
            }
        }

        // Fallback: search tree from root visual provider if available
        var root = RootVisualProvider?.Invoke();
        if (root != null)
        {
            var found = FindHostInSubtree(root, hostIdentifier);
            if (found != null) return found;
        }

        // Fallback: If only one active host exists, return it
        if (_activeHosts.Count > 0 && hostIdentifier == null)
        {
            return _activeHosts[^1];
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
    public static Task<DialogResponse> ShowAsync(Dialog dialog, UIElement visualContext)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(visualContext);
        return dialog.ShowAsync(visualContext);
    }

    /// <summary>
    /// Displays a dialog within the specified <see cref="DialogHost"/>, or the default host if <paramref name="hostIdentifier"/> is null.
    /// </summary>
    public static Task<DialogResponse> ShowAsync(Dialog dialog, string? hostIdentifier = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        var host = FindNearestHost(null, hostIdentifier);
        if (host == null)
        {
            throw new InvalidOperationException(
                string.IsNullOrEmpty(hostIdentifier)
                    ? "No DialogHost was found in the visual tree or active registry."
                    : $"No DialogHost with Identifier '{hostIdentifier}' was found.");
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
                if (_host.Dialog is Dialog dlg)
                {
                    dlg.Close(DialogResult.Cancel);
                }
                else
                {
                    _host.Dialog = null;
                }
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
