using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a Material Design 3 <see cref="CheckBox"/>: an 18 px box with a 2 px corner, outlined (on-surface-variant, 2 px)
/// when unchecked and filled with primary plus an on-primary check mark or dash when checked or indeterminate.
/// </summary>
/// <remarks>
/// Hover and press show the circular state layer (40 px, smaller in dense sizing; on-surface when unchecked, primary when
/// checked); keyboard focus shows the focus ring hugging the box. Disabled: on-surface at 38% (outline or container),
/// surface mark.
/// </remarks>
/// <param name="colors">The color scheme.</param>
/// <param name="sizing">The theme sizing (the state layer shrinks with the density); <c>null</c> for desktop sizing.</param>
public class MaterialCheckBoxRenderer(MaterialColorScheme colors, MaterialSizing? sizing = null) : ControlRenderer<CheckBox>
{
    private static readonly CornerRadius BoxCorner = new(2f);
    private const float OutlineWidth = 2f;
    private const float MarkWidth = 2f;
    private readonly float _stateLayerRadius = (sizing ?? MaterialSizing.Desktop).StateLayerSize * 0.5f;

    /// <inheritdoc/>
    public override void Render(CheckBox checkBox, ref DrawingContext context)
    {
        var box = checkBox.GetIndicatorBounds();
        var center = new Point(box.X + box.Width * 0.5f, box.Y + box.Height * 0.5f);
        float progress = checkBox.CheckAnimationProgress;
        bool enabled = checkBox.IsEnabled;
        bool selected = checkBox.IsChecked != false;

        MaterialDrawing.DrawStateLayerCircle(ref context, center, selected ? colors.Primary : colors.OnSurface,
            MaterialDrawing.HaloOpacity(checkBox), _stateLayerRadius);

        if (progress <= 0.01f)
        {
            Color outline = !enabled ? MaterialDrawing.DisabledContent(colors)
                : checkBox.IsHovered || checkBox.IsPressed ? colors.OnSurface
                : colors.OnSurfaceVariant;
            context.DrawRoundedRectOutline(box, BoxCorner, outline, OutlineWidth);
        }
        else
        {
            Color container = enabled ? colors.Primary : MaterialDrawing.DisabledContent(colors);
            Color mark = enabled ? colors.OnPrimary : colors.Surface;

            // While animating from unchecked, the fill grows from the outline color.
            context.DrawRoundedRect(box, BoxCorner, enabled ? Color.Lerp(colors.OnSurfaceVariant, container, Math.Min(1f, progress * 2f)) : container);

            if (checkBox.IsChecked == null)
            {
                float half = 5f * progress;
                context.DrawLine(new Point(center.X - half, center.Y), new Point(center.X + half, center.Y), mark, MarkWidth);
            }
            else if (progress > 0.1f)
            {
                // Check mark scaled to the box; its long stroke grows with the progress.
                float s = box.Width / 18f;
                float x1 = box.X + 3.5f * s, y1 = box.Y + 9f * s;
                float x2 = box.X + 7.5f * s, y2 = box.Y + 13f * s;
                float x3 = box.X + 14.5f * s, y3 = box.Y + 5.5f * s;
                MaterialDrawing.DrawPolyline(ref context, x1, y1, x2, y2, x2 + (x3 - x2) * progress, y2 + (y3 - y2) * progress, mark, MarkWidth);
            }
        }

        if (checkBox.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRing(ref context, box, BoxCorner, colors.Secondary);
        }
    }
}
