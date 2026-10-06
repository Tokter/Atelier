using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Charts;

/// <summary>A tick of an axis: its value, label and position (x for the horizontal axis, y for the vertical one).</summary>
internal readonly record struct ChartTick(double Value, string Label, float Position, float LabelWidth);

/// <summary>An annotation as laid out: its point, label rectangle and lines (all local coordinates).</summary>
internal readonly record struct PlacedAnnotation(ChartAnnotation Annotation, Point Anchor, Rect Label, string[] Lines, AnnotationPlacement Placement);

/// <summary>A legend entry: the series and its color.</summary>
internal readonly record struct LegendEntry(XYSeries Series, Color Color);

/// <summary>
/// The layout of an <see cref="XYChart"/> for a size and view: margins from the label sizes, the plot area, ticks, the
/// legend's corner and where the annotation labels go.
/// </summary>
internal sealed class ChartLayout
{
    /// <summary>The space around the chart's content.</summary>
    public const float Padding = 8;

    /// <summary>The space between a tick label and the plot.</summary>
    public const float TickLabelGap = 6;

    /// <summary>The space between an annotated point and its label.</summary>
    public const float AnnotationGap = 10;

    /// <summary>The horizontal padding inside annotation labels.</summary>
    public const float LabelPaddingX = 6;

    /// <summary>The vertical padding inside annotation labels.</summary>
    public const float LabelPaddingY = 3;

    /// <summary>The width of a legend entry's swatch.</summary>
    public const float SwatchWidth = 22;

    /// <summary>The order <see cref="AnnotationPlacement.Auto"/> tries the placements in.</summary>
    public static readonly AnnotationPlacement[] AutoOrder =
    [
        AnnotationPlacement.AboveRight, AnnotationPlacement.BelowRight, AnnotationPlacement.AboveLeft, AnnotationPlacement.BelowLeft,
        AnnotationPlacement.Above, AnnotationPlacement.Below, AnnotationPlacement.Right, AnnotationPlacement.Left,
    ];

    private ChartLayout() { }

    public Size Size { get; private init; }
    public Rect PlotArea { get; private init; }
    public double X0 { get; private init; }
    public double X1 { get; private init; }
    public double Y0 { get; private init; }
    public double Y1 { get; private init; }
    public double XStep { get; private init; }
    public double YStep { get; private init; }
    public ChartTick[] XTicks { get; private set; } = [];
    public ChartTick[] YTicks { get; private set; } = [];
    public float FontSpacing { get; private init; }
    public float SmallFontSpacing { get; private init; }
    public Point TitlePosition { get; private init; }
    public Point XTitlePosition { get; private init; }
    public Point YTitlePosition { get; private init; }
    public Color[] SeriesColors { get; private init; } = [];
    public LegendEntry[] Legend { get; private set; } = [];
    public Rect LegendRect { get; private set; }
    public PlacedAnnotation[] Annotations { get; private set; } = [];

    /// <summary>Converts a data point to local coordinates.</summary>
    public Point ToLocal(ChartPoint p) => new(ToLocalX(p.X), ToLocalY(p.Y));

    /// <summary>Converts a horizontal value to a local x.</summary>
    public float ToLocalX(double x) => (float)(PlotArea.X + (x - X0) / (X1 - X0) * PlotArea.Width);

    /// <summary>Converts a vertical value to a local y.</summary>
    public float ToLocalY(double y) => (float)(PlotArea.Bottom - (y - Y0) / (Y1 - Y0) * PlotArea.Height);

