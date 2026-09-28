using System;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Controls;

/// <summary>
/// A color wheel for picking hue and saturation with the pointer: the hue runs around the circle and the saturation
/// from gray at the center to the pure hue at the edge, shown at the current <see cref="Brightness"/>.
/// </summary>
/// <remarks>
/// <para>
/// Red (hue 0) is at the right; the hue increases counterclockwise, through yellow at the top-right to green, cyan,
/// blue and magenta. Pressing the left pointer button picks the color under the pointer and dragging keeps picking;
/// outside the circle the nearest color on the edge is picked.
/// </para>
/// <para>
/// From the keyboard, Left/Right turn the hue by 1° (Page Up/Page Down by 15°) and Up/Down change the saturation by 1%.
/// </para>
/// </remarks>
public class ColorWheel : Control
{
    /// <summary>Identifies the <see cref="Hue"/> property.</summary>
    public static readonly BindableProperty<float> HueProperty =
        BindableProperty.Register<ColorWheel, float>(
            nameof(Hue), 0f, OnColorComponentChanged, coerceValue: (_, v) => WrapHue(v),
            options: PropertyOptions.AffectsRender, validateValue: float.IsFinite);

    /// <summary>Identifies the <see cref="Saturation"/> property.</summary>
    public static readonly BindableProperty<float> SaturationProperty =
        BindableProperty.Register<ColorWheel, float>(
            nameof(Saturation), 0f, OnColorComponentChanged, coerceValue: (_, v) => Math.Clamp(v, 0f, 1f),
            options: PropertyOptions.AffectsRender, validateValue: float.IsFinite);

    /// <summary>Identifies the <see cref="Brightness"/> property.</summary>
    public static readonly BindableProperty<float> BrightnessProperty =
        BindableProperty.Register<ColorWheel, float>(
            nameof(Brightness), 1f, OnColorComponentChanged, coerceValue: (_, v) => Math.Clamp(v, 0f, 1f),
            options: PropertyOptions.AffectsRender, validateValue: float.IsFinite);

    /// <summary>The radius of the thumb marking the picked color; the wheel is inset by it so the thumb stays inside.</summary>
    public const float ThumbRadius = 8f;

    private const float DefaultSize = 160f;

    private bool _isDragging;

    static ColorWheel()
    {
        IsFocusableProperty.OverrideDefaultValue<ColorWheel>(true);
    }

    /// <summary>Gets or sets the hue in degrees, from 0 up to 360 (exclusive; 360 wraps to 0). The default is 0 (red).</summary>
    public float Hue { get => GetValue(HueProperty); set => SetValue(HueProperty, value); }

    /// <summary>Gets or sets the saturation, from 0 (gray, the center) to 1 (the edge). The default is 0.</summary>
    public float Saturation { get => GetValue(SaturationProperty); set => SetValue(SaturationProperty, value); }

    /// <summary>
    /// Gets or sets the brightness (HSV value) the wheel is shown at, from 0 (black) to 1. It isn't changed by the wheel
    /// itself; pair it with a slider. The default is 1.
    /// </summary>
    public float Brightness { get => GetValue(BrightnessProperty); set => SetValue(BrightnessProperty, value); }

    /// <summary>Gets the picked color: <see cref="Hue"/>, <see cref="Saturation"/> and <see cref="Brightness"/>, opaque.</summary>
    public Color Color => Color.FromHsv(Hue, Saturation, Brightness);

    /// <summary>Occurs when <see cref="Hue"/>, <see cref="Saturation"/> or <see cref="Brightness"/> changes.</summary>
    public event EventHandler? ColorChanged;

    /// <summary>Gets the center of the wheel in the element's coordinates.</summary>
    public Point WheelCenter => new(Bounds.Width * 0.5f, Bounds.Height * 0.5f);

    /// <summary>Gets the radius of the wheel: half the smaller side, less <see cref="ThumbRadius"/>.</summary>
    public float WheelRadius => Math.Max(0f, Math.Min(Bounds.Width, Bounds.Height) * 0.5f - ThumbRadius);

    /// <summary>Gets the center of the thumb, where <see cref="Hue"/> and <see cref="Saturation"/> are on the wheel.</summary>
    public Point ThumbPosition
    {
        get
        {
            var center = WheelCenter;
            float distance = Saturation * WheelRadius;
            float angle = Hue * MathF.PI / 180f;
            return new Point(center.X + distance * MathF.Cos(angle), center.Y - distance * MathF.Sin(angle));
        }
    }

    private static float WrapHue(float hue)
    {
        hue %= 360f;
        return hue < 0 ? hue + 360f : hue;
    }

    private static void OnColorComponentChanged(BindableObject sender, float oldValue, float newValue) =>
        ((ColorWheel)sender).ColorChanged?.Invoke(sender, EventArgs.Empty);

    /// <summary>Picks the hue and saturation at <paramref name="point"/> (in the element's coordinates).</summary>
    public void PickAt(Point point)
    {
        float radius = WheelRadius;
        if (radius <= 0) return;

        var center = WheelCenter;
        float dx = point.X - center.X;
        float dy = center.Y - point.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);

        // At the very center the hue is undefined; keep the current one.
        if (distance > 0.5f)
        {
            Hue = MathF.Atan2(dy, dx) * 180f / MathF.PI;
        }
        Saturation = distance / radius;
    }

    /// <inheritdoc/>
    /// <remarks>A left-button press picks the color under the pointer and starts dragging.</remarks>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Button != PointerButtons.Left || !IsEnabled) return;

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
        if (IsEnabled)
        {
            var origin = PointToScreen(Point.Zero);
            PickAt(new Point(e.ScreenPosition.X - origin.X, e.ScreenPosition.Y - origin.Y));
        }
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isDragging && !IsPointerCaptured) return;

        e.Handled = true;
        if (IsPointerCaptured)
        {
            ReleasePointerCapture(); // ends the drag through OnLostPointerCapture
        }
        _isDragging = false;
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        _isDragging = false;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Left/Right turn the hue by 1°, Page Up/Page Down by 15°, and Up/Down change the saturation by 1%. Ignored when
    /// disabled or already handled.
    /// </remarks>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEnabled) return;

        switch (e.Key)
        {
            case Key.Left: Hue -= 1f; break;
            case Key.Right: Hue += 1f; break;
            case Key.PageDown: Hue -= 15f; break;
            case Key.PageUp: Hue += 15f; break;
            case Key.Up: Saturation += 0.01f; break;
            case Key.Down: Saturation -= 0.01f; break;
            default: return;
        }
        e.Handled = true;
    }

    /// <inheritdoc/>
    /// <remarks>A square, 160 px by default, or as large as the available space allows when that is smaller.</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        float size = Math.Min(DefaultSize, Math.Min(availableSize.Width, availableSize.Height));
        return new Size(size, size);
    }
}
