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
/// A modal dialog control supporting titles, supporting message text or custom content,
/// configurable action buttons, Material 3 elevation, and asynchronous lifecycle.
/// </summary>
/// <remarks>
/// Show it in a <see cref="DialogHost"/> with <see cref="ShowAsync(DialogHost)"/>; the returned task completes when the
/// dialog closes, however it is removed from the host. <see cref="Closing"/> lets handlers veto a
/// <see cref="Close"/> (buttons, Enter, Escape and scrim clicks go through <see cref="Close"/>); removals by the host
/// (such as setting <see cref="DialogHost.Dialog"/> or the host leaving the tree) cannot be canceled and complete with
/// <see cref="DialogResult.None"/>.
/// </remarks>
public class Dialog : Control
{
    #region Bindable Properties

    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly BindableProperty<string?> TitleProperty =
        BindableProperty.Register<Dialog, string?>(
            nameof(Title),
            null,
            (s, o, n) => ((Dialog)s).OnTitleChanged(o, n)
        );

    /// <summary>Identifies the <see cref="Message"/> property.</summary>
    public static readonly BindableProperty<string?> MessageProperty =
        BindableProperty.Register<Dialog, string?>(
            nameof(Message),
            null,
            (s, o, n) => ((Dialog)s).OnMessageChanged(o, n)
        );

    /// <summary>Identifies the <see cref="Content"/> property.</summary>
    public static readonly BindableProperty<UIElement?> ContentProperty =
        BindableProperty.Register<Dialog, UIElement?>(
            nameof(Content),
            null,
            (s, o, n) => ((Dialog)s).OnContentChanged(o, n)
        );

    /// <summary>Identifies the <see cref="ButtonsPreset"/> property.</summary>
    public static readonly BindableProperty<DialogButtons> ButtonsPresetProperty =
        BindableProperty.Register<Dialog, DialogButtons>(
            nameof(ButtonsPreset),
            DialogButtons.None,
            (s, o, n) => ((Dialog)s).OnButtonsPresetChanged(o, n)
        );

    /// <summary>Identifies the <see cref="CloseOnEscape"/> property.</summary>
    public static readonly BindableProperty<bool> CloseOnEscapeProperty =
        BindableProperty.Register<Dialog, bool>(
            nameof(CloseOnEscape),
            true
        );

