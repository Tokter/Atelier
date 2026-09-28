using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Rendering;
using Atelier.Theming;

namespace Atelier.DevTools;

/// <summary>
/// The layer the tools put over the window's content: it draws the selected element's box model (or outline), the
/// element under the pointer while picking, and optionally every element's bounds. It takes pointer input only while
/// picking.
/// </summary>
public sealed class DevToolsOverlay : UIElement
{
    private readonly DevToolsSession _session;
    private UIElement? _hovered;

    internal DevToolsOverlay(DevToolsSession session)
    {
        _session = session;
        ZIndex = 1;
    }

    /// <summary>Gets the tools.</summary>
    public DevToolsSession Session => _session;

    /// <summary>Gets or sets the element under the pointer while picking.</summary>
    public UIElement? HoveredElement
    {
        get => _hovered;
        set
        {
            if (_hovered == value) return;
            _hovered = value;
            InvalidateVisual();
            _session.Host.InvalidateRender();
        }
    }

    /// <inheritdoc/>
    /// <remarks>The overlay is hit only while picking; otherwise the pointer reaches the content under it.</remarks>
    public override UIElement? HitTest(Point point) =>
        _session.IsPicking && Visibility == Visibility.Visible && Bounds.Contains(point) ? this : null;

    /// <inheritdoc/>
    protected override CursorType GetCursor() => _session.IsPicking ? CursorType.Crosshair : base.GetCursor();

    private Point WindowPoint(PointerEventArgs e)
    {
        var origin = _session.Layout.PointToScreen(Point.Zero);
        return new Point(e.ScreenPosition.X - origin.X, e.ScreenPosition.Y - origin.Y);
    }

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_session.IsPicking) HoveredElement = _session.ElementAt(WindowPoint(e));
    }

    /// <inheritdoc/>
    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        HoveredElement = null;
    }

    /// <inheritdoc/>
    /// <remarks>While picking, a click selects the element under the pointer and stops picking.</remarks>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!_session.IsPicking) return;
        e.Handled = true;
        if (e.Button == PointerButtons.Left && _session.ElementAt(WindowPoint(e)) is { } element)
        {
            _session.Select(element);
        }
        _session.IsPicking = false;
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Handled = true; // the release of a picking click stays here too
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => Size.Zero;
}

/// <summary>The areas of an element's box model in window coordinates.</summary>
/// <param name="Margin">The bounds grown by the margin.</param>
/// <param name="Border">The bounds (the border's outer edge).</param>
/// <param name="Padding">Inside the border.</param>
/// <param name="Content">Inside the padding.</param>
/// <param name="Gaps">The gaps a panel's spacing leaves between its children or grid rows and columns.</param>
public sealed record BoxModel(Rect Margin, Rect Border, Rect Padding, Rect Content, IReadOnlyList<Rect> Gaps)
{
    /// <summary>Gets the margin of the element.</summary>
    public Thickness MarginThickness { get; init; }

    /// <summary>Gets the border thickness of the element (a <see cref="Atelier.Layout.Border"/>'s).</summary>
    public Thickness BorderThickness { get; init; }

    /// <summary>Gets the padding of the element.</summary>
    public Thickness PaddingThickness { get; init; }

    /// <summary>Gets the spacing of the element (a panel's spacing, or a grid's column and row spacing), as "12" or "8 × 4".</summary>
    public string? Spacing { get; init; }

