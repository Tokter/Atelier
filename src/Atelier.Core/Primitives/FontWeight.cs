using System;

namespace Atelier.Core.Primitives;

/// <summary>
/// The weight (thickness) of a font, on the OpenType scale from 1 to 1000 (400 is regular, 700 bold).
/// </summary>
/// <remarks>
/// A font that doesn't have the requested weight is drawn with its closest available weight (for example "Segoe UI" has
/// no 500, so <see cref="Medium"/> falls back to a neighboring weight).
/// </remarks>
public readonly struct FontWeight : IEquatable<FontWeight>, IComparable<FontWeight>
{
    /// <summary>Initializes a weight.</summary>
    /// <param name="weight">The OpenType weight, 1 to 1000.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> is outside 1..1000.</exception>
    public FontWeight(int weight)
    {
        if (weight is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "A font weight must be between 1 and 1000.");
        }
        _weight = weight;
    }

    // Stored as the difference from 400 so that default(FontWeight) is Normal.
    private readonly int _weight;

    /// <summary>Gets the OpenType weight value.</summary>
    public int Value => _weight == 0 ? 400 : _weight;

    /// <summary>Weight 100.</summary>
    public static FontWeight Thin => new(100);
    /// <summary>Weight 200.</summary>
    public static FontWeight ExtraLight => new(200);
    /// <summary>Weight 300.</summary>
    public static FontWeight Light => new(300);
    /// <summary>Weight 400, the regular weight and the default.</summary>
    public static FontWeight Normal => new(400);
    /// <summary>Weight 500, used by Material Design 3 for titles, labels and buttons.</summary>
    public static FontWeight Medium => new(500);
    /// <summary>Weight 600.</summary>
    public static FontWeight SemiBold => new(600);
    /// <summary>Weight 700.</summary>
    public static FontWeight Bold => new(700);
    /// <summary>Weight 800.</summary>
    public static FontWeight ExtraBold => new(800);
    /// <summary>Weight 900.</summary>
    public static FontWeight Black => new(900);

    /// <summary>Returns the heavier of two weights.</summary>
    public static FontWeight Max(FontWeight a, FontWeight b) => a.Value >= b.Value ? a : b;

    /// <inheritdoc/>
    public bool Equals(FontWeight other) => Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is FontWeight other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value;

    /// <inheritdoc/>
    public int CompareTo(FontWeight other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Determines whether two weights are equal.</summary>
    public static bool operator ==(FontWeight left, FontWeight right) => left.Equals(right);

    /// <summary>Determines whether two weights differ.</summary>
    public static bool operator !=(FontWeight left, FontWeight right) => !left.Equals(right);
}