    /// <summary>Lays out <paramref name="chart"/> at <paramref name="size"/>, showing the given range.</summary>
    public static ChartLayout Compute(XYChart chart, Size size, (double X0, double X1, double Y0, double Y1) view)
    {
        string? family = chart.FontFamily;
        float fontSize = chart.FontSize;
        float small = chart.SmallFontSize;
        float spacing = TextMeasurer.GetFontSpacing(fontSize, family);
        float smallSpacing = TextMeasurer.GetFontSpacing(small, family);

        float top = Padding;
        var titlePosition = default(Point);
        if (chart.Title.Length > 0)
        {
            float titleSpacing = TextMeasurer.GetFontSpacing(fontSize + 2, family, bold: true);
            titlePosition = new Point(0, top + titleSpacing * 0.8f);
            top += titleSpacing + 6;
        }
        else
        {
            top += smallSpacing * 0.5f; // the top tick label sticks out above the plot
        }
        float bottom = Padding + smallSpacing + TickLabelGap;
        if (chart.XAxis.Title.Length > 0) bottom += spacing + 2;
        float plotHeight = Math.Max(1, size.Height - top - bottom);

        // The vertical axis' labels decide the left margin.
        var (yStep, yValues) = ChartTicks.Compute(view.Y0, view.Y1, Math.Max(2, (int)(plotHeight / 36)));
        var yLabels = new string[yValues.Length];
        float yLabelWidth = 0;
        for (int i = 0; i < yValues.Length; i++)
        {
            yLabels[i] = chart.YAxis.FormatTick(yValues[i], yStep);
            yLabelWidth = Math.Max(yLabelWidth, TextMeasurer.MeasureWidth(yLabels[i], small, family));
        }
        float left = Padding + yLabelWidth + TickLabelGap;
        if (chart.YAxis.Title.Length > 0) left += spacing + 4;
        float right = Padding + 12; // room for half of the last horizontal tick label
        float plotWidth = Math.Max(1, size.Width - left - right);
        var plot = new Rect(left, top, plotWidth, plotHeight);

        // Horizontal ticks: as many as fit with their labels apart.
        int xCount = Math.Max(2, (int)(plotWidth / 70));
        double xStep;
        double[] xValues;
        string[] xLabels;
        float[] xWidths;
        while (true)
        {
            (xStep, xValues) = ChartTicks.Compute(view.X0, view.X1, xCount);
            xLabels = new string[xValues.Length];
            xWidths = new float[xValues.Length];
            float widest = 0;
            for (int i = 0; i < xValues.Length; i++)
            {
                xLabels[i] = chart.XAxis.FormatTick(xValues[i], xStep);
                xWidths[i] = TextMeasurer.MeasureWidth(xLabels[i], small, family);
                widest = Math.Max(widest, xWidths[i]);
            }
            double pixelsPerStep = xStep / (view.X1 - view.X0) * plotWidth;
            if (pixelsPerStep >= widest + 12 || xCount <= 2) break;
            xCount = Math.Max(2, xCount / 2);
        }

        var layout = new ChartLayout
        {
            Size = size,
            PlotArea = plot,
            X0 = view.X0,
            X1 = view.X1,
            Y0 = view.Y0,
            Y1 = view.Y1,
            XStep = xStep,
            YStep = yStep,
            FontSpacing = spacing,
            SmallFontSpacing = smallSpacing,
            TitlePosition = new Point(plot.X + plot.Width / 2, titlePosition.Y),
            XTitlePosition = new Point(plot.X + plot.Width / 2, size.Height - Padding - spacing * 0.25f),
            YTitlePosition = new Point(Padding + spacing * 0.8f, plot.Y + plot.Height / 2),
            SeriesColors = Enumerable.Range(0, chart.Series.Count).Select(chart.SeriesColor).ToArray(),
        };
        var xTicks = new ChartTick[xValues.Length];
        for (int i = 0; i < xValues.Length; i++) xTicks[i] = new ChartTick(xValues[i], xLabels[i], layout.ToLocalX(xValues[i]), xWidths[i]);
        var yTicks = new ChartTick[yValues.Length];
        for (int i = 0; i < yValues.Length; i++) yTicks[i] = new ChartTick(yValues[i], yLabels[i], layout.ToLocalY(yValues[i]), 0);
        layout.XTicks = xTicks;
        layout.YTicks = yTicks;
        layout.LayOutLegend(chart, small, smallSpacing, family);
        layout.LayOutAnnotations(chart, small, smallSpacing, family);
        return layout;
    }

    #region Legend

    private void LayOutLegend(XYChart chart, float fontSize, float spacing, string? family)
    {
        if (!chart.ShowLegend) return;
        var entries = new List<LegendEntry>();
        float width = 0;
        for (int i = 0; i < chart.Series.Count; i++)
        {
            var series = chart.Series[i];
            if (!series.ShowInLegend || !series.IsVisible || series.Title.Length == 0) continue;
            entries.Add(new LegendEntry(series, SeriesColors[i]));
            width = Math.Max(width, TextMeasurer.MeasureWidth(series.Title, fontSize, family));
        }
        if (entries.Count == 0) return;
        var size = new Size(SwatchWidth + 6 + width + 2 * LabelPaddingX + 2, entries.Count * (spacing + 2) + 2 * LabelPaddingY + 2);
        if (size.Width > PlotArea.Width - 8 || size.Height > PlotArea.Height - 8) return;
        Legend = entries.ToArray();
        LegendRect = ChooseLegendCorner(PlotArea, size, VisiblePoints(chart).ToList());
    }

