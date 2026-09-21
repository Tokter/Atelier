using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using SkiaSharp;

namespace Atelier.Controls;

/// <summary>
/// Manages loading and caching of Google's Material Symbols Rounded variable font,
/// providing high-performance access to cloned typefaces across the 4 variable axes (FILL, wght, GRAD, opsz).
/// </summary>
public static class MaterialIconFontManager
{
    private static readonly Lazy<SKTypeface> _lazyBaseTypeface = new(LoadBaseTypeface);
    private static readonly ConcurrentDictionary<(int fill, int weight, int grad, int opsz), SKTypeface> _cache = new();

    public const string ResourceName = "MaterialSymbolsRounded.ttf";

    public static SKTypeface BaseTypeface => _lazyBaseTypeface.Value;

    private static SKTypeface LoadBaseTypeface()
    {
        var asm = typeof(MaterialIconFontManager).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            // Fallback: check if resource name contains assembly prefix
            string? fallbackName = Array.Find(asm.GetManifestResourceNames(), n => n.EndsWith("MaterialSymbolsRounded.ttf", StringComparison.OrdinalIgnoreCase));
            if (fallbackName != null)
            {
                using var fallbackStream = asm.GetManifestResourceStream(fallbackName);
                if (fallbackStream != null)
                {
                    return SKTypeface.FromStream(fallbackStream)
                        ?? throw new InvalidOperationException("Failed to decode Material Symbols variable font from fallback embedded stream.");
                }
            }

            throw new InvalidOperationException($"Embedded font resource '{ResourceName}' was not found in assembly '{asm.FullName}'.");
        }

        return SKTypeface.FromStream(stream)
            ?? throw new InvalidOperationException("Failed to decode Material Symbols variable font from embedded stream.");
    }

    /// <summary>
    /// Returns a cached <see cref="SKTypeface"/> configured with the requested variable font axis coordinates.
    /// </summary>
    /// <param name="fill">Fill axis (0.0 = Outlined, 1.0 = Filled).</param>
    /// <param name="weight">Stroke weight axis (100 = Thin, 400 = Regular, 700 = Bold).</param>
    /// <param name="grade">Grade / contrast axis (-25 to 200, default 0).</param>
    /// <param name="opticalSize">Optical size axis (20 to 48, default 24).</param>
    public static SKTypeface GetTypeface(float fill = 0f, float weight = 400f, float grade = 0f, float opticalSize = 24f)
    {
        // Clamp axes to valid Material Symbols ranges
        fill = Math.Clamp(fill, 0f, 1f);
        weight = Math.Clamp(weight, 100f, 700f);
        grade = Math.Clamp(grade, -25f, 200f);
        opticalSize = Math.Clamp(opticalSize, 20f, 48f);

        // Fast path for default regular outlined icons
        if (fill == 0f && weight == 400f && grade == 0f && opticalSize == 24f)
        {
            return BaseTypeface;
        }

        int fillKey = (int)MathF.Round(fill * 100f);
        int weightKey = (int)MathF.Round(weight);
        int gradKey = (int)MathF.Round(grade);
        int opszKey = (int)MathF.Round(opticalSize);

        return _cache.GetOrAdd((fillKey, weightKey, gradKey, opszKey), _ =>
        {
            var coords = new[]
            {
                new SKFontVariationPositionCoordinate { Axis = new SKFourByteTag('F', 'I', 'L', 'L'), Value = fill },
                new SKFontVariationPositionCoordinate { Axis = new SKFourByteTag('w', 'g', 'h', 't'), Value = weight },
                new SKFontVariationPositionCoordinate { Axis = new SKFourByteTag('G', 'R', 'A', 'D'), Value = grade },
                new SKFontVariationPositionCoordinate { Axis = new SKFourByteTag('o', 'p', 's', 'z'), Value = opticalSize }
            };

            var args = new SKFontArguments
            {
                VariationDesignPosition = coords
            };

            return BaseTypeface.Clone(args) ?? BaseTypeface;
        });
    }
}
