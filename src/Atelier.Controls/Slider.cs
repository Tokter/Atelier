using System;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Controls;

/// <summary>
/// A horizontal Material Design 3 slider for picking a value from a continuous or stepped range.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Value"/> is always kept between <see cref="Minimum"/> and <see cref="Maximum"/>: a value outside the range
/// is clamped, and the requested value comes back if the range later allows it (as in WPF). When
/// <see cref="Maximum"/> is below <see cref="Minimum"/>, the range is treated as empty and <see cref="Value"/> equals
/// <see cref="Minimum"/>.
/// </para>
/// <para>
/// Pressing the left pointer button moves the thumb to the pointer and starts a drag. From the keyboard, the arrow keys
/// change the value by <see cref="SmallChange"/>, Page Up/Page Down by <see cref="LargeChange"/>, and Home/End jump to
/// the ends of the range. With <see cref="IsSnapToTickEnabled"/>, pointer and keyboard changes land on multiples of
/// <see cref="TickFrequency"/> from <see cref="Minimum"/>. A disabled slider ignores input.
/// </para>
/// </remarks>
public class Slider : Control
{
    /// <summary>Identifies the <see cref="Minimum"/> property.</summary>
    public static readonly BindableProperty<float> MinimumProperty =
        BindableProperty.Register<Slider, float>(nameof(Minimum), 0f, OnRangeChanged, options: PropertyOptions.AffectsRender, validateValue: IsFiniteValue);

    /// <summary>Identifies the <see cref="Maximum"/> property.</summary>
    public static readonly BindableProperty<float> MaximumProperty =
        BindableProperty.Register<Slider, float>(nameof(Maximum), 100f, OnRangeChanged, options: PropertyOptions.AffectsRender, validateValue: IsFiniteValue);

    /// <summary>Identifies the <see cref="Value"/> property.</summary>
    public static readonly BindableProperty<float> ValueProperty =
        BindableProperty.Register<Slider, float>(
            nameof(Value),
            0f,
            (s, o, n) => ((Slider)s).OnValueChanged(n),
            coerceValue: (s, v) => ((Slider)s).ClampToRange(v),
            options: PropertyOptions.AffectsRender,
            validateValue: IsNotNaN);

    /// <summary>Identifies the <see cref="SmallChange"/> property.</summary>
    public static readonly BindableProperty<float> SmallChangeProperty =
        BindableProperty.Register<Slider, float>(nameof(SmallChange), 1f, validateValue: IsNonNegativeFinite);

    /// <summary>Identifies the <see cref="LargeChange"/> property.</summary>
    public static readonly BindableProperty<float> LargeChangeProperty =
        BindableProperty.Register<Slider, float>(nameof(LargeChange), 10f, validateValue: IsNonNegativeFinite);

    /// <summary>Identifies the <see cref="TickFrequency"/> property.</summary>
    public static readonly BindableProperty<float> TickFrequencyProperty =
        BindableProperty.Register<Slider, float>(nameof(TickFrequency), 1f, options: PropertyOptions.AffectsRender, validateValue: IsNonNegativeFinite);

