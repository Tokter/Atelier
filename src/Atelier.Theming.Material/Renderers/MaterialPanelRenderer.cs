using System;
using Atelier.Core.Primitives;
using Atelier.Layout;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>Draws the <see cref="Panel.Background"/> of every panel type.</summary>
public class MaterialPanelRenderer : ControlRenderer<Panel>
{
    /// <inheritdoc/>
    public override void Render(Panel panel, ref DrawingContext context)
    {
        var background = panel.Background;
        var size = panel.Bounds.Size;
        if (background.A > 0 && size.Width > 0 && size.Height > 0)
        {
            context.DrawRect(new Rect(Point.Zero, size), background);
        }
    }
}
