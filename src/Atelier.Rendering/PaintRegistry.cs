using System;
using System.Collections.Concurrent;
using SkiaSharp;
using Atelier.Core.Primitives;

namespace Atelier.Rendering;

public interface IPaintRegistry
{
    SKPaint GetFillPaint(Color color);
    SKPaint GetPixelFillPaint(Color color);
    SKPaint GetStrokePaint(Color color, float strokeWidth);
    SKFont GetFont(float fontSize, SKTypeface? typeface = null);
    SKTypeface GetTypeface(string? familyName, bool bold = false, bool italic = false);
}

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

    private readonly ConcurrentDictionary<string, SKTypeface> _typefaces = new();
    private readonly ConcurrentDictionary<(int size, IntPtr tf), SKFont> _fonts = new();

    public SKTypeface GetTypeface(string? familyName, bool bold = false, bool italic = false)
    {
        string key = $"{familyName ?? "Default"}_{(bold ? "Bold" : "Regular")}_{(italic ? "Italic" : "Upright")}";
        return _typefaces.GetOrAdd(key, _ =>
        {
            var weight = bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
            return SKTypeface.FromFamilyName(familyName, style) ?? SKTypeface.Default;
        });
    }

    public SKFont GetFont(float fontSize, SKTypeface? typeface = null)
    {
        var tf = typeface ?? SKTypeface.Default;
        int sizeKey = (int)MathF.Round(fontSize * 10);
        return _fonts.GetOrAdd((sizeKey, tf.Handle), _ => new SKFont(tf, fontSize));
    }

    public SKPaint GetFillPaint(Color color)
    {
        _fillPaint.Color = new SKColor(color.R, color.G, color.B, color.A);
        return _fillPaint;
    }

    public SKPaint GetPixelFillPaint(Color color)
    {
        _pixelFillPaint.Color = new SKColor(color.R, color.G, color.B, color.A);
        return _pixelFillPaint;
    }

    public SKPaint GetStrokePaint(Color color, float strokeWidth)
    {
        _strokePaint.Color = new SKColor(color.R, color.G, color.B, color.A);
        _strokePaint.StrokeWidth = strokeWidth;
        return _strokePaint;
    }

    public void Dispose()
    {
        _fillPaint.Dispose();
        _pixelFillPaint.Dispose();
        _strokePaint.Dispose();
        foreach (var font in _fonts.Values)
        {
            font.Dispose();
        }
        _fonts.Clear();

        foreach (var tf in _typefaces.Values)
        {
            tf.Dispose();
        }
        _typefaces.Clear();
    }
}