    /// <summary>Identifies the <see cref="IsSnapToTickEnabled"/> property.</summary>
    public static readonly BindableProperty<bool> IsSnapToTickEnabledProperty =
        BindableProperty.Register<Slider, bool>(nameof(IsSnapToTickEnabled), false, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="ShowValueIndicator"/> property.</summary>
    public static readonly BindableProperty<bool> ShowValueIndicatorProperty =
        BindableProperty.Register<Slider, bool>(nameof(ShowValueIndicator), true, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="ValueFormat"/> property.</summary>
    public static readonly BindableProperty<string> ValueFormatProperty =
        BindableProperty.Register<Slider, string>(
            nameof(ValueFormat),
            "{0:0}",
            (s, o, n) => ((Slider)s)._valueText = null,
            options: PropertyOptions.AffectsRender);

    /// <summary>Gets or sets the lowest value. The default is 0.</summary>
    public float Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <summary>Gets or sets the highest value. The default is 100.</summary>
    public float Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>Gets or sets the current value, kept within <see cref="Minimum"/>..<see cref="Maximum"/>. The default is 0.</summary>
    public float Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Gets or sets the step of the arrow keys. The default is 1.</summary>
    public float SmallChange { get => GetValue(SmallChangeProperty); set => SetValue(SmallChangeProperty, value); }

    /// <summary>Gets or sets the step of Page Up and Page Down. The default is 10.</summary>
    public float LargeChange { get => GetValue(LargeChangeProperty); set => SetValue(LargeChangeProperty, value); }

    /// <summary>
    /// Gets or sets the distance between ticks, counted from <see cref="Minimum"/>. Only used with
    /// <see cref="IsSnapToTickEnabled"/>; 0 disables snapping. The default is 1.
    /// </summary>
    public float TickFrequency { get => GetValue(TickFrequencyProperty); set => SetValue(TickFrequencyProperty, value); }

    /// <summary>
    /// Gets or sets whether pointer and keyboard changes snap to the nearest tick (see <see cref="TickFrequency"/>); the
    /// arrow keys then move one tick. Values set from code or bindings are not snapped. The default is <c>false</c>.
    /// </summary>
    public bool IsSnapToTickEnabled { get => GetValue(IsSnapToTickEnabledProperty); set => SetValue(IsSnapToTickEnabledProperty, value); }

    /// <summary>
    /// Gets or sets whether a bubble showing the value appears above the thumb while it is dragged. The default is <c>true</c>.
    /// </summary>
    public bool ShowValueIndicator { get => GetValue(ShowValueIndicatorProperty); set => SetValue(ShowValueIndicatorProperty, value); }

    /// <summary>
    /// Gets or sets the composite format string (with the value as argument 0) for the value indicator. The default is
    /// <c>"{0:0}"</c>.
    /// </summary>
    public string ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    /// <summary>Gets the current opacity of the value indicator bubble, from 0 (hidden) to 1.</summary>
    public float ValueIndicatorOpacity { get; internal set; }

    /// <summary>
    /// Gets <see cref="Value"/> formatted with <see cref="ValueFormat"/>. The string is cached and only rebuilt after
    /// either changes, so renderers can read it every frame.
    /// </summary>
    public string ValueText => _valueText ??= string.Format(ValueFormat, Value);

    /// <summary>Occurs when <see cref="Value"/> changes, with the new value.</summary>
    public event EventHandler<float>? ValueChanged;

    /// <summary>
    /// Gets the position of <see cref="Value"/> within the range, from 0 at <see cref="Minimum"/> to 1 at
    /// <see cref="Maximum"/>; 0 for an empty range.
    /// </summary>
    public float NormalizedValue
    {
        get
        {
            float range = Maximum - Minimum;
            if (range <= 0) return 0;
            return Math.Clamp((Value - Minimum) / range, 0f, 1f);
        }
    }

    // Keep in sync with the renderer: the thumb's center travels between ThumbRadius and Width - ThumbRadius.
    private const float ThumbRadius = 10f;

    private string? _valueText;
    private FloatAnimation? _indicatorFadeAnim;
    private Action<float>? _setIndicatorOpacity;

    private bool _isDragging;
    private Point _dragStartScreenPos;
    private float _dragStartRatio;
    private Point _dragTrackVector;
    private float _dragUsableWidth;

    private static AnimationClock? _clock;

    /// <summary>
    /// Sets the clock that animates the value indicator of all sliders; the platform layer calls this at startup. Without a
    /// clock the indicator shows and hides immediately.
    /// </summary>
    public static void SetGlobalAnimationClock(AnimationClock? clock) => _clock = clock;

    static Slider()
    {
        IsFocusableProperty.OverrideDefaultValue<Slider>(true);
    }

    /// <summary>Initializes a new slider with a 0–100 range.</summary>
    public Slider()
    {
    }

    private static bool IsNotNaN(float value) => !float.IsNaN(value);
    private static bool IsFiniteValue(float value) => float.IsFinite(value);
    private static bool IsNonNegativeFinite(float value) => float.IsFinite(value) && value >= 0f;

    private float ClampToRange(float value)
    {
        float min = Minimum;
        float max = Math.Max(min, Maximum);
        return value < min ? min : value > max ? max : value;
    }

    private static void OnRangeChanged(BindableObject sender, float oldValue, float newValue)
    {
        var slider = (Slider)sender;
        slider.CoerceValue(ValueProperty);

        // Coercion only re-runs for set values; move a default value that is now outside the range into it.
        if (slider.GetValueSource(ValueProperty) == ValueSource.Default)
        {
            float value = slider.Value;
            float clamped = slider.ClampToRange(value);
            if (clamped != value)
            {
                slider.Value = clamped;
            }
        }
    }

    private void OnValueChanged(float newValue)
    {
        _valueText = null;
        ValueChanged?.Invoke(this, newValue);
    }

    // Applies a value chosen by the user: snapped to a tick when enabled, then clamped by coercion.
    private void SetValueFromUser(float value)
    {
        Value = Snap(value);
    }

    private float Snap(float value)
    {
        float tick = TickFrequency;
        if (!IsSnapToTickEnabled || tick <= 0f)
        {
            return value;
        }

        float min = Minimum;
        float snapped = min + MathF.Round((value - min) / tick) * tick;
        float max = Math.Max(min, Maximum);
        // The last partial step snaps to Maximum when that is closer than the last tick.
        if (snapped > max || max - value < Math.Abs(value - snapped))
        {
            snapped = max;
        }
        return snapped;
    }

    #region Pointer input

    /// <inheritdoc/>
    /// <remarks>A left-button press on the enabled slider moves the thumb to the pointer and starts dragging it.</remarks>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Button != PointerButtons.Left || !IsEnabled)
        {
            return;
        }

        CapturePointer();
        e.Handled = true;
        UpdateValueFromPosition(e.Position);

        _isDragging = true;
        _dragStartScreenPos = e.ScreenPosition;
        _dragStartRatio = NormalizedValue;

        Point p0 = PointToScreen(new Point(0, 0));
        Point p1 = PointToScreen(new Point(1, 0));
        _dragTrackVector = new Point(p1.X - p0.X, p1.Y - p0.Y);

        float usableWidth = Bounds.Width - ThumbRadius * 2;
        _dragUsableWidth = usableWidth > 0 ? usableWidth : Math.Max(1f, Bounds.Width);

        if (ShowValueIndicator)
        {
            FadeValueIndicator(1f, 150);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Moves the thumb while a drag started by <see cref="OnPointerPressed"/> is in progress.</remarks>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_isDragging)
        {
            e.Handled = true;
            if (IsEnabled)
            {
                UpdateValueFromDrag(e.ScreenPosition);
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>Ends a drag and releases pointer capture.</remarks>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isDragging && !IsPointerCaptured)
        {
            return;
        }

        e.Handled = true;
        if (IsPointerCaptured)
        {
            ReleasePointerCapture(); // ends the drag through OnLostPointerCapture
        }
        else
        {
            EndDrag();
        }
    }

    /// <inheritdoc/>
    /// <remarks>Ends a drag in progress and hides the value indicator.</remarks>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        EndDrag();
    }

    private void EndDrag()
    {
        _isDragging = false;
        if (ValueIndicatorOpacity > 0f || _indicatorFadeAnim != null)
        {
            FadeValueIndicator(0f, 200);
        }
    }

    private void UpdateValueFromDrag(Point screenPos)
    {
        float dx = screenPos.X - _dragStartScreenPos.X;
        float dy = screenPos.Y - _dragStartScreenPos.Y;

        float trackLenSq = _dragTrackVector.X * _dragTrackVector.X + _dragTrackVector.Y * _dragTrackVector.Y;
        float ratio;
        if (trackLenSq > 0.0001f && _dragUsableWidth > 0)
        {
            float deltaAlongTrack = (dx * _dragTrackVector.X + dy * _dragTrackVector.Y) / trackLenSq;
            ratio = Math.Clamp(_dragStartRatio + deltaAlongTrack / _dragUsableWidth, 0f, 1f);
        }
        else
        {
            ratio = Math.Clamp(_dragStartRatio + dx / Math.Max(1f, _dragUsableWidth), 0f, 1f);
        }

        SetValueFromUser(Minimum + ratio * (Maximum - Minimum));
    }

    private void UpdateValueFromPosition(Point pos)
    {
        float width = Bounds.Width;
        if (width <= 0) return;

        float usableWidth = width - ThumbRadius * 2;
        float ratio = usableWidth > 0
            ? Math.Clamp((pos.X - ThumbRadius) / usableWidth, 0f, 1f)
            : Math.Clamp(pos.X / width, 0f, 1f);

        SetValueFromUser(Minimum + ratio * (Maximum - Minimum));
    }

    #endregion

    /// <summary>
    /// Animates <see cref="ValueIndicatorOpacity"/> to <paramref name="targetOpacity"/> over
    /// <paramref name="durationMs"/> milliseconds, replacing a fade in progress; immediate without an animation clock.
    /// </summary>
    public void FadeValueIndicator(float targetOpacity, int durationMs)
    {
        _indicatorFadeAnim?.Stop();
        _indicatorFadeAnim = null;

        _setIndicatorOpacity ??= val =>
        {
            ValueIndicatorOpacity = val;
            InvalidateVisual();
        };

        if (_clock == null)
        {
            _setIndicatorOpacity(targetOpacity);
            return;
        }

        _indicatorFadeAnim = new FloatAnimation(
            from: ValueIndicatorOpacity,
            to: targetOpacity,
            duration: TimeSpan.FromMilliseconds(durationMs),
            onUpdate: _setIndicatorOpacity,
            easing: Easing.EaseOutCubic);
        _clock.Add(_indicatorFadeAnim);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Left/Down and Right/Up change the value by <see cref="SmallChange"/> (one tick when snapping), Page Down/Page Up by
    /// <see cref="LargeChange"/>, and Home/End go to <see cref="Minimum"/>/<see cref="Maximum"/>. Key auto-repeat keeps
    /// moving the thumb. Ignored when disabled or already handled.
    /// </remarks>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEnabled)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Left:
            case Key.Down:
                Step(-1);
                break;
            case Key.Right:
            case Key.Up:
                Step(1);
                break;
            case Key.PageDown:
                SetValueFromUser(Value - LargeChange);
                break;
            case Key.PageUp:
                SetValueFromUser(Value + LargeChange);
                break;
            case Key.Home:
                Value = Minimum;
                break;
            case Key.End:
                Value = Maximum;
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    // Moves by SmallChange, or to the next tick in the direction when snapping.
    private void Step(int direction)
    {
        float tick = TickFrequency;
        if (!IsSnapToTickEnabled || tick <= 0f)
        {
            Value += direction * SmallChange;
            return;
        }

        float min = Minimum;
        float max = Math.Max(min, Maximum);
        float steps = (Value - min) / tick;
        // A small tolerance, so a value that is on a tick up to rounding errors moves a whole tick.
        float index = direction > 0 ? MathF.Floor(steps + 1e-4f) + 1 : MathF.Ceiling(steps - 1e-4f) - 1;
        float next = min + index * tick;
        Value = next > max ? max : next;
    }

    /// <inheritdoc/>
    /// <remarks>The slider is up to 180 wide and 32 high.</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(Math.Min(180, availableSize.Width), 32f);
    }
}

/// <summary>
/// A Material Design 3 linear progress indicator, showing either how far an operation has progressed or, when
/// <see cref="IsIndeterminate"/>, that it is busy.
/// </summary>
/// <remarks>
/// <see cref="Value"/> is kept between <see cref="Minimum"/> and <see cref="Maximum"/> like <see cref="Slider.Value"/>.
/// </remarks>
public class ProgressBar : Control
{
    /// <summary>Identifies the <see cref="Minimum"/> property.</summary>
    public static readonly BindableProperty<float> MinimumProperty =
        BindableProperty.Register<ProgressBar, float>(nameof(Minimum), 0f, OnRangeChanged, options: PropertyOptions.AffectsRender, validateValue: IsFiniteValue);

