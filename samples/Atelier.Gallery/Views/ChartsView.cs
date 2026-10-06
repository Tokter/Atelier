using System.Globalization;
using Atelier.Charts;
using Atelier.Controls;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class ChartsView : GalleryPage
{
    private readonly ChartsViewModel _vm;
    private readonly XYChart _polar;
    private readonly XYSeries _measured;
    private readonly Random _random = new(7);

    public ChartsView(ChartsViewModel viewModel)
        : base(MaterialIconKind.ShowChart, "Charts",
            "XYChart plots lines and scattered points for engineering data: axes with titles and nice ticks, labeled " +
            "points that keep out of each other's way, a legend, a readout of the point under the pointer, and zooming " +
            "and panning with the mouse or the keyboard.")
    {
        _vm = viewModel;

        // A paraglider's speed polar: vertical speed over airspeed, with its characteristic points labeled.
        var polar = new XYSeries("Polar") { LineWidth = 2.5f };
        for (double v = 22; v <= 54.01; v += 0.5) polar.Add(v, Sink(v));
        _measured = new XYSeries("Measured") { ShowLine = false, Marker = ChartMarker.Circle, MarkerSize = 5 };
        AddMeasurements(24);

        var (bestV, bestW) = BestGlide();
        var tangent = new XYSeries("Best glide") { LineStyle = ChartLineStyle.Dashed, LineWidth = 1.5f };
        tangent.Add(0, 0);
        tangent.Add(bestV * 1.3, bestW * 1.3);

        const double stall = 22, minSink = 31, trim = 37, fullSpeed = 54;
        _polar = new XYChart()
            .Title("Speed polar")
            .XAxisTitle("Airspeed (km/h)")
            .YAxisTitle("Vertical speed (m/s)")
            .XAxis(a => a.IncludeZero = true)
            .YAxis(a => a.IncludeZero = true)
            .Series(polar, _measured, tangent)
            .Annotations(
                new ChartAnnotation(stall, Sink(stall), Format("Stall\n{0:F0} km/h, {1:F1} m/s", stall, Sink(stall))),
                new ChartAnnotation(minSink, Sink(minSink), Format("Min sink\n{0:F0} km/h, {1:F2} m/s", minSink, Sink(minSink))),
                new ChartAnnotation(trim, Sink(trim), Format("Trim\n{0:F0} km/h, {1:F1} m/s", trim, Sink(trim))),
                new ChartAnnotation(bestV, bestW, Format("Best glide {0:F1}\n{1:F0} km/h, {2:F2} m/s", bestV / 3.6 / -bestW, bestV, bestW)),
                new ChartAnnotation(fullSpeed, Sink(fullSpeed), Format("Full speed bar\n{0:F0} km/h, {1:F1} m/s", fullSpeed, Sink(fullSpeed))))
            .OnViewChanged(UpdateRange)
            .Height(480);
        _polar.Bind(XYChart.ShowLegendProperty, _vm, v => v.ShowLegend);
        UpdateRange(_polar);

        // Thousands of points: a damped oscillation, to zoom into.
        var response = new XYSeries("Response") { LineWidth = 1.5f };
        var envelope = new XYSeries("Envelope") { LineStyle = ChartLineStyle.Dotted, LineWidth = 1.5f };
        for (int i = 0; i <= 5000; i++)
        {
            double t = i * 0.004;
            response.Add(t, Math.Exp(-t / 6) * Math.Sin(t * 7) + 0.03 * Math.Sin(t * 91));
            if (i % 50 == 0) envelope.Add(t, Math.Exp(-t / 6));
        }
        double peak = Math.PI / 14;
        var dense = new XYChart()
            .Title("Step response (5,000 points)")
            .XAxisTitle("Time (s)")
            .YAxisTitle("Amplitude")
            .Series(response, envelope)
            .Annotations(new ChartAnnotation(peak, Math.Exp(-peak / 6) * Math.Sin(peak * 7), "First peak"))
            .Height(320);
        dense.Bind(XYChart.ShowLegendProperty, _vm, v => v.ShowLegend);

        Settings(
            new Switch("Legend").BindIsChecked(_vm, v => v.ShowLegend, (v, on) => v.ShowLegend = on),
            new Button("Add measurements").Variant(ButtonVariant.Tonal).OnClick(() => AddMeasurements(8)),
            new Button("Clear measurements").Variant(ButtonVariant.Outlined).OnClick(_measured.Clear),
            new Button("Reset view").Variant(ButtonVariant.Outlined).OnClick(_polar.ResetView));

        Sections(
            Ui.Section("Polar curve",
                "A speed polar with its characteristic points labeled, measured points as markers and the best glide's " +
                "tangent dashed. Add measurements to see the chart follow new data; the view refits until you zoom or pan.",
                _polar,
                Ui.Readout(_vm, v => v.VisibleRange),
                Ui.Code("var chart = new XYChart()\n" +
                        "    .Title(\"Speed polar\").XAxisTitle(\"Airspeed (km/h)\").YAxisTitle(\"Vertical speed (m/s)\")\n" +
                        "    .Series(polar, measured, tangent)\n" +
                        "    .Annotations(new ChartAnnotation(37, -1.2, \"Trim\\n37 km/h, −1.2 m/s\"));\n" +
                        "measured.Add(airspeed, sink);   // redraws, and refits while the view follows the data")),
            Ui.Section("Lots of points", "Lines draw only the segments that show, so zooming into thousands of points stays fast.", dense),
            NavigationSection());
    }

    // A plausible polar: least sink at 31 km/h, steeper towards stall and with speed.
    private static double Sink(double v) => -(1.0 + 0.0028 * (v - 31) * (v - 31) + 0.5 * Math.Exp(-(v - 22) / 1.5));

    // The point of the polar where a line from the origin touches it: the best glide ratio.
    private static (double V, double W) BestGlide()
    {
        double best = 0, bestV = 0;
        for (double v = 22; v <= 54; v += 0.05)
        {
            double ratio = v / 3.6 / -Sink(v);
            if (ratio > best) (best, bestV) = (ratio, v);
        }
        return (bestV, Sink(bestV));
    }

    private void AddMeasurements(int count)
    {
        var points = new List<ChartPoint>();
        for (int i = 0; i < count; i++)
        {
            double v = 23 + _random.NextDouble() * 30;
            points.Add(new ChartPoint(v + (_random.NextDouble() - 0.5) * 0.8, Sink(v) + (_random.NextDouble() - 0.5) * 0.25));
        }
        _measured.AddRange(points);
    }

    private void UpdateRange(XYChart chart) =>
        _vm.VisibleRange = Format("{0}   x {1:F1} … {2:F1}   y {3:F2} … {4:F2}", chart.IsAutoRange ? "fitted" : "zoomed",
            chart.VisibleXMin, chart.VisibleXMax, chart.VisibleYMin, chart.VisibleYMax);

    private static string Format(string format, params object[] args) => string.Format(CultureInfo.CurrentCulture, format, args);

    private static UIElement NavigationSection()
    {
        static UIElement Row(string gesture, string action) =>
            new Grid().Columns(GridLength.Pixels(220), GridLength.Star).ColumnSpacing(12).Children(
                new TextBlock(gesture).Bold(),
                new TextBlock(action).Muted().TextWrapping().Column(1));

        return Ui.Section("Navigation",
            "All of these are commands of the XYChart group, rebindable in the keybinding editor.",
            new StackPanel().Spacing(10).Children(
                Row("Wheel", "Zoom at the pointer (Ctrl: horizontally only, Shift: vertically only)"),
                Row("Drag", "Pan"),
                Row("Right drag", "Zoom into a box (Escape cancels)"),
                Row("+ / −", "Zoom in and out"),
                Row("Home / double click", "Fit the view to the data"),
                Row("Hover", "Read the nearest point's values")));
    }
}
