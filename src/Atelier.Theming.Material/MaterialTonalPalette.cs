using System;
using Atelier.Core.Primitives;

namespace Atelier.Theming.Material;

/// <summary>
/// The colors of one hue and chroma at every tone, from black (tone 0) to white (tone 100), like the tonal palettes
/// Material Design 3 derives its color roles from.
/// </summary>
/// <remarks>
/// Hue and chroma are CIELAB LCh values and the tone is the CIELAB lightness L*, so tones of different palettes have
/// the same perceived lightness and a tone difference predicts contrast: 40 against 100, or 80 against 20, is at least
/// 4.5:1. A colorful palette's light tones (above 50) get less chroma, down to 40% of it at white; a tone the chroma
/// doesn't fit at in sRGB gets the most chroma that does.
/// </remarks>
public sealed class MaterialTonalPalette
{
    /// <summary>Initializes a palette of <paramref name="hue"/> (degrees) and <paramref name="chroma"/>.</summary>
    public MaterialTonalPalette(double hue, double chroma)
    {
        Hue = ((hue % 360) + 360) % 360;
        Chroma = Math.Max(0, chroma);
    }

    /// <summary>Gets the CIELAB hue in degrees, from 0 up to 360.</summary>
    public double Hue { get; }

    /// <summary>Gets the CIELAB chroma the palette aims for (0 is gray).</summary>
    public double Chroma { get; }

    /// <summary>Creates the palette of <paramref name="color"/>'s own hue and chroma.</summary>
    public static MaterialTonalPalette FromColor(Color color)
    {
        ToLch(color, out _, out double chroma, out double hue);
        return new MaterialTonalPalette(hue, chroma);
    }

    /// <summary>Gets the palette's color at <paramref name="tone"/> (0 to 100; 0 is black, 100 white).</summary>
    public Color Tone(double tone)
    {
        if (tone <= 0) return Color.Black;
        if (tone >= 100) return Color.White;

        // Colorful palettes calm down toward white (as containers and light accents do in MD3); then the most chroma
        // that fits sRGB at this lightness.
        double chroma = Chroma;
        if (tone > 50 && chroma > 16) chroma = Math.Max(16, chroma * (1 - (tone - 50) / 50 * 0.6));
        if (TryToColor(tone, chroma, Hue, out var color)) return color;
        double low = 0, high = chroma;
        TryToColor(tone, 0, Hue, out color);
        for (int i = 0; i < 24; i++)
        {
            double mid = (low + high) / 2;
            if (TryToColor(tone, mid, Hue, out var candidate))
            {
                low = mid;
                color = candidate;
            }
            else
            {
                high = mid;
            }
        }
        return color;
    }

    /// <summary>Gets the CIELAB lightness, chroma and hue (degrees) of <paramref name="color"/>.</summary>
    public static void ToLch(Color color, out double lightness, out double chroma, out double hue)
    {
        double r = ToLinear(color.R / 255.0), g = ToLinear(color.G / 255.0), b = ToLinear(color.B / 255.0);
        double x = (0.4124564 * r + 0.3575761 * g + 0.1804375 * b) / WhiteX;
        double y = 0.2126729 * r + 0.7151522 * g + 0.0721750 * b;
        double z = (0.0193339 * r + 0.1191920 * g + 0.9503041 * b) / WhiteZ;
        double fx = LabF(x), fy = LabF(y), fz = LabF(z);
        lightness = 116 * fy - 16;
        double a = 500 * (fx - fy), bb = 200 * (fy - fz);
        chroma = Math.Sqrt(a * a + bb * bb);
        hue = chroma < 1e-9 ? 0 : (Math.Atan2(bb, a) * 180 / Math.PI + 360) % 360;
    }

    /// <summary>Gets the CIELAB lightness L* of <paramref name="color"/> (its tone).</summary>
    public static double ToneOf(Color color)
    {
        ToLch(color, out double lightness, out _, out _);
        return lightness;
    }

    private const double WhiteX = 0.95047, WhiteZ = 1.08883;
    private const double Epsilon = 216.0 / 24389, Kappa = 24389.0 / 27;

    private static bool TryToColor(double lightness, double chroma, double hue, out Color color)
    {
        double radians = hue * Math.PI / 180;
        double fy = (lightness + 16) / 116;
        double fx = fy + chroma * Math.Cos(radians) / 500;
        double fz = fy - chroma * Math.Sin(radians) / 200;
        double x = LabFInverse(fx) * WhiteX, y = LabFInverse(fy), z = LabFInverse(fz) * WhiteZ;
        double r = 3.2404542 * x - 1.5371385 * y - 0.4985314 * z;
        double g = -0.9692660 * x + 1.8760108 * y + 0.0415560 * z;
        double b = 0.0556434 * x - 0.2040259 * y + 1.0572252 * z;
        const double tolerance = 1e-4;
        bool inGamut = r >= -tolerance && r <= 1 + tolerance && g >= -tolerance && g <= 1 + tolerance && b >= -tolerance && b <= 1 + tolerance;
        color = Color.FromRgb(ToByte(r), ToByte(g), ToByte(b));
        return inGamut;
    }

    private static double LabF(double t) => t > Epsilon ? Math.Cbrt(t) : (Kappa * t + 16) / 116;

    private static double LabFInverse(double f)
    {
        double cube = f * f * f;
        return cube > Epsilon ? cube : (116 * f - 16) / Kappa;
    }

    private static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static byte ToByte(double linear)
    {
        double c = Math.Clamp(linear, 0, 1);
        c = c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
        return (byte)Math.Round(Math.Clamp(c, 0, 1) * 255);
    }
}
