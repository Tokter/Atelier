using System;
using System.Numerics;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Layout;
using Atelier.Rendering;
using SkiaSharp;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="WorkspaceView"/>: a surface-container strip behind the leading content, the workspace tabs and the
/// trailing content.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialWorkspaceViewRenderer(MaterialColorScheme colors) : ControlRenderer<WorkspaceView>
{
    /// <inheritdoc/>
    public override void Render(WorkspaceView view, ref DrawingContext context)
    {
        var strip = view.StripBounds;
        if (strip.Width <= 0 || strip.Height <= 0) return;
        context.DrawRect(strip, colors.SurfaceContainer);
    }
}

/// <summary>
/// Draws an <see cref="AreaLayout"/>: the surface behind the areas (showing in the gaps), and over the areas the preview
/// of a split (the new line in primary, the new part tinted), a join (the area that closes tinted in primary with an
/// arrow pointing into it from the area that grows) or a swap (both areas outlined, the target tinted).
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialAreaLayoutRenderer(MaterialColorScheme colors) : ControlRenderer<AreaLayout>
{
    private const float ArrowLength = 56f;
    private const float ArrowHeadWidth = 40f;
    private const float ArrowShaftWidth = 14f;
    private const float ArrowHeadLength = ArrowLength * 0.45f;

    // The arrow pointing along +x, centered on the origin.
    private static readonly SKPath s_arrow = CreateArrow();

    /// <inheritdoc/>
    public override void Render(AreaLayout layout, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, layout.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        context.DrawRect(bounds, colors.Surface);
    }

    /// <inheritdoc/>
    public override void RenderOverlay(AreaLayout layout, ref DrawingContext context)
    {
        var corner = new CornerRadius(MaterialShape.Small);
        switch (layout.PreviewKind)
        {
            case AreaPreviewKind.Split:
            {
                var area = layout.PreviewAreaBounds;
                var line = layout.PreviewSplitLine;
                // Tint the part that becomes the new area.
                bool horizontal = layout.PreviewOrientation == Orientation.Horizontal;
                var part = horizontal
                    ? layout.PreviewNewAreaFirst
                        ? new Rect(area.X, area.Y, line.X - area.X, area.Height)
                        : new Rect(line.Right, area.Y, area.Right - line.Right, area.Height)
                    : layout.PreviewNewAreaFirst
                        ? new Rect(area.X, area.Y, area.Width, line.Y - area.Y)
                        : new Rect(area.X, line.Bottom, area.Width, area.Bottom - line.Bottom);
                using (context.PushRoundedClip(area, corner))
                {
                    context.DrawRect(part, colors.Primary.WithOpacity(0.08f));
                }
                context.DrawRoundedRectOutline(area, corner, colors.Primary, 2f);
                context.DrawRect(line, colors.Primary);
                break;
            }
            case AreaPreviewKind.Join:
            {
                var target = layout.PreviewTargetBounds;
                context.DrawRoundedRect(target, corner, colors.Primary.WithOpacity(0.16f));
                context.DrawRoundedRectOutline(layout.PreviewAreaBounds, corner, colors.Primary, 2f);
                context.DrawRoundedRectOutline(target, corner, colors.Primary.WithOpacity(0.6f), 2f);
                DrawArrow(ref context, target.Center, layout.PreviewDirection, Math.Min(1f, Math.Min(target.Width, target.Height) / (ArrowLength * 1.5f)));
                break;
            }
            case AreaPreviewKind.Swap:
            {
                context.DrawRoundedRect(layout.PreviewTargetBounds, corner, colors.Primary.WithOpacity(0.12f));
                context.DrawRoundedRectOutline(layout.PreviewAreaBounds, corner, colors.Primary, 2f);
                context.DrawRoundedRectOutline(layout.PreviewTargetBounds, corner, colors.Primary, 2f);
                break;
            }
        }
    }

    // An arrow pointing in `direction`, centered on `center`.
    private void DrawArrow(ref DrawingContext context, Point center, Dock direction, float scale)
    {
        if (scale <= 0.2f) return;
        float angle = direction switch
        {
            Dock.Left => MathF.PI,
            Dock.Top => -MathF.PI / 2,
            Dock.Bottom => MathF.PI / 2,
            _ => 0f,
        };
        var transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(transform))
        {
            context.DrawPath(s_arrow, colors.Primary);
        }
    }

    private static SKPath CreateArrow()
    {
        const float half = ArrowLength / 2, neck = half - ArrowHeadLength;
        using var builder = new SKPathBuilder();
        builder.MoveTo(-half, -ArrowShaftWidth / 2);
        builder.LineTo(neck, -ArrowShaftWidth / 2);
        builder.LineTo(neck, -ArrowHeadWidth / 2);
        builder.LineTo(half, 0);
        builder.LineTo(neck, ArrowHeadWidth / 2);
        builder.LineTo(neck, ArrowShaftWidth / 2);
        builder.LineTo(-half, ArrowShaftWidth / 2);
        builder.Close();
        return builder.Detach();
    }
}

