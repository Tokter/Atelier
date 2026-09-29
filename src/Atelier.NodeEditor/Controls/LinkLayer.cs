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
