using Atelier.Core.Primitives;
using Atelier.Core.Styling;
using Atelier.Rendering;
using Atelier.Theming;
using Atelier.Theming.Material;
using SkiaSharp;

namespace Atelier.Nodes;

/// <summary>
/// Adds the node editor's renderers and styles to themes (see <see cref="ThemeExtensions"/>): Material colors for
/// Material themes, neutral grays for others. The node editor's controls register it themselves.
/// </summary>
public static class NodeEditorTheme
{
    /// <summary>Registers the extension; calling it again does nothing.</summary>
    public static void Register() => ThemeExtensions.Register(Extend);

    /// <summary>Adds the renderers and styles to <paramref name="theme"/>.</summary>
    public static void Extend(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        theme.Renderers.Register(new NodeEditorRenderer());
        theme.Renderers.Register(new NodeEditorLayerRenderer());
        theme.Renderers.Register(new LinkLayerRenderer());
        theme.Renderers.Register(new OverlayLayerRenderer());
        theme.Renderers.Register(new NodeViewRenderer());
        theme.Renderers.Register(new SocketViewRenderer());
        theme.Styles.AddRange(theme is MaterialTheme material ? CreateMaterialStyles(material.Colors, material.IsDark) : CreateNeutralStyles(theme.IsDark));
    }

    /// <summary>
    /// Creates the styles for a Material theme: a surface-container-lowest canvas with outline-variant grid lines, and
    /// surface-container-high nodes with on-surface text, a surface-container-highest default title bar and a primary
    /// selection outline.
    /// </summary>
    public static List<Style> CreateMaterialStyles(MaterialColorScheme colors, bool isDark)
    {
        ArgumentNullException.ThrowIfNull(colors);
        return
        [
            new Style(typeof(NodeEditor))
                .Set(Atelier.Controls.Control.BackgroundProperty, colors.SurfaceContainerLowest)
                .Set(NodeEditor.SelectionColorProperty, colors.Primary)
                .Set(NodeEditor.ErrorColorProperty, colors.Error),
            new Style(typeof(GridLayer))
                .Set(GridLayer.LineColorProperty, colors.OutlineVariant.WithAlpha(isDark ? 0.3f : 0.45f))
                .Set(GridLayer.MajorLineColorProperty, colors.OutlineVariant.WithAlpha(isDark ? 0.8f : 1f)),
            new Style(typeof(DotGridLayer))
                .Set(DotGridLayer.DotColorProperty, colors.Outline.WithAlpha(0.6f)),
            new Style(typeof(NodeView))
                .Set(Atelier.Controls.Control.BackgroundProperty, colors.SurfaceContainerHigh)
                .Set(Atelier.Controls.Control.ForegroundProperty, colors.OnSurface)
                .Set(NodeView.HeaderBackgroundProperty, colors.SurfaceContainerHighest)
                .Set(NodeView.BorderColorProperty, colors.OutlineVariant)
                .Set(NodeView.ShadowColorProperty, colors.Shadow),
            new Style(typeof(SocketView))
                .Set(SocketView.OutlineColorProperty, Color.Black.WithAlpha(isDark ? 0.6f : 0.45f)),
            // The add-node menu: a surface-container menu at elevation level 2, primary label-medium category headers.
            new Style(AddNodeMenu.PopupStyleKey, typeof(Atelier.Controls.Popup))
                .Set(Atelier.Controls.Control.CornerRadiusProperty, new CornerRadius(MaterialShape.Medium))
                .Set(Atelier.Controls.Popup.ElevationProperty, MaterialElevation.Level2)
                .Set(Atelier.Controls.Control.BackgroundProperty, colors.SurfaceContainer)
                .Set(Atelier.Controls.Popup.BorderThicknessProperty, Thickness.Zero),
            new Style(AddNodeMenu.CategoryStyleKey, typeof(Atelier.Controls.TextBlock))
                .Set(Atelier.Controls.TextBlock.ForegroundProperty, colors.Primary)
                .Set(Atelier.Controls.TextBlock.FontSizeProperty, MaterialTypescale.LabelMedium.Size)
                .Set(Atelier.Controls.TextBlock.FontWeightProperty, MaterialTypescale.LabelMedium.Weight),
        ];
    }

