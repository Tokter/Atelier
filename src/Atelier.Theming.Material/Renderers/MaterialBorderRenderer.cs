using System;
using Atelier.Core.Primitives;
using Atelier.Layout;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="Border"/>: its elevation shadow, background and border (each side with its own thickness), all
/// following <see cref="Border.CornerRadius"/>.
/// </summary>
public class MaterialBorderRenderer(MaterialColorScheme? colors = null) : ControlRenderer<Border>
{
    /// <inheritdoc/>
    public override void Render(Border border, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, border.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (border.Elevation > 0)
        {
            context.DrawShadow(bounds, border.CornerRadius, border.Elevation, colors?.Shadow ?? Color.Black);
        }

        context.DrawRoundedRect(bounds, border.CornerRadius, border.Background);
        context.DrawBorder(bounds, border.CornerRadius, border.BorderThickness, border.BorderBrush);
    }
}
