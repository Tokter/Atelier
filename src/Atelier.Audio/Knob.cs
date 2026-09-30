using System;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Audio;

/// <summary>
/// A rotary control for picking a value from a range, like a knob on a mixing desk: an arc around a dial shows the value,
/// from the bottom left (<see cref="Minimum"/>) clockwise to the bottom right (<see cref="Maximum"/>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Value"/> is kept between <see cref="Minimum"/> and <see cref="Maximum"/> like <see cref="Slider.Value"/>.
/// Dragging with the left pointer button up or right turns the value up, down or left turns it down;
/// <see cref="DragDistance"/> pixels cover the whole range, ten times as many with Shift held for fine changes. The wheel
/// changes the value by <see cref="SmallChange"/>, and double-clicking resets it to <see cref="DefaultValue"/> when set.
/// </para>
/// <para>
/// From the keyboard, the arrow keys change the value by <see cref="SmallChange"/>, Page Up/Page Down by
/// <see cref="LargeChange"/>, and Home/End jump to the ends of the range. A disabled knob ignores input.
/// </para>
/// </remarks>
public class Knob : Control
{
    /// <summary>Identifies the <see cref="Minimum"/> property.</summary>
    public static readonly BindableProperty<float> MinimumProperty =
        BindableProperty.Register<Knob, float>(nameof(Minimum), 0f, OnRangeChanged, options: PropertyOptions.AffectsRender, validateValue: float.IsFinite);

    /// <summary>Identifies the <see cref="Maximum"/> property.</summary>
    public static readonly BindableProperty<float> MaximumProperty =
        BindableProperty.Register<Knob, float>(nameof(Maximum), 100f, OnRangeChanged, options: PropertyOptions.AffectsRender, validateValue: float.IsFinite);

    /// <summary>Identifies the <see cref="Value"/> property.</summary>
    public static readonly BindableProperty<float> ValueProperty =
        BindableProperty.Register<Knob, float>(
            nameof(Value),
            0f,
            (s, o, n) => ((Knob)s).OnValueChanged(n),
            coerceValue: (s, v) => ((Knob)s).ClampToRange(v),
            options: PropertyOptions.AffectsRender,
            validateValue: v => !float.IsNaN(v));

    /// <summary>Identifies the <see cref="SmallChange"/> property.</summary>
    public static readonly BindableProperty<float> SmallChangeProperty =
        BindableProperty.Register<Knob, float>(nameof(SmallChange), 1f, validateValue: IsNonNegativeFinite);

    /// <summary>Identifies the <see cref="LargeChange"/> property.</summary>
    public static readonly BindableProperty<float> LargeChangeProperty =
        BindableProperty.Register<Knob, float>(nameof(LargeChange), 10f, validateValue: IsNonNegativeFinite);

    /// <summary>Identifies the <see cref="DefaultValue"/> property.</summary>
    public static readonly BindableProperty<float> DefaultValueProperty =
        BindableProperty.Register<Knob, float>(nameof(DefaultValue), float.NaN);

    /// <summary>Identifies the <see cref="DragDistance"/> property.</summary>
    public static readonly BindableProperty<float> DragDistanceProperty =
        BindableProperty.Register<Knob, float>(nameof(DragDistance), 200f, validateValue: v => float.IsFinite(v) && v > 0);

    /// <summary>Identifies the <see cref="ValueFormat"/> property.</summary>
    public static readonly BindableProperty<string> ValueFormatProperty =
        BindableProperty.Register<Knob, string>(nameof(ValueFormat), "{0:0}", (s, o, n) => ((Knob)s)._valueText = null, options: PropertyOptions.AffectsRender);

    /// <summary>The angle of <see cref="Minimum"/> in degrees, clockwise from the right (the bottom left).</summary>
    public const float StartAngle = 135f;

    /// <summary>The angle the knob turns through from <see cref="Minimum"/> to <see cref="Maximum"/>, in degrees.</summary>
    public const float SweepAngle = 270f;

    private const float DefaultSize = 40f;
    private const float FineFactor = 0.1f;

    private string? _valueText;
    private bool _isDragging;
    private Point _lastDragPosition;

    static Knob()
    {
        AudioTheme.Register();
        IsFocusableProperty.OverrideDefaultValue<Knob>(true);
    }

    /// <summary>Gets or sets the lowest value. The default is 0.</summary>
    public float Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <summary>Gets or sets the highest value. The default is 100.</summary>
    public float Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>Gets or sets the current value, kept within <see cref="Minimum"/>..<see cref="Maximum"/>. The default is 0.</summary>
    public float Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Gets or sets the step of the arrow keys and the wheel. The default is 1.</summary>
    public float SmallChange { get => GetValue(SmallChangeProperty); set => SetValue(SmallChangeProperty, value); }

    /// <summary>Gets or sets the step of Page Up and Page Down. The default is 10.</summary>
    public float LargeChange { get => GetValue(LargeChangeProperty); set => SetValue(LargeChangeProperty, value); }