    /// <summary>Creates the styles for themes other than Material: Blender-like grays, dark or light.</summary>
    public static List<Style> CreateNeutralStyles(bool isDark)
    {
        var canvas = isDark ? Color.FromRgb(0x1D, 0x1D, 0x1D) : Color.FromRgb(0xF2, 0xF2, 0xF2);
        var node = isDark ? Color.FromRgb(0x30, 0x30, 0x30) : Color.FromRgb(0xFF, 0xFF, 0xFF);
        var text = isDark ? Color.FromRgb(0xE6, 0xE6, 0xE6) : Color.FromRgb(0x1C, 0x1C, 0x1C);
        var line = isDark ? Color.White : Color.Black;
        return
        [
            new Style(typeof(NodeEditor)).Set(Atelier.Controls.Control.BackgroundProperty, canvas),
            new Style(typeof(GridLayer))
                .Set(GridLayer.LineColorProperty, line.WithAlpha(0.05f))
                .Set(GridLayer.MajorLineColorProperty, line.WithAlpha(0.12f)),
            new Style(typeof(DotGridLayer)).Set(DotGridLayer.DotColorProperty, line.WithAlpha(0.25f)),
            new Style(typeof(NodeView))
                .Set(Atelier.Controls.Control.BackgroundProperty, node)
                .Set(Atelier.Controls.Control.ForegroundProperty, text)
                .Set(NodeView.HeaderBackgroundProperty, isDark ? Color.FromRgb(0x4A, 0x4A, 0x4A) : Color.FromRgb(0xD8, 0xD8, 0xD8))
                .Set(NodeView.BorderColorProperty, line.WithAlpha(isDark ? 0.4f : 0.2f)),
        ];
    }
}

/// <summary>Fills the editor with its background.</summary>
internal sealed class NodeEditorRenderer : ControlRenderer<NodeEditor>
{
    public override void Render(NodeEditor editor, ref DrawingContext context) =>
        context.DrawRect(new Rect(Point.Zero, editor.Bounds.Size), editor.Background);
}

/// <summary>Lets layers draw themselves.</summary>
internal sealed class NodeEditorLayerRenderer : ControlRenderer<NodeEditorLayer>
{
    public override void Render(NodeEditorLayer layer, ref DrawingContext context) => layer.Render(ref context);
}

/// <summary>
/// Draws the links as curves from their output's to their input's type color over a thin dark edge: wider and in the
/// selection color when selected, in the error color when the input doesn't accept the output's type, and faded when
/// either node is muted.
/// </summary>
internal sealed class LinkLayerRenderer : ControlRenderer<LinkLayer>
{
    private static readonly Color Edge = Color.Black.WithAlpha(0.35f);

    public override void Render(LinkLayer layer, ref DrawingContext context)
    {
        var editor = layer.Editor;
        if (editor.Graph is not { } graph || graph.Links.Count == 0) return;

        float zoom = editor.Zoom;
        float width = Math.Max(1f, editor.LinkThickness * Math.Min(1f, zoom));
        var visible = new Rect(Point.Zero, layer.Bounds.Size);
        foreach (var link in graph.Links)
        {
            if (editor.GetNodeView(link.From.Node!) is null || editor.GetNodeView(link.To.Node!) is null || link == editor.HiddenLink) continue;
            var start = editor.GraphToView(link.From.Anchor);
            var end = editor.GraphToView(link.To.Anchor);
            var (c1, c2) = LinkGeometry.GetControlPoints(start, end, zoom);
            if (!Intersects(visible, start, end, c1, c2)) continue;

            using var builder = new SKPathBuilder();
            builder.MoveTo(start.X, start.Y);
            builder.CubicTo(c1.X, c1.Y, c2.X, c2.Y, end.X, end.Y);
            using var path = builder.Detach();

            bool muted = link.From.Node!.IsMuted || link.To.Node!.IsMuted;
            float opacity = muted ? 0.4f : 1f;
            if (link == editor.InsertTarget)
            {
                context.DrawRoundPathOutline(path, editor.SelectionColor.WithAlpha(editor.SelectionColor.Af * 0.6f), width + 6);
            }
            if (link.IsSelected)
            {
                context.DrawRoundPathOutline(path, editor.SelectionColor.WithAlpha(editor.SelectionColor.Af * opacity), width + 4);
            }
            else
            {
                context.DrawRoundPathOutline(path, Edge.WithAlpha(Edge.Af * opacity), width + 2);
            }

            if (!link.To.Type.CanConnectFrom(link.From.Type))
            {
                context.DrawRoundPathOutline(path, editor.ErrorColor.WithAlpha(editor.ErrorColor.Af * opacity), width);
                continue;
            }
            var from = link.From.Type.Color;
            var to = link.To.Type.Color;
            context.DrawRoundPathOutline(path, start, end, from.WithAlpha(from.Af * opacity), to.WithAlpha(to.Af * opacity), width);
        }
    }

