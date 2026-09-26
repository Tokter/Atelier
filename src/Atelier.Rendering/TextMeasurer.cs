using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;
using Atelier.Core.Primitives;

namespace Atelier.Rendering;

/// <summary>
/// One line of laid-out text: a range of the source string plus its measured width. Produced by
/// <see cref="TextMeasurer.LayoutLines(string, float, bool, SKFont, List{TextLine})"/>.
/// </summary>
/// <param name="Start">The index of the first character of the line in the source string.</param>
/// <param name="Length">The number of characters of the source string shown on the line (line breaks and the spaces at
/// a wrap point are excluded).</param>
/// <param name="Width">The width of the line in pixels, including the ellipsis if <paramref name="HasEllipsis"/>.</param>
/// <param name="HasEllipsis">Whether the line was trimmed and is followed by <see cref="TextMeasurer.Ellipsis"/>.</param>
public readonly record struct TextLine(int Start, int Length, float Width, bool HasEllipsis = false);

/// <summary>
/// Measures and lays out single-style text with SkiaSharp fonts.
/// </summary>
/// <remarks>
/// Fonts and typefaces come from a process-wide cache shared with <see cref="PaintRegistry"/>, so measuring and drawing
/// agree. Font sizes are rounded to a quarter pixel. Measuring cached fonts and laying out into a reused
/// <see cref="List{T}"/> does not allocate. Text is measured without shaping or font fallback.
/// </remarks>
public static class TextMeasurer
{
    /// <summary>The ellipsis appended to trimmed lines (U+2026).</summary>
    public const string Ellipsis = "…";

    /// <summary>Gets the number of sized fonts currently held by the shared font cache (bounded).</summary>
    public static int CachedFontCount => FontCache.FontCount;

    /// <summary>
    /// Returns the cached typeface for a family and style. A <c>null</c> or unknown family gives the default typeface.
    /// </summary>
    /// <param name="fontFamily">The family name, or <c>null</c> for the default.</param>
    /// <param name="bold">Whether to use the bold weight.</param>
    /// <param name="italic">Whether to use the italic slant.</param>
    public static SKTypeface GetTypeface(string? fontFamily, bool bold = false, bool italic = false) =>
        FontCache.GetTypeface(fontFamily, bold, italic);

    /// <summary>
    /// Returns the cached font for a family, style and size. The font is owned by the cache: do not dispose it, and use
    /// it right away rather than storing it (rarely used fonts are evicted and disposed).
    /// </summary>
    /// <param name="fontSize">The font size in pixels.</param>
    /// <param name="fontFamily">The family name, or <c>null</c> for the default.</param>
    /// <param name="bold">Whether to use the bold weight.</param>
    /// <param name="italic">Whether to use the italic slant.</param>
    public static SKFont GetFont(float fontSize, string? fontFamily = null, bool bold = false, bool italic = false) =>
        FontCache.GetFont(FontCache.GetTypeface(fontFamily, bold, italic), fontSize);

    /// <summary>
    /// Returns the cached font for <paramref name="typeface"/> at <paramref name="fontSize"/>; see
    /// <see cref="GetFont(float, string?, bool, bool)"/> for ownership rules.
    /// </summary>
    /// <param name="fontSize">The font size in pixels.</param>
    /// <param name="typeface">The typeface.</param>
    public static SKFont GetFont(float fontSize, SKTypeface typeface) => FontCache.GetFont(typeface, fontSize);

    /// <summary>
    /// Measures <paramref name="text"/> as a single line: the width of all characters (line breaks are not interpreted)
    /// and the font's line spacing as height. Returns <see cref="Size.Zero"/> for empty text or a non-positive size.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="fontSize">The font size in pixels.</param>
    /// <param name="fontFamily">The family name, or <c>null</c> for the default.</param>
    /// <param name="bold">Whether to use the bold weight.</param>
    /// <param name="italic">Whether to use the italic slant.</param>
    public static Size Measure(string text, float fontSize, string? fontFamily = null, bool bold = false, bool italic = false)
    {
        if (string.IsNullOrEmpty(text) || fontSize <= 0) return Size.Zero;

        var font = GetFont(fontSize, fontFamily, bold, italic);
        return new Size(font.MeasureText(text.AsSpan()), font.Spacing);
    }

    /// <summary>Measures the width of <paramref name="text"/> as a single line.</summary>
    /// <param name="text">The characters to measure.</param>
    /// <param name="fontSize">The font size in pixels.</param>
    /// <param name="fontFamily">The family name, or <c>null</c> for the default.</param>
    /// <param name="bold">Whether to use the bold weight.</param>
    /// <param name="italic">Whether to use the italic slant.</param>
    public static float MeasureWidth(ReadOnlySpan<char> text, float fontSize, string? fontFamily = null, bool bold = false, bool italic = false)
    {
        if (text.IsEmpty || fontSize <= 0) return 0f;
        return GetFont(fontSize, fontFamily, bold, italic).MeasureText(text);
    }

