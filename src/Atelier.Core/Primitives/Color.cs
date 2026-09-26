using System;
using System.Diagnostics;
using System.Globalization;

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
        ReadOnlySpan<char> span = hex.AsSpan().Trim();
        if (span.StartsWith("#")) span = span[1..];

        if (span.Length == 6)
        {
            byte r = byte.Parse(span[0..2], NumberStyles.HexNumber);
            byte g = byte.Parse(span[2..4], NumberStyles.HexNumber);
            byte b = byte.Parse(span[4..6], NumberStyles.HexNumber);
            return FromRgb(r, g, b);
        }
        if (span.Length == 8)
        {
            byte a = byte.Parse(span[0..2], NumberStyles.HexNumber);
            byte r = byte.Parse(span[2..4], NumberStyles.HexNumber);
            byte g = byte.Parse(span[4..6], NumberStyles.HexNumber);
            byte b = byte.Parse(span[6..8], NumberStyles.HexNumber);
            return FromArgb(a, r, g, b);
        }
        if (span.Length == 3)
        {
            byte r = (byte)(byte.Parse(span[0..1], NumberStyles.HexNumber) * 17);
            byte g = (byte)(byte.Parse(span[1..2], NumberStyles.HexNumber) * 17);
            byte b = (byte)(byte.Parse(span[2..3], NumberStyles.HexNumber) * 17);
            return FromRgb(r, g, b);
        }

        throw new FormatException($"Invalid color hex: '{hex}'");
    }

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
