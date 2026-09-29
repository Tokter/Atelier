using Atelier.Core.Primitives;
using Atelier.Core.Tree;

namespace Atelier.Nodes;

/// <summary>
/// The shape of links: cubic curves that leave outputs to the right and enter inputs from the left, with handles half
/// as long as the horizontal distance (but at least <see cref="MinHandle"/>, so links going backwards loop round).
/// </summary>
public static class LinkGeometry
{
    /// <summary>The shortest handle, in graph units.</summary>
    public const float MinHandle = 40f;

    private const int Segments = 24;

    /// <summary>Gets the control points of the curve from <paramref name="start"/> (an output) to <paramref name="end"/> (an input).</summary>
    /// <param name="start">The output's anchor.</param>
    /// <param name="end">The input's anchor.</param>
    /// <param name="scale">The zoom, when the points are in view coordinates (it scales <see cref="MinHandle"/>).</param>
    public static (Point Control1, Point Control2) GetControlPoints(Point start, Point end, float scale = 1f)
    {
        float handle = Math.Max(Math.Abs(end.X - start.X) * 0.5f, MinHandle * scale);
        return (new Point(start.X + handle, start.Y), new Point(end.X - handle, end.Y));
    }

    /// <summary>Gets the point at <paramref name="t"/> (0 at the start, 1 at the end) on the curve.</summary>
    public static Point GetPoint(Point start, Point end, float t, float scale = 1f)
    {
        var (c1, c2) = GetControlPoints(start, end, scale);
        float u = 1 - t;
        float a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;
        return new Point(
            a * start.X + b * c1.X + c * c2.X + d * end.X,
            a * start.Y + b * c1.Y + c * c2.Y + d * end.Y);
    }

    /// <summary>Gets the distance from <paramref name="point"/> to the curve, measured along 24 straight pieces of it.</summary>
    public static float DistanceTo(Point point, Point start, Point end, float scale = 1f)
    {
        float best = float.MaxValue;
        var previous = start;
        for (int i = 1; i <= Segments; i++)
        {
            var next = GetPoint(start, end, i / (float)Segments, scale);
            best = Math.Min(best, DistanceToSegment(point, previous, next));
            previous = next;
        }
        return best;
    }

    /// <summary>
    /// Finds where the line through <paramref name="stroke"/>'s points first crosses the curve from
    /// <paramref name="start"/> to <paramref name="end"/> (measured along 24 straight pieces of it).
    /// </summary>
    /// <returns><c>false</c> if they don't cross.</returns>
    public static bool TryIntersect(IReadOnlyList<Point> stroke, Point start, Point end, float scale, out Point hit)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        var previous = start;
        for (int i = 1; i <= Segments; i++)
        {
            var next = GetPoint(start, end, i / (float)Segments, scale);
            for (int j = 1; j < stroke.Count; j++)
            {
                if (SegmentsIntersect(previous, next, stroke[j - 1], stroke[j], out hit)) return true;
            }
            previous = next;
        }
        hit = default;
        return false;
    }

    private static bool SegmentsIntersect(Point a, Point b, Point c, Point d, out Point hit)
    {
        hit = default;
        float rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y;
        float denominator = rx * sy - ry * sx;
        if (MathF.Abs(denominator) < 1e-6f) return false; // parallel
        float t = ((c.X - a.X) * sy - (c.Y - a.Y) * sx) / denominator;
        float u = ((c.X - a.X) * ry - (c.Y - a.Y) * rx) / denominator;
        if (t < 0 || t > 1 || u < 0 || u > 1) return false;
        hit = new Point(a.X + t * rx, a.Y + t * ry);
        return true;
    }

    private static float DistanceToSegment(Point p, Point a, Point b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        float lengthSquared = dx * dx + dy * dy;
        float t = lengthSquared > 0 ? Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0f, 1f) : 0f;
        float x = a.X + t * dx - p.X, y = a.Y + t * dy - p.Y;
        return MathF.Sqrt(x * x + y * y);
    }
}

/// <summary>Draws the links of a <see cref="NodeEditor"/>'s graph, between the nodes and the background layers.</summary>
internal sealed class LinkLayer : UIElement
{
    static LinkLayer()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<LinkLayer>(false);
    }

    public LinkLayer(NodeEditor editor)
    {
        Editor = editor;
    }

    public NodeEditor Editor { get; }

    protected override Size MeasureOverride(Size availableSize) => Size.Zero;
}

/// <summary>Draws what the editor's tools show over the nodes: the selection box and the link being dragged.</summary>
internal sealed class OverlayLayer : UIElement
{
    static OverlayLayer()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<OverlayLayer>(false);
    }

    public OverlayLayer(NodeEditor editor)
    {
        Editor = editor;
    }

    public NodeEditor Editor { get; }

    protected override Size MeasureOverride(Size availableSize) => Size.Zero;
}
