using Atelier.Core.Primitives;

namespace Atelier.Charts;

/// <summary>A point of a chart, in data coordinates.</summary>
/// <param name="X">The horizontal value.</param>
/// <param name="Y">The vertical value.</param>
public readonly record struct ChartPoint(double X, double Y);

/// <summary>How a series' line is drawn.</summary>
public enum ChartLineStyle
{
    /// <summary>A continuous line.</summary>
    Solid,
    /// <summary>A dashed line.</summary>
    Dashed,
    /// <summary>A dotted line.</summary>
    Dotted,
}

/// <summary>The shape drawn at a series' points or an annotated point.</summary>
public enum ChartMarker
{
    /// <summary>No marker.</summary>
    None,
    /// <summary>A filled circle.</summary>
    Circle,
    /// <summary>A filled square.</summary>
    Square,
    /// <summary>A filled diamond.</summary>
    Diamond,
    /// <summary>A filled triangle pointing up.</summary>
    Triangle,
}

/// <summary>Where an annotation's label goes relative to its point.</summary>
public enum AnnotationPlacement
{
    /// <summary>The first of the other placements that doesn't overlap other labels, markers or the plot's edges.</summary>
    Auto,
    /// <summary>Centered above the point.</summary>
    Above,
    /// <summary>Centered below the point.</summary>
    Below,
    /// <summary>Left of the point.</summary>
    Left,
    /// <summary>Right of the point.</summary>
    Right,
    /// <summary>Above and to the left.</summary>
    AboveLeft,
    /// <summary>Above and to the right.</summary>
    AboveRight,
    /// <summary>Below and to the left.</summary>
    BelowLeft,
    /// <summary>Below and to the right.</summary>
    BelowRight,
}

/// <summary>
/// A data series of an <see cref="XYChart"/>: its points, drawn as a line through them and/or as markers.
/// </summary>
/// <remarks>Every change raises <see cref="Changed"/>, which redraws the charts showing the series.</remarks>
public sealed class XYSeries
{
    private readonly List<ChartPoint> _points = [];
    private string _title;
    private Color? _color;
    private float _lineWidth = 2f;
    private bool _showLine = true;
    private ChartLineStyle _lineStyle = ChartLineStyle.Solid;
    private ChartMarker _marker = ChartMarker.None;
    private float _markerSize = 6f;
    private bool _showInLegend = true;
    private bool _isVisible = true;

    /// <summary>Initializes an empty series.</summary>
    /// <param name="title">The title shown in the legend and the hover readout.</param>
    public XYSeries(string title = "") => _title = title ?? string.Empty;

    /// <summary>Gets or sets the title shown in the legend and the hover readout.</summary>
    public string Title { get => _title; set => Set(ref _title, value ?? string.Empty); }

    /// <summary>Gets or sets the color; <c>null</c> (the default) takes the next color of the chart's palette.</summary>
    public Color? Color { get => _color; set => Set(ref _color, value); }

    /// <summary>Gets or sets the width of the line in pixels. The default is 2.</summary>
    public float LineWidth { get => _lineWidth; set => Set(ref _lineWidth, Math.Max(0, value)); }

    /// <summary>Gets or sets whether a line connects the points. The default is <c>true</c>.</summary>
    public bool ShowLine { get => _showLine; set => Set(ref _showLine, value); }

    /// <summary>Gets or sets how the line is drawn. The default is <see cref="ChartLineStyle.Solid"/>.</summary>
    public ChartLineStyle LineStyle { get => _lineStyle; set => Set(ref _lineStyle, value); }

    /// <summary>Gets or sets the marker drawn at each point. The default is <see cref="ChartMarker.None"/>.</summary>
    public ChartMarker Marker { get => _marker; set => Set(ref _marker, value); }

    /// <summary>Gets or sets the marker size (its width) in pixels. The default is 6.</summary>
    public float MarkerSize { get => _markerSize; set => Set(ref _markerSize, Math.Max(0, value)); }

    /// <summary>Gets or sets whether the series is listed in the legend. The default is <c>true</c>.</summary>
    public bool ShowInLegend { get => _showInLegend; set => Set(ref _showInLegend, value); }