    private static bool Intersects(in Rect visible, Point a, Point b, Point c, Point d)
    {
        float left = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
        float right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
        float top = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
        float bottom = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
        return right >= visible.Left && left <= visible.Right && bottom >= visible.Top && top <= visible.Bottom;
    }
}

/// <summary>
/// Draws the tools' feedback: the selection box (the selection color, faintly filled), and a link being dragged from a
/// socket to the pointer in the socket's type color, with a ring around the socket it would connect to.
/// </summary>
internal sealed class OverlayLayerRenderer : ControlRenderer<OverlayLayer>
{
    public override void Render(OverlayLayer layer, ref DrawingContext context)
    {
        var editor = layer.Editor;
        if (editor.Stroke is { Count: >= 2 } stroke)
        {
            using var line = new SKPathBuilder();
            line.MoveTo(stroke[0].X, stroke[0].Y);
            for (int i = 1; i < stroke.Count; i++) line.LineTo(stroke[i].X, stroke[i].Y);
            using var strokePath = line.Detach();
            context.DrawRoundPathOutline(strokePath, Color.Black.WithAlpha(0.35f), 3.5f);
            context.DrawRoundPathOutline(strokePath, editor.StrokeCuts ? editor.ErrorColor : editor.SelectionColor, 1.5f);
        }
        if (editor.SelectionBox is { } box)
        {
            context.DrawRect(box, editor.SelectionColor.WithAlpha(0.12f));
            context.DrawRoundedRectOutline(box, CornerRadius.Zero, editor.SelectionColor, 1f);
        }

        if (editor.DraggedFrom is not { } socket) return;
        var anchor = editor.GraphToView(socket.Anchor);
        var loose = editor.DropTarget is { } target ? editor.GraphToView(target.Anchor) : editor.DraggedTo;
        var (start, end) = socket is OutputSocketViewModel ? (anchor, loose) : (loose, anchor);
        var (c1, c2) = LinkGeometry.GetControlPoints(start, end, editor.Zoom);
        using var builder = new SKPathBuilder();
        builder.MoveTo(start.X, start.Y);
        builder.CubicTo(c1.X, c1.Y, c2.X, c2.Y, end.X, end.Y);
        using var path = builder.Detach();
        float width = Math.Max(1f, editor.LinkThickness * Math.Min(1f, editor.Zoom));
        context.DrawRoundPathOutline(path, Color.Black.WithAlpha(0.35f), width + 2);
        context.DrawRoundPathOutline(path, socket.Type.Color, width);
        if (editor.DropTarget is { } drop)
        {
            context.DrawCircleOutline(editor.GraphToView(drop.Anchor), SocketView.HoverRadius + 3, editor.SelectionColor, 2f);
        }
    }
}

/// <summary>
/// Draws a node's body: a shadow, the background with the title bar in its color on top, and an outline (in the
/// selection color when selected, the error color with the message below it when it has an error). A collapsed node's
/// connected sockets are drawn at the ends of its title bar.
/// </summary>
internal sealed class NodeViewRenderer : ControlRenderer<NodeView>
{
    private const float ErrorFontSize = 11f;

