using System;
using System.Linq;
using Atelier.Audio;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class TimelineView : GalleryPage
{
    // Each track: its sound, color and the bars its clips start at.
    private static readonly (string Name, WaveformData Sound, int Color, int[] Bars)[] Tracks =
    [
        ("Drums", TimelineSounds.Drums, 1, [.. Enumerable.Range(1, 16), .. Enumerable.Range(33, 16)]),
        ("Bass", TimelineSounds.Bass, 5, [9, 13, 17, 21, 25, 29, 49, 53]),
        ("Keys", TimelineSounds.Pad, 6, [17, 25, 33, 41]),
    ];

    private readonly TimelineViewModel _vm;
    private readonly TimelineRuler _ruler;

    public TimelineView(TimelineViewModel viewModel)
        : base(MaterialIconKind.Straighten, "Timeline",
            "A beat and time ruler and track lanes (Atelier.Audio) that share one TimelineContext: zooming or scrolling " +
            "any of them moves them all. Ticks and labels adapt to the zoom, from bar groups down to sixteenths, or " +
            "from minutes down to milliseconds and single samples.")
    {
        _vm = viewModel;
        _ruler = new TimelineRuler()
            .Timeline(_vm.Song)
            .BindMode(_vm, v => v.Mode, (v, m) => v.Mode = m)
            .BindSecondaryMode(_vm, v => v.SecondaryMode, (v, m) => v.SecondaryMode = m);

        Settings(new Button().Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));
        Sections(TracksSection(), TransportSection(), SettingsSection(), AnywhereSection());
    }

    private UIElement TracksSection()
    {
        var tracks = new Grid().Columns("112,*").Rows("Auto," + string.Join(",", Tracks.Select(_ => "56")));
        tracks.Children(_ruler.Column(1));
        for (int i = 0; i < Tracks.Length; i++)
        {
            tracks.Children(new Border()
                .Row(i + 1).Padding(12, 0).Margin(0, 1, 0, 0)
                .Themed(Border.BackgroundProperty, c => c.SurfaceContainerHigh)
                .Child(new TextBlock(Tracks[i].Name).LabelLarge().VerticalAlignment(VerticalAlignment.Center)));
            var (name, sound, colorIndex, bars) = Tracks[i];
            var color = TimelineMarker.Palette[colorIndex].Color;
            var clips = new TimelinePanel().Children(bars.Select(bar => Clip(name, sound, TimelinePosition.Bar(_vm.Song.TempoMap, bar), color)).ToArray());
            var lane = new TimelineLane { Content = clips }.Timeline(_vm.Song).Row(i + 1).Column(1).Margin(0, 1, 0, 0).BindMode(_vm, v => v.Mode);
            if (name == "Bass") lane.Markers(_vm.BassDrop);
            tracks.Children(lane);
        }

        return Ui.Section("Ruler and lanes",
            "Like Bitwig's ruler: drag it up or down to zoom around the point you grabbed and sideways to scroll, turn " +
            "the wheel over it to zoom, double-click it to see the whole song, and right-click it to choose what it " +
            "counts in. Over the lanes, Ctrl+Alt+wheel zooms, Shift+wheel and middle drag scroll, and = and - zoom " +
            "once a lane has the focus. All of these are commands you can rebind. Each lane holds a TimelinePanel of clips: a " +
            "WaveformView in a box, placed on a bar and as long as its sound. The drum clips all share one WaveformData, " +
            "and zooming in far enough shows the single samples.",
            new Border().CornerRadius(8).ClipToBounds(true).Child(tracks),
            Ui.Row(
                new Button("Zoom to fit").Variant(ButtonVariant.Outlined).OnClick(() => _ruler.ZoomToFit()),
                Ui.Readout(_vm, v => v.ViewText)),
            Ui.Code("var song = new TimelineContext { TempoMap = TempoMap.Constant(120) };\n" +
                    "new TimelineRuler().Timeline(song).SecondaryMode(TimelineRulerMode.Time);\n" +
                    "new TimelineLane { Content = new TimelinePanel().Children(\n" +
                    "    new Border().TimelineRange(TimelinePosition.Bar(song.TempoMap, 9), TimelinePosition.Seconds(drums.Duration))\n" +
                    "        .Child(new WaveformView().Source(drums))) }.Timeline(song);   // zooms and scrolls with the ruler"));
    }

    // A clip: a tinted box with the sound's name over its waveform, starting on a bar and as long as the sound.
    private static Border Clip(string name, WaveformData sound, TimelinePosition start, Color color) =>
        new Border()
            .CornerRadius(4)
            .Margin(0, 2)
            .ClipToBounds(true)
            .Background(color.WithAlpha(0.18f))
            .BorderBrush(color.WithAlpha(0.6f))
            .BorderThickness(1)
            .TimelineRange(start, TimelinePosition.Seconds(sound.Duration))
            .Child(new Grid().Rows("13,*").Children(
                new TextBlock(name).FontSize(10).Margin(4, 0, 0, 0).Foreground(color),
                new WaveformView().Source(sound).Foreground(color).Row(1)));

    private UIElement TransportSection() => Ui.Section("Markers, loop and playback",
        "Click the ruler to move the play start marker (the triangle); clicks and drags snap to the finest ticks in view, " +
        "and Shift turns snapping off for the moment (/ turns it off for good). Markers shared by the whole song show as " +
        "flags and as lines across the lanes: drag a flag to move it, click it to start there, double-click the marker " +
        "lane to add one or a flag to rename it, and right-click a flag to recolor or delete it. The Bass lane has a " +
        "marker of its own, pinned to 0:20 in seconds, so it stays put when the tempo changes while the others move " +
        "with the bars. Drag on the loop bar below the labels to draw a loop, drag its edges or middle to change it, and " +
        "double-click it (or press L) to turn it on or off.",
        Ui.Row(
            new Button().Variant(ButtonVariant.Filled).Command(_vm.PlayStopCommand),
            new Switch("Loop").BindIsChecked(_vm, v => v.LoopEnabled, (v, on) => v.LoopEnabled = on)),
        Ui.Readout(_vm, v => v.TransportText),
        Ui.Readout(_vm, v => v.MarkersText),
        Ui.Code("song.Markers.Add(new TimelineMarker(TimelinePosition.Bar(song.TempoMap, 9), \"Verse\", color));   // on every control\n" +
                "bassLane.Markers.Add(new TimelineMarker(TimelinePosition.Seconds(20), \"Drop\"));                  // on this lane only\n" +
                "song.Loop = new TimelineRange(TimelinePosition.Bar(song.TempoMap, 17), TimelinePosition.Bar(song.TempoMap, 25));\n" +
                "song.Playhead = position;   // while your app plays"));

    private UIElement SettingsSection()
    {
        var modes = Enum.GetValues<TimelineRulerMode>();
        return Ui.Section("Units, tempo and meter",
            "The main row counts in bars and beats, time or samples, with an optional second row in another unit. The " +
            "TempoMap can change tempo and time signature along the song; beats follow it, and time and samples " +
            "don't.",
            Ui.Row(
                Ui.Labeled("Main row", new ComboBox().Items(modes.Select(m => m.ToString()).ToArray()).MinWidth(140)
                    .BindSelectedIndex(_vm, v => (int)v.Mode, (v, i) => v.Mode = modes[Math.Max(0, i)])),
                Ui.Labeled("Second row", new ComboBox().Items(new[] { "None" }.Concat(modes.Select(m => m.ToString())).ToArray()).MinWidth(140)
                    .BindSelectedIndex(_vm, v => v.SecondaryMode is { } m ? (int)m + 1 : 0,
                        (v, i) => v.SecondaryMode = i <= 0 ? null : modes[i - 1])),
                Ui.Labeled("Time signature", new ComboBox().Items(TimelineViewModel.Signatures.Select(s => s.ToString()).ToArray()).MinWidth(120)
                    .BindSelectedIndex(_vm, v => v.SignatureIndex, (v, i) => v.SignatureIndex = Math.Max(0, i))),
                Ui.SliderSetting("Tempo", _vm, v => v.Bpm, (v, bpm) => v.Bpm = bpm, 40, 240, "0 BPM", 1),
                new Switch("7/8 bars and a faster tempo").BindIsChecked(_vm, v => v.TempoChanges, (v, on) => v.TempoChanges = on)),
            Ui.Readout(_vm, v => v.TempoText),
            Ui.Code("new TempoMap(\n" +
                    "    [new TempoChange(0, 120), new TempoChange(64, 150)],          // faster from bar 17\n" +
                    "    [new MeterChange(9, new TimeSignature(7, 8)), new MeterChange(13, TimeSignature.Common)]);"));
    }

    private UIElement AnywhereSection() => Ui.Section("Anywhere, at any width",
        "Connected controls don't need to line up: each shows the shared start time at its left edge, at the shared " +
        "zoom. These rulers use the same context as the tracks above, in other units.",
        Ui.Columns(280,
            Ui.Demo("Time", new TimelineRuler().Timeline(_vm.Song).Mode(TimelineRulerMode.Time)),
            Ui.Demo("Samples at 48 kHz", new TimelineRuler().Timeline(_vm.Song).Mode(TimelineRulerMode.Samples))),
        Ui.Code("new TimelineRuler().Timeline(song).Mode(TimelineRulerMode.Samples)"));
}
