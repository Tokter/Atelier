using System;
using System.Diagnostics;
using System.Numerics;

namespace Atelier.Core.Primitives;

/// <summary>
/// Shared helpers for comparing layout values.
/// </summary>
/// <remarks>
/// The <c>Equals</c> implementations and <c>==</c> operators of the geometric primitives use exact comparison
/// (consistent with <c>GetHashCode</c>, and with infinity equal to itself). Use the <c>IsClose</c> methods when
/// floating-point noise should be tolerated.
/// </remarks>
internal static class LayoutMath
{
    /// <summary>The default tolerance used by the <c>IsClose</c> methods.</summary>
    public const float Epsilon = 1e-5f;

    /// <summary>
    /// Returns <c>true</c> if <paramref name="a"/> and <paramref name="b"/> are equal (including equal infinities and NaN)
    /// or differ by less than <paramref name="epsilon"/>.
    /// </summary>
    public static bool AreClose(float a, float b, float epsilon = Epsilon) =>
        a.Equals(b) || MathF.Abs(a - b) < epsilon;

    /// <summary>
    /// Clamps <paramref name="value"/> to [<paramref name="min"/>, <paramref name="max"/>]. Unlike <see cref="Math.Clamp(float, float, float)"/>,
    /// it does not throw when <paramref name="min"/> exceeds <paramref name="max"/>: the minimum wins, as in WPF.
    /// </summary>
    public static float ClampMinWins(float value, float min, float max) => MathF.Max(min, MathF.Min(value, max));
}

/// <summary>
/// Represents a point in two-dimensional space.
/// </summary>
/// <param name="x">The horizontal coordinate.</param>
/// <param name="y">The vertical coordinate.</param>
[DebuggerDisplay("X={X}, Y={Y}")]
public readonly struct Point(float x, float y) : IEquatable<Point>
{
    /// <summary>The point (0, 0).</summary>
    public static readonly Point Zero = new(0, 0);

    /// <summary>Gets the horizontal coordinate.</summary>
    public float X { get; } = x;

    /// <summary>Gets the vertical coordinate.</summary>
    public float Y { get; } = y;

    /// <summary>Returns this point moved by the given deltas.</summary>
    public Point Offset(float dx, float dy) => new(X + dx, Y + dy);

    /// <summary>Returns this point moved by the coordinates of <paramref name="p"/>.</summary>
    public Point Offset(in Point p) => new(X + p.X, Y + p.Y);

    /// <summary>Adds two points component-wise.</summary>
    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>Subtracts two points component-wise.</summary>
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>Multiplies both coordinates by <paramref name="scalar"/>.</summary>
    public static Point operator *(Point a, float scalar) => new(a.X * scalar, a.Y * scalar);

    /// <summary>Divides both coordinates by <paramref name="scalar"/>.</summary>
    public static Point operator /(Point a, float scalar) => new(a.X / scalar, a.Y / scalar);

    /// <summary>Determines whether two points are exactly equal.</summary>
    public static bool operator ==(Point left, Point right) => left.Equals(right);

    /// <summary>Determines whether two points differ.</summary>
    public static bool operator !=(Point left, Point right) => !left.Equals(right);

    /// <summary>Determines whether both coordinates are within <paramref name="epsilon"/> of <paramref name="other"/>'s.</summary>
    public bool IsClose(in Point other, float epsilon = LayoutMath.Epsilon) =>
        LayoutMath.AreClose(X, other.X, epsilon) && LayoutMath.AreClose(Y, other.Y, epsilon);

    /// <inheritdoc/>
    public bool Equals(Point other) => X.Equals(other.X) && Y.Equals(other.Y);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Point other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <inheritdoc/>
    public override string ToString() => $"({X}, {Y})";
}

