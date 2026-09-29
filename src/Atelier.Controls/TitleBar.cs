using System;
using Atelier.Core.Events;
using Atelier.Core.Platform;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// A caption button of a <see cref="TitleBar"/> (minimize, maximize/restore or close): a flat, square-cornered
/// <see cref="Button"/> that never takes keyboard focus.
/// </summary>
/// <remarks>
/// Defaults: 46×44, <see cref="ButtonVariant.Text"/>, no corner radius or padding, not focusable. They are default
/// values, so styles can change them.
/// </remarks>
public class TitleBarButton : Button
{
    /// <summary>Identifies the <see cref="IsCloseButton"/> property.</summary>
    public static readonly BindableProperty<bool> IsCloseButtonProperty =
        BindableProperty.Register<TitleBarButton, bool>(nameof(IsCloseButton), false, options: PropertyOptions.AffectsRender);

    /// <summary>
    /// Gets or sets whether this is the close button, which turns red while hovered or pressed. The default is <c>false</c>.
    /// </summary>
    public bool IsCloseButton
    {
        get => GetValue(IsCloseButtonProperty);
        set => SetValue(IsCloseButtonProperty, value);
    }

    static TitleBarButton()
    {
        VariantProperty.OverrideDefaultValue<TitleBarButton>(ButtonVariant.Text);
        WidthProperty.OverrideDefaultValue<TitleBarButton>(46f);
        HeightProperty.OverrideDefaultValue<TitleBarButton>(44f);
        CornerRadiusProperty.OverrideDefaultValue<TitleBarButton>(CornerRadius.Zero);
        PaddingProperty.OverrideDefaultValue<TitleBarButton>(Thickness.Zero);
        IsFocusableProperty.OverrideDefaultValue<TitleBarButton>(false);
    }

    /// <summary>Initializes a new caption button.</summary>
    public TitleBarButton()
    {
    }
}

/// <summary>
/// A custom window title bar (client-side decoration) with an icon, a title, free content in the middle, and minimize,
/// maximize/restore and close buttons.
/// </summary>
/// <remarks>
/// <para>
/// By default the buttons act on the window displaying the title bar (see <see cref="VisualNode.Host"/>), which also
/// works with several windows open; set <see cref="OnMinimize"/>, <see cref="OnMaximize"/>, <see cref="OnClose"/> or
/// <see cref="OnDragMove"/> to replace an action. Pressing the left button on an empty part of the bar starts moving the
/// window, and double-clicking it toggles maximize.
/// </para>
/// <para>
/// <see cref="IsMaximized"/> follows the host window's state while the title bar is displayed, including changes made
/// outside the title bar (keyboard shortcuts, snapping), when the host raises
/// <see cref="IHostWindow.WindowStateChanged"/>. The title is drawn bold with the inherited <see cref="Control.FontSize"/>
/// and <see cref="Control.FontFamily"/>. The default height is 44.
/// </para>
/// <para>
/// When the window gets narrow, the parts give way in order: the title shortens (with an ellipsis) and then
/// disappears, the <see cref="Content"/> shrinks as far as its own layout allows (the narrowest width it doesn't ask
/// more than) and is then hidden, and last the icon and menu are cut off. The minimize, maximize and close buttons
/// always keep their place.
/// </para>
/// </remarks>
public class TitleBar : Control
{
    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly BindableProperty<string> TitleProperty =
        BindableProperty.Register<TitleBar, string>(
            nameof(Title),
            string.Empty,
            (s, o, n) => ((TitleBar)s).OnTitleChanged(n));

    /// <summary>Identifies the <see cref="Icon"/> property.</summary>
    public static readonly BindableProperty<object?> IconProperty =
        BindableProperty.Register<TitleBar, object?>(
            nameof(Icon),
            null,
            (s, o, n) => ((TitleBar)s).OnIconChanged(n));

    /// <summary>Identifies the <see cref="Content"/> property.</summary>
    public static readonly BindableProperty<object?> ContentProperty =
        BindableProperty.Register<TitleBar, object?>(
            nameof(Content),
            null,
            (s, o, n) => ((TitleBar)s)._contentPresenter.Content = n);