    /// <summary>Identifies the <see cref="Elevation"/> property.</summary>
    public static readonly BindableProperty<float> ElevationProperty =
        BindableProperty.Register<Dialog, float>(
            nameof(Elevation),
            6f,
            options: PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="BorderBrush"/> property.</summary>
    public static readonly BindableProperty<Color> BorderBrushProperty =
        BindableProperty.Register<Dialog, Color>(
            nameof(BorderBrush),
            Color.Transparent,
            options: PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="BorderThickness"/> property.</summary>
    public static readonly BindableProperty<Thickness> BorderThicknessProperty =
        BindableProperty.Register<Dialog, Thickness>(
            nameof(BorderThickness),
            new Thickness(1),
            options: PropertyOptions.AffectsRender
        );

    #endregion

    #region Property Accessors

    /// <summary>Gets or sets the outline color. The default (transparent) lets the theme choose the color.</summary>
    public Color BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    /// <summary>Gets or sets the outline thickness; the theme draws its left value. The default is 1; 0 draws no outline.</summary>
    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>Gets or sets the headline shown at the top; hidden when <c>null</c> or empty.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the supporting text; shown only when <see cref="Content"/> is <c>null</c>.</summary>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>Gets or sets custom content shown below the title, in place of <see cref="Message"/>.</summary>
    public UIElement? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets a standard button set. Setting it replaces <see cref="Buttons"/> with the preset's buttons. The
    /// default is <see cref="DialogButtons.None"/>.
    /// </summary>
    public DialogButtons ButtonsPreset
    {
        get => GetValue(ButtonsPresetProperty);
        set => SetValue(ButtonsPresetProperty, value);
    }

    /// <summary>
    /// Gets or sets whether Escape closes the dialog with the cancel button's result (or <see cref="DialogResult.Cancel"/>).
    /// The default is <c>true</c>.
    /// </summary>
    public bool CloseOnEscape
    {
        get => GetValue(CloseOnEscapeProperty);
        set => SetValue(CloseOnEscapeProperty, value);
    }

    /// <summary>Gets or sets the shadow depth; 0 draws no shadow. The default is 6.</summary>
    public float Elevation
    {
        get => GetValue(ElevationProperty);
        set => SetValue(ElevationProperty, value);
    }

    /// <summary>
    /// Gets the action buttons, in display order (left to right). After changing the list directly, call
    /// <see cref="RebuildButtons"/> (done automatically by <see cref="AddButton"/> and when the dialog is shown).
    /// </summary>
    public List<DialogButton> Buttons { get; } = [];

    /// <summary>Gets whether the dialog is currently shown in a <see cref="DialogHost"/>.</summary>
    public bool IsOpen => _hostingHost != null;

    #endregion

    #region Events

    /// <summary>Occurs after the dialog has been shown in a <see cref="DialogHost"/>.</summary>
    public event EventHandler? Opened;

    /// <summary>
    /// Occurs when <see cref="Close"/> is about to close the open dialog, with the pending response. Set
    /// <see cref="System.ComponentModel.CancelEventArgs.Cancel"/> to keep it open.
    /// </summary>
    public event EventHandler<DialogClosingEventArgs>? Closing;

    /// <summary>Occurs after the dialog has been removed from its host, with the response it completed with.</summary>
    public event EventHandler<DialogClosedEventArgs>? Closed;

    #endregion

    private readonly StackPanel _rootStack;
    private readonly TextBlock _titleBlock;
    private readonly TextBlock _messageBlock;
    private readonly Border _contentWrapper;
    private readonly StackPanel _buttonsRow;

    private TaskCompletionSource<DialogResponse>? _tcs;
    private DialogHost? _hostingHost;

    static Dialog()
    {
        CornerRadiusProperty.OverrideDefaultValue<Dialog>(new CornerRadius(28)); // Material Design 3 dialog radius
        PaddingProperty.OverrideDefaultValue<Dialog>(new Thickness(24));
        MinWidthProperty.OverrideDefaultValue<Dialog>(280f);
        MaxWidthProperty.OverrideDefaultValue<Dialog>(560f);
    }

    /// <summary>Initializes a new, empty <see cref="Dialog"/> without buttons.</summary>
    public Dialog()
    {
        IsFocusable = true;

        _titleBlock = new TextBlock
        {
            Bold = true,
            FontSize = 20f,
            Margin = new Thickness(0, 0, 0, 16),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };

        _messageBlock = new TextBlock
        {
            FontSize = 14f,
            Margin = new Thickness(0, 0, 0, 24),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };

        _contentWrapper = new Border
        {
            Margin = new Thickness(0, 0, 0, 24),
            Visibility = Visibility.Collapsed
        };

        _buttonsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8f,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        _rootStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0f
        };

        _rootStack.Add(_titleBlock);
        _rootStack.Add(_messageBlock);
        _rootStack.Add(_contentWrapper);
        _rootStack.Add(_buttonsRow);

        AddChild(_rootStack);
    }

    /// <summary>Initializes a new <see cref="Dialog"/> with a title, an optional message and a standard button set.</summary>
    /// <param name="title">The headline.</param>
    /// <param name="message">The supporting text, or <c>null</c>.</param>
    /// <param name="preset">The standard buttons to add.</param>
    public Dialog(string title, string? message = null, DialogButtons preset = DialogButtons.None) : this()
    {
        Title = title;
        Message = message;
        ButtonsPreset = preset;
    }

    /// <summary>Appends an action button and rebuilds the button row.</summary>
    /// <param name="text">The label text.</param>
    /// <param name="result">The result the dialog closes with when the button is clicked.</param>
    /// <param name="isDefault">Whether Enter triggers the button.</param>
    /// <param name="isCancel">Whether Escape triggers the button.</param>
    /// <param name="variant">The button's visual variant.</param>
    /// <param name="tag">Optional user data returned in <see cref="DialogResponse.Tag"/>.</param>
    /// <returns>This dialog, for chaining.</returns>
    public Dialog AddButton(
        string text,
        DialogResult result = DialogResult.Ok,
        bool isDefault = false,
        bool isCancel = false,
        ButtonVariant variant = ButtonVariant.Text,
        object? tag = null)
    {
        Buttons.Add(new DialogButton(text, result, isDefault, isCancel, variant, tag));
        RebuildButtons();
        return this;
    }

    private void OnTitleChanged(string? oldVal, string? newVal)
    {
        _titleBlock.Text = newVal ?? string.Empty;
        _titleBlock.Visibility = !string.IsNullOrEmpty(newVal) ? Visibility.Visible : Visibility.Collapsed;
        InvalidateMeasure();
    }

    private void OnMessageChanged(string? oldVal, string? newVal)
    {
        _messageBlock.Text = newVal ?? string.Empty;
        UpdateBodyVisibility();
    }

    private void OnContentChanged(UIElement? oldVal, UIElement? newVal)
    {
        _contentWrapper.Child = newVal;
        UpdateBodyVisibility();
    }

    private void UpdateBodyVisibility()
    {
        bool hasCustomContent = Content != null;
        _contentWrapper.Visibility = hasCustomContent ? Visibility.Visible : Visibility.Collapsed;

        bool hasMessage = !hasCustomContent && !string.IsNullOrEmpty(Message);
        _messageBlock.Visibility = hasMessage ? Visibility.Visible : Visibility.Collapsed;

        InvalidateMeasure();
    }

    private void OnButtonsPresetChanged(DialogButtons oldVal, DialogButtons newVal)
    {
        Buttons.Clear();

        switch (newVal)
        {
            case DialogButtons.Ok:
                Buttons.Add(new DialogButton("OK", DialogResult.Ok, isDefault: true, variant: ButtonVariant.Filled));
                break;

            case DialogButtons.OkCancel:
                Buttons.Add(new DialogButton("Cancel", DialogResult.Cancel, isCancel: true, variant: ButtonVariant.Text));
                Buttons.Add(new DialogButton("OK", DialogResult.Ok, isDefault: true, variant: ButtonVariant.Filled));
                break;

            case DialogButtons.YesNo:
                Buttons.Add(new DialogButton("No", DialogResult.No, isCancel: true, variant: ButtonVariant.Text));
                Buttons.Add(new DialogButton("Yes", DialogResult.Yes, isDefault: true, variant: ButtonVariant.Filled));
                break;

            case DialogButtons.YesNoCancel:
                Buttons.Add(new DialogButton("Cancel", DialogResult.Cancel, isCancel: true, variant: ButtonVariant.Text));
                Buttons.Add(new DialogButton("No", DialogResult.No, variant: ButtonVariant.Outlined));
                Buttons.Add(new DialogButton("Yes", DialogResult.Yes, isDefault: true, variant: ButtonVariant.Filled));
                break;
        }

        RebuildButtons();
    }

    /// <summary>
    /// Recreates the button row from <see cref="Buttons"/>. Each button closes the dialog with its result through
    /// <see cref="Close"/>.
    /// </summary>
    public void RebuildButtons()
    {
        _buttonsRow.Clear();

        for (int i = 0; i < Buttons.Count; i++)
        {
            var db = Buttons[i];
            var btn = new Button(db.Text)
            {
                Variant = db.Variant,
                Padding = new Thickness(16, 8)
            };

            btn.Click += (s, e) => Close(db.Result, db);
            _buttonsRow.Add(btn);
        }

        _buttonsRow.Visibility = Buttons.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        InvalidateMeasure();
    }

    /// <summary>
    /// Displays this dialog in the <see cref="DialogHost"/> found by <see cref="DialogHost.FindNearestHost"/> from
    /// <paramref name="visualContext"/> (normally its nearest host ancestor).
    /// </summary>
    /// <param name="visualContext">An element inside the target host, such as the control that triggered the dialog.</param>
    /// <returns>A task completing with the response when the dialog closes.</returns>
    /// <exception cref="InvalidOperationException">No suitable host was found, or the dialog is already open.</exception>
    public Task<DialogResponse> ShowAsync(UIElement visualContext)
    {
        ArgumentNullException.ThrowIfNull(visualContext);
        var host = DialogHost.FindNearestHost(visualContext);
        if (host == null)
        {
            throw new InvalidOperationException(
                "No DialogHost was found: the visual context has no DialogHost ancestor and no DialogHost is displayed in a window.");
        }
        return ShowAsync(host);
    }

    /// <summary>
    /// Displays this dialog within the specified <see cref="DialogHost"/>, on top of any dialogs already open there.
    /// </summary>
    /// <remarks>May be called from any thread; the dialog is shown on the UI thread.</remarks>
    /// <param name="host">The host to show the dialog in.</param>
    /// <returns>
    /// A task completing with the response when the dialog closes, however it is removed from the host (with
    /// <see cref="DialogResult.None"/> when it is removed without a result).
    /// </returns>
    /// <exception cref="InvalidOperationException">The dialog is already open.</exception>
    public Task<DialogResponse> ShowAsync(DialogHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (!Dispatcher.CheckAccess())
        {
            return Dispatcher.InvokeAsync(() => ShowAsync(host)).Unwrap();
        }

        if (_hostingHost != null)
        {
            throw new InvalidOperationException("This Dialog is already active and displayed.");
        }

        var tcs = new TaskCompletionSource<DialogResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _tcs = tcs;

        RebuildButtons();
        try
        {
            host.PushDialog(this);
        }
        catch
        {
            _tcs = null;
            throw;
        }

        return tcs.Task;
    }

    /// <summary>
    /// Closes this dialog and resolves the active <see cref="ShowAsync(DialogHost)"/> task with the specified response.
    /// </summary>
    /// <remarks>
    /// Raises <see cref="Closing"/> first; if a handler cancels, the dialog stays open. Dialogs stacked above this one
    /// are closed as well (with <see cref="DialogResult.None"/>). May be called from any thread; the close happens on the
    /// UI thread. Does nothing but complete a pending task if the dialog is not open.
    /// </remarks>
    /// <param name="result">The result code.</param>
    /// <param name="button">The button that was pressed, if any.</param>
    public void Close(DialogResult result = DialogResult.None, DialogButton? button = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Post(() => Close(result, button));
            return;
        }

        var response = new DialogResponse(result, button);
        var host = _hostingHost;
        if (host == null)
        {
            var tcs = _tcs;
            _tcs = null;
            tcs?.TrySetResult(response);
            return;
        }

        var closing = Closing;
        if (closing != null)
        {
            var args = new DialogClosingEventArgs(response);
            closing(this, args);
            if (args.Cancel || _hostingHost != host)
            {
                // Canceled, or a handler already closed the dialog.
                return;
            }
        }

        host.RemoveDialog(this, response);
    }

