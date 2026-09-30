using System.Runtime.CompilerServices;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Markup;

namespace Atelier.Audio;

/// <summary>Fluent methods for <see cref="WaveformView"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class WaveformViewMarkup
{
    /// <summary>Sets the sound shown.</summary>
    public static T Source<T>(this T view, WaveformData? source) where T : WaveformView => view.Set(WaveformView.SourceProperty, source);

    /// <summary>Sets where in the sound (seconds) the view starts, such as a clip's trimmed start. The default is 0.</summary>
    public static T SourceStart<T>(this T view, double seconds) where T : WaveformView => view.Set(WaveformView.SourceStartProperty, seconds);

    /// <summary>Sets how many seconds of the sound are shown; NaN (the default) shows the rest of it.</summary>
    public static T SourceLength<T>(this T view, double seconds) where T : WaveformView => view.Set(WaveformView.SourceLengthProperty, seconds);

    /// <summary>Sets how several channels are shown. The default is <see cref="WaveformChannelLayout.Stacked"/>.</summary>
    public static T ChannelLayout<T>(this T view, WaveformChannelLayout layout) where T : WaveformView => view.Set(WaveformView.ChannelLayoutProperty, layout);

    /// <summary>Sets the vertical scale: 1 (the default) draws full scale to the edges.</summary>
    public static T Gain<T>(this T view, float gain) where T : WaveformView => view.Set(WaveformView.GainProperty, gain);

    /// <summary>Sets the color of each channel's zero line; transparent for none.</summary>
    public static T CenterLineColor<T>(this T view, Color color) where T : WaveformView => view.Set(WaveformView.CenterLineColorProperty, color);

    /// <summary>Binds the sound shown to <paramref name="source"/>.</summary>
    public static T BindSource<T, TSource>(this T view, TSource source, Func<TSource, WaveformData?> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : WaveformView where TSource : class =>
        view.BindToSource(WaveformView.SourceProperty, source, getter, null, UpdateSourceTrigger.PropertyChanged, getterExpression);
}

/// <summary>Fluent methods for placing elements on a <see cref="TimelinePanel"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class TimelinePanelMarkup
{
    /// <summary>Sets where the element begins on its <see cref="TimelinePanel"/>, in seconds or beats.</summary>
    public static T TimelineStart<T>(this T element, TimelinePosition start) where T : UIElement => element.Set(TimelinePanel.StartProperty, start);

    /// <summary>Sets how long the element is on its <see cref="TimelinePanel"/>, in seconds or beats.</summary>
    public static T TimelineLength<T>(this T element, TimelinePosition length) where T : UIElement => element.Set(TimelinePanel.LengthProperty, length);

    /// <summary>Sets where the element begins and how long it is on its <see cref="TimelinePanel"/>.</summary>
    public static T TimelineRange<T>(this T element, TimelinePosition start, TimelinePosition length) where T : UIElement =>
        element.TimelineStart(start).TimelineLength(length);
}
