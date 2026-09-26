using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Rendering;
using SkiaSharp;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Drawing helpers shared by the Material renderers: state layers, focus rings, disabled colors and content colors, so
/// every control renders these states the same way (MD3 md.sys.state and md.comp.focus-ring).
/// </summary>
public static class MaterialDrawing
{
    /// <summary>
    /// The opacity of the MD3 state layer for an interactive element: pressed, then keyboard focus, then hover
    /// (states don't add up). 0 for a disabled element or one in none of these states.
    /// </summary>
    public static float StateLayerOpacity(UIElement element) =>
        !element.IsEnabled ? 0f
        : element.IsPressed ? MaterialState.PressedOpacity
        : element.IsFocusVisible ? MaterialState.FocusOpacity
        : element.IsHovered ? MaterialState.HoverOpacity
        : 0f;

    /// <summary>
    /// The opacity of the circular hover/press halo of selection controls (check box, radio button, switch, slider):
    /// pressed, then hover. Keyboard focus is shown by the focus ring hugging the control instead, which stays compact
    /// in dense layouts (a 40 px focus halo plus a ring around it overwhelms neighboring rows).
    /// </summary>
    public static float HaloOpacity(UIElement element) =>
        !element.IsEnabled ? 0f
        : element.IsPressed ? MaterialState.PressedOpacity
        : element.IsHovered ? MaterialState.HoverOpacity
        : 0f;

    /// <summary>
    /// Draws the state layer of a component: its content color at <paramref name="opacity"/> over the whole shape.
    /// </summary>
    public static void DrawStateLayer(ref DrawingContext context, in Rect bounds, in CornerRadius corner, Color contentColor, float opacity)
    {
        if (opacity > 0f)
        {
            context.DrawRoundedRect(bounds, corner, contentColor.WithAlpha(contentColor.Af * opacity));
        }
    }

    /// <summary>
    /// Draws the circular 40 px state layer of a selection control (check box, radio button, switch handle, slider
    /// handle) around <paramref name="center"/>.
    /// </summary>
    public static void DrawStateLayerCircle(ref DrawingContext context, in Point center, Color color, float opacity, float radius = 20f)
    {
        if (opacity > 0f)
        {
            context.DrawCircle(center, radius, color.WithAlpha(color.Af * opacity));
        }
    }

    /// <summary>
    /// Draws the MD3 focus ring around <paramref name="bounds"/>: <see cref="MaterialFocusRing.Width"/> thick,
    /// <see cref="MaterialFocusRing.OuterOffset"/> outside the shape, following <paramref name="corner"/>. On pixel-aligned
    /// bounds the ring's edges fall on whole pixels, so it is crisp.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="bounds">The component's shape bounds.</param>
    /// <param name="corner">The component's corner radius.</param>
    /// <param name="color">The ring color (the scheme's secondary color).</param>
    /// <param name="offset">The gap between the shape and the ring; negative values draw the ring inside the shape.</param>
    public static void DrawFocusRing(ref DrawingContext context, in Rect bounds, in CornerRadius corner, Color color, float offset = MaterialFocusRing.OuterOffset)
    {
        float grow = offset + MaterialFocusRing.Width;

        // Snap to whole pixels: the shape may sit at a half pixel (e.g. a centered check box), which would blur the ring.
        float left = MathF.Round(bounds.X - grow);
        float top = MathF.Round(bounds.Y - grow);
        float right = MathF.Round(bounds.Right + grow);
        float bottom = MathF.Round(bounds.Bottom + grow);
        var ring = new Rect(left, top, right - left, bottom - top);
        var ringCorner = new CornerRadius(
            Math.Max(0, corner.TopLeft + grow),
            Math.Max(0, corner.TopRight + grow),
            Math.Max(0, corner.BottomRight + grow),
            Math.Max(0, corner.BottomLeft + grow));
        context.DrawRoundedRectOutline(ring, ringCorner, color, MaterialFocusRing.Width);
    }

    /// <summary>Draws a circular MD3 focus ring outside a circle of <paramref name="radius"/>.</summary>
    public static void DrawFocusRingCircle(ref DrawingContext context, in Point center, float radius, Color color)
    {
        context.DrawCircleOutline(center, radius + MaterialFocusRing.OuterOffset + MaterialFocusRing.Width, color, MaterialFocusRing.Width);
    }

