using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Audio;

/// <summary>How a <see cref="WaveformView"/> shows several channels.</summary>
public enum WaveformChannelLayout
{
    /// <summary>One waveform per channel, stacked from top to bottom.</summary>
    Stacked,

    /// <summary>All channels in one waveform, the peaks of all of them together.</summary>
    Combined,
}

/// <summary>
/// A lightweight, read-only waveform: draws part of a sound (<see cref="Source"/>, from <see cref="SourceStart"/> for
/// <see cref="SourceLength"/> seconds) across its bounds, as min/max peaks, or as the samples themselves when zoomed in
/// far enough.
/// </summary>
/// <remarks>
/// <para>
/// On its own it stretches the part of the sound over its width. Inside a clip on a <see cref="TimelinePanel"/> (the
/// panel's child or inside it) it follows the timeline instead: the sound starts at the clip's start (plus the
/// waveform's own offset in the clip) and plays at the timeline's zoom, so a clip longer than the sound shows silence
/// after it and a shorter one cuts it off. Many views can share one <see cref="WaveformData"/>.
/// </para>
/// <para>
/// Drawing takes time proportional to the visible width, whatever the length of the sound: each pixel column reads the
/// peak pyramid, and only the columns inside the clip region are drawn. With fewer than one sample per pixel it draws a
/// line through the samples, with a dot on each from <see cref="SampleDotSpacing"/> pixels per sample.
/// </para>
/// <para>
/// The waveform is drawn in <see cref="Control.Foreground"/> over <see cref="Control.Background"/>, with a
/// <see cref="CenterLineColor"/> line at zero, scaled by <see cref="Gain"/>.
/// </para>
/// </remarks>
public class WaveformView : Control
{
    /// <summary>Identifies the <see cref="Source"/> property.</summary>
    public static readonly BindableProperty<WaveformData?> SourceProperty =
        BindableProperty.Register<WaveformView, WaveformData?>(nameof(Source), null, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="SourceStart"/> property.</summary>
    public static readonly BindableProperty<double> SourceStartProperty =
        BindableProperty.Register<WaveformView, double>(nameof(SourceStart), 0, options: PropertyOptions.AffectsRender, validateValue: double.IsFinite);

    /// <summary>Identifies the <see cref="SourceLength"/> property.</summary>
    public static readonly BindableProperty<double> SourceLengthProperty =
        BindableProperty.Register<WaveformView, double>(nameof(SourceLength), double.NaN, options: PropertyOptions.AffectsRender, validateValue: v => double.IsNaN(v) || v >= 0);

    /// <summary>Identifies the <see cref="ChannelLayout"/> property.</summary>
    public static readonly BindableProperty<WaveformChannelLayout> ChannelLayoutProperty =
        BindableProperty.Register<WaveformView, WaveformChannelLayout>(nameof(ChannelLayout), WaveformChannelLayout.Stacked, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="Gain"/> property.</summary>
    public static readonly BindableProperty<float> GainProperty =
        BindableProperty.Register<WaveformView, float>(nameof(Gain), 1f, options: PropertyOptions.AffectsRender, validateValue: v => float.IsFinite(v) && v >= 0);

    /// <summary>Identifies the <see cref="CenterLineColor"/> property.</summary>
    public static readonly BindableProperty<Color> CenterLineColorProperty =
        BindableProperty.Register<WaveformView, Color>(nameof(CenterLineColor), Color.Transparent, options: PropertyOptions.AffectsRender);

    /// <summary>The pixels per sample from which each sample gets a dot.</summary>
    public const float SampleDotSpacing = 6f;

    static WaveformView()
    {
        AudioTheme.Register();
    }

    /// <summary>Gets or sets the sound shown, or <c>null</c> for none.</summary>
    public WaveformData? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>Gets or sets where in the sound (seconds) the view starts, such as a clip's trimmed start. The default is 0.</summary>
    public double SourceStart { get => GetValue(SourceStartProperty); set => SetValue(SourceStartProperty, value); }

    /// <summary>Gets or sets how many seconds of the sound are shown; NaN (the default) shows the rest of it.</summary>
    public double SourceLength { get => GetValue(SourceLengthProperty); set => SetValue(SourceLengthProperty, value); }

    /// <summary>Gets or sets how several channels are shown. The default is <see cref="WaveformChannelLayout.Stacked"/>.</summary>
    public WaveformChannelLayout ChannelLayout { get => GetValue(ChannelLayoutProperty); set => SetValue(ChannelLayoutProperty, value); }

    /// <summary>Gets or sets the vertical scale: 1 (the default) draws full scale to the edges.</summary>
    public float Gain { get => GetValue(GainProperty); set => SetValue(GainProperty, value); }

    /// <summary>Gets or sets the color of the zero line of each channel; transparent (the default) for none.</summary>
    public Color CenterLineColor { get => GetValue(CenterLineColorProperty); set => SetValue(CenterLineColorProperty, value); }

    /// <summary>Gets the seconds of the sound shown: <see cref="SourceLength"/>, or the rest of the sound from <see cref="SourceStart"/>.</summary>
    public double ShownLength =>
        !double.IsNaN(SourceLength) ? SourceLength : Source is { } source ? Math.Max(0, source.Duration - SourceStart) : 0;

    /// <summary>
    /// Gets how the view maps its x to the sound: the time in the sound (seconds) at x = 0 and the pixels per second.
    /// Inside a clip on a <see cref="TimelinePanel"/> it follows the timeline; otherwise it stretches
    /// <see cref="ShownLength"/> over the width.
    /// </summary>
    public (double TimeAtZero, double PixelsPerSecond) GetMapping()
    {
        if (FindClip() is { } placement)
        {
            var (panel, clip, offset) = placement;
            double pixelsPerSecond = panel.CurrentTimeline.PixelsPerSecond;
            // The sound starts at the clip's start on the timeline plus the view's offset in the clip, even where the
            // panel cut off the clip's left part; x = 0 is wherever the view was arranged.
            double soundStartTime = panel.GetStartSeconds(clip) + offset / pixelsPerSecond;
            double timeAtX0 = panel.XToTime(clip.Bounds.X + offset);
            return (SourceStart + timeAtX0 - soundStartTime, pixelsPerSecond);
        }
        double length = ShownLength;
        return (SourceStart, length > 0 && Bounds.Width > 0 ? Bounds.Width / length : 0);
    }

    // The panel and the panel's child the view is in (itself or an ancestor), and the view's x in that child.
    private (TimelinePanel Panel, UIElement Clip, float Offset)? FindClip()
    {
        float offset = 0;
        UIElement current = this;
        for (var node = Parent; node != null; node = node.Parent)
        {
            if (node is TimelinePanel panel) return (panel, current, offset);
            offset += current.Bounds.X;
            if (node is not UIElement element) return null;
            current = element;
        }
        return null;
    }
}