    /// <summary>Identifies the <see cref="Menu"/> property.</summary>
    public static readonly BindableProperty<Menu?> MenuProperty =
        BindableProperty.Register<TitleBar, Menu?>(
            nameof(Menu),
            null,
            (s, o, n) => ((TitleBar)s).OnMenuChanged(o, n));

    /// <summary>Identifies the <see cref="ShowMinimizeButton"/> property.</summary>
    public static readonly BindableProperty<bool> ShowMinimizeButtonProperty =
        BindableProperty.Register<TitleBar, bool>(
            nameof(ShowMinimizeButton),
            true,
            (s, o, n) => ((TitleBar)s)._minBtn.Visibility = n ? Visibility.Visible : Visibility.Collapsed);

    /// <summary>Identifies the <see cref="ShowMaximizeButton"/> property.</summary>
    public static readonly BindableProperty<bool> ShowMaximizeButtonProperty =
        BindableProperty.Register<TitleBar, bool>(
            nameof(ShowMaximizeButton),
            true,
            (s, o, n) => ((TitleBar)s)._maxBtn.Visibility = n ? Visibility.Visible : Visibility.Collapsed);

    /// <summary>Identifies the <see cref="ShowCloseButton"/> property.</summary>
    public static readonly BindableProperty<bool> ShowCloseButtonProperty =
        BindableProperty.Register<TitleBar, bool>(
            nameof(ShowCloseButton),
            true,
            (s, o, n) => ((TitleBar)s)._closeBtn.Visibility = n ? Visibility.Visible : Visibility.Collapsed);

    /// <summary>Identifies the <see cref="IsMaximized"/> property.</summary>
    public static readonly BindableProperty<bool> IsMaximizedProperty =
        BindableProperty.Register<TitleBar, bool>(
            nameof(IsMaximized),
            false,
            (s, o, n) => ((TitleBar)s)._maxIcon.Kind = n ? MaterialIconKind.FilterNone : MaterialIconKind.CropSquare);

    /// <summary>Gets or sets the window title shown next to the icon; hidden when empty (the default).</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the icon shown at the left: an element, a path to a .png/.jpg/.jpeg/.ico image (shown 20×20), or any
    /// other content. Hidden when <c>null</c> (the default).
    /// </summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets the content filling the space between the title and the caption buttons. When the window gets
    /// narrow it shrinks as far as its own layout allows (e.g. a search box in a star column down to its minimum width)
    /// and is then hidden as a whole.
    /// </summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets a menu bar shown after the icon, before the title (like many editors do). The bar's headers take
    /// clicks; the free space around them still moves the window. <c>null</c> (the default) for none.
    /// </summary>
    public Menu? Menu
    {
        get => GetValue(MenuProperty);
        set => SetValue(MenuProperty, value);
    }

    private void OnMenuChanged(Menu? oldMenu, Menu? newMenu)
    {
        if (oldMenu != null)
        {
            _leftStack.Remove(oldMenu);
        }
        if (newMenu != null)
        {
            newMenu.VerticalAlignment = VerticalAlignment.Center;
            _leftStack.InsertChild(1, newMenu); // after the icon, before the title
        }
    }

    /// <summary>Gets or sets whether the minimize button is shown. The default is <c>true</c>.</summary>
    public bool ShowMinimizeButton
    {
        get => GetValue(ShowMinimizeButtonProperty);
        set => SetValue(ShowMinimizeButtonProperty, value);
    }

    /// <summary>Gets or sets whether the maximize/restore button is shown. The default is <c>true</c>.</summary>
    public bool ShowMaximizeButton
    {
        get => GetValue(ShowMaximizeButtonProperty);
        set => SetValue(ShowMaximizeButtonProperty, value);
    }

