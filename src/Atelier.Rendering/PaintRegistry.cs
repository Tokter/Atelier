using System;
using SkiaSharp;
using Atelier.Core.Primitives;

namespace Atelier.Rendering;

/// <summary>
/// Supplies reusable paints and cached fonts to renderers.
/// </summary>
/// <remarks>
/// Returned paints are shared and reconfigured on every call, and fonts are owned by a shared cache: use them right away
/// and never dispose or store them.
/// </remarks>
public interface IPaintRegistry
{
    /// <summary>Returns the shared anti-aliased fill paint, set to <paramref name="color"/>.</summary>
    SKPaint GetFillPaint(Color color);

    /// <summary>Returns the shared non-anti-aliased fill paint (for pixel-exact rectangles), set to <paramref name="color"/>.</summary>
    SKPaint GetPixelFillPaint(Color color);

    /// <summary>Returns the shared anti-aliased stroke paint, set to <paramref name="color"/> and <paramref name="strokeWidth"/>.</summary>
    SKPaint GetStrokePaint(Color color, float strokeWidth);

    /// <summary>Returns the cached font for <paramref name="typeface"/> (or the default typeface) at <paramref name="fontSize"/>.</summary>
    SKFont GetFont(float fontSize, SKTypeface? typeface = null);

    /// <summary>Returns the cached typeface for a family and style; a <c>null</c> or unknown family gives the default typeface.</summary>
    SKTypeface GetTypeface(string? familyName, bool bold = false, bool italic = false);
}

/// <summary>
/// The default <see cref="IPaintRegistry"/>: three reusable paints plus the process-wide font cache that
/// <see cref="TextMeasurer"/> also uses, so text is drawn with exactly the fonts it was measured with.
/// </summary>
public sealed class PaintRegistry : IPaintRegistry, IDisposable
{
    private readonly SKPaint _fillPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private readonly SKPaint _pixelFillPaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Fill
    };

    private readonly SKPaint _strokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke
    };

    /// <inheritdoc/>
    public SKTypeface GetTypeface(string? familyName, bool bold = false, bool italic = false) =>
        FontCache.GetTypeface(familyName, bold, italic);

    /// <inheritdoc/>
    public SKFont GetFont(float fontSize, SKTypeface? typeface = null) =>
        FontCache.GetFont(typeface ?? SKTypeface.Default, fontSize);

    /// <inheritdoc/>
    public SKPaint GetFillPaint(Color color)
    {
        _fillPaint.Color = new SKColor(color.R, color.G, color.B, color.A);
        return _fillPaint;
    }

    /// <inheritdoc/>
    public SKPaint GetPixelFillPaint(Color color)
    {
        _pixelFillPaint.Color = new SKColor(color.R, color.G, color.B, color.A);
        return _pixelFillPaint;
    }

    /// <inheritdoc/>
    public SKPaint GetStrokePaint(Color color, float strokeWidth)
    {
        _strokePaint.Color = new SKColor(color.R, color.G, color.B, color.A);
        _strokePaint.StrokeWidth = strokeWidth;
        return _strokePaint;
    }

    /// <summary>Disposes the paints. Fonts and typefaces belong to the shared cache and stay alive.</summary>
    public void Dispose()
    {
        _fillPaint.Dispose();
        _pixelFillPaint.Dispose();
        _strokePaint.Dispose();
    }
}