    // Called by the host once the dialog is part of its stack.
    internal void OnShownIn(DialogHost host)
    {
        _hostingHost = host;
    }

    internal void RaiseOpened() => Opened?.Invoke(this, EventArgs.Empty);

    // Called by the host after removing the dialog, whatever the reason: completes the pending task.
    internal void OnRemovedFromHost(DialogHost host, DialogResponse response)
    {
        if (_hostingHost != host)
        {
            return;
        }

        _hostingHost = null;
        var tcs = _tcs;
        _tcs = null;
        tcs?.TrySetResult(response);
        Closed?.Invoke(this, new DialogClosedEventArgs(response));
    }

    /// <summary>
    /// Handles Escape (closes with the cancel button's result when <see cref="CloseOnEscape"/> is set) and Enter
    /// (closes with the default button's result, if there is a default button).
    /// </summary>
    /// <param name="e">The key event; marked handled when the dialog closes.</param>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape && CloseOnEscape)
        {
            var cancelBtn = Buttons.Find(b => b.IsCancel);
            Close(cancelBtn?.Result ?? DialogResult.Cancel, cancelBtn);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            var defaultBtn = Buttons.Find(b => b.IsDefault);
            if (defaultBtn != null)
            {
                Close(defaultBtn.Result, defaultBtn);
                e.Handled = true;
                return;
            }
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        _rootStack.Measure(availableSize.Deflate(padding));
        return _rootStack.DesiredSize.Inflate(padding.Horizontal, padding.Vertical);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = Padding;
        _rootStack.Arrange(new Rect(Point.Zero, finalSize).Deflate(padding));
        return finalSize;
    }

    #region Static Helper Factories

    /// <summary>
    /// Shows a standard alert / information dialog with an OK button.
    /// </summary>
    public static Task<DialogResponse> Information(string title, string message, UIElement visualContext)
    {
        var dlg = new Dialog(title, message, DialogButtons.Ok);
        return dlg.ShowAsync(visualContext);
    }

    /// <summary>
    /// Shows a standard confirmation dialog with OK and Cancel buttons.
    /// </summary>
    public static Task<DialogResponse> Confirm(string title, string message, UIElement visualContext)
    {
        var dlg = new Dialog(title, message, DialogButtons.OkCancel);
        return dlg.ShowAsync(visualContext);
    }

    /// <summary>
    /// Shows a standard question dialog with Yes and No buttons.
    /// </summary>
    public static Task<DialogResponse> Question(string title, string message, UIElement visualContext)
    {
        var dlg = new Dialog(title, message, DialogButtons.YesNo);
        return dlg.ShowAsync(visualContext);
    }

    #endregion
}
