using System;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

public class Slider : Control
{
    public static readonly BindableProperty<float> MinimumProperty =
        BindableProperty.Register<Slider, float>(nameof(Minimum), 0f, (s, o, n) => ((Slider)s).InvalidateVisual());

    public static readonly BindableProperty<float> MaximumProperty =
        BindableProperty.Register<Slider, float>(nameof(Maximum), 100f, (s, o, n) => ((Slider)s).InvalidateVisual());

    public static readonly BindableProperty<float> ValueProperty =
        BindableProperty.Register<Slider, float>(
            nameof(Value),
            0f,
            (s, o, n) => ((Slider)s).OnValueChanged(o, n),
            coerceValue: (s, v) =>
            {
                var slider = (Slider)s;
                return Math.Clamp(v, slider.Minimum, slider.Maximum);
            }
        );

    public static readonly BindableProperty<bool> ShowValueIndicatorProperty =
        BindableProperty.Register<Slider, bool>(
            nameof(ShowValueIndicator),
            true,
            (s, o, n) =>
            {
                var slider = (Slider)s;
                slider.InvalidateMeasure();
                slider.InvalidateVisual();
            }
        );

    public static readonly BindableProperty<string> ValueFormatProperty =
        BindableProperty.Register<Slider, string>(
            nameof(ValueFormat),
            "{0:0}",
            (s, o, n) => ((Slider)s).InvalidateVisual()
        );

    public float Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public float Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public float Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool ShowValueIndicator { get => GetValue(ShowValueIndicatorProperty); set => SetValue(ShowValueIndicatorProperty, value); }
    public string ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    public float ValueIndicatorOpacity { get; internal set; } = 0f;
    private FloatAnimation? _indicatorFadeAnim;

    private static AnimationClock? _clock;
    public static void SetGlobalAnimationClock(AnimationClock clock) => _clock = clock;

    public event EventHandler<float>? ValueChanged;

    public float NormalizedValue
    {
        get
        {
            float range = Maximum - Minimum;
            if (range <= 0) return 0;
            return Math.Clamp((Value - Minimum) / range, 0f, 1f);
        }
    }

    private void OnValueChanged(float oldValue, float newValue)
    {
        InvalidateVisual();
        ValueChanged?.Invoke(this, newValue);
    }

    public Slider()
    {
        IsFocusable = true;
        PointerCaptureChanged += OnPointerCaptureChanged;
    }

    private void OnPointerCaptureChanged(UIElement? captured)
    {
        if (captured != this && ValueIndicatorOpacity > 0f && !IsPressed)
        {
            FadeValueIndicator(0f, 200);
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Button == PointerButtons.Left)
        {
            CapturePointer();
            e.Handled = true;
            UpdateValueFromPosition(e.Position);

            if (ShowValueIndicator)
            {
                FadeValueIndicator(1f, 150);
            }
        }
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (IsPressed || IsPointerCaptured)
        {
            e.Handled = true;
            UpdateValueFromPosition(e.Position);
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (IsPointerCaptured)
        {
            ReleasePointerCapture();
        }
        e.Handled = true;

        if (ShowValueIndicator)
        {
            FadeValueIndicator(0f, 200);
        }
    }

    public void FadeValueIndicator(float targetOpacity, int durationMs)
    {
        if (_clock == null)
        {
            ValueIndicatorOpacity = targetOpacity;
            InvalidateVisual();
            return;
        }

        FloatAnimation? anim = null;
        anim = new FloatAnimation(
            from: ValueIndicatorOpacity,
            to: targetOpacity,
            duration: TimeSpan.FromMilliseconds(durationMs),
            onUpdate: val =>
            {
                if (_indicatorFadeAnim == anim)
                {
                    ValueIndicatorOpacity = val;
                    InvalidateVisual();
                }
            },
            easing: Easing.EaseOutCubic
        );

        _indicatorFadeAnim = anim;
        _clock.Add(anim);
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        float step = (Maximum - Minimum) / 20f;
        if (step <= 0) step = 1f;

        switch (e.Key)
        {
            case Key.Left:
            case Key.Down:
                Value = Math.Max(Minimum, Value - step);
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Up:
                Value = Math.Min(Maximum, Value + step);
                e.Handled = true;
                break;
            case Key.Home:
                Value = Minimum;
                e.Handled = true;
                break;
            case Key.End:
                Value = Maximum;
                e.Handled = true;
                break;
        }
    }

    private void UpdateValueFromPosition(Point pos)
    {
        float width = Bounds.Width;
        if (width <= 0) return;

        float handleRadius = 10f;
        float usableWidth = width - handleRadius * 2;
        float ratio;
        if (usableWidth > 0)
        {
            ratio = Math.Clamp((pos.X - handleRadius) / usableWidth, 0f, 1f);
        }
        else
        {
            ratio = Math.Clamp(pos.X / width, 0f, 1f);
        }

        Value = Minimum + ratio * (Maximum - Minimum);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        float height = ShowValueIndicator ? 48f : 32f;
        return new Size(Math.Min(180, availableSize.Width), height);
    }
}

public class ProgressBar : Control
{
    public static readonly BindableProperty<float> MinimumProperty =
        BindableProperty.Register<ProgressBar, float>(nameof(Minimum), 0f, (s, o, n) => ((ProgressBar)s).InvalidateVisual());

    public static readonly BindableProperty<float> MaximumProperty =
        BindableProperty.Register<ProgressBar, float>(nameof(Maximum), 100f, (s, o, n) => ((ProgressBar)s).InvalidateVisual());

    public static readonly BindableProperty<float> ValueProperty =
        BindableProperty.Register<ProgressBar, float>(nameof(Value), 0f, (s, o, n) => ((ProgressBar)s).InvalidateVisual());

    public static readonly BindableProperty<bool> IsIndeterminateProperty =
        BindableProperty.Register<ProgressBar, bool>(nameof(IsIndeterminate), false, (s, o, n) => ((ProgressBar)s).InvalidateVisual());

    public float Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public float Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public float Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }

    public float NormalizedValue
    {
        get
        {
            float range = Maximum - Minimum;
            if (range <= 0) return 0;
            return Math.Clamp((Value - Minimum) / range, 0f, 1f);
        }
    }

    public float IndeterminateOffset { get; internal set; } = 0f;

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(Math.Min(180, availableSize.Width), 8);
    }
}
