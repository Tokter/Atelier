using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="GridSplitter"/> as a subtle divider: a 1 px outline-variant line along the middle of its hit
/// area, in the outline color while hovered and a 2 px primary line while dragged. Keyboard focus shows the focus ring
/// around the hit area.
/// </summary>
public class MaterialGridSplitterRenderer(MaterialColorScheme colors) : ControlRenderer<GridSplitter>
{
    /// <inheritdoc/>
    public override void Render(GridSplitter splitter, ref DrawingContext context)
    {
        var bounds = new Rect(0, 0, splitter.Bounds.Width, splitter.Bounds.Height);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (splitter.Background.A > 0)
        {
            context.DrawRect(bounds, splitter.Background);
        }

        bool active = splitter.IsDragging;
        float thickness = active ? 2f : 1f;
        Color color = !splitter.IsEnabled ? colors.OutlineVariant.WithOpacity(0.38f)
            : active ? colors.Primary
            : splitter.IsHovered ? colors.Outline
            : colors.OutlineVariant;

        var line = splitter.ActualDirection == GridResizeDirection.Columns
            ? new Rect((bounds.Width - thickness) * 0.5f, 0, thickness, bounds.Height)
            : new Rect(0, (bounds.Height - thickness) * 0.5f, bounds.Width, thickness);
        context.DrawPixelRect(line, color);

        if (splitter.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRing(ref context, bounds, new CornerRadius(2), colors.Secondary, offset: 0);
        }
    }
}
