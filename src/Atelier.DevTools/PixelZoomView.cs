using System.Numerics;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;
using Atelier.Theming;
using SkiaSharp;

namespace Atelier.DevTools;

/// <summary>
/// Magnifies the selected element and its surroundings pixel by pixel: the window's content is rendered around the
/// element and drawn enlarged without smoothing, with an optional pixel grid and the element's box model outlined. The
/// pixel under the pointer is reported (<see cref="PixelHovered"/>).
/// </summary>
public sealed class PixelZoomView : UIElement
{
    private float _zoom = 4;
    private bool _showPixelGrid = true;
    private bool _showBoxModel = true;

    internal PixelZoomView(DevToolsSession session)
    {
        Session = session;
        ClipToBounds = true;
    }

    /// <summary>Gets the tools.</summary>
    public DevToolsSession Session { get; }

    /// <summary>Gets or sets the magnification (1 to 32). The default is 4.</summary>
    public float Zoom
    {
        get => _zoom;
        set
        {
            float zoom = Math.Clamp(value, 1, 32);
            if (zoom == _zoom) return;
            _zoom = zoom;
            ZoomChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }
    }

    /// <summary>Gets or sets whether a grid separates the pixels (from 6× on). The default is <c>true</c>.</summary>
    public bool ShowPixelGrid
    {
        get => _showPixelGrid;
        set
        {
            _showPixelGrid = value;
            InvalidateVisual();
        }
    }

    /// <summary>Gets or sets whether the margin, border, padding and content edges are outlined. The default is <c>true</c>.</summary>
    public bool ShowBoxModel
    {
        get => _showBoxModel;
        set
        {
            _showBoxModel = value;
            InvalidateVisual();
        }
    }

    /// <summary>Occurs when the pointer is over a pixel, with its window coordinates and color (or <c>null</c> when it left).</summary>
    public event EventHandler<(Point Pixel, Color Color)?>? PixelHovered;

    // Written by the renderer: the rendered area, its pixels, and where the area's origin is drawn.
    internal SKBitmap? Pixels { get; set; }

    internal Rect Area { get; set; }

    internal Point Origin { get; set; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) =>
        new(float.IsFinite(availableSize.Width) ? availableSize.Width : 300, float.IsFinite(availableSize.Height) ? availableSize.Height : 300);

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var local = e.ScreenPosition - PointToScreen(Point.Zero);
        int px = (int)MathF.Floor((local.X - Origin.X) / _zoom);
        int py = (int)MathF.Floor((local.Y - Origin.Y) / _zoom);
        if (Pixels is { } pixels && px >= 0 && py >= 0 && px < pixels.Width && py < pixels.Height)
        {
            var c = pixels.GetPixel(px, py);
            PixelHovered?.Invoke(this, (new Point(Area.X + px, Area.Y + py), Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue)));
        }
        else
        {
            PixelHovered?.Invoke(this, null);
        }
    }

    /// <inheritdoc/>
    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PixelHovered?.Invoke(this, null);
    }

    /// <inheritdoc/>
    public override void OnPointerWheel(PointerWheelEventArgs e)
    {
        base.OnPointerWheel(e);
        Zoom = e.DeltaY > 0 ? Zoom * 1.25f : Zoom / 1.25f;
        e.Handled = true;
    }

    /// <summary>Occurs when the wheel changed <see cref="Zoom"/>.</summary>
    public event EventHandler? ZoomChanged;
}

/// <summary>Draws a <see cref="PixelZoomView"/>.</summary>
public sealed class PixelZoomViewRenderer : ControlRenderer<PixelZoomView>
{
    private const float Context = 24; // pixels shown around the element's margin
    private const int MaxSide = 2048;
    private static readonly PaintRegistry s_paints = new();
    private static readonly Color s_hint = Color.FromArgb(255, 120, 120, 120);
    private static readonly Color s_grid = Color.FromArgb(40, 0, 0, 0);
    private static readonly Color s_checkerLight = Color.FromArgb(255, 250, 250, 250);
    private static readonly Color s_checkerDark = Color.FromArgb(255, 225, 225, 225);

