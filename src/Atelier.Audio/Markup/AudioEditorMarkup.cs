using System.Runtime.CompilerServices;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Markup;

namespace Atelier.Audio;

/// <summary>Fluent methods for <see cref="AudioWaveformEditor"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class AudioWaveformEditorMarkup
{
    /// <summary>Sets the sound edited.</summary>
    public static T Source<T>(this T editor, WaveformData? source) where T : AudioWaveformEditor => editor.Set(AudioWaveformEditor.SourceProperty, source);

    /// <summary>Sets where on the timeline the sound's first sample plays.</summary>
    public static T StartTime<T>(this T editor, TimelinePosition start) where T : AudioWaveformEditor => editor.Set(AudioWaveformEditor.StartTimeProperty, start);

    /// <summary>Sets how the fades are shaped. The default is <see cref="Audio.FadeCurve.Linear"/>.</summary>
    public static T FadeCurve<T>(this T editor, FadeCurve curve) where T : AudioWaveformEditor => editor.Set(AudioWaveformEditor.FadeCurveProperty, curve);

    /// <summary>Sets how several channels are shown. The default is <see cref="WaveformChannelLayout.Stacked"/>.</summary>
    public static T ChannelLayout<T>(this T editor, WaveformChannelLayout layout) where T : AudioWaveformEditor => editor.Set(AudioWaveformEditor.ChannelLayoutProperty, layout);

    /// <summary>Sets whether the zoomed-out waveform shows the RMS inside its peaks. The default is <c>true</c>.</summary>
    public static T ShowRms<T>(this T editor, bool show = true) where T : AudioWaveformEditor => editor.Set(AudioWaveformEditor.ShowRmsProperty, show);

    /// <summary>Sets the color of the selection.</summary>
    public static T SelectionColor<T>(this T editor, Color color) where T : AudioWaveformEditor => editor.Set(AudioWaveformEditor.SelectionColorProperty, color);

    /// <summary>Sets the color of the trim edges, the fades and the gain chip.</summary>
    public static T HandleColor<T>(this T editor, Color color) where T : AudioWaveformEditor => editor.Set(AudioWaveformEditor.HandleColorProperty, color);

    /// <summary>Sets where in the sound (seconds) the audible region starts.</summary>
    public static T TrimStart<T>(this T editor, double value) where T : AudioWaveformEditor => editor.Set(AudioWaveformEditor.TrimStartProperty, value);

    /// <summary>Binds where in the sound (seconds) the audible region starts to <paramref name="source"/>. With a <paramref name="setter"/>, editing writes it back.</summary>
    public static T BindTrimStart<T, TSource>(this T editor, TSource source, Func<TSource, double> getter, Action<TSource, double>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : AudioWaveformEditor where TSource : class =>
        editor.BindToSource(AudioWaveformEditor.TrimStartProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds where in the sound (seconds) the audible region starts to the DataContext. With a <paramref name="setter"/>, editing writes it back.</summary>
    public static T BindTrimStart<T, TDataContext>(this T editor, Func<TDataContext, double> getter, Action<TDataContext, double>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : AudioWaveformEditor where TDataContext : class =>
        editor.BindToDataContext(AudioWaveformEditor.TrimStartProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Sets where in the sound (seconds) the audible region ends (NaN for the end of the sound).</summary>
    public static T TrimEnd<T>(this T editor, double value) where T : AudioWaveformEditor => editor.Set(AudioWaveformEditor.TrimEndProperty, value);

    /// <summary>Binds where in the sound (seconds) the audible region ends (NaN for the end of the sound) to <paramref name="source"/>. With a <paramref name="setter"/>, editing writes it back.</summary>
    public static T BindTrimEnd<T, TSource>(this T editor, TSource source, Func<TSource, double> getter, Action<TSource, double>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : AudioWaveformEditor where TSource : class =>
        editor.BindToSource(AudioWaveformEditor.TrimEndProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds where in the sound (seconds) the audible region ends (NaN for the end of the sound) to the DataContext. With a <paramref name="setter"/>, editing writes it back.</summary>
    public static T BindTrimEnd<T, TDataContext>(this T editor, Func<TDataContext, double> getter, Action<TDataContext, double>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : AudioWaveformEditor where TDataContext : class =>
        editor.BindToDataContext(AudioWaveformEditor.TrimEndProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Sets the length of the fade in (seconds).</summary>
    public static T FadeIn<T>(this T editor, double value) where T : AudioWaveformEditor => editor.Set(AudioWaveformEditor.FadeInProperty, value);

    /// <summary>Binds the length of the fade in (seconds) to <paramref name="source"/>. With a <paramref name="setter"/>, editing writes it back.</summary>
    public static T BindFadeIn<T, TSource>(this T editor, TSource source, Func<TSource, double> getter, Action<TSource, double>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : AudioWaveformEditor where TSource : class =>
        editor.BindToSource(AudioWaveformEditor.FadeInProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the length of the fade in (seconds) to the DataContext. With a <paramref name="setter"/>, editing writes it back.</summary>
    public static T BindFadeIn<T, TDataContext>(this T editor, Func<TDataContext, double> getter, Action<TDataContext, double>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : AudioWaveformEditor where TDataContext : class =>
        editor.BindToDataContext(AudioWaveformEditor.FadeInProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Sets the length of the fade out (seconds).</summary>
    public static T FadeOut<T>(this T editor, double value) where T : AudioWaveformEditor => editor.Set(AudioWaveformEditor.FadeOutProperty, value);

    /// <summary>Binds the length of the fade out (seconds) to <paramref name="source"/>. With a <paramref name="setter"/>, editing writes it back.</summary>
    public static T BindFadeOut<T, TSource>(this T editor, TSource source, Func<TSource, double> getter, Action<TSource, double>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : AudioWaveformEditor where TSource : class =>
        editor.BindToSource(AudioWaveformEditor.FadeOutProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the length of the fade out (seconds) to the DataContext. With a <paramref name="setter"/>, editing writes it back.</summary>
    public static T BindFadeOut<T, TDataContext>(this T editor, Func<TDataContext, double> getter, Action<TDataContext, double>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : AudioWaveformEditor where TDataContext : class =>
        editor.BindToDataContext(AudioWaveformEditor.FadeOutProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Sets the gain in dB.</summary>
    public static T Gain<T>(this T editor, float value) where T : AudioWaveformEditor => editor.Set(AudioWaveformEditor.GainProperty, value);

    /// <summary>Binds the gain in dB to <paramref name="source"/>. With a <paramref name="setter"/>, editing writes it back.</summary>
    public static T BindGain<T, TSource>(this T editor, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : AudioWaveformEditor where TSource : class =>
        editor.BindToSource(AudioWaveformEditor.GainProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the gain in dB to the DataContext. With a <paramref name="setter"/>, editing writes it back.</summary>
    public static T BindGain<T, TDataContext>(this T editor, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : AudioWaveformEditor where TDataContext : class =>
        editor.BindToDataContext(AudioWaveformEditor.GainProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Sets the selected stretch of the timeline (null for none).</summary>
    public static T Selection<T>(this T editor, TimelineRange? value) where T : AudioWaveformEditor => editor.Set(AudioWaveformEditor.SelectionProperty, value);

    /// <summary>Binds the selected stretch of the timeline (null for none) to <paramref name="source"/>. With a <paramref name="setter"/>, editing writes it back.</summary>
    public static T BindSelection<T, TSource>(this T editor, TSource source, Func<TSource, TimelineRange?> getter, Action<TSource, TimelineRange?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : AudioWaveformEditor where TSource : class =>
        editor.BindToSource(AudioWaveformEditor.SelectionProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the selected stretch of the timeline (null for none) to the DataContext. With a <paramref name="setter"/>, editing writes it back.</summary>
    public static T BindSelection<T, TDataContext>(this T editor, Func<TDataContext, TimelineRange?> getter, Action<TDataContext, TimelineRange?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : AudioWaveformEditor where TDataContext : class =>
        editor.BindToDataContext(AudioWaveformEditor.SelectionProperty, getter, setter, updateSourceTrigger, getterExpression);
}
