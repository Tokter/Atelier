using System.Runtime.CompilerServices;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Markup;

namespace Atelier.Audio;

/// <summary>Fluent methods for <see cref="TimelineControl"/>s such as the ruler and lanes. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class TimelineControlMarkup
{
    /// <summary>Sets the context shared with connected timeline controls; controls given the same one zoom and scroll together.</summary>
    public static T Timeline<T>(this T control, TimelineContext? timeline) where T : TimelineControl => control.Set(TimelineControl.TimelineProperty, timeline);

    /// <summary>Sets what the ticks count in: bars and beats, time or samples. The default is <see cref="TimelineRulerMode.Beats"/>.</summary>
    public static T Mode<T>(this T control, TimelineRulerMode mode) where T : TimelineControl => control.Set(TimelineControl.ModeProperty, mode);

    /// <summary>Sets the color of the minor ticks or grid lines.</summary>
    public static T LineColor<T>(this T control, Color color) where T : TimelineControl => control.Set(TimelineControl.LineColorProperty, color);

    /// <summary>Sets the color of the labeled and emphasized ticks or grid lines (such as bar lines).</summary>
    public static T MajorLineColor<T>(this T control, Color color) where T : TimelineControl => control.Set(TimelineControl.MajorLineColorProperty, color);

    /// <summary>Sets whether the control shows the shared markers of its context besides its own. The default is <c>true</c>.</summary>
    public static T ShowSharedMarkers<T>(this T control, bool show = true) where T : TimelineControl => control.Set(TimelineControl.ShowSharedMarkersProperty, show);

    /// <summary>Sets whether moves snap to the grid in view (Shift inverts it). The default is <c>true</c>.</summary>
    public static T SnapToGrid<T>(this T control, bool snap = true) where T : TimelineControl => control.Set(TimelineControl.SnapToGridProperty, snap);

    /// <summary>Sets the color of markers without a color of their own.</summary>
    public static T MarkerColor<T>(this T control, Color color) where T : TimelineControl => control.Set(TimelineControl.MarkerColorProperty, color);

    /// <summary>Sets the color of the loop.</summary>
    public static T LoopColor<T>(this T control, Color color) where T : TimelineControl => control.Set(TimelineControl.LoopColorProperty, color);

    /// <summary>Sets the color of the playhead and the play start marker.</summary>
    public static T PlayheadColor<T>(this T control, Color color) where T : TimelineControl => control.Set(TimelineControl.PlayheadColorProperty, color);

    /// <summary>Adds markers only this control shows (to its own <see cref="TimelineControl.Markers"/>).</summary>
    public static T Markers<T>(this T control, params TimelineMarker[] markers) where T : TimelineControl
    {
        foreach (var marker in markers) control.Markers.Add(marker);
        return control;
    }

    /// <summary>Binds the context to <paramref name="source"/>.</summary>
    public static T BindTimeline<T, TSource>(this T control, TSource source, Func<TSource, TimelineContext?> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TimelineControl where TSource : class =>
        control.BindToSource(TimelineControl.TimelineProperty, source, getter, null, UpdateSourceTrigger.PropertyChanged, getterExpression);

    /// <summary>Binds the mode to <paramref name="source"/>. With a <paramref name="setter"/>, choosing a mode in the ruler's menu writes it back.</summary>
    public static T BindMode<T, TSource>(this T control, TSource source, Func<TSource, TimelineRulerMode> getter, Action<TSource, TimelineRulerMode>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TimelineControl where TSource : class =>
        control.BindToSource(TimelineControl.ModeProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the mode to the DataContext. With a <paramref name="setter"/>, choosing a mode in the ruler's menu writes it back.</summary>
    public static T BindMode<T, TDataContext>(this T control, Func<TDataContext, TimelineRulerMode> getter, Action<TDataContext, TimelineRulerMode>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TimelineControl where TDataContext : class =>
        control.BindToDataContext(TimelineControl.ModeProperty, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>Fluent methods for <see cref="TimelineRuler"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class TimelineRulerMarkup
{
    /// <summary>Sets what the second row of labels counts in, or <c>null</c> (the default) for no second row.</summary>
    public static T SecondaryMode<T>(this T ruler, TimelineRulerMode? mode) where T : TimelineRuler => ruler.Set(TimelineRuler.SecondaryModeProperty, mode);

    /// <summary>Sets the color of the second row's labels.</summary>
    public static T SecondaryForeground<T>(this T ruler, Color color) where T : TimelineRuler => ruler.Set(TimelineRuler.SecondaryForegroundProperty, color);

    /// <summary>Sets whether the ruler has the marker lane at its top. The default is <c>true</c>.</summary>
    public static T ShowMarkers<T>(this T ruler, bool show = true) where T : TimelineRuler => ruler.Set(TimelineRuler.ShowMarkersProperty, show);

    /// <summary>Sets whether the ruler has the loop bar at its bottom. The default is <c>true</c>.</summary>
    public static T ShowLoopBar<T>(this T ruler, bool show = true) where T : TimelineRuler => ruler.Set(TimelineRuler.ShowLoopBarProperty, show);

    /// <summary>Handles <see cref="TimelineRuler.MarkerAdding"/>, raised before a marker is added from the ruler; the handler can change it or cancel.</summary>
    public static T OnMarkerAdding<T>(this T ruler, EventHandler<TimelineMarkerAddingEventArgs> handler) where T : TimelineRuler
    {
        ruler.MarkerAdding += handler;
        return ruler;
    }

    /// <summary>Binds the second row's mode to <paramref name="source"/>. With a <paramref name="setter"/>, choosing it in the ruler's menu writes it back.</summary>
    public static T BindSecondaryMode<T, TSource>(this T ruler, TSource source, Func<TSource, TimelineRulerMode?> getter, Action<TSource, TimelineRulerMode?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TimelineRuler where TSource : class =>
        ruler.BindToSource(TimelineRuler.SecondaryModeProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the second row's mode to the DataContext. With a <paramref name="setter"/>, choosing it in the ruler's menu writes it back.</summary>
    public static T BindSecondaryMode<T, TDataContext>(this T ruler, Func<TDataContext, TimelineRulerMode?> getter, Action<TDataContext, TimelineRulerMode?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TimelineRuler where TDataContext : class =>
        ruler.BindToDataContext(TimelineRuler.SecondaryModeProperty, getter, setter, updateSourceTrigger, getterExpression);
}
