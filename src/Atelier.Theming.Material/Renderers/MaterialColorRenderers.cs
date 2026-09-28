using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

// Shared look of the color controls: a light gray checkerboard behind transparency, thin outlines so light colors keep an
// edge on light surfaces, and white thumbs ringed in black at low opacity so they show on any color.
internal static class ColorDrawing
{
    public const float CheckerCell = 4f;
    public static readonly Color CheckerLight = Color.White;
    public static readonly Color CheckerDark = Color.FromRgb(0xCC, 0xCC, 0xCC);
    public static readonly Color ThumbOutline = Color.FromArgb(0x55, 0, 0, 0);

    public static Color EdgeColor(MaterialColorScheme colors) => colors.OnSurface.WithOpacity(0.16f);

    // A white ring with a subtle shadow and outline, filled with the color (over a checkerboard when it's translucent).
    public static void DrawThumb(ref DrawingContext context, in Point center, float radius, float ringWidth, in Color fill, MaterialColorScheme colors)
    {
        var rect = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
        context.DrawShadow(rect, new CornerRadius(radius), MaterialElevation.Level1, colors.Shadow);
        context.DrawCircle(center, radius, Color.White);

        float inner = radius - ringWidth;
        if (fill.A < 255)
        {
            var innerRect = new Rect(center.X - inner, center.Y - inner, inner * 2, inner * 2);
            context.DrawCheckerboard(innerRect, new CornerRadius(inner), CheckerCell, CheckerLight, CheckerDark);
        }
        context.DrawCircle(center, inner, fill);
        context.DrawCircleOutline(center, radius + 1f, ThumbOutline, 1f);
    }
}

/// <summary>
/// Draws a <see cref="ColorSlider"/>: a 12 px rounded track filled with its gradient (over a checkerboard when it shows
/// transparency) and a white-ringed thumb filled with the thumb color.
/// </summary>
/// <remarks>
/// Hover and press show an on-surface state layer around the thumb; keyboard focus shows the focus ring. A disabled
/// slider is drawn at 38% opacity.
/// </remarks>
/// <param name="colors">The color scheme.</param>
public class MaterialColorSliderRenderer(MaterialColorScheme colors) : ControlRenderer<ColorSlider>
{
    private const float TrackHeight = 12f;
    private const float ThumbRadius = 10f; // the thumb travels like a slider's handle, between 10 and Width - 10

    /// <inheritdoc/>
    public override void Render(ColorSlider slider, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, slider.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        int layer = slider.IsEnabled ? -1 : context.SaveOpacityLayer(MaterialState.DisabledContentOpacity);

        float centerY = bounds.Height * 0.5f;
        var track = new Rect(0, centerY - TrackHeight * 0.5f, bounds.Width, TrackHeight);
        var corner = new CornerRadius(TrackHeight * 0.5f);
        if (slider.ShowsTransparency)
        {
            context.DrawCheckerboard(track, corner, ColorDrawing.CheckerCell, ColorDrawing.CheckerLight, ColorDrawing.CheckerDark);
        }
        context.DrawLinearGradient(track, corner, new Point(ThumbRadius, centerY), new Point(bounds.Width - ThumbRadius, centerY), slider.TrackColors);
        context.DrawRoundedRectOutline(track, corner, ColorDrawing.EdgeColor(colors), 1f);

        float usable = Math.Max(0f, bounds.Width - ThumbRadius * 2);
        var thumb = new Point(ThumbRadius + slider.NormalizedValue * usable, centerY);
        MaterialDrawing.DrawStateLayerCircle(ref context, thumb, colors.OnSurface, MaterialDrawing.HaloOpacity(slider), 16f);
        ColorDrawing.DrawThumb(ref context, thumb, ThumbRadius - 1f, 3f, slider.ThumbColor, colors);

        if (slider.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRingCircle(ref context, thumb, ThumbRadius, colors.Secondary);
        }

        if (layer >= 0) context.Canvas.RestoreToCount(layer);
    }
}

/// <summary>
/// Draws a <see cref="ColorWheel"/>: the hue around the circle, fading to white towards the center, darkened by the
/// wheel's brightness, and a white-ringed thumb at the picked color.
/// </summary>
/// <remarks>Keyboard focus shows the focus ring around the thumb. A disabled wheel is drawn at 38% opacity.</remarks>
/// <param name="colors">The color scheme.</param>
public class MaterialColorWheelRenderer(MaterialColorScheme colors) : ControlRenderer<ColorWheel>
{
    // Hues clockwise from the right (the sweep gradient's direction); the wheel's hue runs counterclockwise.
    private static readonly Color[] s_hues = CreateHues();

    private static Color[] CreateHues()
    {
        var hues = new Color[13];
        for (int i = 0; i < hues.Length; i++)
        {
            hues[i] = Color.FromHsv(360f - i * 30f, 1f, 1f);
        }
        return hues;
    }

    /// <inheritdoc/>
    public override void Render(ColorWheel wheel, ref DrawingContext context)
    {
        float radius = wheel.WheelRadius;
        if (radius <= 0) return;

        int layer = wheel.IsEnabled ? -1 : context.SaveOpacityLayer(MaterialState.DisabledContentOpacity);

        var center = wheel.WheelCenter;
        context.DrawSweepGradientCircle(center, radius, s_hues);
        context.DrawRadialGradientCircle(center, radius, Color.White, Color.White.WithAlpha((byte)0));
        context.DrawCircle(center, radius, Color.Black.WithAlpha(1f - wheel.Brightness));
        context.DrawCircleOutline(center, radius, ColorDrawing.EdgeColor(colors), 1f);

        var thumb = wheel.ThumbPosition;
        ColorDrawing.DrawThumb(ref context, thumb, ColorWheel.ThumbRadius, 2.5f, wheel.Color, colors);
        if (wheel.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRingCircle(ref context, thumb, ColorWheel.ThumbRadius, colors.Secondary);
        }

        if (layer >= 0) context.Canvas.RestoreToCount(layer);
    }
}

/// <summary>
/// Draws a <see cref="ColorSwatch"/>: its color in its corner radius with a thin outline, over a checkerboard when the
/// color is translucent.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialColorSwatchRenderer(MaterialColorScheme colors) : ControlRenderer<ColorSwatch>
{
    /// <inheritdoc/>
    public override void Render(ColorSwatch swatch, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, swatch.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var color = swatch.Color;
        var corner = swatch.CornerRadius;
        if (color.A < 255)
        {
            context.DrawCheckerboard(bounds, corner, ColorDrawing.CheckerCell * 1.5f, ColorDrawing.CheckerLight, ColorDrawing.CheckerDark);
        }
        context.DrawRoundedRect(bounds, corner, color);
        context.DrawRoundedRectOutline(bounds, corner, ColorDrawing.EdgeColor(colors), 1f);
    }
}
