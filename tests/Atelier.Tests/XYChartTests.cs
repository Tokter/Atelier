using System.Globalization;
using Atelier.Charts;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Tests;

public class XYChartTests
{
    private static XYChart CreateChart(float width = 600, float height = 400, params ChartPoint[] points)
    {
        var series = new XYSeries("Data");
        series.AddRange(points.Length > 0 ? points : [new(0, 0), new(10, 5), new(20, -5)]);
        var chart = new XYChart();
        chart.Series.Add(series);
        chart.Measure(new Size(width, height));
        chart.Arrange(new Rect(0, 0, width, height));
        return chart;
    }

    private static void AssertClose(double expected, double actual, double tolerance = 1e-6) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"Expected {expected}, got {actual}");

    #region Ticks

    [Theory]
    [InlineData(0.7, 1)]
    [InlineData(1, 1)]
    [InlineData(1.3, 2)]
    [InlineData(3, 5)]
    [InlineData(7, 10)]
    [InlineData(0.013, 0.02)]
    [InlineData(420, 500)]
    public void NiceStep_RoundsUpTo1_2Or5TimesAPowerOfTen(double rough, double expected) =>
        AssertClose(expected, ChartTicks.NiceStep(rough), expected * 1e-9);

    [Fact]
    public void Ticks_AreMultiplesOfTheStep_InsideTheRange()
    {
        var (step, ticks) = ChartTicks.Compute(-1.3, 8.7, 5);

        Assert.Equal(2, step);
        Assert.Equal([0, 2, 4, 6, 8], ticks);
    }

    [Fact]
    public void Ticks_OfAnEmptyRange_AreNone() => Assert.Empty(ChartTicks.Compute(5, 5, 5).Ticks);

    [Fact]
    public void TickLabels_ShowTheDecimalsTheStepNeeds_InTheCurrentCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-CH");
            var axis = new ChartAxis();
            Assert.Equal("10", axis.FormatTick(10, 5));
            Assert.Equal("0.25", axis.FormatTick(0.25, 0.05));
            Assert.Equal("0.0", axis.FormatTick(-1e-17, 0.5)); // no "-0.0"

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("1,5", axis.FormatTick(1.5, 0.5));
            axis.LabelFormat = "0.00";
            Assert.Equal("1,50", axis.FormatTick(1.5, 0.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void TheLayout_LabelsTicksInsideTheVisibleRange()
    {
        var chart = CreateChart();
        var layout = chart.GetLayout();

        Assert.NotEmpty(layout.XTicks);
        Assert.NotEmpty(layout.YTicks);
        Assert.All(layout.XTicks, t => Assert.InRange(t.Value, chart.VisibleXMin, chart.VisibleXMax));
        Assert.All(layout.YTicks, t => Assert.InRange(t.Value, chart.VisibleYMin, chart.VisibleYMax));
        Assert.All(layout.XTicks, t => Assert.InRange(t.Position, chart.PlotArea.Left - 0.01f, chart.PlotArea.Right + 0.01f));
    }

    #endregion

    #region Range

    [Fact]
    public void TheAutomaticRange_PadsTheData()
    {
        var chart = CreateChart(points: [new(10, 2), new(30, 6)]);

        Assert.True(chart.IsAutoRange);
        AssertClose(9, chart.VisibleXMin);
        AssertClose(31, chart.VisibleXMax);
        AssertClose(1.8, chart.VisibleYMin);
        AssertClose(6.2, chart.VisibleYMax);
    }

    [Fact]
    public void IncludeZero_ExtendsTheRangeToZero_WithoutPaddingBeyondIt()
    {
        var chart = CreateChart(points: [new(20, -1), new(50, -3)]);
        chart.XAxis.IncludeZero = true;
        chart.YAxis.IncludeZero = true;

        Assert.Equal(0, chart.VisibleXMin);
        AssertClose(52.5, chart.VisibleXMax);
        Assert.Equal(0, chart.VisibleYMax);
        AssertClose(-3.15, chart.VisibleYMin);
    }

    [Fact]
    public void AnAxisMinimumAndMaximum_FixTheAutomaticRange()
    {
        var chart = CreateChart(points: [new(20, -1), new(50, -3)]);
        chart.XAxis.Minimum = 15;
        chart.XAxis.Maximum = 60;

        Assert.Equal(15, chart.VisibleXMin);
        Assert.Equal(60, chart.VisibleXMax);
    }

    [Fact]
    public void ASinglePoint_OrNoData_StillGivesARange()
    {
        var single = CreateChart(points: [new(5, 5)]);
        Assert.True(single.VisibleXMax > single.VisibleXMin);
        Assert.InRange(5, single.VisibleXMin, single.VisibleXMax);

        var empty = new XYChart();
        Assert.Equal(0 - 0.05, empty.VisibleXMin, 6);
        Assert.Equal(1 + 0.05, empty.VisibleXMax, 6);
    }

    [Fact]
    public void HiddenSeries_DoNotCountForTheRange_ButAnnotationsDo()
    {
        var chart = CreateChart(points: [new(0, 0), new(10, 10)]);
        var hidden = new XYSeries { IsVisible = false };
        hidden.Add(1000, 1000);
        chart.Series.Add(hidden);
        chart.Annotations.Add(new ChartAnnotation(-20, 0, "Far left"));

        Assert.True(chart.VisibleXMax < 20);
        Assert.True(chart.VisibleXMin < -20);
    }

    [Fact]
    public void AddingPoints_RefitsTheAutomaticRange_AndRaisesViewChanged()
    {
        var chart = CreateChart(points: [new(0, 0), new(10, 10)]);
        int changes = 0;
        chart.ViewChanged += (_, _) => changes++;

        chart.Series[0].Add(100, 50);

        Assert.True(chart.VisibleXMax > 100);
        Assert.True(chart.VisibleYMax > 50);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void AddingPoints_KeepsAZoomedView()
    {
        var chart = CreateChart(points: [new(0, 0), new(10, 10)]);
        chart.ZoomTo(2, 4, 2, 4);

        chart.Series[0].Add(100, 50);

        Assert.Equal(4, chart.VisibleXMax);
        Assert.False(chart.IsAutoRange);
    }

    #endregion

    #region Mapping and navigation

    [Fact]
    public void DataToLocal_AndLocalToData_RoundTrip()
    {
        var chart = CreateChart();
        var point = new ChartPoint(7.25, -1.5);

        var local = chart.DataToLocal(point);
        var back = chart.LocalToData(local);

        Assert.True(chart.PlotArea.Contains(local));
        AssertClose(point.X, back.X, 1e-3);
        AssertClose(point.Y, back.Y, 1e-3);
    }

    [Fact]
    public void TheVisibleRange_SpansThePlotArea_WithLargerValuesUp()
    {
        var chart = CreateChart();
        var plot = chart.PlotArea;

        var bottomLeft = chart.DataToLocal(new ChartPoint(chart.VisibleXMin, chart.VisibleYMin));
        var topRight = chart.DataToLocal(new ChartPoint(chart.VisibleXMax, chart.VisibleYMax));

        Assert.Equal(plot.Left, bottomLeft.X, 2);
        Assert.Equal(plot.Bottom, bottomLeft.Y, 2);
        Assert.Equal(plot.Right, topRight.X, 2);
        Assert.Equal(plot.Top, topRight.Y, 2);
    }

    [Fact]
    public void ZoomAt_KeepsTheCenterAtTheSamePlaceOnScreen()
    {
        var chart = CreateChart();
        var center = new ChartPoint(4, 2);
        var before = chart.DataToLocal(center);
        double width = chart.VisibleXMax - chart.VisibleXMin;

        chart.ZoomAt(center, 0.5, 0.8);

        var after = chart.DataToLocal(center);
        Assert.Equal(before.X, after.X, 2);
        Assert.Equal(before.Y, after.Y, 2);
        AssertClose(width * 0.5, chart.VisibleXMax - chart.VisibleXMin, 1e-9);
        Assert.False(chart.IsAutoRange);
    }

    [Fact]
    public void Pan_MovesTheViewByDataUnits()
    {
        var chart = CreateChart();
        double x0 = chart.VisibleXMin, y0 = chart.VisibleYMin;

        chart.Pan(3, -1);

        AssertClose(x0 + 3, chart.VisibleXMin);
        AssertClose(y0 - 1, chart.VisibleYMin);
    }

    [Fact]
    public void ResetView_FitsTheDataAgain_AndRaisesViewChanged()
    {
        var chart = CreateChart();
        double x1 = chart.VisibleXMax;
        chart.ZoomTo(1, 2, 1, 2);
        int changes = 0;
        chart.ViewChanged += (_, _) => changes++;

        chart.ResetView();

        Assert.True(chart.IsAutoRange);
        Assert.Equal(x1, chart.VisibleXMax);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void ZoomTo_IgnoresEmptyAndInvalidRanges()
    {
        var chart = CreateChart();

        chart.ZoomTo(5, 5, 0, 1);
        chart.ZoomTo(0, 1, double.NaN, 1);

        Assert.True(chart.IsAutoRange);
    }

    [Fact]
    public void TheZoomCommands_ZoomAroundThePlotCenter_WithoutAPointer_AndHonorIsZoomEnabled()
    {
        var chart = CreateChart();
        var center = chart.LocalToData(chart.PlotArea.Center);
        double width = chart.VisibleXMax - chart.VisibleXMin, height = chart.VisibleYMax - chart.VisibleYMin;

        XYChartCommands.ZoomInX.Execute(chart);

        AssertClose(width / XYChartCommands.ZoomStep, chart.VisibleXMax - chart.VisibleXMin, 1e-9);
        AssertClose(height, chart.VisibleYMax - chart.VisibleYMin, 1e-9);
        var after = chart.LocalToData(chart.PlotArea.Center);
        AssertClose(center.X, after.X, 1e-3);

        chart.IsZoomEnabled = false;
        Assert.False(XYChartCommands.ZoomIn.CanExecute(chart));
        Assert.False(XYChartCommands.ZoomBox.CanExecute(chart));
        Assert.True(XYChartCommands.Pan.CanExecute(chart));
        chart.IsPanEnabled = false;
        Assert.False(XYChartCommands.Pan.CanExecute(chart));
    }

    [Fact]
    public void Home_ResetsTheView_AndPlusZoomsIn_WhileTheChartHasTheFocus()
    {
        var chart = CreateChart();
        chart.AttachToHost();
        try
        {
            chart.Focus();
            double width = chart.VisibleXMax - chart.VisibleXMin;

            FocusManager.DispatchKeyDown(new KeyEventArgs(Key.Equal), chart);
            Assert.False(chart.IsAutoRange);
            Assert.True(chart.VisibleXMax - chart.VisibleXMin < width);

            FocusManager.DispatchKeyDown(new KeyEventArgs(Key.Home), chart);
            Assert.True(chart.IsAutoRange);
        }
        finally
        {
            chart.DetachFromHost();
        }
    }

    [Fact]
    public void TheCommands_AreRegisteredInTheXYChartGroup()
    {
        _ = new XYChart();
        var names = Atelier.Core.Keybinding.KeybindingManager.GetKeybindings(XYChart.CommandGroup).Select(k => k.Name).ToList();

        Assert.Contains("Pan", names);
        Assert.Contains("ZoomBox", names);
        Assert.Contains("ZoomIn", names);
        Assert.Contains("ResetView", names);
    }

    #endregion

    #region Annotations and legend

    [Fact]
    public void AutoPlacement_AvoidsOverlappingLabels()
    {
        var size = new Size(80, 30);
        var plot = new Rect(0, 0, 400, 300);
        // Two points close together: the second label has to go elsewhere.
        var placed = ChartLayout_PlaceLabels([(new Point(200, 150), size, AnnotationPlacement.Auto), (new Point(210, 150), size, AnnotationPlacement.Auto)], plot);

        Assert.False(placed[0].Rect.IntersectsWith(placed[1].Rect));
        Assert.NotEqual(placed[0].Placement, placed[1].Placement);
    }

    [Fact]
    public void AutoPlacement_StaysInsideThePlot()
    {
        var size = new Size(80, 30);
        var plot = new Rect(0, 0, 400, 300);
        // At the top right corner, only below-left fits.
        var placed = ChartLayout_PlaceLabels([(new Point(395, 5), size, AnnotationPlacement.Auto)], plot);

        Assert.Equal(AnnotationPlacement.BelowLeft, placed[0].Placement);
        Assert.True(placed[0].Rect.Left >= 0 && placed[0].Rect.Bottom <= 300);
    }

    [Fact]
    public void AFixedPlacement_IsKept()
    {
        var placed = ChartLayout_PlaceLabels([(new Point(395, 5), new Size(80, 30), AnnotationPlacement.Above)], new Rect(0, 0, 400, 300));

        Assert.Equal(AnnotationPlacement.Above, placed[0].Placement);
        Assert.Equal(395 - 40, placed[0].Rect.X);
    }

    [Fact]
    public void AutoPlacement_PrefersSpotsOffTheSeries()
    {
        // A horizontal line through the point: the first placement (above right) is clear of it.
        var line = Enumerable.Range(0, 80).Select(i => new Point(i * 5, 150)).ToList();
        var placed = ChartLayout.PlaceLabels([(new Point(200, 150), new Size(80, 30), AnnotationPlacement.Auto)],
            new Rect(0, 0, 400, 300), [], line);
        Assert.DoesNotContain(line, p => placed[0].Rect.Contains(p));

        // A vertical line just right of the point: the label goes left.
        var vertical = Enumerable.Range(0, 60).Select(i => new Point(215, i * 5)).ToList();
        placed = ChartLayout.PlaceLabels([(new Point(200, 150), new Size(80, 30), AnnotationPlacement.Auto)],
            new Rect(0, 0, 400, 300), [], vertical);
        Assert.DoesNotContain(vertical, p => placed[0].Rect.Contains(p));
        Assert.True(placed[0].Rect.Right <= 215);
    }

    [Fact]
    public void Annotations_OutsideTheView_AreNotLaidOut()
    {
        var chart = CreateChart();
        chart.Annotations.Add(new ChartAnnotation(5, 0, "Inside"));
        chart.Annotations.Add(new ChartAnnotation(5, 0, "Later outside"));

        Assert.Equal(2, chart.GetLayout().Annotations.Length);
        chart.Annotations[1].X = 1000;
        chart.ZoomTo(0, 10, -5, 5);

        Assert.Single(chart.GetLayout().Annotations);
    }

    [Fact]
    public void Annotations_InTheChart_DoNotOverlapEachOther()
    {
        var chart = CreateChart();
        for (int i = 0; i < 4; i++) chart.Annotations.Add(new ChartAnnotation(4 + i * 2, 4, $"Point {i}\nsecond line"));

        var labels = chart.GetLayout().Annotations.Select(a => a.Label).ToList();

        Assert.Equal(4, labels.Count);
        for (int i = 0; i < labels.Count; i++)
        {
            for (int j = i + 1; j < labels.Count; j++) Assert.False(labels[i].IntersectsWith(labels[j]), $"Labels {i} and {j} overlap");
        }
    }

    [Fact]
    public void TheLegend_GoesTopRight_UnlessPointsAreThere()
    {
        var plot = new Rect(0, 0, 400, 300);
        var size = new Size(100, 40);

        var empty = ChartLayout.ChooseLegendCorner(plot, size, []);
        Assert.Equal(400 - 8 - 100, empty.X);
        Assert.Equal(8, empty.Y);

        // Points in the top right: the legend moves to the top left.
        var crowded = ChartLayout.ChooseLegendCorner(plot, size, [new Point(350, 20), new Point(330, 30)]);
        Assert.Equal(8, crowded.X);
        Assert.Equal(8, crowded.Y);
    }

    [Fact]
    public void TheLegend_ListsTitledVisibleSeries_AndHidesWhenThereAreNone()
    {
        var chart = CreateChart();
        Assert.Single(chart.GetLayout().Legend);

        chart.Series[0].ShowInLegend = false;
        Assert.Empty(chart.GetLayout().Legend);

        chart.Series[0].ShowInLegend = true;
        chart.ShowLegend = false;
        Assert.Empty(chart.GetLayout().Legend);
    }

    #endregion

    #region Layout, rendering and updates

    [Theory]
    [InlineData(600, 400)]
    [InlineData(200, 120)]
    [InlineData(40, 30)]
    public void ItLaysOut_AtVariousSizes(float width, float height)
    {
        using var _ = ActiveTheme.Use(MaterialTheme.CreateLight());
        var chart = CreateChart(width, height);
        chart.Title = "Title";
        chart.XAxis.Title = "X";
        chart.YAxis.Title = "Y";

        Assert.Equal(new Size(width, height), chart.DesiredSize);
        var plot = chart.PlotArea;
        Assert.True(plot.Width >= 1 && plot.Height >= 1);
        if (width >= 200)
        {
            Assert.True(plot.Left > 0 && plot.Right < width && plot.Top > 0 && plot.Bottom < height);
        }
        using var bitmap = Render(chart, (int)width, (int)height);
    }

    [Fact]
    public void WithoutASize_ItAsksForADefaultSize()
    {
        var chart = new XYChart();
        chart.Measure(Size.Infinity);

        Assert.Equal(new Size(480, 320), chart.DesiredSize);
    }

    [Fact]
    public void LongerTickLabels_MakeTheLeftMarginWider()
    {
        var small = CreateChart(points: [new(0, 0), new(1, 1)]);
        var large = CreateChart(points: [new(0, 0), new(1, 1_000_000)]);

        Assert.True(large.PlotArea.Left > small.PlotArea.Left);
    }

    [Fact]
    public void Changes_RedrawTheChart()
    {
        var chart = CreateChart();
        chart.Annotations.Add(new ChartAnnotation(1, 1, "A"));
        int redraws = 0;
        chart.NeedsVisualUpdate += () => redraws++;

        chart.Series[0].Add(30, 3);
        Assert.Equal(1, redraws);
        chart.Series[0].Color = Color.FromRgb(255, 0, 0);
        Assert.Equal(2, redraws);
        chart.Annotations[0].Text = "B";
        Assert.Equal(3, redraws);
        chart.XAxis.Title = "Speed";
        Assert.Equal(4, redraws);
        chart.Series.Add(new XYSeries());
        Assert.Equal(5, redraws);
        chart.Pan(1, 0);
        Assert.Equal(6, redraws);
    }

    [Fact]
    public void RemovedSeries_NoLongerRedrawTheChart()
    {
        var chart = CreateChart();
        var series = chart.Series[0];
        chart.Series.Remove(series);
        int redraws = 0;
        chart.NeedsVisualUpdate += () => redraws++;

        series.Add(1, 1);

        Assert.Equal(0, redraws);
    }

    [Fact]
    public void SeriesWithoutAColor_TakeThePaletteInOrder()
    {
        var chart = CreateChart();
        chart.Series.Add(new XYSeries { Color = Color.FromRgb(1, 2, 3) });
        chart.Series.Add(new XYSeries());

        Assert.Equal(chart.Palette[0], chart.SeriesColor(0));
        Assert.Equal(Color.FromRgb(1, 2, 3), chart.SeriesColor(1));
        Assert.Equal(chart.Palette[1], chart.SeriesColor(2));
    }

    [Fact]
    public void ItDraws_SeriesInTheirColor_InsideThePlot()
    {
        using var _ = ActiveTheme.Use(MaterialTheme.CreateLight());
        var series = new XYSeries { Color = Color.FromRgb(255, 0, 0), LineWidth = 6 };
        series.AddRange([new(0, 5), new(10, 5)]);
        var dotted = new XYSeries { LineStyle = ChartLineStyle.Dotted, Marker = ChartMarker.Diamond };
        dotted.AddRange([new(0, 0), new(10, 10)]);
        var chart = new XYChart();
        chart.Series.Add(series);
        chart.Series.Add(dotted);
        chart.Measure(new Size(400, 300));
        chart.Arrange(new Rect(0, 0, 400, 300));

        using var bitmap = Render(chart, 400, 300);

        var middle = chart.DataToLocal(new ChartPoint(5, 5));
        var pixel = bitmap.GetPixel((int)middle.X, (int)middle.Y);
        Assert.True(pixel.Red > 200 && pixel.Green < 60, $"Expected red, got {pixel}");
    }

    [Fact]
    public void TheThemes_StyleTheChart()
    {
        using (ActiveTheme.Use(MaterialTheme.CreateDark()))
        {
            var chart = new XYChart();
            chart.ApplyStyles();
            Assert.NotEqual(XYChart.DefaultPalette, chart.Palette);
            Assert.True(chart.Foreground.R > 128);
        }
        using (ActiveTheme.Use(MaterialTheme.CreateLight()))
        {
            var chart = new XYChart();
            chart.ApplyStyles();
            Assert.True(chart.Foreground.R < 128);
        }
    }

    [Fact]
    public void TheMarkupMethods_SetTheChart()
    {
        var series = new XYSeries("S");
        var annotation = new ChartAnnotation(1, 2, "A");
        int views = 0;
        var chart = new XYChart()
            .Title("T")
            .ShowLegend(false)
            .XAxisTitle("X")
            .YAxisTitle("Y")
            .Series(series)
            .Annotations(annotation)
            .OnViewChanged(_ => views++);

        chart.Pan(1, 1);

        Assert.Equal("T", chart.Title);
        Assert.False(chart.ShowLegend);
        Assert.Equal("X", chart.XAxis.Title);
        Assert.Equal("Y", chart.YAxis.Title);
        Assert.Same(series, chart.Series.Single());
        Assert.Same(annotation, chart.Annotations.Single());
        Assert.Equal(1, views);
    }

    #endregion

    private static (Rect Rect, AnnotationPlacement Placement)[] ChartLayout_PlaceLabels(
        IReadOnlyList<(Point, Size, AnnotationPlacement)> items, Rect plot) =>
        ChartLayout.PlaceLabels(items, plot, [], null);

    private static SkiaSharp.SKBitmap Render(XYChart chart, int width, int height)
    {
        var bitmap = new SkiaSharp.SKBitmap(width, height);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        using var paints = new PaintRegistry();
        var context = new DrawingContext(canvas, paints);
        VisualTreeRenderer.Render(chart, ref context, ThemeVisualPresenter.Instance);
        return bitmap;
    }
}