    /// <summary>Gets or sets whether the close button is shown. The default is <c>true</c>.</summary>
    public bool ShowCloseButton
    {
        get => GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the window is shown as maximized, which switches the maximize button to a restore icon.
    /// Synchronized from the host window while displayed; set it yourself only when replacing <see cref="OnMaximize"/>
    /// for a host that doesn't report its state.
    /// </summary>
    public bool IsMaximized
    {
        get => GetValue(IsMaximizedProperty);
        set => SetValue(IsMaximizedProperty, value);
    }

    /// <summary>Gets or sets an action that replaces minimizing the host window.</summary>
    public Action? OnMinimize { get; set; }

    /// <summary>Gets or sets an action that replaces toggling maximize on the host window (button and double-click).</summary>
    public Action? OnMaximize { get; set; }

    /// <summary>Gets or sets an action that replaces closing the host window.</summary>
    public Action? OnClose { get; set; }

    /// <summary>Gets or sets an action that replaces moving the host window with the pointer.</summary>
    public Action? OnDragMove { get; set; }

    // The parts, left to right: icon and menu, title, content, and the caption buttons (see ArrangeOverride).
    private readonly StackPanel _leftStack = new() { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), ClipToBounds = true };
    private readonly StackPanel _rightStack = new() { Orientation = Orientation.Horizontal, Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentControl _contentPresenter = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch, ClipToBounds = true };

