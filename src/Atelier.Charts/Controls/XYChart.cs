using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Rendering;

namespace Atelier.Charts;

/// <summary>
/// A 2D chart of <see cref="XYSeries"/> (lines and/or markers) with labeled points (<see cref="Annotations"/>), axes with
/// titles and nice ticks, a legend, zooming and panning, and a readout of the data point under the pointer.
/// </summary>
/// <remarks>
/// <para>
/// The view fits the data (<see cref="IsAutoRange"/>) until the user zooms or pans; <see cref="ResetView"/> (Home, or a
/// double click) goes back. Interaction is made of commands of the <see cref="CommandGroup"/> keybinding group (see
/// <see cref="XYChartCommands"/>), so users can rebind them: the wheel zooms at the pointer (Ctrl only horizontally, Shift
/// only vertically), dragging pans, right-dragging zooms into a box (Escape cancels), + and − zoom.
/// </para>
/// <para>
/// Colors come from the theme (<see cref="ChartsTheme"/>): <see cref="Control.Foreground"/> for text,
/// <see cref="PlotBackground"/>, <see cref="GridColor"/>, <see cref="AxisColor"/>, <see cref="SecondaryForeground"/>
/// (tick labels), <see cref="LabelBackground"/> (annotation labels and the legend) and the series <see cref="Palette"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var polar = new XYSeries("Polar") { Marker = ChartMarker.Circle };
/// polar.AddRange(points);
/// var chart = new XYChart()
///     .Title("Polar curve").XAxisTitle("Airspeed (km/h)").YAxisTitle("Vertical speed (m/s)")
///     .Series(polar)
///     .Annotations(new ChartAnnotation(37, -1.2, "Trim\n37 km/h, −1.2 m/s"));
/// </code>
/// </example>
public partial class XYChart : KeybindingHandler
{
    /// <summary>The keybinding group of the chart's commands (see <see cref="XYChartCommands"/>).</summary>
    public const string CommandGroup = "XYChart";

    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly BindableProperty<string> TitleProperty =
        BindableProperty.Register<XYChart, string>(nameof(Title), string.Empty, (c, _, _) => ((XYChart)c).Invalidate());

    /// <summary>Identifies the <see cref="ShowLegend"/> property.</summary>
    public static readonly BindableProperty<bool> ShowLegendProperty =
        BindableProperty.Register<XYChart, bool>(nameof(ShowLegend), true, (c, _, _) => ((XYChart)c).Invalidate());

    /// <summary>Identifies the <see cref="IsZoomEnabled"/> property.</summary>
    public static readonly BindableProperty<bool> IsZoomEnabledProperty =
        BindableProperty.Register<XYChart, bool>(nameof(IsZoomEnabled), true);

    /// <summary>Identifies the <see cref="IsPanEnabled"/> property.</summary>
    public static readonly BindableProperty<bool> IsPanEnabledProperty =
        BindableProperty.Register<XYChart, bool>(nameof(IsPanEnabled), true);

