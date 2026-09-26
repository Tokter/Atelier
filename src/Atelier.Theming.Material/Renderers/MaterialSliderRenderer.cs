using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a Material Design 3 <see cref="Slider"/>: a 4 px track (primary up to the value, surface-container-highest after
/// it), a 20 px primary handle at elevation level 1, tick marks for snapping sliders, and the value label.
/// </summary>
/// <remarks>
/// Hover and press show the primary state layer around the handle (40 px, smaller in dense sizing); keyboard focus
/// shows the focus ring around the handle. Disabled: on-surface at 38% (active track, handle) and 12% (inactive track).
/// </remarks>
/// <param name="colors">The color scheme.</param>
/// <param name="sizing">The theme sizing (the state layer shrinks with the density); <c>null</c> for desktop sizing.</param>
public class MaterialSliderRenderer(MaterialColorScheme colors, MaterialSizing? sizing = null) : ControlRenderer<Slider>
{
    private const float TrackHeight = 4f;
    private const float HandleRadius = 10f;
    private const float LabelHeight = 28f;
    private const float LabelFontSize = 12f; // label-medium
    private readonly float _stateLayerRadius = (sizing ?? MaterialSizing.Desktop).StateLayerSize * 0.5f;

    /// <inheritdoc/>
    public override void Render(Slider slider, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, slider.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        bool enabled = slider.IsEnabled;
        float centerY = bounds.Height * 0.5f;
        float usable = Math.Max(0f, bounds.Width - HandleRadius * 2);
        float progress = slider.NormalizedValue;
        float handleX = HandleRadius + progress * usable;
        var trackCorner = new CornerRadius(TrackHeight * 0.5f);

        Color active = enabled ? colors.Primary : colors.OnSurface.WithOpacity(0.38f);
        Color inactive = enabled ? colors.SurfaceContainerHighest : MaterialDrawing.DisabledContainer(colors);
        Color handle = enabled ? colors.Primary : colors.OnSurface.WithOpacity(0.38f);

        float trackY = centerY - TrackHeight * 0.5f;
        context.DrawRoundedRect(new Rect(HandleRadius, trackY, usable, TrackHeight), trackCorner, inactive);
        if (handleX > HandleRadius)
        {
            context.DrawRoundedRect(new Rect(HandleRadius, trackY, handleX - HandleRadius, TrackHeight), trackCorner, active);
        }

        // Tick marks of a snapping slider; skipped when closer than 4 px.
        float range = slider.Maximum - slider.Minimum;
        float tick = slider.TickFrequency;
        if (slider.IsSnapToTickEnabled && tick > 0 && range > 0 && usable * tick / range >= 4f)
        {
            int count = (int)MathF.Floor(range / tick + 1e-4f);
            for (int i = 0; i <= count; i++)
            {
                float ratio = i * tick / range;
                Color dot = ratio <= progress
                    ? (enabled ? colors.OnPrimary : colors.OnSurface).WithOpacity(0.38f)
                    : colors.OnSurfaceVariant.WithOpacity(0.38f);
                context.DrawCircle(new Point(HandleRadius + ratio * usable, centerY), 1f, dot);
            }
        }

        var handleCenter = new Point(handleX, centerY);
        float stateOpacity = MaterialDrawing.HaloOpacity(slider);
        MaterialDrawing.DrawStateLayerCircle(ref context, handleCenter, colors.Primary, stateOpacity, _stateLayerRadius);

        if (enabled)
        {
            var handleRect = new Rect(handleX - HandleRadius, centerY - HandleRadius, HandleRadius * 2, HandleRadius * 2);
            context.DrawShadow(handleRect, new CornerRadius(HandleRadius), MaterialElevation.Level1, colors.Shadow);
        }
        context.DrawCircle(handleCenter, HandleRadius, handle);

        if (slider.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRingCircle(ref context, handleCenter, HandleRadius, colors.Secondary);
        }

        if (slider.ShowValueIndicator && slider.ValueIndicatorOpacity > 0.005f)
        {
            DrawValueLabel(slider, bounds, handleCenter, ref context);
        }
    }

    // The value label: a primary pill (28 px high) above the handle with the on-primary value (label-medium).
    private void DrawValueLabel(Slider slider, in Rect bounds, in Point handle, ref DrawingContext context)
    {
        float opacity = slider.ValueIndicatorOpacity;
        string text = slider.ValueText; // cached by the slider
        var textSize = context.MeasureText(text, LabelFontSize, null, FontWeight.Medium);

        float width = Math.Max(LabelHeight, textSize.Width + 16f);
        float x = Math.Clamp(handle.X - width * 0.5f, bounds.Left, Math.Max(bounds.Left, bounds.Right - width));
        float y = handle.Y - HandleRadius - 8f - LabelHeight;
        var label = new Rect(x, y, width, LabelHeight);

        context.DrawRoundedRect(label, new CornerRadius(LabelHeight * 0.5f), colors.Primary.WithOpacity(opacity));
        float textX = label.Left + (label.Width - textSize.Width) * 0.5f;
        float textY = label.Top + (label.Height + LabelFontSize * 0.72f) * 0.5f;
        context.DrawText(text, new Point(textX, textY), colors.OnPrimary.WithOpacity(opacity), LabelFontSize, null, FontWeight.Medium);
    }
}