    /// <summary>Gets or sets whether the series is drawn (and counts for the automatic range). The default is <c>true</c>.</summary>
    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }

    /// <summary>Gets the points, in the order they are connected.</summary>
    public IReadOnlyList<ChartPoint> Points => _points;

    /// <summary>Occurs when the points or the style changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Adds a point.</summary>
    public void Add(double x, double y) => Add(new ChartPoint(x, y));

    /// <summary>Adds a point.</summary>
    public void Add(ChartPoint point)
    {
        _points.Add(point);
        OnChanged();
    }

    /// <summary>Adds points.</summary>
    public void AddRange(IEnumerable<ChartPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points.AddRange(points);
        OnChanged();
    }

    /// <summary>Replaces the points.</summary>
    public void SetPoints(IEnumerable<ChartPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points.Clear();
        _points.AddRange(points);
        OnChanged();
    }

    /// <summary>Removes all points.</summary>
    public void Clear()
    {
        if (_points.Count == 0) return;
        _points.Clear();
        OnChanged();
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// A labeled point of an <see cref="XYChart"/>, such as "Trim 37 km/h": a marker at the point and its text in a small
/// label next to it.
/// </summary>
public sealed class ChartAnnotation
{
    private double _x, _y;
    private string _text;
    private Color? _color;
    private AnnotationPlacement _placement = AnnotationPlacement.Auto;
    private ChartMarker _marker = ChartMarker.Circle;
    private bool _showLeader = true;

    /// <summary>Initializes an annotation of the point (<paramref name="x"/>, <paramref name="y"/>).</summary>
    /// <param name="x">The point's horizontal value.</param>
    /// <param name="y">The point's vertical value.</param>
    /// <param name="text">The label; may contain line breaks.</param>
    public ChartAnnotation(double x, double y, string text)
    {
        _x = x;
        _y = y;
        _text = text ?? string.Empty;
    }

    /// <summary>Gets or sets the point's horizontal value.</summary>
    public double X { get => _x; set => Set(ref _x, value); }

    /// <summary>Gets or sets the point's vertical value.</summary>
    public double Y { get => _y; set => Set(ref _y, value); }

    /// <summary>Gets or sets the label; '\n' starts a new line.</summary>
    public string Text { get => _text; set => Set(ref _text, value ?? string.Empty); }

    /// <summary>Gets or sets the color of the marker and the label's outline; <c>null</c> uses the chart's text color.</summary>
    public Color? Color { get => _color; set => Set(ref _color, value); }

    /// <summary>Gets or sets where the label goes. The default, <see cref="AnnotationPlacement.Auto"/>, avoids overlaps.</summary>
    public AnnotationPlacement Placement { get => _placement; set => Set(ref _placement, value); }

    /// <summary>Gets or sets the marker at the point. The default is <see cref="ChartMarker.Circle"/>.</summary>
    public ChartMarker Marker { get => _marker; set => Set(ref _marker, value); }

    /// <summary>Gets or sets whether a thin line connects the point and its label. The default is <c>true</c>.</summary>
    public bool ShowLeader { get => _showLeader; set => Set(ref _showLeader, value); }

    /// <summary>Occurs when the annotation changed.</summary>
    public event EventHandler? Changed;

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>An axis of an <see cref="XYChart"/>: its title, range and labels.</summary>
public sealed class ChartAxis
{
    private string _title = string.Empty;
    private double? _minimum, _maximum;
    private bool _includeZero;
    private string? _labelFormat;
    private bool _showGrid = true;

    /// <summary>Gets or sets the axis title, e.g. "Airspeed (km/h)".</summary>
    public string Title { get => _title; set => Set(ref _title, value ?? string.Empty); }

    /// <summary>Gets or sets the lower end of the automatic range; <c>null</c> (the default) follows the data.</summary>
    public double? Minimum { get => _minimum; set => Set(ref _minimum, value); }

    /// <summary>Gets or sets the upper end of the automatic range; <c>null</c> (the default) follows the data.</summary>
    public double? Maximum { get => _maximum; set => Set(ref _maximum, value); }

    /// <summary>Gets or sets whether the automatic range always includes 0. The default is <c>false</c>.</summary>
    public bool IncludeZero { get => _includeZero; set => Set(ref _includeZero, value); }

    /// <summary>
    /// Gets or sets the .NET format of the tick labels, e.g. "0.0"; <c>null</c> (the default) shows as many decimals as the
    /// tick spacing needs.
    /// </summary>
    public string? LabelFormat { get => _labelFormat; set => Set(ref _labelFormat, value); }

    /// <summary>Gets or sets whether grid lines are drawn at the ticks. The default is <c>true</c>.</summary>
    public bool ShowGrid { get => _showGrid; set => Set(ref _showGrid, value); }

    /// <summary>Occurs when the axis changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Formats a tick value with <see cref="LabelFormat"/>, or with the decimals <paramref name="step"/> needs.</summary>
    public string FormatTick(double value, double step)
    {
        if (Math.Abs(value) < step * 1e-9) value = 0; // no "-0"
        if (_labelFormat != null) return value.ToString(_labelFormat, System.Globalization.CultureInfo.CurrentCulture);
        int decimals = ChartTicks.Decimals(step);
        return value.ToString("F" + decimals, System.Globalization.CultureInfo.CurrentCulture);
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Tick values at "nice" steps (1, 2 or 5 × 10ⁿ).</summary>
public static class ChartTicks
{
    /// <summary>Gets the nice step (1, 2 or 5 × 10ⁿ) closest above <paramref name="rough"/>.</summary>
    public static double NiceStep(double rough)
    {
        if (!(rough > 0) || double.IsInfinity(rough)) return 1;
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        double n = rough / magnitude;
        return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * magnitude;
    }

    /// <summary>Gets the ticks within [<paramref name="min"/>, <paramref name="max"/>] for about <paramref name="maxCount"/> ticks.</summary>
    /// <returns>The step and the tick values, ascending.</returns>
    public static (double Step, double[] Ticks) Compute(double min, double max, int maxCount)
    {
        if (!(max > min)) return (1, []);
        double step = NiceStep((max - min) / Math.Max(1, maxCount));
        double first = Math.Ceiling(min / step - 1e-9) * step;
        var ticks = new List<double>();
        for (double t = first; t <= max + step * 1e-9 && ticks.Count < 1000; t = first + ticks.Count * step)
        {
            ticks.Add(t);
        }
        return (step, ticks.ToArray());
    }

    /// <summary>Gets the decimals labels of ticks <paramref name="step"/> apart need.</summary>
    public static int Decimals(double step) => step >= 1 || step <= 0 ? 0 : Math.Min(10, (int)Math.Ceiling(-Math.Log10(step) - 1e-9));
}
