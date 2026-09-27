using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="ToolTip"/> as an MD3 tooltip. A plain tooltip is a small inverse-surface container with
/// inverse-on-surface text. A rich tooltip is a surface container at its elevation (level 2 by default) whose content
/// uses the on-surface-variant color. Sizes, shapes and elevation come from the theme's tooltip styles.
/// </summary>
public class MaterialToolTipRenderer(MaterialColorScheme colors) : ControlRenderer<ToolTip>, IContentColorProvider
{
    /// <inheritdoc/>
    public override void Render(ToolTip toolTip, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, toolTip.ActualBounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (toolTip.Elevation > 0)
        {
            context.DrawShadow(bounds, toolTip.CornerRadius, toolTip.Elevation, colors.Shadow);
        }

        Color background = toolTip.Background.A > 0 ? toolTip.Background
            : toolTip.IsRich ? colors.SurfaceContainer : colors.InverseSurface;
        context.DrawRoundedRect(bounds, toolTip.CornerRadius, background);
    }

    /// <inheritdoc/>
    public bool TryGetContentColor(UIElement element, out Color color)
    {
        var toolTip = (ToolTip)element;
        color = toolTip.IsRich ? colors.OnSurfaceVariant : colors.InverseOnSurface;
        return true;
    }
}
