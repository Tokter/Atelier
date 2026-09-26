using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace Atelier.Rendering;

/// <summary>
/// Process-wide cache of typefaces and sized fonts shared by <see cref="TextMeasurer"/> and <see cref="PaintRegistry"/>,
/// so measuring and drawing use the same font objects.
/// </summary>
/// <remarks>
/// <para>
/// Font sizes are quantized to <see cref="SizeStep"/> (a quarter pixel), which bounds the number of fonts an animated
/// font size creates without visible steps. Sized fonts are kept in a least-recently-used cache of at most
/// <see cref="MaxFonts"/> entries; evicted fonts are disposed. Callers must therefore use a returned font right away and
/// not keep it. Typefaces are cached per (family, bold, italic) and live for the process.
/// </para>
/// <para>Lookups of cached entries do not allocate.</para>
/// </remarks>
internal static class FontCache
{
    /// <summary>The granularity font sizes are rounded to.</summary>
    public const float SizeStep = 0.25f;

    /// <summary>The maximum number of sized fonts kept alive.</summary>
    public const int MaxFonts = 128;

    private static readonly object s_lock = new();
    private static readonly Dictionary<(string? Family, int Weight, bool Italic), SKTypeface> s_typefaces = new();
    private static readonly Dictionary<FontKey, LinkedListNode<FontEntry>> s_fonts = new();
    private static readonly LinkedList<FontEntry> s_lru = new();

    /// <summary>Gets the number of sized fonts currently cached.</summary>
    public static int FontCount
    {
        get
        {
            lock (s_lock)
            {
                return s_fonts.Count;
            }
        }
    }

    /// <summary>Returns the cached typeface for a family and style, falling back to the default typeface.</summary>
    public static SKTypeface GetTypeface(string? family, bool bold, bool italic) =>
        GetTypeface(family, bold ? 700 : 400, italic);

    /// <summary>
    /// Returns the cached typeface for a family, OpenType weight (1..1000) and slant; the font manager picks the closest
    /// weight the family has. Falls back to the default typeface.
    /// </summary>
    public static SKTypeface GetTypeface(string? family, int weight, bool italic)
    {
        lock (s_lock)
        {
            var key = (family, weight, italic);
            if (!s_typefaces.TryGetValue(key, out var typeface))
            {
                var style = new SKFontStyle(
                    weight,
                    (int)SKFontStyleWidth.Normal,
                    italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
                // Without a family name Skia returns the default typeface regardless of the style, so match the style
                // within the default family instead (otherwise weights and italics of default-font text are ignored).
                string familyName = family ?? SKTypeface.Default.FamilyName;
                typeface = SKTypeface.FromFamilyName(familyName, style) ?? SKTypeface.Default;

                // CSS font matching: for a missing weight between 400 and 500, prefer a lighter face (e.g. Segoe UI has
                // no 500, and its 600 would make "medium" text look bold). Skia picks the heavier one.
                if (weight is > 400 and <= 500 && typeface.FontWeight >= 600)
                {
                    var lighter = SKTypeface.FromFamilyName(familyName, new SKFontStyle(400, (int)SKFontStyleWidth.Normal,
                        italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright));
                    if (lighter != null && lighter.FontWeight <= weight)
                    {
                        typeface = lighter;
                    }
                }
                s_typefaces[key] = typeface;
            }
            return typeface;
        }
    }

    /// <summary>Returns the cached font for <paramref name="typeface"/> at <paramref name="fontSize"/> (quantized).</summary>
    public static SKFont GetFont(SKTypeface typeface, float fontSize)
    {
        int sizeKey = (int)MathF.Round(fontSize / SizeStep);
        var key = new FontKey(typeface, sizeKey);
        lock (s_lock)
        {
            if (s_fonts.TryGetValue(key, out var node))
            {
                if (node != s_lru.First)
                {
                    s_lru.Remove(node);
                    s_lru.AddFirst(node);
                }
                return node.Value.Font;
            }

            var font = new SKFont(typeface, Math.Max(SizeStep, sizeKey * SizeStep));
            node = s_lru.AddFirst(new FontEntry(key, font));
            s_fonts[key] = node;

            if (s_fonts.Count > MaxFonts)
            {
                var last = s_lru.Last!;
                s_lru.RemoveLast();
                s_fonts.Remove(last.Value.Key);
                last.Value.Font.Dispose();
            }

            return font;
        }
    }

    private readonly record struct FontEntry(FontKey Key, SKFont Font);

    // Typefaces are compared by reference: a disposed typeface's native handle can be reused by a new one.
    private readonly struct FontKey(SKTypeface typeface, int sizeKey) : IEquatable<FontKey>
    {
        private readonly SKTypeface _typeface = typeface;
        private readonly int _sizeKey = sizeKey;

        public bool Equals(FontKey other) => ReferenceEquals(_typeface, other._typeface) && _sizeKey == other._sizeKey;

        public override bool Equals(object? obj) => obj is FontKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(_typeface), _sizeKey);
    }
}