    /// <summary>Identifies the <see cref="Maximum"/> property.</summary>
    public static readonly BindableProperty<float> MaximumProperty =
        BindableProperty.Register<ProgressBar, float>(nameof(Maximum), 100f, OnRangeChanged, options: PropertyOptions.AffectsRender, validateValue: IsFiniteValue);

    /// <summary>Identifies the <see cref="Value"/> property.</summary>
    public static readonly BindableProperty<float> ValueProperty =
        BindableProperty.Register<ProgressBar, float>(
            nameof(Value),
            0f,
            coerceValue: (s, v) => ((ProgressBar)s).ClampToRange(v),
            options: PropertyOptions.AffectsRender,
            validateValue: v => !float.IsNaN(v));

    /// <summary>Identifies the <see cref="IsIndeterminate"/> property.</summary>
    public static readonly BindableProperty<bool> IsIndeterminateProperty =
        BindableProperty.Register<ProgressBar, bool>(nameof(IsIndeterminate), false, options: PropertyOptions.AffectsRender);

    /// <summary>Gets or sets the value at which the bar is empty. The default is 0.</summary>
    public float Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <summary>Gets or sets the value at which the bar is full. The default is 100.</summary>
    public float Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>Gets or sets the progress, kept within <see cref="Minimum"/>..<see cref="Maximum"/>. The default is 0.</summary>
    public float Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>
    /// Gets or sets whether the bar shows an animated busy indicator instead of <see cref="Value"/>. The default is <c>false</c>.
    /// </summary>
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }

