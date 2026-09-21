using System;
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
        var rrect = new SKRoundRect();
        rrect.SetRectRadii(
            new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom),
            [
                new SKPoint(radius.TopLeft, radius.TopLeft),
                new SKPoint(radius.TopRight, radius.TopRight),
                new SKPoint(radius.BottomRight, radius.BottomRight),
                new SKPoint(radius.BottomLeft, radius.BottomLeft)
            ]);
        Canvas.ClipRoundRect(rrect, SKClipOperation.Intersect, antialias: true);
        return scope;
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
            var rrect = new SKRoundRect();
            rrect.SetRectRadii(
                new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom),
                [
                    new SKPoint(radius.TopLeft, radius.TopLeft),
                    new SKPoint(radius.TopRight, radius.TopRight),
                    new SKPoint(radius.BottomRight, radius.BottomRight),
                    new SKPoint(radius.BottomLeft, radius.BottomLeft)
                ]);
            Canvas.DrawRoundRect(rrect, paint);
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
            var rrect = new SKRoundRect();
            rrect.SetRectRadii(
                new SKRect(adjustedRect.Left, adjustedRect.Top, adjustedRect.Right, adjustedRect.Bottom),
                [
                    new SKPoint(Math.Max(0, radius.TopLeft - inset), Math.Max(0, radius.TopLeft - inset)),
                    new SKPoint(Math.Max(0, radius.TopRight - inset), Math.Max(0, radius.TopRight - inset)),
                    new SKPoint(Math.Max(0, radius.BottomRight - inset), Math.Max(0, radius.BottomRight - inset)),
                    new SKPoint(Math.Max(0, radius.BottomLeft - inset), Math.Max(0, radius.BottomLeft - inset))
                ]);
            Canvas.DrawRoundRect(rrect, paint);
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
        Canvas.DrawText(text, position.X, position.Y, SKTextAlign.Left, font, paint);
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

    private void DrawShadowLayer(in Rect rect, in CornerRadius radius, float blur, float dx, float dy, in Color color)
    {
        var finalColor = ApplyOpacity(color);
        if (finalColor.A == 0) return;

        var skColor = new SKColor(finalColor.R, finalColor.G, finalColor.B, finalColor.A);
        using var filter = SKImageFilter.CreateDropShadowOnly(dx, dy, blur, blur, skColor);
        using var shadowPaint = new SKPaint
        {
            IsAntialias = true,
            ImageFilter = filter,
            Color = SKColors.Black
        };

        if (radius.IsUniform)
        {
            Canvas.DrawRoundRect(rect.Left, rect.Top, rect.Width, rect.Height, radius.TopLeft, radius.TopLeft, shadowPaint);
        }
        else
        {
            var rrect = new SKRoundRect();
            rrect.SetRectRadii(
                new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom),
                [
                    new SKPoint(radius.TopLeft, radius.TopLeft),
                    new SKPoint(radius.TopRight, radius.TopRight),
                    new SKPoint(radius.BottomRight, radius.BottomRight),
                    new SKPoint(radius.BottomLeft, radius.BottomLeft)
                ]);
            Canvas.DrawRoundRect(rrect, shadowPaint);
        }
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
            using var paint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(effOpacity * 255)) };
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
