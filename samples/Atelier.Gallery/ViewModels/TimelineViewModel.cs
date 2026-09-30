using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Atelier.Audio;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using Atelier.Core.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

/// <summary>
/// The timeline page: a 64-bar song whose ruler and lanes share one <see cref="TimelineContext"/>, with its tempo,
/// time signature and the ruler's units to play with.
/// </summary>
public partial class TimelineViewModel : PageViewModel
{
    /// <summary>The keybinding group of this page's commands.</summary>
    public const string Group = "TimelinePage";

    /// <summary>The song's length in bars.</summary>
    public const int Bars = 64;

    /// <summary>The time signatures to choose from.</summary>
    public static readonly TimeSignature[] Signatures = [new(4, 4), new(3, 4), new(5, 4), new(6, 8), new(7, 8)];

    [ObservableProperty]
    private TimelineRulerMode _mode = TimelineRulerMode.Beats;

    [ObservableProperty]
    private TimelineRulerMode? _secondaryMode = TimelineRulerMode.Time;

    [ObservableProperty]
    private float _bpm = 120;

    [ObservableProperty]
    private int _signatureIndex;

    [ObservableProperty]
    private bool _tempoChanges;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditText))]
    private double _trimStart;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditText))]
    private double _trimEnd = double.NaN;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditText))]
    private double _fadeIn = 0.1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditText))]
    private double _fadeOut = 1.5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditText))]
    private float _gain;

    /// <summary>Gets the context of the editing demo: its own view of a short song, so it zooms on its own.</summary>
    public TimelineContext EditorSong { get; } = new() { PixelsPerSecond = 80, Duration = 12 };

    /// <summary>Gets the edits of the editing demo as text, as the editor writes them to the view model.</summary>
    public string EditText =>
        $"Trim {TimelineFormat.Time(TrimStart)} to {(double.IsNaN(TrimEnd) ? "end" : TimelineFormat.Time(TrimEnd))}, " +
        $"fades {FadeIn:0.00} s / {FadeOut:0.00} s, gain {Gain:+0.0;-0.0;0} dB";

    public TimelineViewModel()
    {
        PageIcon = MaterialIconKind.Straighten;
        PageTitle = "Timeline";
        CommandGroup = Group;
        Keywords = "timeline ruler beat bar tempo bpm time signature audio daw track lane zoom samples";
        Song.ViewChanged += (_, _) => OnPropertyChanged(nameof(ViewText));
        Song.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(TimelineContext.PlayStart) or nameof(TimelineContext.Loop) or nameof(TimelineContext.IsLoopEnabled))
            {
                OnPropertyChanged(nameof(TransportText));
                OnPropertyChanged(nameof(LoopEnabled));
            }
        };
        Song.Markers.Changed += (_, _) => OnPropertyChanged(nameof(MarkersText));
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(15), (_, _) => Advance());
        UpdateTempoMap();
        AddMarkers();
        LoopChorus();
    }

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private double _position;

    /// <summary>Gets the marker only the Bass lane shows: pinned to 0:20, so it stays there when the tempo changes.</summary>
    public TimelineMarker BassDrop { get; } = new(TimelinePosition.Seconds(20), "Drop", TimelineMarker.Palette[7].Color);

    /// <summary>Gets whether the song is playing.</summary>
    public bool IsPlaying => _timer.IsEnabled;

    /// <summary>Gets or sets whether the loop is on.</summary>
    public bool LoopEnabled { get => Song.IsLoopEnabled; set => Song.IsLoopEnabled = value; }

    /// <summary>Gets the play start and loop as text.</summary>
    public string TransportText
    {
        get
        {
            var map = Song.TempoMap;
            string loop = Song.Loop.IsEmpty(map) ? "none" : $"{Describe(Song.Loop.Start)} to {Describe(Song.Loop.End)}{(Song.IsLoopEnabled ? "" : " (off)")}";
            return $"Play start = {Describe(Song.PlayStart)}; loop = {loop}";
        }
    }

    /// <summary>Gets the shared markers as text.</summary>
    public string MarkersText => Song.Markers.Count == 0 ? "No markers" : string.Join(", ", Song.Markers.Select(m => $"{m.Label} at {Describe(m.Position)}"));

    // "5.2 (beats)" or "0:12.500 (seconds)": where a position is, and what it's pinned to.
    private string Describe(TimelinePosition position)
    {
        var map = Song.TempoMap;
        if (position.Unit == TimelineUnit.Seconds) return $"{TimelineFormat.Time(position.Value)} (s)";
        var bar = map.GetBarPosition(position.Value);
        return $"{bar.Bar}.{bar.Beat}{(bar.Fraction > 0 ? $"+{bar.Fraction:0.##}" : "")} (beats)";
    }

    // Loops the chorus, bars 17 to 24.
    private void LoopChorus()
    {
        var map = Song.TempoMap;
        Song.Loop = new TimelineRange(TimelinePosition.Bar(map, 17), TimelinePosition.Bar(map, 25));
        Song.IsLoopEnabled = true;
    }

    // The song's sections as shared markers, pinned to the music.
    private void AddMarkers()
    {
        var map = Song.TempoMap;
        Song.Markers.Clear();
        Song.Markers.Add(new TimelineMarker(TimelinePosition.Beats(map.BarToBeats(1)), "Intro", TimelineMarker.Palette[5].Color));
        Song.Markers.Add(new TimelineMarker(TimelinePosition.Beats(map.BarToBeats(9)), "Verse", TimelineMarker.Palette[3].Color));
        Song.Markers.Add(new TimelineMarker(TimelinePosition.Beats(map.BarToBeats(17)), "Chorus", TimelineMarker.Palette[1].Color));
        Song.Markers.Add(new TimelineMarker(TimelinePosition.Beats(map.BarToBeats(33)), "Bridge", TimelineMarker.Palette[6].Color));
        Song.Markers.Add(new TimelineMarker(TimelinePosition.Beats(map.BarToBeats(49)), "Outro", TimelineMarker.Palette[0].Color));
    }

    [RelayCommand]
    [property: Command("PlayStop", Group, Label = "Play / stop", Icon = MaterialIcons.PlayArrow, Description = "Play from the play start marker, or stop")]
    private void PlayStop()
    {
        if (IsPlaying)
        {
            _timer.Stop();
            _clock.Reset();
            Song.Playhead = null;
        }
        else
        {
            _position = Song.PlayStart.ToSeconds(Song.TempoMap);
            Song.Playhead = _position;
            _clock.Restart();
            _timer.Start();
        }
        OnPropertyChanged(nameof(IsPlaying));
    }

    // Moves the playhead by the time passed, jumping back at the loop's end while the loop is on.
    private void Advance()
    {
        double elapsed = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        var map = Song.TempoMap;
        double previous = _position;
        _position += elapsed;
        if (Song.IsLoopEnabled && !Song.Loop.IsEmpty(map))
        {
            double start = Song.Loop.GetStartSeconds(map);
            double end = Song.Loop.GetEndSeconds(map);
            if (previous < end && _position >= end) _position = start + (_position - end) % (end - start);
        }
        if (_position >= Song.Duration)
        {
            PlayStop();
            return;
        }
        Song.Playhead = _position;
    }

    /// <summary>Gets the context the page's timeline controls share.</summary>
    public TimelineContext Song { get; } = new() { PixelsPerSecond = 16 };

    /// <summary>Gets the view's start and zoom as text.</summary>
    public string ViewText => $"Start = {TimelineFormat.Time(Song.Start)}, {Song.PixelsPerSecond:0.##} px/s";

    /// <summary>Gets the tempo map as text.</summary>
    public string TempoText
    {
        get
        {
            var map = Song.TempoMap;
            return $"{Bars} bars, {TimelineFormat.Time(Song.Duration, 1)}; "
                + string.Join(", ", map.Tempos.Select(t => $"{t.Bpm:0} BPM from bar {map.GetBarPosition(t.Beat).Bar}")) + "; "
                + string.Join(", ", map.Meters.Select(m => $"{m.Signature} from bar {m.Bar}"));
        }
    }

    partial void OnBpmChanged(float value) => UpdateTempoMap();

    partial void OnSignatureIndexChanged(int value) => UpdateTempoMap();

    partial void OnTempoChangesChanged(bool value) => UpdateTempoMap();

    // With tempo changes, bars 9 to 12 are in 7/8 and the tempo rises by a quarter from bar 17.
    private void UpdateTempoMap()
    {
        var signature = Signatures[Math.Clamp(SignatureIndex, 0, Signatures.Length - 1)];
        var meters = new List<MeterChange> { new(1, signature) };
        var tempos = new List<TempoChange> { new(0, Bpm) };
        if (TempoChanges)
        {
            meters.Add(new MeterChange(9, new TimeSignature(7, 8)));
            meters.Add(new MeterChange(13, signature));
            tempos.Add(new TempoChange(new TempoMap(tempos, meters).BarToBeats(17), Bpm * 1.25));
        }
        var map = new TempoMap(tempos, meters);
        Song.TempoMap = map;
        Song.Duration = map.BeatsToSeconds(map.BarToBeats(Bars + 1));
        OnPropertyChanged(nameof(TempoText));
        OnPropertyChanged(nameof(TransportText));
        OnPropertyChanged(nameof(MarkersText));
    }

    [RelayCommand]
    [property: Command("Reset", Group, Icon = MaterialIcons.RestartAlt, Description = "Put the timeline back as it was")]
    private void Reset()
    {
        Mode = TimelineRulerMode.Beats;
        SecondaryMode = TimelineRulerMode.Time;
        Bpm = 120;
        SignatureIndex = 0;
        TempoChanges = false;
        if (IsPlaying) PlayStop();
        Song.PixelsPerSecond = 16;
        Song.Start = 0;
        Song.PlayStart = TimelinePosition.Zero;


        BassDrop.Position = TimelinePosition.Seconds(20);
        TrimStart = 0;
        TrimEnd = double.NaN;
        FadeIn = 0.1;
        FadeOut = 1.5;
        Gain = 0;
        EditorSong.PixelsPerSecond = 80;
        EditorSong.Start = 0;
        AddMarkers();
        LoopChorus();
    }
}