    // Collapsed until an icon or title is set, so they don't take up spacing.
    private readonly ContentControl _iconContainer = new() { VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly TextBlock _titleTextBlock = new() { Bold = true, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Visibility = Visibility.Collapsed };

    // Clips the title to the width the layout gives it (a text block draws its own text unclipped).
    private readonly ContentControl _titleHost = new() { VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };

    // Gaps between the parts, and the narrowest title worth showing (shorter, it disappears instead).
    private const float TitleGap = 8f;
    private const float ContentGap = 8f;
    private const float MinTitleWidth = 40f;

    // The widths the last measure gave the parts; 0 hides a part.
    private float _leftWidth, _titleWidth, _contentWidth, _rightWidth, _distributedWidth = -1;

    // The natural widths of the title and content (measured without a width limit), from the last measure.
    private float _titleNatural, _contentNatural, _contentMin;
    private bool _hasTitle, _hasContent;

    // What _contentMin was found for: the content and its natural width (a change of either searches again).
    private object? _contentMinFor;
    private float _contentMinNatural = -1;

    private readonly Icon _minIcon = new(MaterialIconKind.Minimize, 18f)
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    private readonly Icon _maxIcon = new(MaterialIconKind.CropSquare, 16f)
    {
        Fill = 0f,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    private readonly Icon _closeIcon = new(MaterialIconKind.Close, 18f)
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    private readonly TitleBarButton _minBtn;
    private readonly TitleBarButton _maxBtn;
    private readonly TitleBarButton _closeBtn;

    // The window whose state changes this title bar listens to; only set while displayed, so the window never keeps
    // a removed title bar alive.
    private IHostWindow? _observedHost;

    /// <summary>Gets the minimize button.</summary>
    public TitleBarButton MinimizeButton => _minBtn;

    /// <summary>Gets the maximize/restore button.</summary>
    public TitleBarButton MaximizeButton => _maxBtn;

    /// <summary>Gets the close button.</summary>
    public TitleBarButton CloseButton => _closeBtn;

    /// <summary>Gets the icon of the minimize button.</summary>
    public Icon MinimizeIcon => _minIcon;

    /// <summary>Gets the icon of the maximize/restore button.</summary>
    public Icon MaximizeIcon => _maxIcon;

    /// <summary>Gets the icon of the close button.</summary>
    public Icon CloseIcon => _closeIcon;

    static TitleBar()
    {
        HeightProperty.OverrideDefaultValue<TitleBar>(44f);
    }

    /// <summary>Initializes a new title bar without icon, title or content.</summary>
    public TitleBar()
    {
        _minBtn = new TitleBarButton { Content = _minIcon };
        _maxBtn = new TitleBarButton { Content = _maxIcon };
        _closeBtn = new TitleBarButton { IsCloseButton = true, Content = _closeIcon };

        _leftStack.Add(_iconContainer);

        _minBtn.Click += OnMinimizeButtonClick;
        _maxBtn.Click += OnMaximizeButtonClick;
        _closeBtn.Click += OnCloseButtonClick;

        _rightStack.Add(_minBtn);
        _rightStack.Add(_maxBtn);
        _rightStack.Add(_closeBtn);

        AddChild(_leftStack);
        _titleHost.Content = _titleTextBlock;
        AddChild(_titleHost);
        AddChild(_contentPresenter);
        AddChild(_rightStack); // last, so the caption buttons are drawn and hit-tested on top
    }

    private void OnMinimizeButtonClick(object? sender, EventArgs e)
    {
        if (OnMinimize != null)
        {
            OnMinimize();
        }
        else if (Host is { } host)
        {
            host.Minimize();
            SyncWithHost();
        }
    }

    private void OnMaximizeButtonClick(object? sender, EventArgs e) => ToggleMaximize();

    private void OnCloseButtonClick(object? sender, EventArgs e)
    {
        if (OnClose != null)
        {
            OnClose();
        }
        else
        {
            Host?.Close();
        }
    }

    private void ToggleMaximize()
    {
        if (OnMaximize != null)
        {
            OnMaximize();
        }
        else if (Host is { } host)
        {
            host.ToggleMaximize();
            SyncWithHost(); // for hosts that don't raise WindowStateChanged
        }
    }

    /// <summary>
    /// Starts moving the window with the pointer: calls <see cref="OnDragMove"/> if set, otherwise
    /// <see cref="IHostWindow.DragMove"/> on the host window. Call while the left pointer button is pressed.
    /// </summary>
    public void TriggerDragMove()
    {
        if (OnDragMove != null)
        {
            OnDragMove();
        }
        else
        {
            Host?.DragMove();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A left-button press on the bar itself (not on a button or interactive content) starts moving the window; the
    /// second press of a double click toggles maximize instead.
    /// </remarks>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || e.Button != PointerButtons.Left)
        {
            return;
        }

        e.Handled = true;
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else if (e.ClickCount <= 1)
        {
            TriggerDragMove();
        }
    }

    /// <inheritdoc/>
    /// <remarks>Starts following the host window's state.</remarks>
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        ObserveHost(Host);
        SyncWithHost();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree()
    {
        ObserveHost(null);
        base.OnDetachedFromVisualTree();
    }

    private void ObserveHost(IHostWindow? host)
    {
        if (_observedHost == host)
        {
            return;
        }

        if (_observedHost != null)
        {
            _observedHost.WindowStateChanged -= OnHostWindowStateChanged;
        }

        _observedHost = host;

        if (host != null)
        {
            host.WindowStateChanged += OnHostWindowStateChanged;
        }
    }

    private void OnHostWindowStateChanged(object? sender, EventArgs e) => SyncWithHost();

    private void SyncWithHost()
    {
        if (Host is { } host)
        {
            IsMaximized = host.IsMaximized;
        }
    }

    private void OnTitleChanged(string title)
    {
        _titleTextBlock.Text = title;
        _titleTextBlock.Visibility = string.IsNullOrEmpty(title) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnIconChanged(object? icon)
    {
        if (icon is string path && (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                   path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                   path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                   path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)))
        {
            _iconContainer.Content = new Image(path) { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center };
        }
        else
        {
            _iconContainer.Content = icon;
        }
        _iconContainer.Visibility = icon != null ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Shares the width by priority: the caption buttons get theirs, then the icon and menu, then the content and the
    /// title get the rest; the title shortens first, the content is hidden once it can't shrink any further.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var unbounded = new Size(float.PositiveInfinity, availableSize.Height);
        _rightStack.Measure(unbounded);
        _leftStack.Measure(unbounded);
        _titleHost.Measure(unbounded);
        _contentPresenter.Measure(unbounded);

        _hasTitle = _titleTextBlock.Visibility == Visibility.Visible;
        _hasContent = _contentPresenter.Content != null;
        _titleNatural = _hasTitle ? _titleHost.DesiredSize.Width : 0;
        _contentNatural = _hasContent ? _contentPresenter.DesiredSize.Width : 0;
        _contentMin = _hasContent ? ContentMinWidth(availableSize.Height) : 0;
        float height = Math.Max(Math.Max(_rightStack.DesiredSize.Height, _leftStack.DesiredSize.Height),
            Math.Max(_titleHost.DesiredSize.Height, _contentPresenter.DesiredSize.Height));

        float naturalWidth = _leftStack.DesiredSize.Width + (_hasTitle ? TitleGap + _titleNatural : 0)
            + (_hasContent ? ContentGap + _contentNatural : 0) + _rightStack.DesiredSize.Width;
        float width = float.IsFinite(availableSize.Width) ? availableSize.Width : naturalWidth;
        Distribute(width, availableSize.Height);

        return new Size(width, float.IsFinite(availableSize.Height) ? Math.Min(height, availableSize.Height) : height);
    }

    // The narrowest width the content fits in (it doesn't ask for more than it gets), found by halving the range between
    // nothing and its natural width. Searched again only when the content or its natural width changes.
    private float ContentMinWidth(float height)
    {
        if (ReferenceEquals(_contentMinFor, _contentPresenter.Content) && Math.Abs(_contentMinNatural - _contentNatural) < 0.5f)
        {
            return _contentMin;
        }

        float fits = _contentNatural, tooNarrow = 0;
        for (int i = 0; i < 16 && fits - tooNarrow > 1; i++)
        {
            float width = (fits + tooNarrow) / 2;
            _contentPresenter.Measure(new Size(width, height));
            if (_contentPresenter.DesiredSize.Width <= width + 0.5f) fits = width;
            else tooNarrow = width;
        }

        _contentMinFor = _contentPresenter.Content;
        _contentMinNatural = _contentNatural;
        return MathF.Ceiling(fits);
    }

    // Shares width among the parts (see MeasureOverride) and measures the title and content at the widths they get.
    private void Distribute(float width, float height)
    {
        _distributedWidth = width;

        // 1. The caption buttons, always in full.
        _rightWidth = Math.Min(_rightStack.DesiredSize.Width, width);
        float rest = width - _rightWidth;

        // 2. The icon and menu (cut off only when even they don't fit).
        _leftWidth = Math.Min(_leftStack.DesiredSize.Width, rest);
        rest -= _leftWidth;

        // 3. The content if it fits at its minimum width; the title then gets what the content can spare, the content the
        //    rest. Else the content is hidden and the title gets it all.
        float contentSpace = _hasContent ? rest - ContentGap : 0;
        if (_hasContent && _contentMin <= contentSpace)
        {
            _titleWidth = TitleWidth(contentSpace - _contentMin - TitleGap);
            _contentWidth = contentSpace - (_titleWidth > 0 ? _titleWidth + TitleGap : 0);
        }
        else
        {
            _contentWidth = 0;
            _titleWidth = TitleWidth(rest - TitleGap);
        }

        // The title at the width it gets (it trims); the content filling its slot.
        if (_titleWidth > 0) _titleHost.Measure(new Size(_titleWidth, height));
        if (_contentWidth > 0) _contentPresenter.Measure(new Size(_contentWidth, height));

        float TitleWidth(float space) =>
            _hasTitle && space >= Math.Min(MinTitleWidth, _titleNatural) ? Math.Min(_titleNatural, space) : 0;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        // Parents may arrange at another width than they measured with: share the width actually given.
        if (Math.Abs(finalSize.Width - _distributedWidth) > 0.5f) Distribute(finalSize.Width, finalSize.Height);

        float h = finalSize.Height;
        float x = 0;
        _leftStack.Arrange(new Rect(x, 0, _leftWidth, h));
        x += _leftWidth;

        if (_titleWidth > 0) x += TitleGap;
        _titleHost.Arrange(new Rect(x, 0, _titleWidth, h));
        x += _titleWidth;

        if (_contentWidth > 0) x += ContentGap;
        _contentPresenter.Arrange(new Rect(x, 0, _contentWidth, h));

        _rightStack.Arrange(new Rect(finalSize.Width - _rightWidth, 0, _rightWidth, h));
        return finalSize;
    }
}
