using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a Material Design 3 <see cref="Dialog"/> container: surface-container-high (or the set background) with 28 px
/// corners at its elevation (level 3 in the theme's default style), with an optional border.
/// </summary>
public class MaterialDialogRenderer(MaterialColorScheme colors) : ControlRenderer<Dialog>
{
    /// <inheritdoc/>
    public override void Render(Dialog dialog, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, dialog.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (dialog.Elevation > 0)
        {
            context.DrawShadow(bounds, dialog.CornerRadius, dialog.Elevation, colors.Shadow);
        }

        context.DrawRoundedRect(bounds, dialog.CornerRadius, dialog.Background.A > 0 ? dialog.Background : colors.SurfaceContainerHigh);

        Color border = dialog.BorderBrush.A > 0 ? dialog.BorderBrush : colors.OutlineVariant;
        context.DrawBorder(bounds, dialog.CornerRadius, dialog.BorderThickness, border);
    }
}
