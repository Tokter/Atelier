using System;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

public class Switch : Control
{
    public static readonly BindableProperty<bool> IsCheckedProperty =
        BindableProperty.Register<Switch, bool>(
            nameof(IsChecked),
            false,
            (s, o, n) => ((Switch)s).OnIsCheckedChanged(o, n)
        );

    public static readonly BindableProperty<object?> ContentProperty =
        BindableProperty.Register<Switch, object?>(
            nameof(Content),
            null,
            (s, o, n) => ((Switch)s).OnContentChanged(o, n)
        );

    public static readonly BindableProperty<bool> ShowThumbIconProperty =
        BindableProperty.Register<Switch, bool>(
            nameof(ShowThumbIcon),
            false,
            options: PropertyOptions.AffectsRender
        );

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public bool ShowThumbIcon
    {
        get => GetValue(ShowThumbIconProperty);
        set => SetValue(ShowThumbIconProperty, value);
    }

    public float ThumbAnimationProgress { get; private set; } = 0f;

    public event EventHandler<bool>? CheckedChanged;

    private static AnimationClock? _clock;
    public static void SetGlobalAnimationClock(AnimationClock? clock) => _clock = clock;

    static Switch()
    {
        PaddingProperty.OverrideDefaultValue<Switch>(new Thickness(0, 4));
    }

    public Switch()
    {
        IsFocusable = true;
    }

    public Switch(string text) : this()
    {
        Content = new TextBlock(text);
    }

    private void OnContentChanged(object? oldContent, object? newContent)
    {
        if (oldContent is UIElement oldElement) RemoveChild(oldElement);
        if (newContent is string str)
        {
            AddChild(new TextBlock(str));
        }
        else if (newContent is UIElement newElement)
        {
            AddChild(newElement);
        }
        InvalidateMeasure();
    }

    protected virtual void OnIsCheckedChanged(bool oldValue, bool newValue)
    {
        float target = newValue ? 1f : 0f;
        if (_clock == null)
        {
            ThumbAnimationProgress = target;
            InvalidateVisual();
        }
        else
        {
            var anim = new FloatAnimation(
                ThumbAnimationProgress,
                target,
                TimeSpan.FromMilliseconds(200),
                p =>
                {
                    ThumbAnimationProgress = p;
                    InvalidateVisual();
                },
                Easing.EmphasizedDecelerate
            );

            _clock.Add(anim);
        }

        CheckedChanged?.Invoke(this, newValue);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        bool wasPressed = IsPressed;
        base.OnPointerReleased(e);
        e.Handled = true;

        if (wasPressed && IsHovered)
        {
            IsChecked = !IsChecked;
        }
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsEnabled && (e.Key is Key.Space or Key.Enter))
        {
            IsChecked = !IsChecked;
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var pad = Padding;
        float trackW = 40f;
        float trackH = 22f;
        float spacing = 10f;

        if (Children.Count > 0 && Children[0] is UIElement child && child.Visibility != Visibility.Collapsed)
        {
            child.Measure(new Size(Math.Max(0, availableSize.Width - trackW - spacing - pad.Horizontal), Math.Max(0, availableSize.Height - pad.Vertical)));
            return new Size(
                pad.Left + trackW + spacing + child.DesiredSize.Width + pad.Right,
                pad.Top + Math.Max(trackH, child.DesiredSize.Height) + pad.Bottom
            );
        }

        return new Size(pad.Left + trackW + pad.Right, pad.Top + trackH + pad.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var pad = Padding;
        float trackW = 40f;
        float spacing = 10f;

        if (Children.Count > 0 && Children[0] is UIElement child && child.Visibility != Visibility.Collapsed)
        {
            float childX = pad.Left + trackW + spacing;
            float childY = pad.Top + (finalSize.Height - pad.Vertical - child.DesiredSize.Height) * 0.5f;
            child.Arrange(new Rect(childX, childY, child.DesiredSize.Width, child.DesiredSize.Height));
        }

        return finalSize;
    }
}
