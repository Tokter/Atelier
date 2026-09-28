using System;
using System.Globalization;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Controls;

/// <summary>Whether a <see cref="ClockDial"/> picks the hour or the minute.</summary>
public enum ClockDialMode
{
    /// <summary>The hour: 12, 1–11 around the dial, and on a 24-hour dial 00, 13–23 on an inner ring.</summary>
    Hours,

    /// <summary>The minute: 00, 05, … 55 around the dial; any minute can be picked between the labels.</summary>
    Minutes,
}

/// <summary>A number on a <see cref="ClockDial"/>: its text, where it sits and the value it stands for.</summary>
/// <param name="Text">The label text, such as "12" or "05".</param>
/// <param name="Angle">The angle in degrees, clockwise from 12 o'clock.</param>
/// <param name="IsInner">Whether the label is on the inner ring of a 24-hour dial.</param>
/// <param name="Value">The hour (0–23) or minute (0–59).</param>
public readonly record struct ClockDialLabel(string Text, float Angle, bool IsInner, int Value);

/// <summary>
/// The Material Design 3 time picker dial: a round face with the hours or minutes, and a handle pointing at the value.
/// </summary>
/// <remarks>
/// <para>
/// Pressing or dragging on the dial moves the handle to the nearest hour, or to the exact minute; on a 24-hour dial the
/// ring under the pointer decides between 1–12 (outer) and 13–00 (inner). Releasing raises
/// <see cref="SelectionCompleted"/>, which the time picker uses to switch from hours to minutes.
/// </para>
/// <para>
/// From the keyboard, Up/Right and Down/Left change the value by one (wrapping around), Page Up/Page Down by 5 minutes
/// or 3 hours.
/// </para>
/// </remarks>
public class ClockDial : Control
{
    /// <summary>Identifies the <see cref="Value"/> property.</summary>
    public static readonly BindableProperty<int> ValueProperty =
        BindableProperty.Register<ClockDial, int>(
            nameof(Value), 12, (s, o, n) => ((ClockDial)s).ValueChanged?.Invoke(s, n),
            coerceValue: (s, v) => ((ClockDial)s).Wrap(v), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="Mode"/> property.</summary>
    public static readonly BindableProperty<ClockDialMode> ModeProperty =
        BindableProperty.Register<ClockDial, ClockDialMode>(nameof(Mode), ClockDialMode.Hours, (s, o, n) => ((ClockDial)s).OnFormatChanged(), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="Is24Hour"/> property.</summary>
    public static readonly BindableProperty<bool> Is24HourProperty =
        BindableProperty.Register<ClockDial, bool>(nameof(Is24Hour), false, (s, o, n) => ((ClockDial)s).OnFormatChanged(), options: PropertyOptions.AffectsRender);

    /// <summary>The dial's diameter (MD3: 256).</summary>
    public const float DialSize = 256f;

    /// <summary>The radius of the handle's circle around the selected value (MD3: 24).</summary>
    public const float HandleRadius = 24f;

    /// <summary>The distance of the outer labels from the center.</summary>
    public const float OuterRadius = DialSize / 2 - HandleRadius;

    /// <summary>The distance of a 24-hour dial's inner labels from the center.</summary>
    public const float InnerRadius = OuterRadius - 2 * HandleRadius + 4;

    private ClockDialLabel[] _labels = [];
    private bool _isDragging;

    static ClockDial()
    {
        IsFocusableProperty.OverrideDefaultValue<ClockDial>(true);
    }

    /// <summary>Initializes a 12-hour dial showing the hours.</summary>
    public ClockDial()
    {
        OnFormatChanged();
    }

    /// <summary>
    /// Gets or sets the hour (0–23) or minute (0–59) the handle points at; values wrap around. The default is 12.
    /// </summary>
    public int Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Gets or sets whether the dial shows hours or minutes. The default is hours.</summary>
    public ClockDialMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <summary>
    /// Gets or sets whether the hour dial has 24 hours (1–12 outside, 13–00 inside) instead of 12. On a 12-hour dial
    /// <see cref="Value"/> still holds 0–23, and the dial keeps its AM/PM half. The default is <c>false</c>.
    /// </summary>
    public bool Is24Hour { get => GetValue(Is24HourProperty); set => SetValue(Is24HourProperty, value); }

    /// <summary>Occurs when <see cref="Value"/> changes, with the new value.</summary>
    public event EventHandler<int>? ValueChanged;

    /// <summary>Occurs when the pointer is released after picking on the dial.</summary>
    public event EventHandler? SelectionCompleted;

    /// <summary>Gets the numbers on the dial for the current mode.</summary>
    public ReadOnlySpan<ClockDialLabel> Labels => _labels;

    /// <summary>Gets the center of the dial in the element's coordinates.</summary>
    public Point Center => new(Bounds.Width * 0.5f, Bounds.Height * 0.5f);

    /// <summary>Gets the angle of the handle in degrees, clockwise from 12 o'clock.</summary>
    public float HandleAngle => Mode == ClockDialMode.Minutes ? Value * 6f : Value % 12 * 30f;

    /// <summary>Gets whether the handle is on the inner ring (hours 13–23 and 00 of a 24-hour dial).</summary>
    public bool IsHandleInner => Mode == ClockDialMode.Hours && Is24Hour && (Value == 0 || Value > 12);

    /// <summary>Gets whether the handle sits between two labels (a minute that isn't a multiple of 5).</summary>
    public bool IsHandleBetweenLabels => Mode == ClockDialMode.Minutes && Value % 5 != 0;

    /// <summary>Gets the center of the handle's circle.</summary>
    public Point HandleCenter => GetPosition(HandleAngle, IsHandleInner);

    /// <summary>Gets the point at <paramref name="angle"/> (clockwise from 12 o'clock) on the outer or inner ring.</summary>
    public Point GetPosition(float angle, bool isInner)
    {
        var center = Center;
        float radius = isInner ? InnerRadius : OuterRadius;
        float radians = angle * MathF.PI / 180f;
        return new Point(center.X + radius * MathF.Sin(radians), center.Y - radius * MathF.Cos(radians));
    }

    private int Wrap(int value)
    {
        int count = Mode == ClockDialMode.Minutes ? 60 : 24;
        value %= count;
        return value < 0 ? value + count : value;
    }

    private void OnFormatChanged()
    {
        var culture = CultureInfo.CurrentCulture;
        if (Mode == ClockDialMode.Minutes)
        {
            _labels = new ClockDialLabel[12];
            for (int i = 0; i < 12; i++)
            {
                _labels[i] = new ClockDialLabel((i * 5).ToString("00", culture), i * 30f, false, i * 5);
            }
        }
        else
        {
            _labels = new ClockDialLabel[Is24Hour ? 24 : 12];
            for (int i = 0; i < 12; i++)
            {
                int outer = i == 0 ? 12 : i;
                _labels[i] = new ClockDialLabel(outer.ToString(culture), i * 30f, false, outer);
                if (Is24Hour)
                {
                    int inner = i == 0 ? 0 : i + 12;
                    _labels[12 + i] = new ClockDialLabel(inner.ToString("00", culture), i * 30f, true, inner);
                }
            }
        }
        CoerceValue(ValueProperty);
        InvalidateVisual();
    }

    /// <summary>
    /// Picks the value at <paramref name="point"/> (in the element's coordinates): the nearest hour, or the exact minute.
    /// A 12-hour dial keeps the AM/PM half of <see cref="Value"/>.
    /// </summary>
    public void PickAt(Point point)
    {
        var center = Center;
        float dx = point.X - center.X, dy = point.Y - center.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance < 4f) return;

        float angle = MathF.Atan2(dx, -dy) * 180f / MathF.PI;
        if (angle < 0) angle += 360f;

        if (Mode == ClockDialMode.Minutes)
        {
            Value = (int)MathF.Round(angle / 6f) % 60;
            return;
        }

        int position = (int)MathF.Round(angle / 30f) % 12; // 0 = 12 o'clock
        if (Is24Hour)
        {
            bool inner = distance < (InnerRadius + OuterRadius) * 0.5f;
            Value = inner ? (position == 0 ? 0 : position + 12) : (position == 0 ? 12 : position);
        }
        else
        {
            bool pm = Value >= 12;
            Value = position + (pm ? 12 : 0);
        }
    }

    /// <inheritdoc/>
    /// <remarks>A left-button press picks the value under the pointer and starts dragging.</remarks>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Button != PointerButtons.Left || !IsEnabled) return;

        Focus();
        CapturePointer();
        _isDragging = true;
        e.Handled = true;
        PickAt(e.Position);
    }

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDragging) return;

        e.Handled = true;
        var origin = PointToScreen(Point.Zero);
        PickAt(new Point(e.ScreenPosition.X - origin.X, e.ScreenPosition.Y - origin.Y));
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isDragging) return;

        e.Handled = true;
        _isDragging = false;
        if (IsPointerCaptured) ReleasePointerCapture();
        SelectionCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        _isDragging = false;
    }

    /// <inheritdoc/>
    /// <remarks>Up/Right and Down/Left step by one, Page Up/Page Down by 5 minutes or 3 hours.</remarks>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEnabled) return;

        int big = Mode == ClockDialMode.Minutes ? 5 : 3;
        switch (e.Key)
        {
            case Key.Up or Key.Right: Step(1); break;
            case Key.Down or Key.Left: Step(-1); break;
            case Key.PageUp: Step(big); break;
            case Key.PageDown: Step(-big); break;
            default: return;
        }
        e.Handled = true;
    }

    // On a 12-hour dial the hour stays within its AM/PM half.
    private void Step(int delta)
    {
        if (Mode == ClockDialMode.Hours && !Is24Hour)
        {
            int half = Value >= 12 ? 12 : 0;
            Value = half + ((Value % 12 + delta) % 12 + 12) % 12;
        }
        else
        {
            Value += delta;
        }
    }

    /// <inheritdoc/>
    /// <remarks>256×256, or smaller when less space is available.</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        float size = Math.Min(DialSize, Math.Min(availableSize.Width, availableSize.Height));
        return new Size(size, size);
    }
}