    /// <summary>Gets the recommended distance between the baselines of consecutive lines (the font's line spacing).</summary>
    /// <param name="fontSize">The font size in pixels.</param>
    /// <param name="fontFamily">The family name, or <c>null</c> for the default.</param>
    /// <param name="bold">Whether to use the bold weight.</param>
    /// <param name="italic">Whether to use the italic slant.</param>
    public static float GetFontSpacing(float fontSize, string? fontFamily = null, bool bold = false, bool italic = false)
    {
        if (fontSize <= 0) return 0f;
        return GetFont(fontSize, fontFamily, bold, italic).Spacing;
    }

    /// <summary>
    /// Lays out <paramref name="text"/> and returns the lines as strings. Convenience wrapper around
    /// <see cref="LayoutLines(string, float, bool, SKFont, List{TextLine})"/> that allocates the result; prefer that
    /// method in hot paths.
    /// </summary>
    /// <param name="text">The text; '\n' (optionally preceded by '\r') starts a new line.</param>
    /// <param name="maxWidth">The width to wrap at; <see cref="float.PositiveInfinity"/> or a non-positive value only breaks at line breaks.</param>
    /// <param name="fontSize">The font size in pixels.</param>
    /// <param name="fontFamily">The family name, or <c>null</c> for the default.</param>
    /// <param name="bold">Whether to use the bold weight.</param>
    /// <param name="italic">Whether to use the italic slant.</param>
    /// <returns>The size of the laid-out text (widest line by line count times line spacing) and the line strings.</returns>
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

