using System;
using System.Collections.Generic;
using System.Numerics;
using SkiaSharp;
using Atelier.Core.Primitives;

namespace Atelier.Rendering;

public readonly ref struct DrawingContext
{
    public SKCanvas Canvas { get; }
    public IPaintRegistry PaintRegistry { get; }
    public float CurrentOpacity { get; }

    public DrawingContext(SKCanvas canvas, IPaintRegistry paintRegistry, float opacity = 1.0f)
    {
        Canvas = canvas;
        PaintRegistry = paintRegistry;
        CurrentOpacity = opacity;
    }

    public readonly ref struct Scope
    {
        private readonly SKCanvas? _canvas;
        private readonly int _saveCount;

        public Scope(SKCanvas canvas)
        {
            _canvas = canvas;
            _saveCount = canvas.Save();
        }

        public void Dispose()
        {
            if (_canvas != null)
            {
                _canvas.RestoreToCount(_saveCount);
            }
        }
    }

    public Scope PushTransform(in Matrix3x2 matrix)
    {
        if (matrix.IsIdentity) return default;
        var scope = new Scope(Canvas);
        var skMatrix = new SKMatrix(
            matrix.M11, matrix.M21, matrix.M31,
            matrix.M12, matrix.M22, matrix.M32,
            0, 0, 1);
        Canvas.Concat(in skMatrix);
        return scope;
    }

    public Scope PushClip(in Rect rect)
    {
        var scope = new Scope(Canvas);
        Canvas.ClipRect(new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom), SKClipOperation.Intersect, antialias: true);
        return scope;
    }

    public Scope PushRoundedClip(in Rect rect, in CornerRadius radius)
    {
        var scope = new Scope(Canvas);
        Canvas.ClipRoundRect(GetRoundRect(rect, radius, 0f), SKClipOperation.Intersect, antialias: true);
        return scope;
    }

    /// <summary>
    /// Intersects the current clip with the rounded rectangle (without saving the canvas; the caller restores it).
    /// </summary>
    public void ClipRoundedRect(in Rect rect, in CornerRadius radius)
    {
        Canvas.ClipRoundRect(GetRoundRect(rect, radius, 0f), SKClipOperation.Intersect, antialias: true);
    }

    /// <summary>
    /// Saves the canvas into an offscreen layer that is composited at <paramref name="opacity"/> when restored, and
    /// returns the save count to restore to. Reuses one paint, so it doesn't allocate.
    /// </summary>
    public int SaveOpacityLayer(float opacity)
    {
        var paint = t_layerPaint ??= new SKPaint();
        paint.Color = new SKColor(255, 255, 255, (byte)Math.Clamp((int)MathF.Round(opacity * 255f), 0, 255));
        return Canvas.SaveLayer(paint);
    }

    [ThreadStatic] private static SKPaint? t_layerPaint;

    // Rendering happens on the UI thread; these scratch objects are reused per thread so drawing rounded shapes with
    // per-corner radii, shadows and translucent images doesn't allocate managed or native objects per call.
    [ThreadStatic] private static SKRoundRect? t_roundRect;
    [ThreadStatic] private static SKPoint[]? t_radii;
    [ThreadStatic] private static SKPaint? t_shadowPaint;
    [ThreadStatic] private static SKPaint? t_imagePaint;
    [ThreadStatic] private static Dictionary<int, SKMaskFilter>? t_blurFilters;

    // Blur sigmas are quantized to this step, so animated elevations reuse a handful of mask filters.
    private const float BlurStep = 0.25f;
    private const int MaxBlurFilters = 64;

    /// <summary>
    /// Returns the thread's reusable round rect set to <paramref name="rect"/> with each corner's radius reduced by
    /// <paramref name="inset"/>. The result is only valid until the next call.
    /// </summary>
    private static SKRoundRect GetRoundRect(in Rect rect, in CornerRadius radius, float inset)
    {
        var rrect = t_roundRect ??= new SKRoundRect();
        var radii = t_radii ??= new SKPoint[4];
        radii[0] = Corner(radius.TopLeft, inset);
        radii[1] = Corner(radius.TopRight, inset);
        radii[2] = Corner(radius.BottomRight, inset);
        radii[3] = Corner(radius.BottomLeft, inset);
        rrect.SetRectRadii(new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom), radii);
        return rrect;

        static SKPoint Corner(float r, float inset)
        {
            float v = Math.Max(0, r - inset);
            return new SKPoint(v, v);
        }
    }

    private static SKMaskFilter GetBlurFilter(float sigma)
    {
        int key = (int)MathF.Round(sigma / BlurStep);
        var filters = t_blurFilters ??= new Dictionary<int, SKMaskFilter>();
        if (!filters.TryGetValue(key, out var filter))
        {
            if (filters.Count >= MaxBlurFilters)
            {
                // Unbounded elevations shouldn't grow native memory without limit; the cache refills quickly.
                foreach (var f in filters.Values) f.Dispose();
                filters.Clear();
            }

            filter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, Math.Max(BlurStep, key * BlurStep));
            filters[key] = filter;
        }
        return filter;
    }

    public void DrawRect(in Rect rect, in Color color)
    {
        if (color.A == 0 || rect.Width <= 0 || rect.Height <= 0) return;
        var paint = PaintRegistry.GetFillPaint(ApplyOpacity(color));
        Canvas.DrawRect(rect.Left, rect.Top, rect.Width, rect.Height, paint);
    }

    public void DrawPixelRect(in Rect rect, in Color color)
    {
        if (color.A == 0 || rect.Width <= 0 || rect.Height <= 0) return;
        var paint = PaintRegistry.GetPixelFillPaint(ApplyOpacity(color));
        float x = MathF.Round(rect.Left);
        float y = MathF.Round(rect.Top);
        float w = MathF.Max(1f, MathF.Round(rect.Width));
        float h = MathF.Max(1f, MathF.Round(rect.Height));
        Canvas.DrawRect(x, y, w, h, paint);
    }

    public void DrawRoundedRect(in Rect rect, in CornerRadius radius, in Color color)
    {
        if (color.A == 0 || rect.Width <= 0 || rect.Height <= 0) return;

        var paint = PaintRegistry.GetFillPaint(ApplyOpacity(color));
        if (radius.IsUniform)
        {
            Canvas.DrawRoundRect(rect.Left, rect.Top, rect.Width, rect.Height, radius.TopLeft, radius.TopLeft, paint);
        }
        else
        {
            Canvas.DrawRoundRect(GetRoundRect(rect, radius, 0f), paint);
        }
    }

    public void DrawRoundedRectOutline(in Rect rect, in CornerRadius radius, in Color color, float strokeWidth)
    {
        if (color.A == 0 || strokeWidth <= 0 || rect.Width <= 0 || rect.Height <= 0) return;

        var paint = PaintRegistry.GetStrokePaint(ApplyOpacity(color), strokeWidth);
        float inset = strokeWidth * 0.5f;
        var adjustedRect = new Rect(rect.X + inset, rect.Y + inset, Math.Max(0, rect.Width - strokeWidth), Math.Max(0, rect.Height - strokeWidth));

        if (radius.IsUniform)
        {
            float r = Math.Max(0, radius.TopLeft - inset);
            Canvas.DrawRoundRect(adjustedRect.Left, adjustedRect.Top, adjustedRect.Width, adjustedRect.Height, r, r, paint);
        }
        else
        {
            Canvas.DrawRoundRect(GetRoundRect(adjustedRect, radius, inset), paint);
        }
    }

    [ThreadStatic] private static SKRoundRect? t_innerRoundRect;

    /// <summary>
    /// Fills a border of per-side <paramref name="thickness"/> just inside <paramref name="rect"/>, following
    /// <paramref name="radius"/> on the outside (the inner corners are reduced by the adjacent thicknesses). Unlike a
    /// stroked outline, this supports different thicknesses per side.
    /// </summary>
    public void DrawBorder(in Rect rect, in CornerRadius radius, in Thickness thickness, in Color color)
    {
        if (color.A == 0 || rect.Width <= 0 || rect.Height <= 0) return;
        if (thickness.Left <= 0 && thickness.Top <= 0 && thickness.Right <= 0 && thickness.Bottom <= 0) return;

        var paint = PaintRegistry.GetFillPaint(ApplyOpacity(color));
        var outer = GetRoundRect(rect, radius, 0f);

        var innerRect = new SKRect(
            rect.Left + thickness.Left,
            rect.Top + thickness.Top,
            Math.Max(rect.Left + thickness.Left, rect.Right - thickness.Right),
            Math.Max(rect.Top + thickness.Top, rect.Bottom - thickness.Bottom));
        var radii = t_radii ??= new SKPoint[4];
        radii[0] = new SKPoint(Math.Max(0, radius.TopLeft - thickness.Left), Math.Max(0, radius.TopLeft - thickness.Top));
        radii[1] = new SKPoint(Math.Max(0, radius.TopRight - thickness.Right), Math.Max(0, radius.TopRight - thickness.Top));
        radii[2] = new SKPoint(Math.Max(0, radius.BottomRight - thickness.Right), Math.Max(0, radius.BottomRight - thickness.Bottom));
        radii[3] = new SKPoint(Math.Max(0, radius.BottomLeft - thickness.Left), Math.Max(0, radius.BottomLeft - thickness.Bottom));
        var inner = t_innerRoundRect ??= new SKRoundRect();
        inner.SetRectRadii(innerRect, radii);

        if (inner.Rect.Width <= 0 || inner.Rect.Height <= 0)
        {
            Canvas.DrawRoundRect(outer, paint); // the border fills the whole shape
        }
        else
        {
            Canvas.DrawRoundRectDifference(outer, inner, paint);
        }
    }

    public void DrawCircle(in Point center, float radius, in Color color)
    {
        if (color.A == 0 || radius <= 0) return;
        var paint = PaintRegistry.GetFillPaint(ApplyOpacity(color));
        Canvas.DrawCircle(center.X, center.Y, radius, paint);
    }

    public void DrawCircleOutline(in Point center, float radius, in Color color, float strokeWidth)
    {
        if (color.A == 0 || radius <= 0 || strokeWidth <= 0) return;
        var paint = PaintRegistry.GetStrokePaint(ApplyOpacity(color), strokeWidth);
        Canvas.DrawCircle(center.X, center.Y, radius - strokeWidth * 0.5f, paint);
    }

    public void DrawLine(in Point start, in Point end, in Color color, float strokeWidth)
    {
        if (color.A == 0 || strokeWidth <= 0) return;
        var paint = PaintRegistry.GetStrokePaint(ApplyOpacity(color), strokeWidth);
        Canvas.DrawLine(start.X, start.Y, end.X, end.Y, paint);
    }

    public void DrawText(
        string text,
        in Point position,
        in Color color,
        float fontSize,
        string? fontFamily = null,
        bool bold = false,
        bool italic = false)
    {
        if (string.IsNullOrEmpty(text) || color.A == 0 || fontSize <= 0) return;
        var tf = PaintRegistry.GetTypeface(fontFamily, bold, italic);
        var font = PaintRegistry.GetFont(fontSize, tf);
        var paint = PaintRegistry.GetFillPaint(ApplyOpacity(color));
        DrawTextBlob(text, position.X, position.Y, font, paint);
    }

    /// <summary>Draws single-line text with its baseline at <paramref name="position"/> in the given weight.</summary>
    public void DrawText(string text, in Point position, in Color color, float fontSize, string? fontFamily, FontWeight weight, bool italic = false)
    {
        if (string.IsNullOrEmpty(text) || color.A == 0 || fontSize <= 0) return;
        var font = PaintRegistry.GetFont(fontSize, PaintRegistry.GetTypeface(fontFamily, weight, italic));
        DrawTextBlob(text, position.X, position.Y, font, PaintRegistry.GetFillPaint(ApplyOpacity(color)));
    }

    /// <summary>
    /// Draws single-line text in <paramref name="font"/> (for example an icon font) with its baseline at
    /// (<paramref name="x"/>, <paramref name="y"/>). Unchanged text is shaped once and cached, so it doesn't allocate.
    /// </summary>
    public void DrawText(string text, float x, float y, SKFont font, in Color color)
    {
        if (string.IsNullOrEmpty(text) || color.A == 0) return;
        DrawTextBlob(text, x, y, font, PaintRegistry.GetFillPaint(ApplyOpacity(color)));
    }

    // SKCanvas.DrawText(string) shapes the text into a new native blob per call; the cache shapes each text once.
    private void DrawTextBlob(string text, float x, float y, SKFont font, SKPaint paint)
    {
        var blob = TextBlobCache.Get(text, font);
        if (blob != null)
        {
            Canvas.DrawText(blob, x, y, paint);
        }
    }

    /// <summary>Measures single-line text in the given weight: its width and the font's line spacing.</summary>
    public Size MeasureText(string text, float fontSize, string? fontFamily, FontWeight weight, bool italic = false)
    {
        if (string.IsNullOrEmpty(text) || fontSize <= 0) return Size.Zero;
        var font = PaintRegistry.GetFont(fontSize, PaintRegistry.GetTypeface(fontFamily, weight, italic));
        return new Size(font.MeasureText(text, out _), font.Spacing);
    }

    public Size MeasureText(string text, float fontSize, string? fontFamily = null, bool bold = false, bool italic = false)
    {
        if (string.IsNullOrEmpty(text) || fontSize <= 0) return Size.Zero;
        var tf = PaintRegistry.GetTypeface(fontFamily, bold, italic);
        var font = PaintRegistry.GetFont(fontSize, tf);
        float width = font.MeasureText(text, out _);
        return new Size(width, font.Spacing);
    }

    public void DrawShadow(in Rect rect, in CornerRadius radius, float elevation, in Color shadowColor)
    {
        if (elevation <= 0 || shadowColor.A == 0) return;

        // Material 3 two-component shadow: Key light + Ambient light
        float ambientAlpha = Math.Clamp(0.08f + elevation * 0.025f, 0.05f, 0.30f);
        float keyAlpha = Math.Clamp(0.12f + elevation * 0.035f, 0.08f, 0.40f);

        float ambientBlur = 2.5f + elevation * 2.0f;
        float keyBlur = 2.5f + elevation * 2.5f;
        float keyOffsetY = 1.0f + elevation * 1.5f;

        DrawShadowLayer(rect, radius, ambientBlur, 0, 0, shadowColor.WithAlpha(ambientAlpha));
        DrawShadowLayer(rect, radius, keyBlur, 0, keyOffsetY, shadowColor.WithAlpha(keyAlpha));
    }

    // A blurred copy of the shape, offset by (dx, dy). Uses a cached blur mask filter and a reused paint, which is much
    // cheaper than an image filter (no offscreen layer) and allocates nothing per frame.
    private void DrawShadowLayer(in Rect rect, in CornerRadius radius, float blur, float dx, float dy, in Color color)
    {
        var finalColor = ApplyOpacity(color);
        if (finalColor.A == 0) return;

        var paint = t_shadowPaint ??= new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        paint.Color = new SKColor(finalColor.R, finalColor.G, finalColor.B, finalColor.A);
        paint.MaskFilter = GetBlurFilter(blur);

        var shadowRect = new Rect(rect.X + dx, rect.Y + dy, rect.Width, rect.Height);
        if (radius.IsUniform)
        {
            Canvas.DrawRoundRect(shadowRect.Left, shadowRect.Top, shadowRect.Width, shadowRect.Height, radius.TopLeft, radius.TopLeft, paint);
        }
        else
        {
            Canvas.DrawRoundRect(GetRoundRect(shadowRect, radius, 0f), paint);
        }
    }

    [ThreadStatic] private static SKPaint? t_gradientPaint;

    /// <summary>
    /// Fills a rounded rectangle with a linear gradient from <paramref name="start"/> to <paramref name="end"/> through
    /// <paramref name="colors"/>, spaced evenly. Beyond the ends the first and last colors continue.
    /// </summary>
    public void DrawLinearGradient(in Rect rect, in CornerRadius radius, in Point start, in Point end, ReadOnlySpan<Color> colors)
    {
        if (colors.Length == 0 || rect.Width <= 0 || rect.Height <= 0) return;
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(start.X, start.Y), new SKPoint(end.X, end.Y), ToSkColors(colors), SKShaderTileMode.Clamp);
        var paint = GetGradientPaint(shader);
        if (radius.IsUniform)
        {
            Canvas.DrawRoundRect(rect.Left, rect.Top, rect.Width, rect.Height, radius.TopLeft, radius.TopLeft, paint);
        }
        else
        {
            Canvas.DrawRoundRect(GetRoundRect(rect, radius, 0f), paint);
        }
        paint.Shader = null;
    }

    /// <summary>
    /// Fills a circle with a sweep (conic) gradient through <paramref name="colors"/>, spaced evenly, clockwise from
    /// the right (3 o'clock). Repeat the first color at the end for a seamless ring.
    /// </summary>
    public void DrawSweepGradientCircle(in Point center, float radius, ReadOnlySpan<Color> colors)
    {
        if (colors.Length == 0 || radius <= 0) return;
        using var shader = SKShader.CreateSweepGradient(new SKPoint(center.X, center.Y), ToSkColors(colors));
        var paint = GetGradientPaint(shader);
        Canvas.DrawCircle(center.X, center.Y, radius, paint);
        paint.Shader = null;
    }

    /// <summary>Fills a circle with a radial gradient from <paramref name="inner"/> at the center to <paramref name="outer"/> at the edge.</summary>
    public void DrawRadialGradientCircle(in Point center, float radius, in Color inner, in Color outer)
    {
        if (radius <= 0) return;
        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(center.X, center.Y), radius, ToSkColors([inner, outer]), SKShaderTileMode.Clamp);
        var paint = GetGradientPaint(shader);
        Canvas.DrawCircle(center.X, center.Y, radius, paint);
        paint.Shader = null;
    }

    /// <summary>
    /// Fills a rounded rectangle with a checkerboard of <paramref name="cellSize"/> squares, the usual backdrop that
    /// makes transparency visible.
    /// </summary>
    public void DrawCheckerboard(in Rect rect, in CornerRadius radius, float cellSize, in Color light, in Color dark)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || cellSize <= 0) return;
        using var clip = PushRoundedClip(rect, radius);
        DrawRect(rect, light);
        var darkPaint = PaintRegistry.GetFillPaint(ApplyOpacity(dark));
        int columns = (int)MathF.Ceiling(rect.Width / cellSize);
        int rows = (int)MathF.Ceiling(rect.Height / cellSize);
        for (int row = 0; row < rows; row++)
        {
            for (int column = row % 2; column < columns; column += 2)
            {
                Canvas.DrawRect(rect.Left + column * cellSize, rect.Top + row * cellSize, cellSize, cellSize, darkPaint);
            }
        }
    }

    private SKColor[] ToSkColors(ReadOnlySpan<Color> colors)
    {
        var result = new SKColor[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            var c = ApplyOpacity(colors[i]);
            result[i] = new SKColor(c.R, c.G, c.B, c.A);
        }
        return result;
    }

    // A reused antialiased fill paint with the shader set; the caller clears the shader after drawing.
    private static SKPaint GetGradientPaint(SKShader shader)
    {
        var paint = t_gradientPaint ??= new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        paint.Shader = shader;
        return paint;
    }

    public void DrawPath(SKPath path, in Color color)
    {
        if (color.A == 0) return;
        var paint = PaintRegistry.GetFillPaint(ApplyOpacity(color));
        Canvas.DrawPath(path, paint);
    }

    public void DrawPathOutline(SKPath path, in Color color, float strokeWidth)
    {
        if (color.A == 0 || strokeWidth <= 0) return;
        var paint = PaintRegistry.GetStrokePaint(ApplyOpacity(color), strokeWidth);
        Canvas.DrawPath(path, paint);
    }

    public void DrawImage(SKImage? image, in Rect destRect, float opacity = 1.0f)
    {
        if (image == null || destRect.Width <= 0 || destRect.Height <= 0) return;
        var skDest = new SKRect(destRect.Left, destRect.Top, destRect.Right, destRect.Bottom);
        float effOpacity = CurrentOpacity * opacity;
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        if (effOpacity >= 0.999f)
        {
            Canvas.DrawImage(image, skDest, sampling);
        }
        else
        {
            var paint = t_imagePaint ??= new SKPaint();
            paint.Color = new SKColor(255, 255, 255, (byte)(Math.Clamp(effOpacity, 0f, 1f) * 255));
            Canvas.DrawImage(image, skDest, sampling, paint);
        }
    }

    public void DrawImage(SKBitmap? bitmap, in Rect destRect, float opacity = 1.0f)
    {
        if (bitmap == null || destRect.Width <= 0 || destRect.Height <= 0) return;
        using var img = SKImage.FromBitmap(bitmap);
        DrawImage(img, destRect, opacity);
    }

    private Color ApplyOpacity(in Color c)
    {
        if (MathF.Abs(CurrentOpacity - 1.0f) < 1e-4f) return c;
        return c.WithAlpha(c.Af * CurrentOpacity);
    }
}