    /// <summary>Computes the box model of <paramref name="element"/>, with rectangles from <paramref name="rectOf"/>.</summary>
    public static BoxModel Compute(UIElement element, Func<UIElement, Rect> rectOf)
    {
        var bounds = rectOf(element);
        var margin = element.Margin;
        var border = ThicknessOf(element, "BorderThickness");
        var padding = ThicknessOf(element, "Padding");
        var paddingBox = bounds.Deflate(border);
        var content = paddingBox.Deflate(padding);
        var gaps = new List<Rect>();
        string? spacing = null;

        if (FloatOf(element, "Spacing") is > 0 and var gap)
        {
            spacing = $"{gap:0.##}";
            var orientation = BindableProperty.FindByName(element.GetType(), "Orientation");
            bool vertical = orientation == null || element.GetValueUntyped(orientation) is Orientation.Vertical;
            var children = element.Children.OfType<UIElement>().Where(c => c.Visibility != Visibility.Collapsed)
                .Select(c => (Rect: rectOf(c).Inflate(c.Margin), c)).OrderBy(c => vertical ? c.Rect.Y : c.Rect.X).ToList();
            for (int i = 0; i + 1 < children.Count; i++)
            {
                var a = children[i].Rect;
                var b = children[i + 1].Rect;
                gaps.Add(vertical
                    ? new Rect(content.X, a.Bottom, content.Width, Math.Max(0, b.Y - a.Bottom))
                    : new Rect(a.Right, content.Y, Math.Max(0, b.X - a.Right), content.Height));
            }
        }
        if (element is Grid grid && (grid.ColumnSpacing > 0 || grid.RowSpacing > 0))
        {
            spacing = $"{grid.ColumnSpacing:0.##} × {grid.RowSpacing:0.##}";
            for (int i = 0; i + 1 < grid.ColumnDefinitions.Count && grid.ColumnSpacing > 0; i++)
            {
                var column = grid.ColumnDefinitions[i];
                gaps.Add(new Rect(content.X + column.Offset + column.ActualWidth, content.Y, grid.ColumnSpacing, content.Height));
            }
            for (int i = 0; i + 1 < grid.RowDefinitions.Count && grid.RowSpacing > 0; i++)
            {
                var row = grid.RowDefinitions[i];
                gaps.Add(new Rect(content.X, content.Y + row.Offset + row.ActualHeight, content.Width, grid.RowSpacing));
            }
        }

        return new BoxModel(bounds.Inflate(margin), bounds, paddingBox, content, gaps)
        {
            MarginThickness = margin,
            BorderThickness = border,
            PaddingThickness = padding,
            Spacing = spacing,
        };
    }

    // A Thickness property by name (Padding, BorderThickness), on any element type that has one.
    private static Thickness ThicknessOf(UIElement element, string name) =>
        BindableProperty.FindByName(element.GetType(), name) is { } property && element.GetValueUntyped(property) is Thickness thickness ? thickness : Thickness.Zero;

    private static float? FloatOf(UIElement element, string name) =>
        BindableProperty.FindByName(element.GetType(), name) is { } property && element.GetValueUntyped(property) is float value ? value : null;
}

/// <summary>Draws the <see cref="DevToolsOverlay"/>: colors like browser developer tools.</summary>
public sealed class DevToolsOverlayRenderer : ControlRenderer<DevToolsOverlay>
{
    /// <summary>The color of margins.</summary>
    public static readonly Color MarginColor = Color.FromArgb(150, 246, 178, 107);

    /// <summary>The color of borders.</summary>
    public static readonly Color BorderColor = Color.FromArgb(170, 255, 229, 153);

    /// <summary>The color of padding.</summary>
    public static readonly Color PaddingColor = Color.FromArgb(150, 147, 196, 125);

    /// <summary>The color of content.</summary>
    public static readonly Color ContentColor = Color.FromArgb(140, 111, 168, 220);

    /// <summary>The color of spacing gaps.</summary>
    public static readonly Color SpacingColor = Color.FromArgb(150, 190, 120, 230);

    /// <summary>The color of outlines.</summary>
    public static readonly Color OutlineColor = Color.FromArgb(255, 26, 115, 232);

    private static readonly Color s_boundsColor = Color.FromArgb(90, 0, 150, 255);
    private static readonly Color s_labelBackground = Color.FromArgb(235, 32, 33, 36);
    private static readonly Color s_labelText = Color.FromArgb(255, 255, 255, 255);