    /// <summary>Gets or sets the value a double click resets the knob to; <see cref="float.NaN"/> (the default) for none.</summary>
    public float DefaultValue { get => GetValue(DefaultValueProperty); set => SetValue(DefaultValueProperty, value); }

    /// <summary>Gets or sets how many pixels of dragging cover the whole range. The default is 200.</summary>
    public float DragDistance { get => GetValue(DragDistanceProperty); set => SetValue(DragDistanceProperty, value); }

    /// <summary>Gets or sets the composite format string (with the value as argument 0) for <see cref="ValueText"/>. The default is <c>"{0:0}"</c>.</summary>
    public string ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    /// <summary>Gets <see cref="Value"/> formatted with <see cref="ValueFormat"/>, cached so renderers can read it every frame.</summary>
    public string ValueText => _valueText ??= string.Format(ValueFormat, Value);

    /// <summary>Gets the position of <see cref="Value"/> within the range, from 0 at <see cref="Minimum"/> to 1 at <see cref="Maximum"/>; 0 for an empty range.</summary>
    public float NormalizedValue
    {
        get
        {
            float range = Maximum - Minimum;
            return range <= 0 ? 0 : Math.Clamp((Value - Minimum) / range, 0f, 1f);
        }
    }

    /// <summary>Gets the angle the knob points at, in degrees clockwise from the right: <see cref="StartAngle"/> plus the value's share of <see cref="SweepAngle"/>.</summary>
    public float Angle => StartAngle + NormalizedValue * SweepAngle;

    /// <summary>Gets whether the knob is being dragged.</summary>
    public bool IsDragging => _isDragging;

    /// <summary>Occurs when <see cref="Value"/> changes, with the new value.</summary>
    public event EventHandler<float>? ValueChanged;

    private static bool IsNonNegativeFinite(float value) => float.IsFinite(value) && value >= 0f;

    private float ClampToRange(float value)
    {
        float min = Minimum;
        float max = Math.Max(min, Maximum);
        return value < min ? min : value > max ? max : value;
    }

    private static void OnRangeChanged(BindableObject sender, float oldValue, float newValue)
    {
        var knob = (Knob)sender;
        knob.CoerceValue(ValueProperty);
        if (knob.GetValueSource(ValueProperty) == ValueSource.Default)
        {
            float clamped = knob.ClampToRange(knob.Value);
            if (clamped != knob.Value) knob.Value = clamped;
        }
    }

    private void OnValueChanged(float newValue)
    {
        _valueText = null;
        ValueChanged?.Invoke(this, newValue);
    }

    /// <inheritdoc/>
    /// <remarks>A left-button press starts turning the knob; a double click resets it to <see cref="DefaultValue"/> when set.</remarks>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Button != PointerButtons.Left || !IsEnabled) return;

        e.Handled = true;
        Focus();
        if (e.ClickCount >= 2 && !float.IsNaN(DefaultValue))
        {
            Value = DefaultValue;
            return;
        }
        CapturePointer();
        _isDragging = true;
        _lastDragPosition = e.ScreenPosition;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDragging) return;

        e.Handled = true;
        // Up and right turn it up; the screen's y axis points down.
        float delta = (e.ScreenPosition.X - _lastDragPosition.X) - (e.ScreenPosition.Y - _lastDragPosition.Y);
        _lastDragPosition = e.ScreenPosition;
        if (!IsEnabled || delta == 0) return;

        float factor = (e.Modifiers & ModifierKeys.Shift) != 0 ? FineFactor : 1f;
        Value += delta / DragDistance * (Maximum - Minimum) * factor;
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isDragging && !IsPointerCaptured) return;

        e.Handled = true;
        if (IsPointerCaptured) ReleasePointerCapture(); // ends the drag through OnLostPointerCapture
        else EndDrag();
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        EndDrag();
    }

    private void EndDrag()
    {
        if (!_isDragging) return;
        _isDragging = false;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    /// <remarks>Each wheel notch changes the value by <see cref="SmallChange"/>.</remarks>
    public override void OnPointerWheel(PointerWheelEventArgs e)
    {
        base.OnPointerWheel(e);
        if (e.Handled || !IsEnabled || e.DeltaY == 0) return;

        Value += MathF.Sign(e.DeltaY) * SmallChange;
        e.Handled = true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Left/Down and Right/Up change the value by <see cref="SmallChange"/>, Page Down/Page Up by <see cref="LargeChange"/>,
    /// and Home/End go to <see cref="Minimum"/>/<see cref="Maximum"/>. Ignored when disabled or already handled.
    /// </remarks>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEnabled) return;

        switch (e.Key)
        {
            case Key.Left:
            case Key.Down:
                Value -= SmallChange;
                break;
            case Key.Right:
            case Key.Up:
                Value += SmallChange;
                break;
            case Key.PageDown:
                Value -= LargeChange;
                break;
            case Key.PageUp:
                Value += LargeChange;
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

    /// <inheritdoc/>
    /// <remarks>A circle, 40 px by default, or as large as the available space allows when that is smaller.</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        float size = Math.Min(DefaultSize, Math.Min(availableSize.Width, availableSize.Height));
        return new Size(size, size);
    }
}
