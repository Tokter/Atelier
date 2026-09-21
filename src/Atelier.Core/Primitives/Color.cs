using System;
using System.Diagnostics;
using System.Globalization;

namespace Atelier.Core.Primitives;

[DebuggerDisplay("A={A}, R={R}, G={G}, B={B}")]
public readonly struct Color(byte a, byte r, byte g, byte b) : IEquatable<Color>
{
    public byte A { get; } = a;
    public byte R { get; } = r;
    public byte G { get; } = g;
    public byte B { get; } = b;

    public float Af => A / 255.0f;
    public float Rf => R / 255.0f;
    public float Gf => G / 255.0f;
    public float Bf => B / 255.0f;

    public uint ToUint32() => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | B;

    public static Color FromArgb(byte a, byte r, byte g, byte b) => new(a, r, g, b);
    public static Color FromRgb(byte r, byte g, byte b) => new(255, r, g, b);
    public static Color FromRgba(float r, float g, float b, float a = 1.0f) =>
        new(
            (byte)Math.Clamp((int)MathF.Round(a * 255), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(r * 255), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(g * 255), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(b * 255), 0, 255)
        );

    public static Color FromUint(uint argb) =>
        new((byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));

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

    public Color WithAlpha(byte alpha) => new(alpha, R, G, B);
    public Color WithAlpha(float alpha) => new((byte)Math.Clamp((int)MathF.Round(alpha * 255), 0, 255), R, G, B);

    public static Color Lerp(in Color a, in Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(a.A + (b.A - a.A) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t)
        );
    }

    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color Black = new(255, 0, 0, 0);
    public static readonly Color White = new(255, 255, 255, 255);
    public static readonly Color Red = new(255, 255, 0, 0);
    public static readonly Color Green = new(255, 0, 255, 0);
    public static readonly Color Blue = new(255, 0, 0, 255);

    public bool Equals(Color other) => A == other.A && R == other.R && G == other.G && B == other.B;
    public override bool Equals(object? obj) => obj is Color other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(A, R, G, B);
    public static bool operator ==(Color left, Color right) => left.Equals(right);
    public static bool operator !=(Color left, Color right) => !left.Equals(right);

    public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}