    public override void Render(NodeView view, ref DrawingContext context)
    {
        if (view.Node is RerouteNodeViewModel reroute)
        {
            RenderReroute(view, reroute, ref context);
            return;
        }

        var body = view.BodyBounds;
        if (body.Width <= 0 || body.Height <= 0) return;

        var node = view.Node;
        var radius = view.CornerRadius;
        float r = radius.TopLeft;
        context.DrawShadow(body, radius, 2f, view.ShadowColor);
        context.DrawRoundedRect(body, radius, view.Background);

        var header = new Rect(body.X, 0, body.Width, Math.Min(view.HeaderHeight, body.Height));
        var headerRadius = node.IsCollapsed ? radius : new CornerRadius(r, r, 0, 0);
        context.DrawRoundedRect(header, headerRadius, view.EffectiveHeaderColor);

        var selection = view.Editor?.SelectionColor ?? NodeEditor.SelectionColorProperty.DefaultValue;
        var error = view.Editor?.ErrorColor ?? NodeEditor.ErrorColorProperty.DefaultValue;
        if (node.Error != null || node.IsSelected)
        {
            const float outset = 1.5f;
            var outline = new Rect(body.X - outset, body.Y - outset, body.Width + 2 * outset, body.Height + 2 * outset);
            var outlineRadius = new CornerRadius(radius.TopLeft + outset, radius.TopRight + outset, radius.BottomRight + outset, radius.BottomLeft + outset);
            context.DrawRoundedRectOutline(outline, outlineRadius, node.Error != null ? error : selection, 2f);
        }
        else
        {
            context.DrawRoundedRectOutline(body, radius, view.BorderColor, 1f);
        }

        if (node.IsCollapsed)
        {
            DrawCollapsedSocket(ref context, node.Inputs.FirstOrDefault(s => s.IsConnected), new Point(body.Left, header.Height * 0.5f));
            DrawCollapsedSocket(ref context, node.Outputs.FirstOrDefault(s => s.IsConnected), new Point(body.Right, header.Height * 0.5f));
        }

        if (node.Error is { Length: > 0 } message)
        {
            context.DrawText(message, new Point(body.X, body.Bottom + 6 + ErrorFontSize), error, ErrorFontSize, null, FontWeight.Normal);
        }
    }

    // A reroute point: a dot in its type's color, ringed in the selection color when selected.
    private static void RenderReroute(NodeView view, RerouteNodeViewModel reroute, ref DrawingContext context)
    {
        var center = new Point(view.Bounds.Width * 0.5f, view.Bounds.Height * 0.5f);
        float radius = view.IsHovered ? SocketView.HoverRadius : SocketView.Radius + 0.5f;
        if (reroute.IsSelected)
        {
            var selection = view.Editor?.SelectionColor ?? NodeEditor.SelectionColorProperty.DefaultValue;
            context.DrawCircleOutline(center, radius + 3, selection, 2f);
        }
        context.DrawCircle(center, radius, reroute.Type.Color);
        context.DrawCircleOutline(center, radius + 0.5f, SocketView.OutlineColorProperty.DefaultValue, 1f);
    }

    private static void DrawCollapsedSocket(ref DrawingContext context, SocketViewModel? socket, Point center)
    {
        if (socket is null) return;
        context.DrawCircle(center, SocketView.Radius - 1, socket.Type.Color);
        context.DrawCircleOutline(center, SocketView.Radius - 0.5f, SocketView.OutlineColorProperty.DefaultValue, 1f);
    }
}

/// <summary>Draws a socket's shape in its type's color with a thin outline, larger while hovered.</summary>
internal sealed class SocketViewRenderer : ControlRenderer<SocketView>
{
    public override void Render(SocketView view, ref DrawingContext context)
    {
        var center = new Point(view.Bounds.Width * 0.5f, view.Bounds.Height * 0.5f);
        float radius = view.IsHovered ? SocketView.HoverRadius : SocketView.Radius;
        var type = view.Socket.Type;
        switch (type.Shape)
        {
            case SocketShape.Diamond:
            {
                using var builder = new SKPathBuilder();
                float d = radius * 1.3f;
                builder.MoveTo(center.X, center.Y - d);
                builder.LineTo(center.X + d, center.Y);
                builder.LineTo(center.X, center.Y + d);
                builder.LineTo(center.X - d, center.Y);
                builder.Close();
                using var path = builder.Detach();
                context.DrawPath(path, type.Color);
                context.DrawPathOutline(path, view.OutlineColor, 1f);
                break;
            }
            case SocketShape.Square:
            {
                var rect = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
                context.DrawRoundedRect(rect, new CornerRadius(1.5f), type.Color);
                context.DrawRoundedRectOutline(rect, new CornerRadius(1.5f), view.OutlineColor, 1f);
                break;
            }
            default:
                context.DrawCircle(center, radius, type.Color);
                context.DrawCircleOutline(center, radius + 0.5f, view.OutlineColor, 1f);
                break;
        }
    }
}
