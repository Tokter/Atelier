using System;
using System.Diagnostics;
using Atelier.Core.Events;
using Atelier.Core.Platform;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

public class TitleBarButton : Button
{
    public static readonly BindableProperty<bool> IsCloseButtonProperty =
        BindableProperty.Register<TitleBarButton, bool>(nameof(IsCloseButton), false, options: PropertyOptions.AffectsRender);

    public bool IsCloseButton
    {
        get => GetValue(IsCloseButtonProperty);
        set => SetValue(IsCloseButtonProperty, value);
    }

    public TitleBarButton()
    {
        Variant = ButtonVariant.Text;
        Width = 46f;
        Height = 44f;
        CornerRadius = CornerRadius.Zero;
        Padding = Thickness.Zero;
        IsFocusable = false;
    }
}

public class TitleBar : Control
{
    public static readonly BindableProperty<string> TitleProperty =
        BindableProperty.Register<TitleBar, string>(
            nameof(Title),
            string.Empty,
            (s, o, n) => ((TitleBar)s).OnTitleChanged(n)
        );

    public static readonly BindableProperty<object?> IconProperty =
        BindableProperty.Register<TitleBar, object?>(
            nameof(Icon),
            null,
            (s, o, n) => ((TitleBar)s).OnIconChanged(n)
        );

    public static readonly BindableProperty<object?> ContentProperty =
        BindableProperty.Register<TitleBar, object?>(
            nameof(Content),
            null,
            (s, o, n) => ((TitleBar)s).OnContentChanged(n)
        );

    public static readonly BindableProperty<bool> ShowMinimizeButtonProperty =
        BindableProperty.Register<TitleBar, bool>(
            nameof(ShowMinimizeButton),
            true,
            (s, o, n) => ((TitleBar)s)._minBtn.Visibility = n ? Visibility.Visible : Visibility.Collapsed
        );

    public static readonly BindableProperty<bool> ShowMaximizeButtonProperty =
        BindableProperty.Register<TitleBar, bool>(
            nameof(ShowMaximizeButton),
            true,
            (s, o, n) => ((TitleBar)s)._maxBtn.Visibility = n ? Visibility.Visible : Visibility.Collapsed
        );

    public static readonly BindableProperty<bool> ShowCloseButtonProperty =
        BindableProperty.Register<TitleBar, bool>(
            nameof(ShowCloseButton),
            true,
            (s, o, n) => ((TitleBar)s)._closeBtn.Visibility = n ? Visibility.Visible : Visibility.Collapsed
        );

    public static readonly BindableProperty<bool> IsMaximizedProperty =
        BindableProperty.Register<TitleBar, bool>(
            nameof(IsMaximized),
            false,
            (s, o, n) => ((TitleBar)s).OnIsMaximizedChanged(n)
        );

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public bool ShowMinimizeButton
    {
        get => GetValue(ShowMinimizeButtonProperty);
        set => SetValue(ShowMinimizeButtonProperty, value);
    }

    public bool ShowMaximizeButton
    {
        get => GetValue(ShowMaximizeButtonProperty);
        set => SetValue(ShowMaximizeButtonProperty, value);
    }

    public bool ShowCloseButton
    {
        get => GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    public bool IsMaximized
    {
        get => GetValue(IsMaximizedProperty);
        set => SetValue(IsMaximizedProperty, value);
    }

    // Optional overrides; by default the buttons act on the window hosting this title bar (see VisualNode.Host).
    public Action? OnMinimize { get; set; }
    public Action? OnMaximize { get; set; }
    public Action? OnClose { get; set; }
    public Action? OnDragMove { get; set; }

    private readonly DockPanel _layout = new() { LastChildFill = true };
    private readonly StackPanel _leftStack = new() { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
    private readonly StackPanel _rightStack = new() { Orientation = Orientation.Horizontal, Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentControl _contentPresenter = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };

    private readonly ContentControl _iconContainer = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _titleTextBlock = new() { FontSize = 14f, Bold = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };

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

    public TitleBarButton MinimizeButton => _minBtn;
    public TitleBarButton MaximizeButton => _maxBtn;
    public TitleBarButton CloseButton => _closeBtn;

    public Icon MinimizeIcon => _minIcon;
    public Icon MaximizeIcon => _maxIcon;
    public Icon CloseIcon => _closeIcon;

    private long _lastClickTicks;

    public TitleBar()
    {
        Height = 44f;

        _minBtn = new TitleBarButton { Content = _minIcon };
        _maxBtn = new TitleBarButton { Content = _maxIcon };
        _closeBtn = new TitleBarButton { IsCloseButton = true, Content = _closeIcon };

        // Assemble Left
        _leftStack.Add(_iconContainer);
        _leftStack.Add(_titleTextBlock);

        // Assemble Right
        _minBtn.Click += (s, e) => (OnMinimize ?? (() => TriggerWindowAction(w => w.Minimize())))();
        _maxBtn.Click += (s, e) => (OnMaximize ?? (() => TriggerWindowAction(w => w.ToggleMaximize())))();
        _closeBtn.Click += (s, e) => (OnClose ?? (() => TriggerWindowAction(w => w.Close())))();

        _rightStack.Add(_minBtn);
        _rightStack.Add(_maxBtn);
        _rightStack.Add(_closeBtn);

        // Assemble DockPanel
        DockPanel.SetDock(_leftStack, Dock.Left);
        DockPanel.SetDock(_rightStack, Dock.Right);

        _layout.Add(_leftStack);
        _layout.Add(_rightStack);
        _layout.Add(_contentPresenter);

        AddChild(_layout);
    }

    private void OnIsMaximizedChanged(bool isMax)
    {
        _maxIcon.Kind = isMax ? MaterialIconKind.FilterNone : MaterialIconKind.CropSquare;
    }

    // Acts on the window displaying this title bar (not the "current" window, which may be another one).
    private void TriggerWindowAction(Action<IHostWindow> action)
    {
        if (Host is { } host)
        {
            action(host);
            IsMaximized = host.IsMaximized;
        }
    }

    public void TriggerDragMove()
    {
        if (OnDragMove != null)
        {
            OnDragMove();
            return;
        }

        TriggerWindowAction(w => w.DragMove());
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.Button == PointerButtons.Left)
        {
            long now = Stopwatch.GetTimestamp();
            long elapsedMs = (now - _lastClickTicks) * 1000 / Stopwatch.Frequency;
            _lastClickTicks = now;

            if (elapsedMs < 350)
            {
                // Double click -> Toggle Maximize
                (OnMaximize ?? (() => TriggerWindowAction(w => w.ToggleMaximize())))();
            }
            else
            {
                // Drag move
                TriggerDragMove();
            }

            e.Handled = true;
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

    private void OnContentChanged(object? content)
    {
        _contentPresenter.Content = content;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _layout.Measure(availableSize);
        return _layout.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _layout.Arrange(new Rect(Point.Zero, finalSize));
        return finalSize;
    }
}