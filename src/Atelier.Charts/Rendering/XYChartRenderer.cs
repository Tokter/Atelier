using Atelier.Core.Primitives;
using Atelier.Rendering;
using Atelier.Theming;
using SkiaSharp;

namespace Atelier.Charts.Rendering;

/// <summary>
/// Draws an <see cref="XYChart"/>: its background and title; the plot area with grid lines at the ticks and stronger
/// lines at zero; the axes with their tick labels and titles (the vertical one turned); clipped to the plot, the series
/// (lines, then markers) and the annotations (marker, leader and a rounded label); the legend; and while the pointer is
/// over the plot, a crosshair with a readout of the nearest point, or the zoom box being dragged.
/// </summary>
/// <remarks>
/// Lines skip the segments entirely outside the plot on one side, so zoomed-in views of thousands of points draw only
/// what shows.
/// </remarks>
public sealed class XYChartRenderer : ControlRenderer<XYChart>
{
    [ThreadStatic] private static SKPaint? t_linePaint;

    // A reused antialiased stroke paint for series lines (with dashes, which DrawingContext's paints have none of).
    private static SKPaint LinePaint => t_linePaint ??= new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeJoin = SKStrokeJoin.Round };

    /// <inheritdoc/>
    public override void Render(XYChart chart, ref DrawingContext context)
    {
        float width = chart.Bounds.Width;
        float height = chart.Bounds.Height;
        if (width <= 0 || height <= 0) return;

        using var clip = context.PushClip(new Rect(0, 0, width, height));
        context.DrawRect(new Rect(0, 0, width, height), chart.Background);
        var layout = chart.GetLayout();
        var plot = layout.PlotArea;
        string? family = chart.FontFamily;
        float small = chart.SmallFontSize;

        if (chart.Title.Length > 0)
        {
            float titleWidth = TextMeasurer.MeasureWidth(chart.Title, chart.FontSize + 2, family, bold: true);
            context.DrawText(chart.Title, new Point(layout.TitlePosition.X - titleWidth / 2, layout.TitlePosition.Y),
                chart.Foreground, chart.FontSize + 2, family, bold: true);
        }

        context.DrawRect(plot, chart.PlotBackground);
        DrawGrid(chart, layout, ref context);
        DrawAxes(chart, layout, ref context, small, family);

        using (context.PushClip(plot))
        {
            for (int i = 0; i < chart.Series.Count; i++)
            {
                var series = chart.Series[i];
                if (!series.IsVisible || series.Points.Count == 0) continue;
                if (series.ShowLine && series.LineWidth > 0 && series.Points.Count > 1)
                {
                    DrawLine(series, layout, layout.SeriesColors[i], ref context);
                }
                if (series.Marker != ChartMarker.None && series.MarkerSize > 0)
                {
                    DrawMarkers(series, layout, layout.SeriesColors[i], ref context);
                }
            }
        }

        DrawAnnotations(chart, layout, ref context, small, family);
        DrawLegend(chart, layout, ref context, small, family);

        if (chart.ZoomBox is { } box)
        {
            context.DrawRect(box, chart.Foreground.WithAlpha(0.08f));
            context.DrawRoundedRectOutline(box, new CornerRadius(0), chart.Foreground.WithAlpha(0.6f), 1);
        }
        else if (chart.HoveredPoint() is { } hovered)
        {
            DrawReadout(chart, layout, hovered.Series, hovered.Point, hovered.Local, hovered.Color, ref context, small, family);
        }
    }

    private static void DrawGrid(XYChart chart, ChartLayout layout, ref DrawingContext context)
    {
        var plot = layout.PlotArea;
        if (chart.XAxis.ShowGrid)
        {
            foreach (var tick in layout.XTicks)
            {
                float x = Snap(tick.Position);
                context.DrawLine(new Point(x, plot.Top), new Point(x, plot.Bottom), chart.GridColor, 1);
            }
        }
        if (chart.YAxis.ShowGrid)
        {
            foreach (var tick in layout.YTicks)
            {
                float y = Snap(tick.Position);
                context.DrawLine(new Point(plot.Left, y), new Point(plot.Right, y), chart.GridColor, 1);
            }
        }
        // Zero lines, when zero shows (but not on the axes themselves).
        var zero = chart.AxisColor.WithAlpha(chart.AxisColor.Af * 0.6f);
        if (layout.X0 < 0 && layout.X1 > 0)
        {
            float x = Snap(layout.ToLocalX(0));
            if (x > plot.Left + 1) context.DrawLine(new Point(x, plot.Top), new Point(x, plot.Bottom), zero, 1);
        }
        if (layout.Y0 < 0 && layout.Y1 > 0)
        {
            float y = Snap(layout.ToLocalY(0));
            if (y < plot.Bottom - 1) context.DrawLine(new Point(plot.Left, y), new Point(plot.Right, y), zero, 1);
        }
    }

    private static void DrawAxes(XYChart chart, ChartLayout layout, ref DrawingContext context, float small, string? family)
    {
        var plot = layout.PlotArea;
        float left = Snap(plot.Left), bottom = Snap(plot.Bottom);
        context.DrawLine(new Point(left, plot.Top), new Point(left, bottom), chart.AxisColor, 1);
        context.DrawLine(new Point(left, bottom), new Point(plot.Right, bottom), chart.AxisColor, 1);

        var font = TextMeasurer.GetFont(small, family);
        float centerOffset = -(font.Metrics.Ascent + font.Metrics.Descent) / 2; // baseline below a line's middle
        float xBaseline = plot.Bottom + ChartLayout.TickLabelGap - font.Metrics.Ascent;
        foreach (var tick in layout.XTicks)
        {
            context.DrawLine(new Point(Snap(tick.Position), bottom), new Point(Snap(tick.Position), bottom + 4), chart.AxisColor, 1);
            context.DrawText(tick.Label, new Point(tick.Position - tick.LabelWidth / 2, xBaseline), chart.SecondaryForeground, small, family);
        }
        foreach (var tick in layout.YTicks)
        {
            float labelWidth = TextMeasurer.MeasureWidth(tick.Label, small, family);
            context.DrawLine(new Point(left - 4, Snap(tick.Position)), new Point(left, Snap(tick.Position)), chart.AxisColor, 1);
            context.DrawText(tick.Label, new Point(plot.Left - ChartLayout.TickLabelGap - labelWidth, tick.Position + centerOffset),
                chart.SecondaryForeground, small, family);
        }

        float fontSize = chart.FontSize;
        if (chart.XAxis.Title.Length > 0)
        {
            float w = TextMeasurer.MeasureWidth(chart.XAxis.Title, fontSize, family);
            context.DrawText(chart.XAxis.Title, new Point(layout.XTitlePosition.X - w / 2, layout.XTitlePosition.Y), chart.Foreground, fontSize, family);
        }
        if (chart.YAxis.Title.Length > 0)
        {
            float w = TextMeasurer.MeasureWidth(chart.YAxis.Title, fontSize, family);
            var canvas = context.Canvas;
            int save = canvas.Save();
            canvas.RotateDegrees(-90, layout.YTitlePosition.X, layout.YTitlePosition.Y);
            context.DrawText(chart.YAxis.Title, new Point(layout.YTitlePosition.X - w / 2, layout.YTitlePosition.Y), chart.Foreground, fontSize, family);
            canvas.RestoreToCount(save);
        }
    }

    // Lines through the points, leaving out the segments wholly beyond one side of the plot.
    private static void DrawLine(XYSeries series, ChartLayout layout, Color color, ref DrawingContext context)
    {
        var plot = layout.PlotArea;
        float margin = series.LineWidth + 2;
        float minX = plot.Left - margin, maxX = plot.Right + margin, minY = plot.Top - margin, maxY = plot.Bottom + margin;
        var points = series.Points;
        using var builder = new SKPathBuilder();
        bool open = false;
        Point previous = default;
        bool hasPrevious = false;
        for (int i = 0; i < points.Count; i++)
        {
            var p = layout.ToLocal(points[i]);
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y))
            {
                hasPrevious = open = false; // a gap in the data
                continue;
            }
            p = new Point(Math.Clamp(p.X, -1e6f, 1e6f), Math.Clamp(p.Y, -1e6f, 1e6f));
            if (hasPrevious)
            {
                bool outside = (previous.X < minX && p.X < minX) || (previous.X > maxX && p.X > maxX)
                    || (previous.Y < minY && p.Y < minY) || (previous.Y > maxY && p.Y > maxY);
                if (outside)
                {
                    open = false;
                }
                else
                {
                    if (!open) builder.MoveTo(previous.X, previous.Y);
                    builder.LineTo(p.X, p.Y);
                    open = true;
                }
            }
            previous = p;
            hasPrevious = true;
        }
        using var path = builder.Detach();
        var paint = LinePaint;
        paint.Color = ToSkColor(color, context.CurrentOpacity);
        paint.StrokeWidth = series.LineWidth;
        paint.StrokeCap = series.LineStyle == ChartLineStyle.Dotted ? SKStrokeCap.Round : SKStrokeCap.Butt;
        using var effect = CreateDash(series.LineStyle, series.LineWidth);
        paint.PathEffect = effect;
        context.Canvas.DrawPath(path, paint);
        paint.PathEffect = null;
    }

    private static void DrawMarkers(XYSeries series, ChartLayout layout, Color color, ref DrawingContext context)
    {
        var area = layout.PlotArea;
        float size = series.MarkerSize;
        var visible = new Rect(area.X - size, area.Y - size, area.Width + 2 * size, area.Height + 2 * size);
        using var builder = new SKPathBuilder();
        foreach (var point in series.Points)
        {
            var p = layout.ToLocal(point);
            if (visible.Contains(p)) AddMarker(builder, series.Marker, p, size);
        }
        using var path = builder.Detach();
        context.DrawPath(path, color);
    }

    /// <summary>Adds a marker of <paramref name="size"/> (its width) centered at <paramref name="center"/>.</summary>
    private static void AddMarker(SKPathBuilder builder, ChartMarker marker, Point center, float size)
    {
        float r = size / 2;
        switch (marker)
        {
            case ChartMarker.Circle:
                builder.AddCircle(center.X, center.Y, r, SKPathDirection.Clockwise);
                break;
            case ChartMarker.Square:
                r *= 0.9f;
                builder.AddRect(new SKRect(center.X - r, center.Y - r, center.X + r, center.Y + r), SKPathDirection.Clockwise);
                break;
            case ChartMarker.Diamond:
                r *= 1.2f;
                builder.MoveTo(center.X, center.Y - r);
                builder.LineTo(center.X + r, center.Y);
                builder.LineTo(center.X, center.Y + r);
                builder.LineTo(center.X - r, center.Y);
                builder.Close();
                break;
            case ChartMarker.Triangle:
                r *= 1.2f;
                builder.MoveTo(center.X, center.Y - r);
                builder.LineTo(center.X + r * 0.866f, center.Y + r * 0.5f);
                builder.LineTo(center.X - r * 0.866f, center.Y + r * 0.5f);
                builder.Close();
                break;
        }
    }

    private static void DrawMarker(ChartMarker marker, Point center, float size, Color color, ref DrawingContext context)
    {
        if (marker == ChartMarker.None) return;
        using var builder = new SKPathBuilder();
        AddMarker(builder, marker, center, size);
        using var path = builder.Detach();
        context.DrawPath(path, color);
    }

    private static void DrawAnnotations(XYChart chart, ChartLayout layout, ref DrawingContext context, float small, string? family)
    {
        if (layout.Annotations.Length == 0) return;
        var font = TextMeasurer.GetFont(small, family);
        float spacing = layout.SmallFontSpacing;
        using var clip = context.PushClip(layout.PlotArea);
        foreach (var placed in layout.Annotations)
        {
            var color = placed.Annotation.Color ?? chart.Foreground;
            var label = placed.Label;
            if (placed.Lines.Length > 0)
            {
                if (placed.Annotation.ShowLeader)
                {
                    var end = Nearest(label, placed.Anchor);
                    context.DrawLine(placed.Anchor, end, color.WithAlpha(color.Af * 0.7f), 1);
                }
                var radius = new CornerRadius(Math.Min(8, label.Height / 2));
                context.DrawRoundedRect(label, radius, chart.LabelBackground);
                context.DrawRoundedRectOutline(label, radius, color.WithAlpha(color.Af * 0.55f), 1);
                float baseline = label.Y + ChartLayout.LabelPaddingY - font.Metrics.Ascent;
                foreach (var line in placed.Lines)
                {
                    context.DrawText(line, new Point(label.X + ChartLayout.LabelPaddingX, baseline), chart.Foreground, small, family);
                    baseline += spacing;
                }
            }
            // A ring of the background keeps the marker apart from the line it sits on.
            DrawMarker(placed.Annotation.Marker, placed.Anchor, 10, chart.PlotBackground.A > 0 ? chart.PlotBackground : chart.Background, ref context);
            DrawMarker(placed.Annotation.Marker, placed.Anchor, 7, color, ref context);
        }
    }

    private static void DrawLegend(XYChart chart, ChartLayout layout, ref DrawingContext context, float small, string? family)
    {
        if (layout.Legend.Length == 0) return;
        var rect = layout.LegendRect;
        var radius = new CornerRadius(6);
        context.DrawRoundedRect(rect, radius, chart.LabelBackground);
        context.DrawRoundedRectOutline(rect, radius, chart.GridColor, 1);
        var font = TextMeasurer.GetFont(small, family);
        float rowHeight = layout.SmallFontSpacing + 2;
        float y = rect.Y + ChartLayout.LabelPaddingY + 1;
        foreach (var entry in layout.Legend)
        {
            var series = entry.Series;
            float middle = y + rowHeight / 2;
            float x = rect.X + ChartLayout.LabelPaddingX;
            if (series.ShowLine && series.LineWidth > 0)
            {
                var paint = LinePaint;
                paint.Color = ToSkColor(entry.Color, context.CurrentOpacity);
                paint.StrokeWidth = Math.Min(series.LineWidth, 4);
                paint.StrokeCap = series.LineStyle == ChartLineStyle.Dotted ? SKStrokeCap.Round : SKStrokeCap.Butt;
                using var effect = CreateDash(series.LineStyle, paint.StrokeWidth);
                paint.PathEffect = effect;
                context.Canvas.DrawLine(x, middle, x + ChartLayout.SwatchWidth, middle, paint);
                paint.PathEffect = null;
            }
            if (series.Marker != ChartMarker.None)
            {
                DrawMarker(series.Marker, new Point(x + ChartLayout.SwatchWidth / 2, middle), Math.Clamp(series.MarkerSize, 5, 9), entry.Color, ref context);
            }
            float centerOffset = -(font.Metrics.Ascent + font.Metrics.Descent) / 2;
            context.DrawText(series.Title, new Point(x + ChartLayout.SwatchWidth + 6, middle + centerOffset), chart.Foreground, small, family);
            y += rowHeight;
        }
    }

    private static void DrawReadout(XYChart chart, ChartLayout layout, XYSeries series, ChartPoint point, Point local, Color color,
        ref DrawingContext context, float small, string? family)
    {
        var plot = layout.PlotArea;
        var cross = chart.AxisColor.WithAlpha(chart.AxisColor.Af * 0.5f);
        context.DrawLine(new Point(local.X, plot.Top), new Point(local.X, plot.Bottom), cross, 1);
        context.DrawLine(new Point(plot.Left, local.Y), new Point(plot.Right, local.Y), cross, 1);
        context.DrawCircle(local, 6, chart.PlotBackground.A > 0 ? chart.PlotBackground : chart.Background);
        context.DrawCircle(local, 4.5f, color);

        string values = $"{chart.XAxis.FormatTick(point.X, layout.XStep / 10)}, {chart.YAxis.FormatTick(point.Y, layout.YStep / 10)}";
        string[] lines = series.Title.Length > 0 ? [series.Title, values] : [values];
        float width = 0;
        foreach (var line in lines) width = Math.Max(width, TextMeasurer.MeasureWidth(line, small, family));
        float spacing = layout.SmallFontSpacing;
        var size = new Size(width + 2 * ChartLayout.LabelPaddingX, lines.Length * spacing + 2 * ChartLayout.LabelPaddingY);
        // Up and right of the point, flipped to stay inside the plot.
        float x = local.X + 12, y = local.Y - 12 - size.Height;
        if (x + size.Width > plot.Right) x = local.X - 12 - size.Width;
        if (y < plot.Top) y = local.Y + 12;
        var rect = new Rect(Math.Max(plot.Left, x), Math.Min(plot.Bottom - size.Height, y), size.Width, size.Height);
        var radius = new CornerRadius(6);
        context.DrawRoundedRect(rect, radius, chart.LabelBackground);
        context.DrawRoundedRectOutline(rect, radius, color, 1);
        var font = TextMeasurer.GetFont(small, family);
        float baseline = rect.Y + ChartLayout.LabelPaddingY - font.Metrics.Ascent;
        for (int i = 0; i < lines.Length; i++)
        {
            context.DrawText(lines[i], new Point(rect.X + ChartLayout.LabelPaddingX, baseline), chart.Foreground, small, family, bold: i == 0 && lines.Length > 1);
            baseline += spacing;
        }
    }

    // Dashes and dots scale with the line's width; solid lines have no effect.
    private static SKPathEffect? CreateDash(ChartLineStyle style, float lineWidth)
    {
        float w = Math.Max(1, lineWidth);
        return style switch
        {
            ChartLineStyle.Dashed => SKPathEffect.CreateDash([w * 4, w * 3], 0),
            ChartLineStyle.Dotted => SKPathEffect.CreateDash([0.01f, w * 2.5f], 0),
            _ => null,
        };
    }

    private static Point Nearest(Rect rect, Point p) =>
        new(Math.Clamp(p.X, rect.Left, rect.Right), Math.Clamp(p.Y, rect.Top, rect.Bottom));

    // Thin lines on pixel centers stay sharp.
    private static float Snap(float value) => MathF.Floor(value) + 0.5f;

    private static SKColor ToSkColor(Color color, float opacity) =>
        new(color.R, color.G, color.B, (byte)Math.Clamp(color.A * opacity, 0, 255));
}