    /// <inheritdoc/>
    public override void Render(PixelZoomView view, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, view.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var clip = context.PushClip(bounds); // the element's own drawing isn't clipped by ClipToBounds
        context.DrawCheckerboard(bounds, CornerRadius.Zero, 8, s_checkerLight, s_checkerDark);

        var session = view.Session;
        if (session.SelectedElement is not { } target || !target.IsAttachedToVisualTree)
        {
            view.Pixels = null;
            context.DrawText("Select an element to zoom in on it.", new Point(16, 28), s_hint, 13, null, FontWeight.Normal);
            return;
        }

        // The area: the element's margin box and some surroundings, inside the content.
        var box = BoxModel.Compute(target, session.RectOf);
        var root = session.RectOf(session.InspectedRoot);
        var area = Intersect(box.Margin.Inflate(new Thickness(Context)), root);
        int width = Math.Clamp((int)MathF.Ceiling(area.Width), 1, MaxSide);
        int height = Math.Clamp((int)MathF.Ceiling(area.Height), 1, MaxSide);
        area = new Rect(MathF.Floor(area.X), MathF.Floor(area.Y), width, height);

        // Render the content there, at 1:1.
        var pixels = view.Pixels;
        if (pixels == null || pixels.Width != width || pixels.Height != height)
        {
            pixels?.Dispose();
            pixels = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        }
        using (var canvas = new SKCanvas(pixels))
        {
            canvas.Clear(SKColors.Transparent);
            var inner = new DrawingContext(canvas, s_paints);
            using (inner.PushTransform(Matrix3x2.CreateTranslation(-area.X, -area.Y)))
            {
                VisualTreeRenderer.Render(session.InspectedRoot, ref inner, ThemeVisualPresenter.Instance);
            }
        }
        view.Pixels = pixels;
        view.Area = area;

        // Center the element; draw the pixels enlarged, without smoothing.
        float zoom = view.Zoom;
        var targetCenter = new Point(box.Border.X + box.Border.Width / 2 - area.X, box.Border.Y + box.Border.Height / 2 - area.Y);
        var origin = new Point(MathF.Round(bounds.Width / 2 - targetCenter.X * zoom), MathF.Round(bounds.Height / 2 - targetCenter.Y * zoom));
        view.Origin = origin;
        using var image = SKImage.FromBitmap(pixels);
        context.Canvas.DrawImage(image, new SKRect(origin.X, origin.Y, origin.X + width * zoom, origin.Y + height * zoom),
            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));

        if (view.ShowPixelGrid && zoom >= 6) DrawGrid(ref context, bounds, origin, zoom, width, height);

        if (view.ShowBoxModel)
        {
            Rect Map(Rect r) => new(origin.X + (r.X - area.X) * zoom, origin.Y + (r.Y - area.Y) * zoom, r.Width * zoom, r.Height * zoom);
            Outline(ref context, Map(box.Margin), DevToolsOverlayRenderer.MarginColor);
            Outline(ref context, Map(box.Border), DevToolsOverlayRenderer.OutlineColor);
            Outline(ref context, Map(box.Padding), DevToolsOverlayRenderer.BorderColor);
            Outline(ref context, Map(box.Content), DevToolsOverlayRenderer.PaddingColor);
            foreach (var gap in box.Gaps) Outline(ref context, Map(gap), DevToolsOverlayRenderer.SpacingColor);
        }
    }

    private static void DrawGrid(ref DrawingContext context, Rect bounds, Point origin, float zoom, int width, int height)
    {
        int first = Math.Max(0, (int)((0 - origin.X) / zoom)), last = Math.Min(width, (int)((bounds.Width - origin.X) / zoom) + 1);
        for (int x = first; x <= last; x++) context.DrawRect(new Rect(origin.X + x * zoom, Math.Max(0, origin.Y), 1, Math.Min(bounds.Height, height * zoom)), s_grid);
        first = Math.Max(0, (int)((0 - origin.Y) / zoom));
        last = Math.Min(height, (int)((bounds.Height - origin.Y) / zoom) + 1);
        for (int y = first; y <= last; y++) context.DrawRect(new Rect(Math.Max(0, origin.X), origin.Y + y * zoom, Math.Min(bounds.Width, width * zoom), 1), s_grid);
    }

    private static void Outline(ref DrawingContext context, Rect rect, Color color)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        context.DrawRoundedRectOutline(rect, CornerRadius.Zero, Color.FromArgb(255, color.R, color.G, color.B), 2);
    }

    private static Rect Intersect(Rect a, Rect b)
    {
        float x = Math.Max(a.X, b.X), y = Math.Max(a.Y, b.Y);
        float right = Math.Min(a.Right, b.Right), bottom = Math.Min(a.Bottom, b.Bottom);
        return new Rect(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
    }
}