    /// <summary>
    /// Gets the filled fraction, from 0 at <see cref="Minimum"/> to 1 at <see cref="Maximum"/>; 0 for an empty range.
    /// </summary>
    public float NormalizedValue
    {
        get
        {
            float range = Maximum - Minimum;
            if (range <= 0) return 0;
            return Math.Clamp((Value - Minimum) / range, 0f, 1f);
        }
    }

    /// <summary>Gets the position of the moving segment in indeterminate mode, from 0 to 1.</summary>
    public float IndeterminateOffset { get; internal set; }

    private static bool IsFiniteValue(float value) => float.IsFinite(value);

    private float ClampToRange(float value)
    {
        float min = Minimum;
        float max = Math.Max(min, Maximum);
        return value < min ? min : value > max ? max : value;
    }

    private static void OnRangeChanged(BindableObject sender, float oldValue, float newValue)
    {
        var bar = (ProgressBar)sender;
        bar.CoerceValue(ValueProperty);

        // Coercion only re-runs for set values; move a default value that is now outside the range into it.
        if (bar.GetValueSource(ValueProperty) == ValueSource.Default)
        {
            float value = bar.Value;
            float clamped = bar.ClampToRange(value);
            if (clamped != value)
            {
                bar.Value = clamped;
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>The bar is up to 180 wide and 8 high.</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(Math.Min(180, availableSize.Width), 8);
    }
}