    /// <summary>Identifies the <see cref="PlotBackground"/> property.</summary>
    public static readonly BindableProperty<Color> PlotBackgroundProperty =
        BindableProperty.Register<XYChart, Color>(nameof(PlotBackground), Color.Transparent, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="GridColor"/> property.</summary>
    public static readonly BindableProperty<Color> GridColorProperty =
        BindableProperty.Register<XYChart, Color>(nameof(GridColor), Color.FromRgb(0x80, 0x80, 0x80).WithAlpha(0.25f), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="AxisColor"/> property.</summary>
    public static readonly BindableProperty<Color> AxisColorProperty =
        BindableProperty.Register<XYChart, Color>(nameof(AxisColor), Color.FromRgb(0x80, 0x80, 0x80), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="SecondaryForeground"/> property.</summary>
    public static readonly BindableProperty<Color> SecondaryForegroundProperty =
        BindableProperty.Register<XYChart, Color>(nameof(SecondaryForeground), Color.FromRgb(0x70, 0x70, 0x70), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="LabelBackground"/> property.</summary>
    public static readonly BindableProperty<Color> LabelBackgroundProperty =
        BindableProperty.Register<XYChart, Color>(nameof(LabelBackground), Color.White.WithAlpha(0.9f), options: PropertyOptions.AffectsRender);

    /// <summary>Gets the palette used when no theme sets one.</summary>
    public static IReadOnlyList<Color> DefaultPalette { get; } =
    [
        Color.FromRgb(0x19, 0x76, 0xD2), Color.FromRgb(0xE6, 0x51, 0x00), Color.FromRgb(0x2E, 0x7D, 0x32),
        Color.FromRgb(0x8E, 0x24, 0xAA), Color.FromRgb(0xC6, 0x28, 0x28), Color.FromRgb(0x00, 0x83, 0x8F),
    ];

    /// <summary>Identifies the <see cref="Palette"/> property.</summary>
    public static readonly BindableProperty<IReadOnlyList<Color>> PaletteProperty =
        BindableProperty.Register<XYChart, IReadOnlyList<Color>>(nameof(Palette), DefaultPalette, (c, _, _) => ((XYChart)c).Invalidate());

    private bool _isAutoRange = true;
    private (double X0, double X1, double Y0, double Y1) _manual;
    private (double X0, double X1, double Y0, double Y1)? _autoRange;
    private ChartLayout? _layout;
    private Size _layoutSize;
    private (float Size, string? Family) _layoutFont;
    private Point? _pointer;

    static XYChart()
    {
        ChartsTheme.Register();
        IsFocusableProperty.OverrideDefaultValue<XYChart>(true);
        ClipToBoundsProperty.OverrideDefaultValue<XYChart>(true);
    }

    /// <summary>Initializes an empty chart.</summary>
    public XYChart()
    {
        XYChartCommands.Register();
        AdditionalScopes.Add(new KeybindingScope(CommandGroup, this));
        Series.CollectionChanged += (_, e) => OnItemsChanged<XYSeries>(e, s => s.Changed += OnDataChanged, s => s.Changed -= OnDataChanged);
        Annotations.CollectionChanged += (_, e) => OnItemsChanged<ChartAnnotation>(e, a => a.Changed += OnDataChanged, a => a.Changed -= OnDataChanged);
        XAxis.Changed += OnDataChanged;
        YAxis.Changed += OnDataChanged;
    }

    /// <summary>Gets or sets the title shown above the plot; empty for none.</summary>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>Gets the data series, drawn in this order.</summary>
    public ObservableCollection<XYSeries> Series { get; } = [];

    /// <summary>Gets the labeled points, drawn over the series.</summary>
    public ObservableCollection<ChartAnnotation> Annotations { get; } = [];

    /// <summary>Gets the horizontal axis.</summary>
    public ChartAxis XAxis { get; } = new();

    /// <summary>Gets the vertical axis.</summary>
    public ChartAxis YAxis { get; } = new();

    /// <summary>Gets or sets whether the legend is shown (when a series is listed in it). The default is <c>true</c>.</summary>
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }

    /// <summary>Gets or sets whether the user can zoom. The default is <c>true</c>.</summary>
    public bool IsZoomEnabled { get => GetValue(IsZoomEnabledProperty); set => SetValue(IsZoomEnabledProperty, value); }

    /// <summary>Gets or sets whether the user can pan. The default is <c>true</c>.</summary>
    public bool IsPanEnabled { get => GetValue(IsPanEnabledProperty); set => SetValue(IsPanEnabledProperty, value); }

    /// <summary>Gets or sets the background of the plot area.</summary>
    public Color PlotBackground { get => GetValue(PlotBackgroundProperty); set => SetValue(PlotBackgroundProperty, value); }

    /// <summary>Gets or sets the color of the grid lines.</summary>
    public Color GridColor { get => GetValue(GridColorProperty); set => SetValue(GridColorProperty, value); }

    /// <summary>Gets or sets the color of the axis lines and the zero lines.</summary>
    public Color AxisColor { get => GetValue(AxisColorProperty); set => SetValue(AxisColorProperty, value); }

    /// <summary>Gets or sets the color of the tick labels.</summary>
    public Color SecondaryForeground { get => GetValue(SecondaryForegroundProperty); set => SetValue(SecondaryForegroundProperty, value); }

    /// <summary>Gets or sets the background of the annotation labels, the legend and the hover readout.</summary>
    public Color LabelBackground { get => GetValue(LabelBackgroundProperty); set => SetValue(LabelBackgroundProperty, value); }

    /// <summary>Gets or sets the colors of series without a color of their own, in order.</summary>
    public IReadOnlyList<Color> Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }

    /// <summary>Gets whether the view fits the data (until the user zooms or pans).</summary>
    public bool IsAutoRange => _isAutoRange;

    /// <summary>Gets the left end of the visible range.</summary>
    public double VisibleXMin => View.X0;

    /// <summary>Gets the right end of the visible range.</summary>
    public double VisibleXMax => View.X1;

    /// <summary>Gets the bottom end of the visible range.</summary>
    public double VisibleYMin => View.Y0;

    /// <summary>Gets the top end of the visible range.</summary>
    public double VisibleYMax => View.Y1;

    /// <summary>Gets the area inside the axes (local coordinates), as of the last layout.</summary>
    public Rect PlotArea => GetLayout().PlotArea;

    /// <summary>Gets the pointer position over the chart (local coordinates), or <c>null</c> when it is elsewhere.</summary>
    public Point? PointerPosition => _pointer;

    /// <summary>Occurs when the visible range changed (zoom, pan, reset, or the data while fitted).</summary>
    public event EventHandler? ViewChanged;

    private (double X0, double X1, double Y0, double Y1) View => _isAutoRange ? AutoRange() : _manual;

    /// <summary>Fits the view to the data again.</summary>
    public void ResetView()
    {
        _isAutoRange = true;
        OnViewChanged();
    }

    /// <summary>Shows the given range.</summary>
    public void ZoomTo(double xMin, double xMax, double yMin, double yMax)
    {
        if (!(xMax > xMin) || !(yMax > yMin) || !double.IsFinite(xMax - xMin) || !double.IsFinite(yMax - yMin)) return;
        _manual = (xMin, xMax, yMin, yMax);
        _isAutoRange = false;
        OnViewChanged();
    }

    /// <summary>
    /// Zooms around <paramref name="center"/> (in data coordinates), which stays at the same place on screen: factors below
    /// 1 zoom in, above 1 out, 1 leaves an axis as it is.
    /// </summary>
    public void ZoomAt(ChartPoint center, double factorX, double factorY)
    {
        var v = View;
        ZoomTo(center.X - (center.X - v.X0) * factorX, center.X + (v.X1 - center.X) * factorX,
            center.Y - (center.Y - v.Y0) * factorY, center.Y + (v.Y1 - center.Y) * factorY);
    }

    /// <summary>Moves the view by (<paramref name="dx"/>, <paramref name="dy"/>) in data units (positive shows larger values).</summary>
    public void Pan(double dx, double dy)
    {
        var v = View;
        ZoomTo(v.X0 + dx, v.X1 + dx, v.Y0 + dy, v.Y1 + dy);
    }

    /// <summary>Converts a data point to local coordinates.</summary>
    public Point DataToLocal(ChartPoint point) => GetLayout().ToLocal(point);

    /// <summary>Converts local coordinates to a data point.</summary>
    public ChartPoint LocalToData(Point point)
    {
        var layout = GetLayout();
        var plot = layout.PlotArea;
        var v = (X0: layout.X0, X1: layout.X1, Y0: layout.Y0, Y1: layout.Y1);
        return new ChartPoint(
            v.X0 + (point.X - plot.X) / Math.Max(1e-6, plot.Width) * (v.X1 - v.X0),
            v.Y0 + (plot.Bottom - point.Y) / Math.Max(1e-6, plot.Height) * (v.Y1 - v.Y0));
    }

    #region Data and range

    private void OnItemsChanged<T>(NotifyCollectionChangedEventArgs e, Action<T> subscribe, Action<T> unsubscribe)
    {
        if (e.OldItems != null) foreach (T item in e.OldItems) unsubscribe(item);
        if (e.NewItems != null) foreach (T item in e.NewItems) subscribe(item);
        OnDataChanged(this, EventArgs.Empty);
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        _autoRange = null;
        if (_isAutoRange) ViewChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private void OnViewChanged()
    {
        ViewChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>Drops the cached layout and redraws.</summary>
    protected void Invalidate()
    {
        _layout = null;
        InvalidateVisual();
    }

    // The data range (visible series and annotations), padded, with the axes' fixed ends and zero when asked for.
    private (double X0, double X1, double Y0, double Y1) AutoRange()
    {
        if (_autoRange is { } cached) return cached;
        double x0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y0 = double.PositiveInfinity, y1 = double.NegativeInfinity;
        void Include(double x, double y)
        {
            if (!double.IsFinite(x) || !double.IsFinite(y)) return;
            if (x < x0) x0 = x;
            if (x > x1) x1 = x;
            if (y < y0) y0 = y;
            if (y > y1) y1 = y;
        }
        foreach (var series in Series)
        {
            if (!series.IsVisible) continue;
            foreach (var p in series.Points) Include(p.X, p.Y);
        }
        foreach (var a in Annotations) Include(a.X, a.Y);
        var (ax0, ax1) = AxisRange(XAxis, x0, x1);
        var (ay0, ay1) = AxisRange(YAxis, y0, y1);
        _autoRange = (ax0, ax1, ay0, ay1);
        return _autoRange.Value;
    }

    /// <summary>The automatic range of one axis from the data's extent: padded by 5%, the axis' fixed ends, zero if asked.</summary>
    internal static (double Min, double Max) AxisRange(ChartAxis axis, double min, double max)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max))
        {
            min = 0;
            max = 1;
        }
        if (axis.IncludeZero)
        {
            min = Math.Min(min, 0);
            max = Math.Max(max, 0);
        }
        if (max - min < 1e-12)
        {
            double pad = Math.Abs(min) > 1e-12 ? Math.Abs(min) * 0.1 : 1;
            min -= pad;
            max += pad;
        }
        double padding = (max - min) * 0.05;
        // Zero stays at the edge when it is one (an axis that starts at 0 shouldn't show negative values).
        if (!(axis.IncludeZero && min == 0)) min -= padding;
        if (!(axis.IncludeZero && max == 0)) max += padding;
        if (axis.Minimum is { } fixedMin) min = fixedMin;
        if (axis.Maximum is { } fixedMax) max = fixedMax;
        if (!(max > min)) max = min + 1;
        return (min, max);
    }

    #endregion

    #region Layout

    /// <summary>Gets the layout of the chart (plot area, ticks, labels, legend) for its current size and view.</summary>
    internal ChartLayout GetLayout()
    {
        var size = new Size(Bounds.Width, Bounds.Height);
        var font = (FontSize, FontFamily);
        if (_layout == null || _layoutSize != size || _layoutFont != font)
        {
            _layout = ChartLayout.Compute(this, size, View);
            _layoutSize = size;
            _layoutFont = font;
        }
        return _layout;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) =>
        new(float.IsInfinity(availableSize.Width) ? 480 : availableSize.Width,
            float.IsInfinity(availableSize.Height) ? 320 : availableSize.Height);

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _layout = null;
        return finalSize;
    }