        var ranges = new List<TextLine>();
        var size = LayoutLines(text, maxWidth, wrap: true, GetFont(fontSize, fontFamily, bold, italic), ranges);
        var lines = new List<string>(ranges.Count);
        foreach (var line in ranges)
        {
            lines.Add(text.Substring(line.Start, line.Length));
        }
        return (size, lines);
    }

    /// <summary>
    /// Breaks <paramref name="text"/> into lines without allocating (apart from growing <paramref name="lines"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every '\n' starts a new line; a '\r' before it is dropped, and text ending in a line break ends with an empty line.
    /// With <paramref name="wrap"/> and a finite positive <paramref name="maxWidth"/>, lines are additionally broken at
    /// spaces so that they fit (spaces at a break are dropped), and a word wider than <paramref name="maxWidth"/> is broken
    /// between characters, never inside a surrogate pair or grapheme cluster. Each word is measured once.
    /// </para>
    /// </remarks>
    /// <param name="text">The text to lay out.</param>
    /// <param name="maxWidth">The available width.</param>
    /// <param name="wrap">Whether to wrap lines at <paramref name="maxWidth"/>.</param>
    /// <param name="font">The font to measure with.</param>
    /// <param name="lines">Receives the lines; cleared first.</param>
    /// <returns>The width of the widest line and the line count times the font's line spacing.</returns>
    public static Size LayoutLines(string text, float maxWidth, bool wrap, SKFont font, List<TextLine> lines)
    {
        lines.Clear();
        if (string.IsNullOrEmpty(text)) return Size.Zero;

        bool canWrap = wrap && maxWidth > 0 && !float.IsPositiveInfinity(maxWidth);
        float spaceWidth = canWrap ? font.MeasureText(" ".AsSpan()) : 0f;
        float widest = 0f;

        int paragraphStart = 0;
        while (true)
        {
            int newline = text.IndexOf('\n', paragraphStart);
            int paragraphEnd = newline < 0 ? text.Length : newline;
            int contentEnd = paragraphEnd;
            if (contentEnd > paragraphStart && text[contentEnd - 1] == '\r')
            {
                contentEnd--;
            }

            if (canWrap)
            {
                WrapParagraph(text, paragraphStart, contentEnd, maxWidth, spaceWidth, font, lines, ref widest);
            }
            else
            {
                float width = contentEnd > paragraphStart ? font.MeasureText(text.AsSpan(paragraphStart, contentEnd - paragraphStart)) : 0f;
                lines.Add(new TextLine(paragraphStart, contentEnd - paragraphStart, width));
                widest = Math.Max(widest, width);
            }

            if (newline < 0) break;
            paragraphStart = newline + 1;
        }

        return new Size(widest, lines.Count * font.Spacing);
    }

    private static void WrapParagraph(string text, int start, int end, float maxWidth, float spaceWidth, SKFont font, List<TextLine> lines, ref float widest)
    {
        int lineStart = -1;
        int lineEnd = start;
        float lineWidth = 0f;
        int i = start;

        while (i < end)
        {
            int gapStart = i;
            while (i < end && text[i] == ' ') i++;
            int gap = i - gapStart;
            if (i >= end) break; // trailing spaces are not shown

            int wordStart = i;
            while (i < end && text[i] != ' ') i++;
            float wordWidth = font.MeasureText(text.AsSpan(wordStart, i - wordStart));

            if (lineStart < 0)
            {
                // First word of the paragraph keeps its leading spaces.
                lineStart = gapStart;
                lineWidth = gap * spaceWidth + wordWidth;
            }
            else
            {
                float candidate = lineWidth + gap * spaceWidth + wordWidth;
                if (candidate <= maxWidth)
                {
                    lineWidth = candidate;
                    lineEnd = i;
                    continue;
                }

                AddLine(lines, lineStart, lineEnd, lineWidth, ref widest);
                lineStart = wordStart;
                lineWidth = wordWidth;
            }
            lineEnd = i;

            if (lineWidth > maxWidth)
            {
                // The line holds a single word that is too wide on its own: break it between text elements.
                while (lineWidth > maxWidth)
                {
                    int fit = FitCharacters(text, lineStart, lineEnd - lineStart, maxWidth, font, out float fitWidth);
                    AddLine(lines, lineStart, lineStart + fit, fitWidth, ref widest);
                    lineStart += fit;
                    lineWidth = font.MeasureText(text.AsSpan(lineStart, lineEnd - lineStart));
                }
            }
        }

        if (lineStart < 0)
        {
            lines.Add(new TextLine(start, 0, 0f)); // empty paragraph or only spaces
        }
        else if (lineEnd > lineStart)
        {
            AddLine(lines, lineStart, lineEnd, lineWidth, ref widest);
        }
    }

    private static void AddLine(List<TextLine> lines, int start, int end, float width, ref float widest)
    {
        lines.Add(new TextLine(start, end - start, width));
        if (width > widest) widest = width;
    }

    /// <summary>
    /// Returns how many characters of <c>text[start..start+length]</c> fit into <paramref name="maxWidth"/>, ending at a
    /// text-element boundary; at least one text element even if it doesn't fit (so layout always makes progress).
    /// </summary>
    private static int FitCharacters(string text, int start, int length, float maxWidth, SKFont font, out float width)
    {
        var span = text.AsSpan(start, length);
        int fit = (int)font.BreakText(span, maxWidth, out width);
        fit = Math.Clamp(fit, 0, length);

        // Snap back to a text-element boundary.
        int boundary = 0;
        while (boundary < length)
        {
            int next = boundary + StringInfo.GetNextTextElementLength(span[boundary..]);
            if (next > fit) break;
            boundary = next;
        }

        if (boundary == 0)
        {
            boundary = Math.Max(1, StringInfo.GetNextTextElementLength(span));
        }

        if (boundary != fit)
        {
            width = font.MeasureText(span[..boundary]);
        }
        return boundary;
    }

    /// <summary>
    /// Trims <paramref name="line"/> so that it plus <see cref="Ellipsis"/> fits into <paramref name="maxWidth"/>.
    /// </summary>
    /// <param name="text">The source text of the line.</param>
    /// <param name="line">The line to trim.</param>
    /// <param name="maxWidth">The available width; with <see cref="float.PositiveInfinity"/> the ellipsis is appended without trimming.</param>
    /// <param name="wordBoundary">Whether to trim at the last space that fits (word ellipsis) instead of after any character.</param>
    /// <param name="force">Whether to add the ellipsis even if the line already fits (used for the last shown line when
    /// lines are cut off).</param>
    /// <param name="font">The font to measure with.</param>
    /// <returns>The trimmed line with <see cref="TextLine.HasEllipsis"/> set, or <paramref name="line"/> if it fits and <paramref name="force"/> is <c>false</c>.</returns>
    public static TextLine TrimLine(string text, TextLine line, float maxWidth, bool wordBoundary, bool force, SKFont font)
    {
        if (!force && line.Width <= maxWidth)
        {
            return line;
        }

        float ellipsisWidth = font.MeasureText(Ellipsis.AsSpan());
        if (line.Width + ellipsisWidth <= maxWidth)
        {
            return line with { Width = line.Width + ellipsisWidth, HasEllipsis = true };
        }

        float available = maxWidth - ellipsisWidth;
        if (available <= 0 || line.Length == 0)
        {
            return new TextLine(line.Start, 0, ellipsisWidth, true);
        }

        var span = text.AsSpan(line.Start, line.Length);
        int fit = Math.Clamp((int)font.BreakText(span, available, out _), 0, line.Length);

        // Snap back to a text-element boundary.
        int boundary = 0;
        while (boundary < fit)
        {
            int next = boundary + StringInfo.GetNextTextElementLength(span[boundary..]);
            if (next > fit) break;
            boundary = next;
        }
        fit = boundary;

        if (wordBoundary && fit < line.Length && span[fit] != ' ')
        {
            int lastSpace = span[..fit].LastIndexOf(' ');
            if (lastSpace > 0)
            {
                fit = lastSpace;
            }
        }

        while (fit > 0 && span[fit - 1] == ' ')
        {
            fit--;
        }

        float width = fit > 0 ? font.MeasureText(span[..fit]) : 0f;
        return new TextLine(line.Start, fit, width + ellipsisWidth, true);
    }
}
