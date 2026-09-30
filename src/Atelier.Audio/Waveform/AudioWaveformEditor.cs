using System.Globalization;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Audio;

/// <summary>What part of an <see cref="AudioWaveformEditor"/> is at a point.</summary>
public enum AudioEditorPart
{
    /// <summary>Nothing of the sound.</summary>
    None,

    /// <summary>The sound: dragging selects a stretch of time.</summary>
    Body,

    /// <summary>The start edge of the audible region: dragging trims the start.</summary>
    TrimStart,

    /// <summary>The end edge of the audible region: dragging trims the end.</summary>
    TrimEnd,

    /// <summary>The fade in's handle, at the top where the fade in ends: dragging changes its length.</summary>
    FadeIn,

    /// <summary>The fade out's handle, at the top where the fade out begins: dragging changes its length.</summary>
    FadeOut,

    /// <summary>The gain chip at the top: dragging up or down changes the gain.</summary>
    Gain,
}

/// <summary>
/// A waveform editor for one sound on the timeline, like a clip in a DAW: trim its start and end, fade it in and out,
/// change its gain and select a stretch of it, all non-destructively (the edits are properties; the samples don't change).
/// </summary>
/// <remarks>
/// <para>
/// The editor is a <see cref="TimelineControl"/>: give it the context of a <see cref="TimelineRuler"/> and it zooms and
/// scrolls with it, draws the grid, the shared markers, the loop and the playhead, and runs the timeline's commands. The
/// sound's first sample plays at <see cref="StartTime"/>; only <see cref="TrimStart"/> to <see cref="TrimEnd"/> of it
/// is heard, and the parts trimmed away are drawn dimmed so they can be dragged back in.
/// </para>
/// <para>
/// Drag the audible region's edges to trim it, the square handles at its top corners to change <see cref="FadeIn"/> and
/// <see cref="FadeOut"/> (shaped by <see cref="FadeCurve"/>), and the dB chip at its top up or down to change
/// <see cref="Gain"/> (Shift for fine steps); double-click a fade handle or the chip to reset it. Drag across the sound
/// to set the <see cref="Selection"/>, double-click it to select the audible region, click to move the play start
/// marker, and right-click for a menu (trim to the selection, reset). Ctrl+A selects the audible region, Escape clears
/// the selection and Ctrl+T trims to it. Trims, fades and the selection snap to the grid in view (Shift inverts it).
/// These are commands of <see cref="TimelineCommands.EditorGroup"/> users can rebind.
/// </para>
/// <para>
/// The waveform is drawn scaled by the gain and the fades (<see cref="GetGain"/>): apply the same
/// <see cref="WaveformGain"/> when playing the sound so what's heard matches what's shown.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// new AudioWaveformEditor().Timeline(song).Source(vocals).StartTime(TimelinePosition.Bar(song.TempoMap, 5))
///     .BindTrimStart(vm, v => v.TrimStart, (v, t) => v.TrimStart = t);
/// </code>
/// </example>
public class AudioWaveformEditor : TimelineControl
{
    /// <summary>Identifies the <see cref="Source"/> property.</summary>
    public static readonly BindableProperty<WaveformData?> SourceProperty =
        BindableProperty.Register<AudioWaveformEditor, WaveformData?>(nameof(Source), null, (s, o, n) => ((AudioWaveformEditor)s).OnEdited(), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="StartTime"/> property.</summary>
    public static readonly BindableProperty<TimelinePosition> StartTimeProperty =
        BindableProperty.Register<AudioWaveformEditor, TimelinePosition>(nameof(StartTime), TimelinePosition.Zero, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="TrimStart"/> property.</summary>
    public static readonly BindableProperty<double> TrimStartProperty =
        BindableProperty.Register<AudioWaveformEditor, double>(nameof(TrimStart), 0, (s, o, n) => ((AudioWaveformEditor)s).OnEdited(),
            options: PropertyOptions.AffectsRender, validateValue: v => double.IsFinite(v) && v >= 0);

    /// <summary>Identifies the <see cref="TrimEnd"/> property.</summary>
    public static readonly BindableProperty<double> TrimEndProperty =
        BindableProperty.Register<AudioWaveformEditor, double>(nameof(TrimEnd), double.NaN, (s, o, n) => ((AudioWaveformEditor)s).OnEdited(),
            options: PropertyOptions.AffectsRender, validateValue: v => double.IsNaN(v) || v >= 0);

    /// <summary>Identifies the <see cref="FadeIn"/> property.</summary>
    public static readonly BindableProperty<double> FadeInProperty =
        BindableProperty.Register<AudioWaveformEditor, double>(nameof(FadeIn), 0, (s, o, n) => ((AudioWaveformEditor)s).OnEdited(),
            options: PropertyOptions.AffectsRender, validateValue: v => double.IsFinite(v) && v >= 0);

    /// <summary>Identifies the <see cref="FadeOut"/> property.</summary>
    public static readonly BindableProperty<double> FadeOutProperty =
        BindableProperty.Register<AudioWaveformEditor, double>(nameof(FadeOut), 0, (s, o, n) => ((AudioWaveformEditor)s).OnEdited(),
            options: PropertyOptions.AffectsRender, validateValue: v => double.IsFinite(v) && v >= 0);

    /// <summary>Identifies the <see cref="FadeCurve"/> property.</summary>
    public static readonly BindableProperty<FadeCurve> FadeCurveProperty =
        BindableProperty.Register<AudioWaveformEditor, FadeCurve>(nameof(FadeCurve), FadeCurve.Linear, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="Gain"/> property.</summary>
    public static readonly BindableProperty<float> GainProperty =
        BindableProperty.Register<AudioWaveformEditor, float>(nameof(Gain), 0f, (s, o, n) => ((AudioWaveformEditor)s)._gainText = null,
            coerceValue: (s, v) => Math.Clamp(v, MinGain, MaxGain), options: PropertyOptions.AffectsRender, validateValue: float.IsFinite);

    /// <summary>Identifies the <see cref="Selection"/> property.</summary>
    public static readonly BindableProperty<TimelineRange?> SelectionProperty =
        BindableProperty.Register<AudioWaveformEditor, TimelineRange?>(nameof(Selection), null, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="ChannelLayout"/> property.</summary>
    public static readonly BindableProperty<WaveformChannelLayout> ChannelLayoutProperty =
        BindableProperty.Register<AudioWaveformEditor, WaveformChannelLayout>(nameof(ChannelLayout), WaveformChannelLayout.Stacked, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="ShowRms"/> property.</summary>
    public static readonly BindableProperty<bool> ShowRmsProperty =
        BindableProperty.Register<AudioWaveformEditor, bool>(nameof(ShowRms), true, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="SelectionColor"/> property.</summary>
    public static readonly BindableProperty<Color> SelectionColorProperty =
        BindableProperty.Register<AudioWaveformEditor, Color>(nameof(SelectionColor), Color.FromRgb(0x42, 0x85, 0xF4), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="HandleColor"/> property.</summary>
    public static readonly BindableProperty<Color> HandleColorProperty =
        BindableProperty.Register<AudioWaveformEditor, Color>(nameof(HandleColor), Color.White, options: PropertyOptions.AffectsRender);

    /// <summary>The lowest <see cref="Gain"/>, in dB.</summary>
    public const float MinGain = -48f;

    /// <summary>The highest <see cref="Gain"/>, in dB.</summary>
    public const float MaxGain = 24f;

    /// <summary>How many dB dragging the gain chip one pixel changes (a tenth of it with Shift).</summary>
    public const float GainPerPixel = 0.25f;

    /// <summary>The shortest audible region trimming leaves, in seconds.</summary>
    public const double MinLength = 0.001;

    /// <summary>The size of the fade handles, in pixels.</summary>
    public const float HandleSize = 8f;

    /// <summary>How far from an edge of the audible region (in pixels) the pointer grabs it.</summary>
    public const float EdgeGrip = 5f;

    /// <summary>The height of the strip at the top with the fade handles and the gain chip, in pixels.</summary>
    public const float HandleStripHeight = 16f;

    /// <summary>The height the editor wants without a height set, in pixels.</summary>
    public const float DefaultHeight = 120f;

    private string? _gainText;
    private AudioEditorPart _highlightedPart;

    /// <summary>Initializes an editor, running the <see cref="TimelineCommands.EditorGroup"/> commands on itself as well.</summary>
    public AudioWaveformEditor()
    {
        AdditionalScopes.Add(new KeybindingScope(TimelineCommands.EditorGroup, this));
    }

    /// <summary>Gets or sets the sound edited, or <c>null</c> for none.</summary>
    public WaveformData? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>Gets or sets where on the timeline the sound's first sample plays (trimming doesn't move it). The default is the start.</summary>
    public TimelinePosition StartTime { get => GetValue(StartTimeProperty); set => SetValue(StartTimeProperty, value); }

    /// <summary>Gets or sets where in the sound (seconds) the audible region starts. The default is 0.</summary>
    public double TrimStart { get => GetValue(TrimStartProperty); set => SetValue(TrimStartProperty, value); }

    /// <summary>Gets or sets where in the sound (seconds) the audible region ends; NaN (the default) is the end of the sound.</summary>
    public double TrimEnd { get => GetValue(TrimEndProperty); set => SetValue(TrimEndProperty, value); }

    /// <summary>Gets or sets the length of the fade in at the start of the audible region, in seconds. The default is 0.</summary>
    public double FadeIn { get => GetValue(FadeInProperty); set => SetValue(FadeInProperty, value); }

    /// <summary>Gets or sets the length of the fade out at the end of the audible region, in seconds. The default is 0.</summary>
    public double FadeOut { get => GetValue(FadeOutProperty); set => SetValue(FadeOutProperty, value); }

    /// <summary>Gets or sets the shape of the fades. The default is <see cref="Audio.FadeCurve.Linear"/>.</summary>
    public FadeCurve FadeCurve { get => GetValue(FadeCurveProperty); set => SetValue(FadeCurveProperty, value); }

    /// <summary>Gets or sets the gain in dB, kept between <see cref="MinGain"/> and <see cref="MaxGain"/>. The default is 0.</summary>
    public float Gain { get => GetValue(GainProperty); set => SetValue(GainProperty, value); }

    /// <summary>Gets or sets the selected stretch of the timeline, or <c>null</c> (the default) for none.</summary>
    public TimelineRange? Selection { get => GetValue(SelectionProperty); set => SetValue(SelectionProperty, value); }

    /// <summary>Gets or sets how several channels are shown. The default is <see cref="WaveformChannelLayout.Stacked"/>.</summary>
    public WaveformChannelLayout ChannelLayout { get => GetValue(ChannelLayoutProperty); set => SetValue(ChannelLayoutProperty, value); }

    /// <summary>Gets or sets whether the zoomed-out waveform shows the RMS inside its peaks. The default is <c>true</c>.</summary>
    public bool ShowRms { get => GetValue(ShowRmsProperty); set => SetValue(ShowRmsProperty, value); }

    /// <summary>Gets or sets the color of the selection.</summary>
    public Color SelectionColor { get => GetValue(SelectionColorProperty); set => SetValue(SelectionColorProperty, value); }

    /// <summary>Gets or sets the color of the trim edges, the fade curves and handles, and the gain chip.</summary>
    public Color HandleColor { get => GetValue(HandleColorProperty); set => SetValue(HandleColorProperty, value); }

    /// <summary>Gets <see cref="Gain"/> as text, like <c>"+1.5 dB"</c>, cached so renderers can read it every frame.</summary>
    public string GainText => _gainText ??= Gain == 0 ? "0 dB" : Gain.ToString("+0.0 dB;−0.0 dB", CultureInfo.InvariantCulture);

    /// <summary>Gets the part of the editor drawn highlighted: the handle under the pointer, which a drag keeps while it lasts.</summary>
    public AudioEditorPart HighlightedPart
    {
        get => _highlightedPart;
        private set
        {
            if (_highlightedPart == value) return;
            _highlightedPart = value;
            InvalidateVisual();
        }
    }

    /// <summary>Gets the length of the sound in seconds (0 without one).</summary>
    public double SourceDuration => Source?.Duration ?? 0;

    /// <summary>Gets where the audible region ends in the sound: <see cref="TrimEnd"/>, or the sound's end, and not before <see cref="TrimStart"/>.</summary>
    public double EffectiveTrimEnd => Math.Max(TrimStart, double.IsNaN(TrimEnd) ? SourceDuration : Math.Min(TrimEnd, SourceDuration));

    /// <summary>Gets where the sound's first sample plays on the timeline, in seconds.</summary>
    public double SoundStartSeconds => StartTime.ToSeconds(CurrentTimeline.TempoMap);

    /// <summary>Gets where the audible region starts on the timeline, in seconds.</summary>
    public double AudibleStart => SoundStartSeconds + TrimStart;

    /// <summary>Gets where the audible region ends on the timeline, in seconds.</summary>
    public double AudibleEnd => SoundStartSeconds + EffectiveTrimEnd;

    /// <summary>Gets the level of the sound over time (seconds into the sound): the gain, and the fades over the audible region.</summary>
    public WaveformGain GetGain()
    {
        double start = TrimStart, end = EffectiveTrimEnd;
        return new WaveformGain(WaveformGain.FromDecibels(Gain), start, start + FadeIn, end - FadeOut, end, FadeCurve);
    }

    /// <summary>Gets the selection as seconds into the sound, clipped to it, or <c>null</c> without a selection in it.</summary>
    public (double Start, double End)? GetSelectionInSource()
    {
        if (Selection is not { } selection) return null;
        var map = CurrentTimeline.TempoMap;
        double offset = SoundStartSeconds;
        double start = Math.Max(0, selection.GetStartSeconds(map) - offset);
        double end = Math.Min(SourceDuration, selection.GetEndSeconds(map) - offset);
        return end > start ? (start, end) : null;
    }

    /// <summary>Gets the bounds of a fade handle (<see cref="AudioEditorPart.FadeIn"/> or <see cref="AudioEditorPart.FadeOut"/>).</summary>
    public Rect GetFadeHandleBounds(AudioEditorPart fade)
    {
        double time = fade == AudioEditorPart.FadeIn ? AudibleStart + FadeIn : AudibleEnd - FadeOut;
        float x = TimeToX(time);
        return new Rect(x - HandleSize / 2, (HandleStripHeight - HandleSize) / 2, HandleSize, HandleSize);
    }

    /// <summary>Gets the bounds of the gain chip: at the top, in the middle of the visible part of the audible region.</summary>
    public Rect GetGainChipBounds()
    {
        float left = Math.Max(0, TimeToX(AudibleStart));
        float right = Math.Min(Bounds.Width, TimeToX(AudibleEnd));
        float width = 52;
        return new Rect((left + right) / 2 - width / 2, 1, width, HandleStripHeight - 2);
    }

    /// <summary>Gets the part of the editor at <paramref name="point"/>: fade handles and the gain chip first, then the trim edges, then the sound.</summary>
    public AudioEditorPart PartAt(Point point)
    {
        if (Source == null || point.Y < 0 || point.Y > Bounds.Height) return AudioEditorPart.None;
        float start = TimeToX(AudibleStart);
        float end = TimeToX(AudibleEnd);
        if (point.Y <= HandleStripHeight)
        {
            if (Grows(GetFadeHandleBounds(AudioEditorPart.FadeIn), 3).Contains(point)) return AudioEditorPart.FadeIn;
            if (Grows(GetFadeHandleBounds(AudioEditorPart.FadeOut), 3).Contains(point)) return AudioEditorPart.FadeOut;
            if (end - start > 60 && GetGainChipBounds().Contains(point)) return AudioEditorPart.Gain;
        }
        float toStart = Math.Abs(point.X - start), toEnd = Math.Abs(point.X - end);
        if (toStart <= EdgeGrip || toEnd <= EdgeGrip) return toStart < toEnd ? AudioEditorPart.TrimStart : AudioEditorPart.TrimEnd;
        float soundLeft = TimeToX(SoundStartSeconds), soundRight = TimeToX(SoundStartSeconds + SourceDuration);
        return point.X >= soundLeft && point.X <= soundRight ? AudioEditorPart.Body : AudioEditorPart.None;

        static Rect Grows(Rect rect, float by) => new(rect.X - by, rect.Y - by, rect.Width + 2 * by, rect.Height + 2 * by);
    }

    /// <summary>Gets the cursor that shows what dragging at <paramref name="point"/> does.</summary>
    public CursorType CursorAt(Point point) => PartAt(point) switch
    {
        AudioEditorPart.TrimStart or AudioEditorPart.TrimEnd or AudioEditorPart.FadeIn or AudioEditorPart.FadeOut => CursorType.SizeWestEast,
        AudioEditorPart.Gain => CursorType.SizeNorthSouth,
        AudioEditorPart.Body => CursorType.IBeam,
        _ => CursorType.Default,
    };

    /// <summary>Selects the audible region.</summary>
    public void SelectAll()
    {
        if (Source == null) return;
        var timeline = CurrentTimeline;
        Selection = new TimelineRange(timeline.CreatePosition(AudibleStart, PositionUnit), timeline.CreatePosition(AudibleEnd, PositionUnit));
    }

    /// <summary>Clears the selection.</summary>
    public void ClearSelection() => Selection = null;

    /// <summary>Trims the sound to the selection (within the sound) and clears the selection; returns whether it did.</summary>
    public bool TrimToSelection()
    {
        if (GetSelectionInSource() is not var (start, end)) return false;
        TrimStart = start;
        TrimEnd = end;
        Selection = null;
        return true;
    }

    /// <summary>Removes both fades.</summary>
    public void ResetFades()
    {
        FadeIn = 0;
        FadeOut = 0;
    }

    /// <summary>Sets the trim start (seconds into the sound), kept within the sound and before the end; fades shorten to fit.</summary>
    public void SetTrimStart(double seconds)
    {
        TrimStart = Math.Clamp(seconds, 0, Math.Max(0, EffectiveTrimEnd - MinLength));
        FitFades();
    }

    /// <summary>Sets the trim end (seconds into the sound), kept within the sound and after the start; fades shorten to fit.</summary>
    public void SetTrimEnd(double seconds)
    {
        TrimEnd = Math.Clamp(seconds, Math.Min(SourceDuration, TrimStart + MinLength), SourceDuration);
        FitFades();
    }

    /// <summary>Sets the fade in's length (seconds), kept within what the audible region and the fade out leave.</summary>
    public void SetFadeIn(double seconds) => FadeIn = Math.Clamp(seconds, 0, Math.Max(0, EffectiveTrimEnd - TrimStart - FadeOut));

    /// <summary>Sets the fade out's length (seconds), kept within what the audible region and the fade in leave.</summary>
    public void SetFadeOut(double seconds) => FadeOut = Math.Clamp(seconds, 0, Math.Max(0, EffectiveTrimEnd - TrimStart - FadeIn));

    /// <summary>Opens the editor's menu (see <see cref="CreateContextMenu"/>); returns it, or <c>null</c> when it couldn't open.</summary>
    public ContextMenu? ShowContextMenu()
    {
        var menu = CreateContextMenu();
        return menu.Open(this) ? menu : null;
    }

    /// <summary>
    /// Creates the editor's menu: "Select all", "Trim to selection", "Clear selection", the fade curve, "Reset fades" and
    /// "Reset gain". Override it to change the menu.
    /// </summary>
    public virtual ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem(null, TimelineCommands.SelectAllSound) { CommandParameter = this });
        menu.Items.Add(new MenuItem(null, TimelineCommands.TrimToSelection) { CommandParameter = this });
        menu.Items.Add(new MenuItem(null, TimelineCommands.ClearSelection) { CommandParameter = this });
        menu.Items.Add(new Separator());
        foreach (var curve in Enum.GetValues<FadeCurve>())
        {
            var item = new MenuItem($"{curve} fades") { IsCheckable = true, GroupName = "AudioEditorFadeCurve", IsChecked = FadeCurve == curve };
            item.Click += (_, _) => FadeCurve = curve;
            menu.Items.Add(item);
        }
        var resetFades = new MenuItem("Reset fades") { IsEnabled = FadeIn > 0 || FadeOut > 0 };
        resetFades.Click += (_, _) => ResetFades();
        menu.Items.Add(resetFades);
        var resetGain = new MenuItem("Reset gain") { IsEnabled = Gain != 0 };
        resetGain.Click += (_, _) => Gain = 0;
        menu.Items.Add(resetGain);
        return menu;
    }

    /// <inheritdoc/>
    /// <remarks>Shows the cursor for what's under the pointer and highlights the handle there; a drag keeps both.</remarks>
    public override void OnPreviewPointerMoved(PointerEventArgs e)
    {
        base.OnPreviewPointerMoved(e);
        if (IsDragging) return;
        Cursor = CursorAt(e.Position);
        HighlightedPart = HandleAt(e.Position);
    }

    /// <inheritdoc/>
    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsDragging) HighlightedPart = AudioEditorPart.None;
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!IsDragging) HighlightedPart = HandleAt(e.Position);
    }

    /// <inheritdoc/>
    /// <remarks><see cref="DefaultHeight"/> high unless given less; as wide as it's given.</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        return new Size(0, Math.Min(DefaultHeight, availableSize.Height));
    }

    /// <summary>Sets <see cref="HighlightedPart"/> for a drag of <paramref name="part"/> (drag commands call it).</summary>
    internal void HighlightForDrag(AudioEditorPart part) => HighlightedPart = part;

    // The handles highlight under the pointer; the rest of the sound doesn't.
    private AudioEditorPart HandleAt(Point point) => PartAt(point) is var part && part != AudioEditorPart.Body ? part : AudioEditorPart.None;

    private void FitFades()
    {
        double length = EffectiveTrimEnd - TrimStart;
        if (FadeIn + FadeOut <= length) return;
        double scale = length / (FadeIn + FadeOut);
        FadeIn *= scale;
        FadeOut *= scale;
    }

    private void OnEdited() => InvalidateVisual();
}