    /// <summary>Returns <paramref name="color"/> at <paramref name="opacity"/> times its own alpha.</summary>
    public static Color WithOpacity(this Color color, float opacity) => color.WithAlpha(color.Af * opacity);

    /// <summary>Returns the MD3 disabled content color: on-surface at 38%.</summary>
    public static Color DisabledContent(MaterialColorScheme colors) => colors.OnSurface.WithOpacity(MaterialState.DisabledContentOpacity);

    /// <summary>Returns the MD3 disabled container color: on-surface at 12%.</summary>
    public static Color DisabledContainer(MaterialColorScheme colors) => colors.OnSurface.WithOpacity(MaterialState.DisabledContainerOpacity);

    /// <summary>
    /// Returns whether <paramref name="property"/> was set on <paramref name="element"/> itself (locally, by a style,
    /// coercion or an animation), as opposed to its default or a value inherited from an ancestor.
    /// </summary>
    public static bool IsSet<T>(UIElement element, BindableProperty<T> property) =>
        element.GetValueSource(property) > ValueSource.Inherited;

    /// <summary>
    /// Resolves the color of text or an icon: its own or an ancestor's set foreground, else the content color of the
    /// nearest ancestor whose renderer defines one (a filled button's label is on-primary), else on-surface. Disabled
    /// elements get 38% opacity.
    /// </summary>
    public static Color ResolveContentColor(UIElement element, MaterialColorScheme colors, RendererRegistry? renderers)
    {
        var color = ContentColor.Resolve(element, Control.ForegroundProperty, renderers, colors.OnSurface, out _);
        return element.IsEnabled ? color : color.WithOpacity(MaterialState.DisabledContentOpacity);
    }

    // Reused per thread so drawing check marks and arrows doesn't allocate paths or arrays per frame.
    [ThreadStatic] private static SKPoint[]? t_points;

    /// <summary>
    /// Strokes the polyline (x1,y1)-(x2,y2)-(x3,y3), such as a check mark, with round caps (so the joint is closed),
    /// without allocating.
    /// </summary>
    public static void DrawPolyline(ref DrawingContext context, float x1, float y1, float x2, float y2, float x3, float y3, Color color, float strokeWidth)
    {
        if (color.A == 0 || strokeWidth <= 0) return;

        var points = t_points ??= new SKPoint[3];
        points[0] = new SKPoint(x1, y1);
        points[1] = new SKPoint(x2, y2);
        points[2] = new SKPoint(x3, y3);

        var paint = context.PaintRegistry.GetStrokePaint(ApplyOpacity(context, color), strokeWidth);
        var cap = paint.StrokeCap;
        paint.StrokeCap = SKStrokeCap.Round;
        context.Canvas.DrawPoints(SKPointMode.Polygon, points, paint);
        paint.StrokeCap = cap;
    }

    // The MD3 drop-down arrow (a 10×5 triangle) around the origin, built once: pointing down and, flipped, up.
    private static readonly SKPath ArrowDown = CreateArrow(pointingUp: false);
    private static readonly SKPath ArrowUp = CreateArrow(pointingUp: true);

    private static SKPath CreateArrow(bool pointingUp)
    {
        using var builder = new SKPathBuilder();
        float tip = pointingUp ? -2.5f : 2.5f;
        builder.MoveTo(-5f, -tip);
        builder.LineTo(5f, -tip);
        builder.LineTo(0f, tip);
        builder.Close();
        return builder.Detach();
    }

    /// <summary>
    /// Fills the MD3 drop-down arrow (a 10×5 triangle) centered at (<paramref name="cx"/>, <paramref name="cy"/>),
    /// pointing up while the drop-down is open. Uses a cached path, so it doesn't allocate.
    /// </summary>
    public static void FillDropDownArrow(ref DrawingContext context, float cx, float cy, bool pointingUp, Color color)
    {
        if (color.A == 0) return;

        int save = context.Canvas.Save();
        context.Canvas.Translate(cx, cy);
        context.DrawPath(pointingUp ? ArrowUp : ArrowDown, color);
        context.Canvas.RestoreToCount(save);
    }

    // Applies the context's opacity like DrawingContext's own drawing methods do.
    private static Color ApplyOpacity(in DrawingContext context, Color color) =>
        context.CurrentOpacity >= 0.9999f ? color : color.WithOpacity(context.CurrentOpacity);
}
