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
/// A modal dialog control supporting titles, supporting message text or custom content,
/// configurable action buttons, Material 3 elevation, and asynchronous lifecycle.
/// </summary>
public class Dialog : Control
{
    #region Bindable Properties

    public static readonly BindableProperty<string?> TitleProperty =
        BindableProperty.Register<Dialog, string?>(
            nameof(Title),
            null,
            (s, o, n) => ((Dialog)s).OnTitleChanged(o, n)
        );

    public static readonly BindableProperty<string?> MessageProperty =
        BindableProperty.Register<Dialog, string?>(
            nameof(Message),
            null,
            (s, o, n) => ((Dialog)s).OnMessageChanged(o, n)
        );

    public static readonly BindableProperty<UIElement?> ContentProperty =
        BindableProperty.Register<Dialog, UIElement?>(
            nameof(Content),
            null,
            (s, o, n) => ((Dialog)s).OnContentChanged(o, n)
        );

    public static readonly BindableProperty<DialogButtons> ButtonsPresetProperty =
        BindableProperty.Register<Dialog, DialogButtons>(
            nameof(ButtonsPreset),
            DialogButtons.None,
            (s, o, n) => ((Dialog)s).OnButtonsPresetChanged(o, n)
        );

    public static readonly BindableProperty<bool> CloseOnEscapeProperty =
        BindableProperty.Register<Dialog, bool>(
            nameof(CloseOnEscape),
            true
        );

    public static readonly BindableProperty<float> ElevationProperty =
        BindableProperty.Register<Dialog, float>(
            nameof(Elevation),
            6f,
            (s, o, n) => ((Dialog)s).InvalidateVisual()
        );

    public static readonly BindableProperty<Color> BorderBrushProperty =
        BindableProperty.Register<Dialog, Color>(
            nameof(BorderBrush),
            Color.Transparent,
            (s, o, n) => ((Dialog)s).InvalidateVisual()
        );

    public static readonly BindableProperty<Thickness> BorderThicknessProperty =
        BindableProperty.Register<Dialog, Thickness>(
            nameof(BorderThickness),
            Thickness.Zero,
            (s, o, n) => ((Dialog)s).InvalidateVisual()
        );

    #endregion

    #region Property Accessors

    public Color BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public UIElement? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public DialogButtons ButtonsPreset
    {
        get => GetValue(ButtonsPresetProperty);
        set => SetValue(ButtonsPresetProperty, value);
    }

    public bool CloseOnEscape
    {
        get => GetValue(CloseOnEscapeProperty);
        set => SetValue(CloseOnEscapeProperty, value);
    }

    public float Elevation
    {
        get => GetValue(ElevationProperty);
        set => SetValue(ElevationProperty, value);
    }

    public List<DialogButton> Buttons { get; } = [];

    #endregion

    private readonly StackPanel _rootStack;
    private readonly TextBlock _titleBlock;
    private readonly TextBlock _messageBlock;
    private readonly Border _contentWrapper;
    private readonly StackPanel _buttonsRow;

    private TaskCompletionSource<DialogResponse>? _tcs;
    private DialogHost? _hostingHost;

    public Dialog()
    {
        IsFocusable = true;
        CornerRadius = new CornerRadius(28); // Material Design 3 dialog radius
        Padding = new Thickness(24);
        MinWidth = 280f;
        MaxWidth = 560f;

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

    public Dialog(string title, string? message = null, DialogButtons preset = DialogButtons.None) : this()
    {
        Title = title;
        Message = message;
        ButtonsPreset = preset;
    }

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
    /// Displays this dialog by finding the nearest <see cref="DialogHost"/> ancestor of <paramref name="visualContext"/>.
    /// </summary>
    public Task<DialogResponse> ShowAsync(UIElement visualContext)
    {
        ArgumentNullException.ThrowIfNull(visualContext);
        var host = DialogHost.FindNearestHost(visualContext);
        if (host == null)
        {
            throw new InvalidOperationException("No DialogHost found in visual tree ancestors of the provided visual context.");
        }
        return ShowAsync(host);
    }

    /// <summary>
    /// Displays this dialog within the specified <see cref="DialogHost"/>.
    /// </summary>
    public Task<DialogResponse> ShowAsync(DialogHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (_tcs != null && !_tcs.Task.IsCompleted)
        {
            throw new InvalidOperationException("This Dialog is already active and displayed.");
        }

        _hostingHost = host;
        _tcs = new TaskCompletionSource<DialogResponse>();

        RebuildButtons();
        host.Dialog = this;

        return _tcs.Task;
    }

    /// <summary>
    /// Closes this dialog and resolves the active <see cref="ShowAsync"/> task with the specified response.
    /// </summary>
    public void Close(DialogResult result = DialogResult.None, DialogButton? button = null)
    {
        var host = _hostingHost ?? Parent as DialogHost;
        if (host != null && host.Dialog == this)
        {
            host.Dialog = null;
            _hostingHost = null;
        }

        _tcs?.TrySetResult(new DialogResponse(result, button));
        _tcs = null;
    }

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

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        _rootStack.Measure(availableSize.Deflate(padding));
        return _rootStack.DesiredSize.Inflate(padding.Horizontal, padding.Vertical);
    }

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
