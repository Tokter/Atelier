using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace Atelier.Controls;

/// <summary>
/// Manages loading and caching of Google's Material Symbols Rounded variable font,
/// providing high-performance access to cloned typefaces across the 4 variable axes (FILL, wght, GRAD, opsz).
/// </summary>
/// <remarks>
/// <para>
/// Axis values are quantized before lookup (fill to 0.05, weight and grade to 25, optical size to 1), which is below
/// what is visible between two animation frames, and the variation typefaces are kept in a least-recently-used cache of
/// at most <see cref="MaxCachedTypefaces"/> entries. Evicted typefaces are disposed, so an animated axis doesn't create an
/// unbounded number of native typefaces. Use a returned typeface right away (e.g. to create or look up a font) rather
/// than storing it. Fonts created from an evicted typeface stay valid, since they hold their own native reference.
/// </para>
/// <para>All variation typefaces share the font data of <see cref="BaseTypeface"/>.</para>
/// </remarks>
public static class MaterialIconFontManager
{
    /// <summary>The maximum number of variation typefaces kept alive (the base typeface is not counted).</summary>
    public const int MaxCachedTypefaces = 32;

    private const float FillStep = 0.05f;
    private const float WeightStep = 25f;
    private const float GradeStep = 25f;

    private static readonly Lazy<SKTypeface> _lazyBaseTypeface = new(LoadBaseTypeface);
    private static readonly object s_lock = new();
    private static readonly Dictionary<(int fill, int weight, int grade, int opsz), LinkedListNode<CacheEntry>> _cache = new();
    private static readonly LinkedList<CacheEntry> s_lru = new();
    private static readonly Dictionary<MaterialIconKind, string> s_glyphs = new();

    /// <summary>The logical name of the embedded Material Symbols Rounded font resource.</summary>
    public const string ResourceName = "MaterialSymbolsRounded.ttf";

    /// <summary>
    /// Gets the Material Symbols Rounded typeface at its default axis values (FILL 0, wght 400, GRAD 0, opsz 24), loaded
    /// from the embedded resource on first use. It lives for the process and must not be disposed.
    /// </summary>
    /// <exception cref="InvalidOperationException">The embedded font is missing or cannot be decoded.</exception>
    public static SKTypeface BaseTypeface => _lazyBaseTypeface.Value;

    /// <summary>Gets the number of variation typefaces currently cached (at most <see cref="MaxCachedTypefaces"/>).</summary>
    public static int CachedTypefaceCount
    {
        get
        {
            lock (s_lock)
            {
                return _cache.Count;
            }
        }
    }

    private static SKTypeface LoadBaseTypeface()
    {
        var asm = typeof(MaterialIconFontManager).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? OpenFallbackStream(asm)
            ?? throw new InvalidOperationException($"Embedded font resource '{ResourceName}' was not found in assembly '{asm.FullName}'.");

        // Loading from SKData lets every variation clone share the same font bytes.
        using var data = SKData.Create(stream);
        return (data == null ? null : SKTypeface.FromData(data))
            ?? throw new InvalidOperationException("Failed to decode Material Symbols variable font from embedded stream.");
    }

    // Checks for the resource under an assembly-prefixed name.
    private static Stream? OpenFallbackStream(System.Reflection.Assembly asm)
    {
        string? fallbackName = Array.Find(asm.GetManifestResourceNames(), n => n.EndsWith(ResourceName, StringComparison.OrdinalIgnoreCase));
        return fallbackName != null ? asm.GetManifestResourceStream(fallbackName) : null;
    }

    /// <summary>
    /// Returns the glyph string (the icon's code point) for <paramref name="kind"/>, cached so drawing an icon doesn't
    /// allocate.
    /// </summary>
    /// <param name="kind">The icon.</param>
    public static string GetGlyph(MaterialIconKind kind)
    {
        lock (s_lock)
        {
            if (!s_glyphs.TryGetValue(kind, out var glyph))
            {
                glyph = char.ConvertFromUtf32((int)kind);
                s_glyphs[kind] = glyph;
            }
            return glyph;
        }
    }

    /// <summary>
    /// Returns a cached <see cref="SKTypeface"/> configured with the requested variable font axis coordinates.
    /// Values are clamped to the font's ranges and quantized (see the remarks of <see cref="MaterialIconFontManager"/>).
    /// </summary>
    /// <param name="fill">Fill axis (0.0 = Outlined, 1.0 = Filled).</param>
    /// <param name="weight">Stroke weight axis (100 = Thin, 400 = Regular, 700 = Bold).</param>
    /// <param name="grade">Grade / contrast axis (-25 to 200, default 0).</param>
    /// <param name="opticalSize">Optical size axis (20 to 48, default 24).</param>
    public static SKTypeface GetTypeface(float fill = 0f, float weight = 400f, float grade = 0f, float opticalSize = 24f)
    {
        // Clamp axes to valid Material Symbols ranges, then quantize.
        int fillKey = (int)MathF.Round(Math.Clamp(Sanitize(fill, 0f), 0f, 1f) / FillStep);
        int weightKey = (int)MathF.Round(Math.Clamp(Sanitize(weight, 400f), 100f, 700f) / WeightStep);
        int gradeKey = (int)MathF.Round(Math.Clamp(Sanitize(grade, 0f), -25f, 200f) / GradeStep);
        int opszKey = (int)MathF.Round(Math.Clamp(Sanitize(opticalSize, 24f), 20f, 48f));

        // Fast path for default regular outlined icons
        if (fillKey == 0 && weightKey * WeightStep == 400f && gradeKey == 0 && opszKey == 24)
        {
            return BaseTypeface;
        }

        var key = (fillKey, weightKey, gradeKey, opszKey);
        lock (s_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                if (node != s_lru.First)
                {
                    s_lru.Remove(node);
                    s_lru.AddFirst(node);
                }
                return node.Value.Typeface;
            }

            var coords = new[]
            {
                new SKFontVariationPositionCoordinate { Axis = new SKFourByteTag('F', 'I', 'L', 'L'), Value = fillKey * FillStep },
                new SKFontVariationPositionCoordinate { Axis = new SKFourByteTag('w', 'g', 'h', 't'), Value = weightKey * WeightStep },
                new SKFontVariationPositionCoordinate { Axis = new SKFourByteTag('G', 'R', 'A', 'D'), Value = gradeKey * GradeStep },
                new SKFontVariationPositionCoordinate { Axis = new SKFourByteTag('o', 'p', 's', 'z'), Value = opszKey }
            };

            var args = new SKFontArguments
            {
                VariationDesignPosition = coords
            };

            var typeface = BaseTypeface.Clone(args) ?? BaseTypeface;
            node = s_lru.AddFirst(new CacheEntry(key, typeface));
            _cache[key] = node;

            if (_cache.Count > MaxCachedTypefaces)
            {
                var last = s_lru.Last!;
                s_lru.RemoveLast();
                _cache.Remove(last.Value.Key);
                if (!ReferenceEquals(last.Value.Typeface, BaseTypeface))
                {
                    last.Value.Typeface.Dispose();
                }
            }

            return typeface;
        }
    }

    private static float Sanitize(float value, float fallback) => float.IsNaN(value) ? fallback : value;

    private readonly record struct CacheEntry((int fill, int weight, int grade, int opsz) Key, SKTypeface Typeface);
}
