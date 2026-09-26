using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws <see cref="ScrollViewer"/> scroll bars over the content (MD3 has no scroll bar spec; they follow its colors):
/// a thin, translucent outline-colored thumb that widens with a surface-container-highest track on hover and turns
/// primary while dragged.
/// </summary>
public class MaterialScrollViewerRenderer(MaterialColorScheme colors) : ControlRenderer<ScrollViewer>
{
    /// <inheritdoc/>
    public override void RenderOverlay(ScrollViewer scrollViewer, ref DrawingContext context)
    {
        if (scrollViewer.IsVerticalScrollBarVisible)
        {
            var track = scrollViewer.GetVerticalTrackRect();
            DrawBar(track, scrollViewer.GetVerticalThumbRect(), track.Width, scrollViewer.IsVerticalThumbDragging, ref context);
        }

        if (scrollViewer.IsHorizontalScrollBarVisible)
        {
            var track = scrollViewer.GetHorizontalTrackRect();
            DrawBar(track, scrollViewer.GetHorizontalThumbRect(), track.Height, scrollViewer.IsHorizontalThumbDragging, ref context);
        }
    }

    private void DrawBar(in Rect track, in Rect thumb, float thickness, bool dragging, ref DrawingContext context)
    {
        if (thumb.Width <= 0 || thumb.Height <= 0 || track.Width <= 0 || track.Height <= 0) return;

        // The bar widens from the normal to the hovered width; its progress drives the hover appearance.
        float hover = Math.Clamp(
            (thickness - ScrollViewer.NormalScrollBarWidth) / Math.Max(1f, ScrollViewer.HoveredScrollBarWidth - ScrollViewer.NormalScrollBarWidth),
            0f, 1f);
        var corner = new CornerRadius(thickness * 0.5f);

        if (hover > 0.001f)
        {
            context.DrawRoundedRect(track, corner, colors.SurfaceContainerHighest.WithOpacity(0.6f * hover));
        }

        Color active = dragging ? colors.Primary : colors.OnSurfaceVariant;
        context.DrawRoundedRect(thumb, corner, Color.Lerp(colors.Outline.WithOpacity(0.45f), active, hover));
    }
}