/// <summary>
/// Represents a non-negative width and height. Negative components are clamped to zero.
/// </summary>
/// <param name="width">The width.</param>
/// <param name="height">The height.</param>
[DebuggerDisplay("Width={Width}, Height={Height}")]
public readonly struct Size(float width, float height) : IEquatable<Size>
{
    /// <summary>A size of zero width and height.</summary>
    public static readonly Size Zero = new(0, 0);

    /// <summary>An unbounded size, typically used as an unconstrained measure input.</summary>
    public static readonly Size Infinity = new(float.PositiveInfinity, float.PositiveInfinity);

    /// <summary>Gets the width.</summary>
    public float Width { get; } = Math.Max(0, width);

    /// <summary>Gets the height.</summary>
    public float Height { get; } = Math.Max(0, height);

    /// <summary>Gets a value indicating whether either dimension is zero.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Returns this size grown by the given amounts (never below zero).</summary>
    public Size Inflate(float dw, float dh) => new(Math.Max(0, Width + dw), Math.Max(0, Height + dh));

    /// <summary>Returns this size shrunk by the given amounts (never below zero).</summary>
    public Size Deflate(float dw, float dh) => new(Math.Max(0, Width - dw), Math.Max(0, Height - dh));

    /// <summary>Returns this size shrunk by a thickness (never below zero).</summary>
    public Size Deflate(in Thickness thickness) =>
        new(Math.Max(0, Width - thickness.Horizontal), Math.Max(0, Height - thickness.Vertical));

    /// <summary>Determines whether two sizes are exactly equal. <see cref="Infinity"/> equals itself.</summary>
    public static bool operator ==(Size left, Size right) => left.Equals(right);

    /// <summary>Determines whether two sizes differ.</summary>
    public static bool operator !=(Size left, Size right) => !left.Equals(right);

    /// <summary>Determines whether both dimensions are within <paramref name="epsilon"/> of <paramref name="other"/>'s.</summary>
    public bool IsClose(in Size other, float epsilon = LayoutMath.Epsilon) =>
        LayoutMath.AreClose(Width, other.Width, epsilon) && LayoutMath.AreClose(Height, other.Height, epsilon);

    /// <inheritdoc/>
    public bool Equals(Size other) => Width.Equals(other.Width) && Height.Equals(other.Height);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Size other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Width, Height);

    /// <inheritdoc/>
    public override string ToString() => $"[{Width} x {Height}]";
}

/// <summary>
/// Represents an axis-aligned rectangle with a non-negative size.
/// </summary>
/// <param name="x">The left edge.</param>
/// <param name="y">The top edge.</param>
/// <param name="width">The width (clamped to zero if negative).</param>
/// <param name="height">The height (clamped to zero if negative).</param>
[DebuggerDisplay("X={X}, Y={Y}, W={Width}, H={Height}")]
public readonly struct Rect(float x, float y, float width, float height) : IEquatable<Rect>
{
    /// <summary>An empty rectangle at the origin.</summary>
    public static readonly Rect Zero = new(0, 0, 0, 0);

    /// <summary>Gets the left edge.</summary>
    public float X { get; } = x;

    /// <summary>Gets the top edge.</summary>
    public float Y { get; } = y;

    /// <summary>Gets the width.</summary>
    public float Width { get; } = Math.Max(0, width);

    /// <summary>Gets the height.</summary>
    public float Height { get; } = Math.Max(0, height);

    /// <summary>Gets the left edge (same as <see cref="X"/>).</summary>
    public float Left => X;

    /// <summary>Gets the top edge (same as <see cref="Y"/>).</summary>
    public float Top => Y;

    /// <summary>Gets the right edge.</summary>
    public float Right => X + Width;

    /// <summary>Gets the bottom edge.</summary>
    public float Bottom => Y + Height;

    /// <summary>Gets a value indicating whether the rectangle has zero width or height.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Gets the top-left corner.</summary>
    public Point Location => new(X, Y);

    /// <summary>Gets the size.</summary>
    public Size Size => new(Width, Height);

    /// <summary>Gets the center point.</summary>
    public Point Center => new(X + Width * 0.5f, Y + Height * 0.5f);

    /// <summary>Initializes a rectangle from a location and a size.</summary>
    public Rect(Point location, Size size) : this(location.X, location.Y, size.Width, size.Height) { }

    /// <summary>Determines whether the point lies inside or on the edge of the rectangle.</summary>
    public bool Contains(in Point p) =>
        p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;

    /// <summary>Determines whether the point lies inside or on the edge of the rectangle.</summary>
    public bool Contains(float px, float py) =>
        px >= Left && px <= Right && py >= Top && py <= Bottom;

    /// <summary>Determines whether this rectangle overlaps or touches <paramref name="other"/>.</summary>
    public bool IntersectsWith(in Rect other) =>
        other.Left <= Right && other.Right >= Left && other.Top <= Bottom && other.Bottom >= Top;

    /// <summary>Returns the overlapping area of two rectangles, or <see cref="Zero"/> if they do not overlap.</summary>
    public Rect Intersect(in Rect other)
    {
        float left = Math.Max(Left, other.Left);
        float top = Math.Max(Top, other.Top);
        float right = Math.Min(Right, other.Right);
        float bottom = Math.Min(Bottom, other.Bottom);

        if (right >= left && bottom >= top)
            return new Rect(left, top, right - left, bottom - top);

        return Zero;
    }

    /// <summary>Returns this rectangle shrunk inward by a thickness.</summary>
    public Rect Deflate(in Thickness thickness) =>
        new(
            X + thickness.Left,
            Y + thickness.Top,
            Math.Max(0, Width - thickness.Horizontal),
            Math.Max(0, Height - thickness.Vertical)
        );

    /// <summary>Returns this rectangle grown outward by a thickness.</summary>
    public Rect Inflate(in Thickness thickness) =>
        new(
            X - thickness.Left,
            Y - thickness.Top,
            Width + thickness.Horizontal,
            Height + thickness.Vertical
        );

    /// <summary>Determines whether two rectangles are exactly equal.</summary>
    public static bool operator ==(Rect left, Rect right) => left.Equals(right);

    /// <summary>Determines whether two rectangles differ.</summary>
    public static bool operator !=(Rect left, Rect right) => !left.Equals(right);

    /// <summary>Determines whether position and size are within <paramref name="epsilon"/> of <paramref name="other"/>'s.</summary>
    public bool IsClose(in Rect other, float epsilon = LayoutMath.Epsilon) =>
        LayoutMath.AreClose(X, other.X, epsilon) && LayoutMath.AreClose(Y, other.Y, epsilon) &&
        LayoutMath.AreClose(Width, other.Width, epsilon) && LayoutMath.AreClose(Height, other.Height, epsilon);

    /// <inheritdoc/>
    public bool Equals(Rect other) =>
        X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Rect other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    /// <inheritdoc/>
    public override string ToString() => $"[({X}, {Y}), {Width} x {Height}]";
}