    private IEnumerable<Point> VisiblePoints(XYChart chart)
    {
        foreach (var series in chart.Series)
        {
            if (!series.IsVisible) continue;
            var points = series.Points;
            // Thousands of points are plenty to judge a corner by.
            int stride = Math.Max(1, points.Count / 4000);
            for (int i = 0; i < points.Count; i += stride) yield return ToLocal(points[i]);
        }
        foreach (var annotation in chart.Annotations) yield return ToLocal(new ChartPoint(annotation.X, annotation.Y));
    }

    /// <summary>
    /// Chooses the corner of <paramref name="plot"/> for a legend of <paramref name="size"/>: top right unless it covers
    /// points, else the corner covering the fewest.
    /// </summary>
    internal static Rect ChooseLegendCorner(Rect plot, Size size, IReadOnlyList<Point> points)
    {
        const float inset = 8;
        Rect[] corners =
        [
            new(plot.Right - inset - size.Width, plot.Y + inset, size.Width, size.Height),
            new(plot.X + inset, plot.Y + inset, size.Width, size.Height),
            new(plot.Right - inset - size.Width, plot.Bottom - inset - size.Height, size.Width, size.Height),
            new(plot.X + inset, plot.Bottom - inset - size.Height, size.Width, size.Height),
        ];
        Rect best = corners[0];
        int bestCount = int.MaxValue;
        foreach (var corner in corners)
        {
            var area = Inflate(corner, 4);
            int count = 0;
            foreach (var p in points)
            {
                if (area.Contains(p)) count++;
            }
            if (count < bestCount)
            {
                best = corner;
                bestCount = count;
                if (count == 0) break;
            }
        }
        return best;
    }

    #endregion

    #region Annotations

    private void LayOutAnnotations(XYChart chart, float fontSize, float spacing, string? family)
    {
        var anchors = new List<(ChartAnnotation Annotation, Point Anchor, Size Size, string[] Lines)>();
        foreach (var annotation in chart.Annotations)
        {
            var anchor = ToLocal(new ChartPoint(annotation.X, annotation.Y));
            if (!double.IsFinite(anchor.X) || !double.IsFinite(anchor.Y) || !Inflate(PlotArea, 0.5f).Contains(anchor)) continue;
            string[] lines = annotation.Text.Length == 0 ? [] : annotation.Text.Split('\n');
            float width = 0;
            foreach (var line in lines) width = Math.Max(width, TextMeasurer.MeasureWidth(line, fontSize, family));
            var size = lines.Length == 0 ? Size.Zero : new Size(width + 2 * LabelPaddingX, lines.Length * spacing + 2 * LabelPaddingY);
            anchors.Add((annotation, anchor, size, lines));
        }
        var obstacles = new List<Rect>();
        if (Legend.Length > 0) obstacles.Add(Inflate(LegendRect, 2));
        var placed = PlaceLabels(anchors.Select(a => (a.Anchor, a.Size, a.Annotation.Placement)).ToList(), PlotArea, obstacles,
            anchors.Count > 0 ? SeriesSamples(chart) : []);
        Annotations = anchors.Select((a, i) => new PlacedAnnotation(a.Annotation, a.Anchor, placed[i].Rect, a.Lines, placed[i].Placement)).ToArray();
    }

    // What covering a point of a series costs a label, in the units of overlapping area (pixels²).
    private const double AvoidCost = 12;

    // Points along the visible series inside the plot, a few pixels apart, for labels to keep off.
    private List<Point> SeriesSamples(XYChart chart)
    {
        const float step = 6;
        const int limit = 20000;
        var samples = new List<Point>();
        var plot = PlotArea;
        foreach (var series in chart.Series)
        {
            if (!series.IsVisible) continue;
            var points = series.Points;
            bool line = series.ShowLine && series.LineWidth > 0;
            Point previous = default;
            for (int i = 0; i < points.Count && samples.Count < limit; i++)
            {
                var p = ToLocal(points[i]);
                if (plot.Contains(p)) samples.Add(p);
                if (line && i > 0)
                {
                    float dx = p.X - previous.X, dy = p.Y - previous.Y;
                    int steps = (int)Math.Min(200, MathF.Sqrt(dx * dx + dy * dy) / step);
                    for (int s = 1; s < steps; s++)
                    {
                        var q = new Point(previous.X + dx * s / steps, previous.Y + dy * s / steps);
                        if (plot.Contains(q)) samples.Add(q);
                    }
                }
                previous = p;
            }
        }
        return samples;
    }

