using System;
using System.Collections.Concurrent;
using SkiaSharp;
using Atelier.Core.Primitives;

namespace Atelier.Rendering;

public static class TextMeasurer
{
    private static readonly ConcurrentDictionary<string, SKTypeface> _typefaces = new();
    private static readonly ConcurrentDictionary<(int size, IntPtr tf), SKFont> _fonts = new();

    public static Size Measure(string text, float fontSize, string? fontFamily = null, bool bold = false, bool italic = false)
    {
        if (string.IsNullOrEmpty(text) || fontSize <= 0) return Size.Zero;

        string tfKey = $"{fontFamily ?? "Default"}_{(bold ? "Bold" : "Regular")}_{(italic ? "Italic" : "Upright")}";
        var tf = _typefaces.GetOrAdd(tfKey, _ =>
        {
            var weight = bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
            return SKTypeface.FromFamilyName(fontFamily, style) ?? SKTypeface.Default;
        });

        int sizeKey = (int)MathF.Round(fontSize * 10);
        var font = _fonts.GetOrAdd((sizeKey, tf.Handle), _ => new SKFont(tf, fontSize));

        float width = font.MeasureText(text, out _);
        return new Size(width, font.Spacing);
    }

    public static float GetFontSpacing(float fontSize, string? fontFamily = null, bool bold = false, bool italic = false)
    {
        if (fontSize <= 0) return 0f;

        string tfKey = $"{fontFamily ?? "Default"}_{(bold ? "Bold" : "Regular")}_{(italic ? "Italic" : "Upright")}";
        var tf = _typefaces.GetOrAdd(tfKey, _ =>
        {
            var weight = bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
            return SKTypeface.FromFamilyName(fontFamily, style) ?? SKTypeface.Default;
        });

        int sizeKey = (int)MathF.Round(fontSize * 10);
        var font = _fonts.GetOrAdd((sizeKey, tf.Handle), _ => new SKFont(tf, fontSize));
        return font.Spacing;
    }

    public static (Size size, List<string> lines) MeasureWrapped(
        string text,
        float maxWidth,
        float fontSize,
        string? fontFamily = null,
        bool bold = false,
        bool italic = false)
    {
        if (string.IsNullOrEmpty(text) || fontSize <= 0)
        {
            return (Size.Zero, []);
        }

        if (maxWidth <= 0 || float.IsPositiveInfinity(maxWidth))
        {
            var singleLine = Measure(text, fontSize, fontFamily, bold, italic);
            return (singleLine, [text]);
        }

        string tfKey = $"{fontFamily ?? "Default"}_{(bold ? "Bold" : "Regular")}_{(italic ? "Italic" : "Upright")}";
        var tf = _typefaces.GetOrAdd(tfKey, _ =>
        {
            var weight = bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
            return SKTypeface.FromFamilyName(fontFamily, style) ?? SKTypeface.Default;
        });

        int sizeKey = (int)MathF.Round(fontSize * 10);
        var font = _fonts.GetOrAdd((sizeKey, tf.Handle), _ => new SKFont(tf, fontSize));

        var lines = new List<string>();
        var paragraphs = text.Split('\n');
        float maxLineWidth = 0f;

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                lines.Add(string.Empty);
                continue;
            }

            var words = paragraph.Split(' ');
            var currentLine = new System.Text.StringBuilder();

            foreach (var word in words)
            {
                if (currentLine.Length == 0)
                {
                    currentLine.Append(word);
                }
                else
                {
                    string candidate = currentLine + " " + word;
                    float candidateWidth = font.MeasureText(candidate, out _);

                    if (candidateWidth <= maxWidth)
                    {
                        currentLine.Append(' ').Append(word);
                    }
                    else
                    {
                        string lineStr = currentLine.ToString();
                        float lineWidth = font.MeasureText(lineStr, out _);
                        if (lineWidth > maxLineWidth) maxLineWidth = lineWidth;
                        lines.Add(lineStr);

                        currentLine.Clear();
                        currentLine.Append(word);
                    }
                }
            }

            if (currentLine.Length > 0)
            {
                string lineStr = currentLine.ToString();
                float lineWidth = font.MeasureText(lineStr, out _);
                if (lineWidth > maxLineWidth) maxLineWidth = lineWidth;
                lines.Add(lineStr);
            }
        }

        float totalHeight = lines.Count * font.Spacing;
        return (new Size(maxLineWidth, totalHeight), lines);
    }
}
