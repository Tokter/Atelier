using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="Toolbar"/> like an MD3 top app bar: a surface-container background (or the set
/// <see cref="Control.Background"/>), an optional elevation shadow, and its border (per-side thickness, e.g. a bottom
/// divider).
/// </summary>
public class MaterialToolbarRenderer(MaterialColorScheme colors) : ControlRenderer<Toolbar>
{
    /// <inheritdoc/>
    public override void Render(Toolbar toolbar, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, toolbar.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (toolbar.Elevation > 0)
        {
            context.DrawShadow(bounds, toolbar.CornerRadius, toolbar.Elevation, colors.Shadow);
        }

        context.DrawRoundedRect(bounds, toolbar.CornerRadius, toolbar.Background.A > 0 ? toolbar.Background : colors.SurfaceContainer);
        context.DrawBorder(bounds, toolbar.CornerRadius, toolbar.BorderThickness, toolbar.BorderBrush);
    }
}