    /// <summary>Gets the rectangle of a label of <paramref name="size"/> at <paramref name="placement"/> of <paramref name="anchor"/>.</summary>
    internal static Rect LabelRect(Point anchor, Size size, AnnotationPlacement placement)
    {
        const float gap = AnnotationGap;
        const float diagonal = AnnotationGap * 0.7f;
        float w = size.Width, h = size.Height;
        return placement switch
        {
            AnnotationPlacement.Above => new Rect(anchor.X - w / 2, anchor.Y - gap - h, w, h),
            AnnotationPlacement.Below => new Rect(anchor.X - w / 2, anchor.Y + gap, w, h),
            AnnotationPlacement.Left => new Rect(anchor.X - gap - w, anchor.Y - h / 2, w, h),
            AnnotationPlacement.Right => new Rect(anchor.X + gap, anchor.Y - h / 2, w, h),
            AnnotationPlacement.AboveLeft => new Rect(anchor.X - diagonal - w, anchor.Y - diagonal - h, w, h),
            AnnotationPlacement.BelowLeft => new Rect(anchor.X - diagonal - w, anchor.Y + diagonal, w, h),
            AnnotationPlacement.BelowRight => new Rect(anchor.X + diagonal, anchor.Y + diagonal, w, h),
            _ => new Rect(anchor.X + diagonal, anchor.Y - diagonal - h, w, h),
        };
    }

    /// <summary>
    /// Places labels in order: a fixed placement is taken as it is; <see cref="AnnotationPlacement.Auto"/> takes the first
    /// of <see cref="AutoOrder"/> that stays inside <paramref name="plot"/> and overlaps neither the labels placed before,
    /// the annotated points nor <paramref name="obstacles"/>, or the one overlapping least; covering points of
    /// <paramref name="avoid"/> (the series' lines and markers) counts too, but less than covering labels.
    /// </summary>
    internal static (Rect Rect, AnnotationPlacement Placement)[] PlaceLabels(
        IReadOnlyList<(Point Anchor, Size Size, AnnotationPlacement Placement)> items, Rect plot, IReadOnlyList<Rect> obstacles,
        IReadOnlyList<Point>? avoid = null)
    {
        var taken = new List<Rect>(obstacles);
        // Every annotated point is kept clear, so labels don't cover other labeled points.
        foreach (var item in items) taken.Add(new Rect(item.Anchor.X - 5, item.Anchor.Y - 5, 10, 10));
        var result = new (Rect, AnnotationPlacement)[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            var (anchor, size, placement) = items[i];
            if (placement != AnnotationPlacement.Auto)
            {
                result[i] = (LabelRect(anchor, size, placement), placement);
            }
            else
            {
                var own = new Rect(anchor.X - 5, anchor.Y - 5, 10, 10);
                double bestCost = double.MaxValue;
                foreach (var candidate in AutoOrder)
                {
                    var rect = LabelRect(anchor, size, candidate);
                    double cost = OutsideArea(rect, plot) * 4;
                    foreach (var other in taken)
                    {
                        if (other == own) continue;
                        cost += OverlapArea(rect, other);
                    }
                    if (avoid != null)
                    {
                        foreach (var p in avoid)
                        {
                            if (rect.Contains(p)) cost += AvoidCost;
                        }
                    }
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        result[i] = (rect, candidate);
                        if (cost <= 0) break;
                    }
                }
            }
            if (size.Width > 0) taken.Add(Inflate(result[i].Item1, 2));
        }
        return result;
    }

    private static double OverlapArea(Rect a, Rect b)
    {
        double w = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
        double h = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
        return w > 0 && h > 0 ? w * h : 0;
    }

    private static double OutsideArea(Rect rect, Rect bounds) => rect.Width * rect.Height - OverlapArea(rect, bounds);

    private static Rect Inflate(Rect rect, float by) => new(rect.X - by, rect.Y - by, rect.Width + 2 * by, rect.Height + 2 * by);

    #endregion
}
