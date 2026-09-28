using System;
using System.Diagnostics;

namespace Atelier.Core.Primitives;

/// <summary>
/// Represents a 32-bit ARGB color.
/// </summary>
/// <param name="a">The alpha channel (0 = transparent, 255 = opaque).</param>
/// <param name="r">The red channel.</param>
/// <param name="g">The green channel.</param>
/// <param name="b">The blue channel.</param>
[DebuggerDisplay("A={A}, R={R}, G={G}, B={B}")]
public readonly struct Color(byte a, byte r, byte g, byte b) : IEquatable<Color>
{
    /// <summary>Gets the alpha channel.</summary>
    public byte A { get; } = a;

    /// <summary>Gets the red channel.</summary>
    public byte R { get; } = r;

    /// <summary>Gets the green channel.</summary>
    public byte G { get; } = g;

    /// <summary>Gets the blue channel.</summary>
    public byte B { get; } = b;

    /// <summary>Gets the alpha channel as a value from 0 to 1.</summary>
    public float Af => A / 255.0f;

    /// <summary>Gets the red channel as a value from 0 to 1.</summary>
    public float Rf => R / 255.0f;

    /// <summary>Gets the green channel as a value from 0 to 1.</summary>
    public float Gf => G / 255.0f;

    /// <summary>Gets the blue channel as a value from 0 to 1.</summary>
    public float Bf => B / 255.0f;

    /// <summary>Packs the color as <c>0xAARRGGBB</c>.</summary>
    public uint ToUint32() => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | B;

    /// <summary>Creates a color from alpha, red, green and blue bytes.</summary>
    public static Color FromArgb(byte a, byte r, byte g, byte b) => new(a, r, g, b);

    /// <summary>Creates an opaque color from red, green and blue bytes.</summary>
    public static Color FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    /// <summary>Creates a color from channel values between 0 and 1; out-of-range values are clamped.</summary>
    public static Color FromRgba(float r, float g, float b, float a = 1.0f) =>
        new(ToByte(a), ToByte(r), ToByte(g), ToByte(b));

    /// <summary>Unpacks a color from <c>0xAARRGGBB</c>.</summary>
    public static Color FromUint(uint argb) =>
        new((byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));

    /// <summary>
    /// Parses a hex color in the form <c>#RGB</c>, <c>#RRGGBB</c> or <c>#AARRGGBB</c> (the <c>#</c> is optional).
    /// </summary>
    /// <exception cref="FormatException">The string is not a valid hex color.</exception>
    public static Color FromHex(string hex)
    {
        if (hex is not null && TryParseHex(hex.AsSpan(), out Color color))
        {
            return color;
        }

        throw new FormatException($"Invalid color hex: '{hex}'");
    }

    /// <summary>
    /// Tries to parse a hex color in the form <c>#RGB</c>, <c>#RRGGBB</c> or <c>#AARRGGBB</c> (the <c>#</c> is optional;
    /// surrounding white space is ignored) without allocating or throwing.
    /// </summary>
    /// <param name="hex">The text to parse.</param>
    /// <param name="color">The parsed color, or <see cref="Transparent"/> when parsing fails.</param>
    /// <returns><see langword="true"/> if <paramref name="hex"/> is a valid hex color.</returns>
    public static bool TryParseHex(ReadOnlySpan<char> hex, out Color color)
    {
        color = Transparent;
        ReadOnlySpan<char> span = hex.Trim();
        if (span.Length > 0 && span[0] == '#') span = span[1..];

        uint value = 0;
        foreach (char c in span)
        {
            int digit = HexDigit(c);
            if (digit < 0) return false;
            value = (value << 4) | (uint)digit;
        }

        switch (span.Length)
        {
            case 3:
                color = FromRgb((byte)(((value >> 8) & 0xF) * 17), (byte)(((value >> 4) & 0xF) * 17), (byte)((value & 0xF) * 17));
                return true;
            case 6:
                color = FromUint(0xFF000000 | value);
                return true;
            case 8:
                color = FromUint(value);
                return true;
            default:
                return false;
        }
    }

    private static int HexDigit(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };

    /// <summary>Returns this color with a different alpha byte.</summary>
    public Color WithAlpha(byte alpha) => new(alpha, R, G, B);

    /// <summary>Returns this color with a different alpha given as a value from 0 to 1.</summary>
    public Color WithAlpha(float alpha) => new(ToByte(alpha), R, G, B);

    /// <summary>
    /// Linearly interpolates between two colors in ARGB space. <paramref name="t"/> is clamped to [0, 1].
    /// </summary>
    public static Color Lerp(in Color a, in Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            LerpChannel(a.A, b.A, t),
            LerpChannel(a.R, b.R, t),
            LerpChannel(a.G, b.G, t),
            LerpChannel(a.B, b.B, t)
        );
    }

    /// <summary>
    /// Creates a color from hue, saturation and value (HSV, also called HSB: hue, saturation, brightness).
    /// </summary>
    /// <param name="hue">The hue in degrees: 0 is red, 120 green, 240 blue; wrapped into [0, 360).</param>
    /// <param name="saturation">The saturation from 0 (gray) to 1 (the pure hue); clamped.</param>
    /// <param name="value">The value (brightness) from 0 (black) to 1; clamped.</param>
    /// <param name="alpha">The alpha from 0 (transparent) to 1 (opaque); clamped.</param>
    public static Color FromHsv(float hue, float saturation, float value, float alpha = 1f)
    {
        HsvToRgb(hue, saturation, value, out float r, out float g, out float b);
        return FromRgba(r, g, b, alpha);
    }

    /// <summary>
    /// Creates a color from hue, saturation and lightness (HSL).
    /// </summary>
    /// <param name="hue">The hue in degrees: 0 is red, 120 green, 240 blue; wrapped into [0, 360).</param>
    /// <param name="saturation">The saturation from 0 (gray) to 1; clamped.</param>
    /// <param name="lightness">The lightness from 0 (black) through 0.5 (the pure hue) to 1 (white); clamped.</param>
    /// <param name="alpha">The alpha from 0 (transparent) to 1 (opaque); clamped.</param>
    public static Color FromHsl(float hue, float saturation, float lightness, float alpha = 1f)
    {
        HslToHsv(saturation, lightness, out float s, out float v);
        return FromHsv(hue, s, v, alpha);
    }

    /// <summary>
    /// Converts the color to hue (degrees in [0, 360)), saturation and value (0 to 1). The hue of a gray is 0.
    /// </summary>
    public void ToHsv(out float hue, out float saturation, out float value)
    {
        float r = Rf, g = Gf, b = Bf;
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;

        value = max;
        saturation = max > 0 ? delta / max : 0;

        if (delta <= 0)
        {
            hue = 0;
            return;
        }

        if (max == r) hue = 60f * ((g - b) / delta);
        else if (max == g) hue = 60f * ((b - r) / delta + 2f);
        else hue = 60f * ((r - g) / delta + 4f);

        if (hue < 0) hue += 360f;
    }

    /// <summary>
    /// Converts the color to hue (degrees in [0, 360)), saturation and lightness (0 to 1). The hue of a gray is 0.
    /// </summary>
    public void ToHsl(out float hue, out float saturation, out float lightness)
    {
        ToHsv(out hue, out float s, out float v);
        HsvToHsl(s, v, out saturation, out lightness);
    }

    /// <summary>Converts HSV saturation and value to HSL saturation and lightness (the hue is the same).</summary>
    public static void HsvToHsl(float saturation, float value, out float hslSaturation, out float lightness)
    {
        saturation = Math.Clamp(saturation, 0f, 1f);
        value = Math.Clamp(value, 0f, 1f);
        lightness = value * (1f - saturation * 0.5f);
        float m = MathF.Min(lightness, 1f - lightness);
        hslSaturation = m > 0 ? (value - lightness) / m : 0f;
    }

    /// <summary>Converts HSL saturation and lightness to HSV saturation and value (the hue is the same).</summary>
    public static void HslToHsv(float saturation, float lightness, out float hsvSaturation, out float value)
    {
        saturation = Math.Clamp(saturation, 0f, 1f);
        lightness = Math.Clamp(lightness, 0f, 1f);
        value = lightness + saturation * MathF.Min(lightness, 1f - lightness);
        hsvSaturation = value > 0 ? 2f * (1f - lightness / value) : 0f;
    }

    private static void HsvToRgb(float hue, float saturation, float value, out float r, out float g, out float b)
    {
        hue %= 360f;
        if (hue < 0) hue += 360f;
        saturation = Math.Clamp(saturation, 0f, 1f);
        value = Math.Clamp(value, 0f, 1f);

        // f(n) = V - V·S·max(0, min(k, 4 - k, 1)) with k = (n + H/60) mod 6.
        r = Channel(5f);
        g = Channel(3f);
        b = Channel(1f);

        float Channel(float n)
        {
            float k = (n + hue / 60f) % 6f;
            return value - value * saturation * MathF.Max(0f, MathF.Min(k, MathF.Min(4f - k, 1f)));
        }
    }

    // Rounds rather than truncates, so e.g. halfway between 0 and 255 is 128 and t = 1 always reaches the target.
    private static byte LerpChannel(byte from, byte to, float t) => (byte)MathF.Round(from + (to - from) * t);

    private static byte ToByte(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255), 0, 255);

    /// <summary>Fully transparent black.</summary>
    public static readonly Color Transparent = new(0, 0, 0, 0);

    /// <summary>Opaque black.</summary>
    public static readonly Color Black = new(255, 0, 0, 0);

    /// <summary>Opaque white.</summary>
    public static readonly Color White = new(255, 255, 255, 255);

    /// <summary>Opaque red.</summary>
    public static readonly Color Red = new(255, 255, 0, 0);

    /// <summary>Opaque green.</summary>
    public static readonly Color Green = new(255, 0, 255, 0);

    /// <summary>Opaque blue.</summary>
    public static readonly Color Blue = new(255, 0, 0, 255);

    /// <inheritdoc/>
    public bool Equals(Color other) => A == other.A && R == other.R && G == other.G && B == other.B;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Color other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (int)ToUint32();

    /// <summary>Determines whether two colors are equal.</summary>
    public static bool operator ==(Color left, Color right) => left.Equals(right);

    /// <summary>Determines whether two colors differ.</summary>
    public static bool operator !=(Color left, Color right) => !left.Equals(right);

    /// <summary>Formats the color as <c>#AARRGGBB</c>.</summary>
    public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}