    #endregion

    #region Pointer

    /// <summary>Gets the zoom box being dragged (local coordinates), or <c>null</c>.</summary>
    internal Rect? ZoomBox { get; set; }

    /// <summary>Shows or clears the zoom box.</summary>
    internal void SetZoomBox(Rect? box)
    {
        ZoomBox = box;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void OnPreviewPointerPressed(PointerEventArgs e)
    {
        _pointer = e.Position;
        base.OnPreviewPointerPressed(e);
        if (!IsFocused) Focus();
    }

    /// <inheritdoc/>
    public override void OnPreviewPointerMoved(PointerEventArgs e)
    {
        _pointer = e.Position;
        base.OnPreviewPointerMoved(e);
        InvalidateVisual(); // the hover readout follows the pointer
    }

    /// <inheritdoc/>
    public override void OnPreviewPointerWheel(PointerWheelEventArgs e)
    {
        _pointer = e.Position;
        base.OnPreviewPointerWheel(e);
    }

    /// <inheritdoc/>
    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointer = null;
        InvalidateVisual();
    }

    /// <summary>Gets the data point nearest to the pointer (within 24 pixels), for the hover readout.</summary>
    internal (XYSeries Series, ChartPoint Point, Point Local, Color Color)? HoveredPoint()
    {
        if (_pointer is not { } pointer || ZoomBox != null) return null;
        var layout = GetLayout();
        if (!layout.PlotArea.Contains(pointer)) return null;
        (XYSeries, ChartPoint, Point, Color)? best = null;
        float bestDistance = 24 * 24;
        for (int s = 0; s < Series.Count; s++)
        {
            var series = Series[s];
            if (!series.IsVisible) continue;
            foreach (var p in series.Points)
            {
                var local = DataToLocal(p);
                float dx = local.X - pointer.X, dy = local.Y - pointer.Y;
                float d = dx * dx + dy * dy;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = (series, p, local, layout.SeriesColors[s]);
                }
            }
        }
        return best;
    }

    #endregion

    /// <summary>Gets the color a series is drawn in: its own, or the palette's.</summary>
    internal Color SeriesColor(int index)
    {
        if (Series[index].Color is { } color) return color;
        var palette = Palette;
        if (palette.Count == 0) return Foreground;
        int paletteIndex = 0;
        for (int i = 0; i < index; i++)
        {
            if (Series[i].Color == null) paletteIndex++;
        }
        return palette[paletteIndex % palette.Count];
    }

    /// <summary>Gets the font size of tick labels and annotations.</summary>
    internal float SmallFontSize => Math.Max(9, FontSize - 1.5f);
}