    /// <inheritdoc/>
    public override void Render(DevToolsOverlay overlay, ref DrawingContext context)
    {
        var session = overlay.Session;
        // The overlay sits at the window's origin, so window coordinates are its own.
        if (session.ShowAllBounds) DrawAllBounds(session, session.InspectedRoot, ref context);

        if (session.SelectedElement is { } selected && IsShown(selected))
        {
            if (session.ShowBoxModel) DrawBoxModel(BoxModel.Compute(selected, session.RectOf), ref context);
            else context.DrawRoundedRectOutline(session.RectOf(selected), CornerRadius.Zero, OutlineColor, 2);
            DrawLabel(selected, session.RectOf(selected), ref context);
        }

        if (session.IsPicking && overlay.HoveredElement is { } hovered && hovered != session.SelectedElement)
        {
            var rect = session.RectOf(hovered);
            DrawBoxModel(BoxModel.Compute(hovered, session.RectOf), ref context);
            context.DrawRoundedRectOutline(rect, CornerRadius.Zero, OutlineColor, 1);
            DrawLabel(hovered, rect, ref context);
        }
    }

    private static bool IsShown(UIElement element)
    {
        for (VisualNode? node = element; node != null; node = node.Parent)
        {
            if (node is UIElement { Visibility: not Visibility.Visible }) return false;
        }
        return element.IsAttachedToVisualTree;
    }

    /// <summary>Draws a box model: margin, border, padding and content areas, and spacing gaps.</summary>
    public static void DrawBoxModel(BoxModel box, ref DrawingContext context)
    {
        FillRing(box.Margin, box.Border, MarginColor, ref context);
        FillRing(box.Border, box.Padding, BorderColor, ref context);
        FillRing(box.Padding, box.Content, PaddingColor, ref context);
        if (box.Content.Width > 0 && box.Content.Height > 0) context.DrawRect(box.Content, ContentColor);
        foreach (var gap in box.Gaps)
        {
            if (gap.Width > 0 && gap.Height > 0) context.DrawRect(gap, SpacingColor);
        }
    }

    // Fills the area between an outer and an inner rectangle (four bands).
    private static void FillRing(Rect outer, Rect inner, Color color, ref DrawingContext context)
    {
        if (outer.Width <= 0 || outer.Height <= 0) return;
        float top = Math.Max(0, inner.Y - outer.Y), bottom = Math.Max(0, outer.Bottom - inner.Bottom);
        float left = Math.Max(0, inner.X - outer.X), right = Math.Max(0, outer.Right - inner.Right);
        if (top > 0) context.DrawRect(new Rect(outer.X, outer.Y, outer.Width, top), color);
        if (bottom > 0) context.DrawRect(new Rect(outer.X, outer.Bottom - bottom, outer.Width, bottom), color);
        float middleHeight = Math.Max(0, outer.Height - top - bottom);
        if (left > 0) context.DrawRect(new Rect(outer.X, outer.Y + top, left, middleHeight), color);
        if (right > 0) context.DrawRect(new Rect(outer.Right - right, outer.Y + top, right, middleHeight), color);
    }

    private static void DrawAllBounds(DevToolsSession session, UIElement element, ref DrawingContext context)
    {
        if (element.Visibility != Visibility.Visible || element is Popup) return;
        var rect = session.RectOf(element);
        if (rect.Width > 0 && rect.Height > 0) context.DrawRoundedRectOutline(rect, CornerRadius.Zero, s_boundsColor, 1);
        foreach (var child in element.Children)
        {
            if (child is UIElement ui) DrawAllBounds(session, ui, ref context);
        }
    }

    // "Button  120 × 32" in a dark chip above (or inside) the element.
    private static void DrawLabel(UIElement element, Rect rect, ref DrawingContext context)
    {
        string text = $"{element.GetType().Name}  {rect.Width:0.##} × {rect.Height:0.##}";
        const float fontSize = 12;
        var size = TextMeasurer.Measure(text, fontSize);
        float width = size.Width + 12, height = size.Height + 6;
        float x = Math.Max(0, rect.X);
        float y = rect.Y - height - 4 >= 0 ? rect.Y - height - 4 : rect.Bottom + 4;
        var chip = new Rect(x, y, width, height);
        context.DrawRoundedRect(chip, new CornerRadius(4), s_labelBackground);
        context.DrawText(text, new Point(x + 6, y + 3 + fontSize * 0.95f), s_labelText, fontSize, null, FontWeight.Normal); // baseline
    }
}
