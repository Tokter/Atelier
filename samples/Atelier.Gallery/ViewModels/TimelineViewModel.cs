using System;
using System.Collections.Generic;
using System.Linq;
using Atelier.Audio;
using Atelier.Controls;
using Atelier.Core.Keybinding;
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

    public TimelineViewModel()
    {
        PageIcon = MaterialIconKind.Straighten;
        PageTitle = "Timeline";
        CommandGroup = Group;
        Keywords = "timeline ruler beat bar tempo bpm time signature audio daw track lane zoom samples";
        Song.ViewChanged += (_, _) => OnPropertyChanged(nameof(ViewText));
        UpdateTempoMap();
    }

    /// <summary>Gets the context the page's timeline controls share.</summary>
    public TimelineContext Song { get; } = new() { PixelsPerSecond = 40 };

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
        Song.PixelsPerSecond = 40;
        Song.Start = 0;
    }
}