/// <summary>
/// Represents the thickness of a frame around a rectangle, such as a margin, padding or border.
/// </summary>
/// <param name="left">The left edge thickness.</param>
/// <param name="top">The top edge thickness.</param>
/// <param name="right">The right edge thickness.</param>
/// <param name="bottom">The bottom edge thickness.</param>
[DebuggerDisplay("L={Left}, T={Top}, R={Right}, B={Bottom}")]
public readonly struct Thickness(float left, float top, float right, float bottom) : IEquatable<Thickness>
{
    /// <summary>A thickness of zero on every side.</summary>
    public static readonly Thickness Zero = new(0);

    /// <summary>Gets the left edge thickness.</summary>
    public float Left { get; } = left;

    /// <summary>Gets the top edge thickness.</summary>
    public float Top { get; } = top;

    /// <summary>Gets the right edge thickness.</summary>
    public float Right { get; } = right;

    /// <summary>Gets the bottom edge thickness.</summary>
    public float Bottom { get; } = bottom;

    /// <summary>Gets the sum of the left and right thickness.</summary>
    public float Horizontal => Left + Right;

    /// <summary>Gets the sum of the top and bottom thickness.</summary>
    public float Vertical => Top + Bottom;

    /// <summary>Initializes a thickness with the same value on every side.</summary>
    public Thickness(float uniform) : this(uniform, uniform, uniform, uniform) { }

    /// <summary>Initializes a thickness with one value for left/right and one for top/bottom.</summary>
    public Thickness(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical) { }

    /// <summary>Determines whether two thicknesses are exactly equal.</summary>
    public static bool operator ==(Thickness left, Thickness right) => left.Equals(right);

    /// <summary>Determines whether two thicknesses differ.</summary>
    public static bool operator !=(Thickness left, Thickness right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(Thickness other) =>
        Left.Equals(other.Left) && Top.Equals(other.Top) && Right.Equals(other.Right) && Bottom.Equals(other.Bottom);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Thickness other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

    /// <inheritdoc/>
    public override string ToString() => $"{{{Left}, {Top}, {Right}, {Bottom}}}";
}

/// <summary>
/// Represents the radii of the four corners of a rounded rectangle.
/// </summary>
/// <param name="topLeft">The top-left radius.</param>
/// <param name="topRight">The top-right radius.</param>
/// <param name="bottomRight">The bottom-right radius.</param>
/// <param name="bottomLeft">The bottom-left radius.</param>
[DebuggerDisplay("TL={TopLeft}, TR={TopRight}, BR={BottomRight}, BL={BottomLeft}")]
public readonly struct CornerRadius(float topLeft, float topRight, float bottomRight, float bottomLeft) : IEquatable<CornerRadius>
{
    /// <summary>Square corners.</summary>
    public static readonly CornerRadius Zero = new(0);

    /// <summary>Gets the top-left radius.</summary>
    public float TopLeft { get; } = topLeft;

    /// <summary>Gets the top-right radius.</summary>
    public float TopRight { get; } = topRight;

    /// <summary>Gets the bottom-right radius.</summary>
    public float BottomRight { get; } = bottomRight;

    /// <summary>Gets the bottom-left radius.</summary>
    public float BottomLeft { get; } = bottomLeft;

    /// <summary>Initializes a corner radius with the same value for every corner.</summary>
    public CornerRadius(float uniform) : this(uniform, uniform, uniform, uniform) { }

    /// <summary>Gets a value indicating whether all four radii are (approximately) the same.</summary>
    public bool IsUniform =>
        LayoutMath.AreClose(TopLeft, TopRight) &&
        LayoutMath.AreClose(TopLeft, BottomRight) &&
        LayoutMath.AreClose(TopLeft, BottomLeft);

    /// <summary>Determines whether two corner radii are exactly equal.</summary>
    public static bool operator ==(CornerRadius left, CornerRadius right) => left.Equals(right);

    /// <summary>Determines whether two corner radii differ.</summary>
    public static bool operator !=(CornerRadius left, CornerRadius right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(CornerRadius other) =>
        TopLeft.Equals(other.TopLeft) && TopRight.Equals(other.TopRight) &&
        BottomRight.Equals(other.BottomRight) && BottomLeft.Equals(other.BottomLeft);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CornerRadius other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(TopLeft, TopRight, BottomRight, BottomLeft);

    /// <inheritdoc/>
    public override string ToString() => $"{{{TopLeft}, {TopRight}, {BottomRight}, {BottomLeft}}}";
}

/// <summary>
/// Specifies how an element is positioned horizontally within the space its parent allocates.
/// </summary>
public enum HorizontalAlignment
{
    /// <summary>The element fills the available width.</summary>
    Stretch,
    /// <summary>The element is aligned to the left.</summary>
    Left,
    /// <summary>The element is centered.</summary>
    Center,
    /// <summary>The element is aligned to the right.</summary>
    Right
}

/// <summary>
/// Specifies how an element is positioned vertically within the space its parent allocates.
/// </summary>
public enum VerticalAlignment
{
    /// <summary>The element fills the available height.</summary>
    Stretch,
    /// <summary>The element is aligned to the top.</summary>
    Top,
    /// <summary>The element is centered.</summary>
    Center,
    /// <summary>The element is aligned to the bottom.</summary>
    Bottom
}

/// <summary>
/// Specifies whether an element is rendered and whether it takes part in layout.
/// </summary>
public enum Visibility
{
    /// <summary>The element is rendered and takes part in layout.</summary>
    Visible,
    /// <summary>The element is not rendered and not hit-testable, but still reserves its space in layout.</summary>
    Hidden,
    /// <summary>The element is not rendered and takes up no space in layout.</summary>
    Collapsed
}

/// <summary>
/// Specifies how content is resized to fill its allocated space.
/// </summary>
public enum Stretch
{
    /// <summary>The content keeps its natural size.</summary>
    None,
    /// <summary>The content is resized to fill the space; the aspect ratio is not preserved.</summary>
    Fill,
    /// <summary>The content is resized to fit inside the space while preserving its aspect ratio.</summary>
    Uniform,
    /// <summary>The content is resized to cover the space while preserving its aspect ratio; overflow is clipped.</summary>
    UniformToFill
}
