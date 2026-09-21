using System;
using System.Diagnostics;
using System.Numerics;

namespace Atelier.Core.Primitives;

[DebuggerDisplay("X={X}, Y={Y}")]
public readonly struct Point(float x, float y) : IEquatable<Point>
{
    public static readonly Point Zero = new(0, 0);

    public float X { get; } = x;
    public float Y { get; } = y;

    public Point Offset(float dx, float dy) => new(X + dx, Y + dy);
    public Point Offset(in Point p) => new(X + p.X, Y + p.Y);

    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
    public static Point operator *(Point a, float scalar) => new(a.X * scalar, a.Y * scalar);
    public static Point operator /(Point a, float scalar) => new(a.X / scalar, a.Y / scalar);

    public static bool operator ==(Point left, Point right) => left.Equals(right);
    public static bool operator !=(Point left, Point right) => !left.Equals(right);

    public bool Equals(Point other) => MathF.Abs(X - other.X) < 1e-5f && MathF.Abs(Y - other.Y) < 1e-5f;
    public override bool Equals(object? obj) => obj is Point other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";
}

[DebuggerDisplay("Width={Width}, Height={Height}")]
public readonly struct Size(float width, float height) : IEquatable<Size>
{
    public static readonly Size Zero = new(0, 0);
    public static readonly Size Infinity = new(float.PositiveInfinity, float.PositiveInfinity);

    public float Width { get; } = Math.Max(0, width);
    public float Height { get; } = Math.Max(0, height);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public Size Inflate(float dw, float dh) => new(Math.Max(0, Width + dw), Math.Max(0, Height + dh));
    public Size Deflate(float dw, float dh) => new(Math.Max(0, Width - dw), Math.Max(0, Height - dh));
    public Size Deflate(in Thickness thickness) =>
        new(Math.Max(0, Width - thickness.Horizontal), Math.Max(0, Height - thickness.Vertical));

    public static bool operator ==(Size left, Size right) => left.Equals(right);
    public static bool operator !=(Size left, Size right) => !left.Equals(right);

    public bool Equals(Size other) => MathF.Abs(Width - other.Width) < 1e-5f && MathF.Abs(Height - other.Height) < 1e-5f;
    public override bool Equals(object? obj) => obj is Size other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Width, Height);
    public override string ToString() => $"[{Width} x {Height}]";
}

[DebuggerDisplay("X={X}, Y={Y}, W={Width}, H={Height}")]
public readonly struct Rect(float x, float y, float width, float height) : IEquatable<Rect>
{
    public static readonly Rect Zero = new(0, 0, 0, 0);

    public float X { get; } = x;
    public float Y { get; } = y;
    public float Width { get; } = Math.Max(0, width);
    public float Height { get; } = Math.Max(0, height);

    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public Point Location => new(X, Y);
    public Size Size => new(Width, Height);
    public Point Center => new(X + Width * 0.5f, Y + Height * 0.5f);

    public Rect(Point location, Size size) : this(location.X, location.Y, size.Width, size.Height) { }

    public bool Contains(in Point p) =>
        p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;

    public bool Contains(float px, float py) =>
        px >= Left && px <= Right && py >= Top && py <= Bottom;

    public bool IntersectsWith(in Rect other) =>
        other.Left <= Right && other.Right >= Left && other.Top <= Bottom && other.Bottom >= Top;

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

    public Rect Deflate(in Thickness thickness) =>
        new(
            X + thickness.Left,
            Y + thickness.Top,
            Math.Max(0, Width - thickness.Horizontal),
            Math.Max(0, Height - thickness.Vertical)
        );

    public Rect Inflate(in Thickness thickness) =>
        new(
            X - thickness.Left,
            Y - thickness.Top,
            Width + thickness.Horizontal,
            Height + thickness.Vertical
        );

    public static bool operator ==(Rect left, Rect right) => left.Equals(right);
    public static bool operator !=(Rect left, Rect right) => !left.Equals(right);

    public bool Equals(Rect other) =>
        MathF.Abs(X - other.X) < 1e-5f && MathF.Abs(Y - other.Y) < 1e-5f &&
        MathF.Abs(Width - other.Width) < 1e-5f && MathF.Abs(Height - other.Height) < 1e-5f;

    public override bool Equals(object? obj) => obj is Rect other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public override string ToString() => $"[({X}, {Y}), {Width} x {Height}]";
}

[DebuggerDisplay("L={Left}, T={Top}, R={Right}, B={Bottom}")]
public readonly struct Thickness(float left, float top, float right, float bottom) : IEquatable<Thickness>
{
    public static readonly Thickness Zero = new(0);

    public float Left { get; } = left;
    public float Top { get; } = top;
    public float Right { get; } = right;
    public float Bottom { get; } = bottom;

    public float Horizontal => Left + Right;
    public float Vertical => Top + Bottom;

    public Thickness(float uniform) : this(uniform, uniform, uniform, uniform) { }
    public Thickness(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical) { }

    public static bool operator ==(Thickness left, Thickness right) => left.Equals(right);
    public static bool operator !=(Thickness left, Thickness right) => !left.Equals(right);

    public bool Equals(Thickness other) =>
        MathF.Abs(Left - other.Left) < 1e-5f && MathF.Abs(Top - other.Top) < 1e-5f &&
        MathF.Abs(Right - other.Right) < 1e-5f && MathF.Abs(Bottom - other.Bottom) < 1e-5f;

    public override bool Equals(object? obj) => obj is Thickness other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
}

[DebuggerDisplay("TL={TopLeft}, TR={TopRight}, BR={BottomRight}, BL={BottomLeft}")]
public readonly struct CornerRadius(float topLeft, float topRight, float bottomRight, float bottomLeft) : IEquatable<CornerRadius>
{
    public static readonly CornerRadius Zero = new(0);

    public float TopLeft { get; } = topLeft;
    public float TopRight { get; } = topRight;
    public float BottomRight { get; } = bottomRight;
    public float BottomLeft { get; } = bottomLeft;

    public CornerRadius(float uniform) : this(uniform, uniform, uniform, uniform) { }

    public bool IsUniform =>
        MathF.Abs(TopLeft - TopRight) < 1e-5f &&
        MathF.Abs(TopLeft - BottomRight) < 1e-5f &&
        MathF.Abs(TopLeft - BottomLeft) < 1e-5f;

    public bool Equals(CornerRadius other) =>
        MathF.Abs(TopLeft - other.TopLeft) < 1e-5f &&
        MathF.Abs(TopRight - other.TopRight) < 1e-5f &&
        MathF.Abs(BottomRight - other.BottomRight) < 1e-5f &&
        MathF.Abs(BottomLeft - other.BottomLeft) < 1e-5f;

    public override bool Equals(object? obj) => obj is CornerRadius other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(TopLeft, TopRight, BottomRight, BottomLeft);
}

public enum HorizontalAlignment
{
    Stretch,
    Left,
    Center,
    Right
}

public enum VerticalAlignment
{
    Stretch,
    Top,
    Center,
    Bottom
}

public enum Visibility
{
    Visible,
    Hidden,
    Collapsed
}

public enum Stretch
{
    None,
    Fill,
    Uniform,
    UniformToFill
}
