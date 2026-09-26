using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="Popup"/> like an MD3 menu: a surface-container background (or the set background) at its
/// elevation (level 2 in the theme's default style), with an optional border.
/// </summary>
public class MaterialPopupRenderer(MaterialColorScheme colors) : ControlRenderer<Popup>
{
    /// <inheritdoc/>
    public override void Render(Popup popup, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, popup.ActualBounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (popup.Elevation > 0)
        {
            context.DrawShadow(bounds, popup.CornerRadius, popup.Elevation, colors.Shadow);
        }

        context.DrawRoundedRect(bounds, popup.CornerRadius, popup.Background.A > 0 ? popup.Background : colors.SurfaceContainer);

        Color border = popup.BorderBrush.A > 0 ? popup.BorderBrush : colors.OutlineVariant;
        context.DrawBorder(bounds, popup.CornerRadius, popup.BorderThickness, border);
    }
}
