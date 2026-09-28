using System;
using System.Numerics;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;
using SkiaSharp;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws <see cref="Icon"/>s: custom geometry scaled into the bounds (its <see cref="Icon.ViewBox"/> if set), or a Material
/// Symbols glyph from the variable icon
/// font, in the icon's content color.
/// </summary>
/// <remarks>
/// The color is the icon's own foreground if set, else that of the nearest ancestor that sets a foreground or defines a
/// content color (an icon in a filled button is on-primary), else on-surface; disabled icons 38% opacity.
/// </remarks>
/// <param name="colors">The color scheme.</param>
/// <param name="renderers">The theme's renderers, asked for content colors; <c>null</c> only honors set foregrounds.</param>
public class MaterialIconRenderer(MaterialColorScheme colors, RendererRegistry? renderers = null) : ControlRenderer<Icon>
{
    /// <inheritdoc/>
    public override void Render(Icon icon, ref DrawingContext context)
    {
        if (icon.Size <= 0) return;

        var bounds = new Rect(Point.Zero, icon.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        Color color = MaterialDrawing.ResolveContentColor(icon, colors, renderers);

        // Custom geometry takes precedence over the glyph.
        if (icon.Data is { IsEmpty: false } path)
        {
            DrawPath(icon, path, bounds, color, ref context);
            return;
        }

        if (icon.Kind == MaterialIconKind.None) return;

        string glyph = MaterialIconFontManager.GetGlyph(icon.Kind); // cached per kind
        var typeface = MaterialIconFontManager.GetTypeface(icon.Fill, icon.Weight, icon.Grade, icon.OpticalSize);
        var font = context.PaintRegistry.GetFont(icon.Size, typeface);

        // Center the glyph optically using the font metrics.
        font.GetFontMetrics(out var metrics);
        float glyphWidth = font.MeasureText(glyph.AsSpan());
        float x = bounds.X + (bounds.Width - glyphWidth) * 0.5f;
        float y = bounds.Y + (bounds.Height - (metrics.Ascent + metrics.Descent)) * 0.5f;

        context.DrawText(glyph, x, y, font, color);
    }

    // Scales the view box (or else the path's bounds) uniformly into the bounds and centers it; strokes keep their width
    // in pixels.
    private static void DrawPath(Icon icon, SKPath path, in Rect bounds, Color color, ref DrawingContext context)
    {
        var pathBounds = icon.ViewBox is { Width: > 0, Height: > 0 } viewBox
            ? SKRect.Create(viewBox.X, viewBox.Y, viewBox.Width, viewBox.Height)
            : path.Bounds;
        if (pathBounds.Width <= 0 || pathBounds.Height <= 0) return;

        float scale = Math.Min(bounds.Width / pathBounds.Width, bounds.Height / pathBounds.Height);
        float offsetX = bounds.X + (bounds.Width - pathBounds.Width * scale) * 0.5f - pathBounds.Left * scale;
        float offsetY = bounds.Y + (bounds.Height - pathBounds.Height * scale) * 0.5f - pathBounds.Top * scale;

        var transform = Matrix3x2.CreateScale(scale, scale) * Matrix3x2.CreateTranslation(offsetX, offsetY);
        using (context.PushTransform(transform))
        {
            if (icon.StrokeWidth > 0)
            {
                context.DrawPathOutline(path, color, icon.StrokeWidth / scale);
            }
            else
            {
                context.DrawPath(path, color);
            }
        }
    }
}