/// <summary>
/// Draws an <see cref="Area"/> as an outlined MD3 surface: a surface-container-low body with a 1 px outline-variant
/// outline, rounded by <see cref="Control.CornerRadius"/> (MD3 small, 8 px), and a surface-container header over it.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialAreaRenderer(MaterialColorScheme colors) : ControlRenderer<Area>
{
    /// <inheritdoc/>
    public override void Render(Area area, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, area.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var corner = area.CornerRadius;

        context.DrawRoundedRect(bounds, corner, area.Background.A > 0 ? area.Background : colors.SurfaceContainerLow);
        var header = area.HeaderBounds;
        if (header.Height > 0)
        {
            context.DrawRoundedRect(header, new CornerRadius(corner.TopLeft, corner.TopRight, 0, 0), colors.SurfaceContainer);
            context.DrawRect(new Rect(header.X, header.Bottom - 1, header.Width, 1), colors.OutlineVariant.WithOpacity(0.6f));
        }
    }

    /// <inheritdoc/>
    public override void RenderOverlay(Area area, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, area.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        // The outline goes over the content, which is clipped to the rounded shape.
        context.DrawRoundedRectOutline(new Rect(0.5f, 0.5f, bounds.Width - 1, bounds.Height - 1), area.CornerRadius, colors.OutlineVariant, 1f);
    }
}

/// <summary>
/// Draws an <see cref="AreaBorder"/>: nothing at rest (the gap shows the surface), a 2 px outline-colored line along the
/// gap while hovered, and a primary line while dragged.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialAreaBorderRenderer(MaterialColorScheme colors) : ControlRenderer<AreaBorder>
{
    /// <inheritdoc/>
    public override void Render(AreaBorder border, ref DrawingContext context)
    {
        if (!border.IsHovered && !border.IsDragging) return;
        var bounds = new Rect(Point.Zero, border.Bounds.Size);
        const float thickness = 2f, inset = 6f;
        var line = border.Split.Orientation == Orientation.Horizontal
            ? new Rect((bounds.Width - thickness) * 0.5f, inset, thickness, Math.Max(0, bounds.Height - 2 * inset))
            : new Rect(inset, (bounds.Height - thickness) * 0.5f, Math.Max(0, bounds.Width - 2 * inset), thickness);
        context.DrawRoundedRect(line, new CornerRadius(1), border.IsDragging ? colors.Primary : colors.Outline);
    }
}

/// <summary>
/// Draws an <see cref="AreaCorner"/>: nothing at rest, and a small triangle filling the corner while hovered or dragged
/// (on-surface-variant, primary while dragged), hinting at the action zone.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialAreaCornerRenderer(MaterialColorScheme colors) : ControlRenderer<AreaCorner>
{
    private const float TriangleSize = 9f;
    private const float Inset = 3f;

    // The triangle of the top-left corner; the others are mirrored.
    private static readonly SKPath s_triangle = CreateTriangle();

    /// <inheritdoc/>
    public override void Render(AreaCorner corner, ref DrawingContext context)
    {
        if (!corner.IsHovered && !corner.IsDragging) return;
        float w = corner.Bounds.Width, h = corner.Bounds.Height;
        var (x, y, sx, sy) = corner.Position switch
        {
            AreaCornerPosition.TopLeft => (Inset, Inset, 1f, 1f),
            AreaCornerPosition.TopRight => (w - Inset, Inset, -1f, 1f),
            AreaCornerPosition.BottomLeft => (Inset, h - Inset, 1f, -1f),
            _ => (w - Inset, h - Inset, -1f, -1f),
        };
        using (context.PushTransform(Matrix3x2.CreateScale(sx, sy) * Matrix3x2.CreateTranslation(x, y)))
        {
            context.DrawPath(s_triangle, corner.IsDragging ? colors.Primary : colors.OnSurfaceVariant.WithOpacity(0.5f));
        }
    }

    private static SKPath CreateTriangle()
    {
        using var builder = new SKPathBuilder();
        builder.MoveTo(0, 0);
        builder.LineTo(TriangleSize, 0);
        builder.LineTo(0, TriangleSize);
        builder.Close();
        return builder.Detach();
    }
}
