using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a custom window <see cref="TitleBar"/>: a surface-container background (or the set
/// <see cref="Control.Background"/>) with an outline-variant divider along the bottom.
/// </summary>
public class MaterialTitleBarRenderer(MaterialColorScheme colors) : ControlRenderer<TitleBar>
{
    /// <inheritdoc/>
    public override void Render(TitleBar titleBar, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, titleBar.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        context.DrawRect(bounds, titleBar.Background.A > 0 ? titleBar.Background : colors.SurfaceContainer);
        context.DrawRect(new Rect(bounds.Left, bounds.Bottom - 1f, bounds.Width, 1f), colors.OutlineVariant);
    }
}
