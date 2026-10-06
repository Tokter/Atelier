using System.Runtime.CompilerServices;
using Atelier.Core.Properties;
using Atelier.Markup;

namespace Atelier.Charts;

/// <summary>Fluent methods for <see cref="XYChart"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class XYChartMarkup
{
    /// <summary>Sets the title shown above the plot.</summary>
    public static T Title<T>(this T chart, string title) where T : XYChart => chart.Set(XYChart.TitleProperty, title);

    /// <summary>Sets whether the legend is shown. The default is <c>true</c>.</summary>
    public static T ShowLegend<T>(this T chart, bool show = true) where T : XYChart => chart.Set(XYChart.ShowLegendProperty, show);

    /// <summary>Sets whether the user can zoom. The default is <c>true</c>.</summary>
    public static T IsZoomEnabled<T>(this T chart, bool enabled = true) where T : XYChart => chart.Set(XYChart.IsZoomEnabledProperty, enabled);

    /// <summary>Sets whether the user can pan. The default is <c>true</c>.</summary>
    public static T IsPanEnabled<T>(this T chart, bool enabled = true) where T : XYChart => chart.Set(XYChart.IsPanEnabledProperty, enabled);

    /// <summary>Sets the title of the horizontal axis.</summary>
    public static T XAxisTitle<T>(this T chart, string title) where T : XYChart
    {
        chart.XAxis.Title = title;
        return chart;
    }

    /// <summary>Sets the title of the vertical axis.</summary>
    public static T YAxisTitle<T>(this T chart, string title) where T : XYChart
    {
        chart.YAxis.Title = title;
        return chart;
    }

    /// <summary>Configures the horizontal axis (range, label format, grid).</summary>
    public static T XAxis<T>(this T chart, Action<ChartAxis> configure) where T : XYChart
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(chart.XAxis);
        return chart;
    }

    /// <summary>Configures the vertical axis (range, label format, grid).</summary>
    public static T YAxis<T>(this T chart, Action<ChartAxis> configure) where T : XYChart
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(chart.YAxis);
        return chart;
    }

    /// <summary>Adds data series.</summary>
    public static T Series<T>(this T chart, params XYSeries[] series) where T : XYChart
    {
        foreach (var s in series) chart.Series.Add(s);
        return chart;
    }

    /// <summary>Adds labeled points.</summary>
    public static T Annotations<T>(this T chart, params ChartAnnotation[] annotations) where T : XYChart
    {
        foreach (var a in annotations) chart.Annotations.Add(a);
        return chart;
    }

    /// <summary>Calls <paramref name="handler"/> when the visible range changed.</summary>
    public static T OnViewChanged<T>(this T chart, Action<T> handler) where T : XYChart
    {
        ArgumentNullException.ThrowIfNull(handler);
        chart.ViewChanged += (_, _) => handler(chart);
        return chart;
    }

    /// <summary>Binds the title to <paramref name="source"/>.</summary>
    public static T BindTitle<T, TSource>(this T chart, TSource source, Func<TSource, string> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : XYChart where TSource : class =>
        chart.BindToSource(XYChart.TitleProperty, source, getter, null, UpdateSourceTrigger.PropertyChanged, getterExpression);
}
