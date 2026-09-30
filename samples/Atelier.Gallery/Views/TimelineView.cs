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
    private static readonly string[] Tracks = ["Drums", "Bass", "Keys"];

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
        Sections(TracksSection(), SettingsSection(), AnywhereSection());
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
                .Child(new TextBlock(Tracks[i]).LabelLarge().VerticalAlignment(VerticalAlignment.Center)));
            tracks.Children(new TimelineLane().Timeline(_vm.Song).Row(i + 1).Column(1).Margin(0, 1, 0, 0).BindMode(_vm, v => v.Mode));
        }

        return Ui.Section("Ruler and lanes",
            "Like Bitwig's ruler: drag it up or down to zoom around the point you grabbed and sideways to scroll, turn " +
            "the wheel over it to zoom, double-click it to see the whole song, and right-click it to choose what it " +
            "counts in. Over the lanes, Ctrl+Alt+wheel zooms, Shift+wheel and middle drag scroll, and = and - zoom " +
            "once a lane has the focus. All of these are commands you can rebind.",
            new Border().CornerRadius(8).ClipToBounds(true).Child(tracks),
            Ui.Row(
                new Button("Zoom to fit").Variant(ButtonVariant.Outlined).OnClick(() => _ruler.ZoomToFit()),
                Ui.Readout(_vm, v => v.ViewText)),
            Ui.Code("var song = new TimelineContext { TempoMap = TempoMap.Constant(120) };\n" +
                    "new TimelineRuler().Timeline(song).SecondaryMode(TimelineRulerMode.Time);\n" +
                    "new TimelineLane().Timeline(song).Height(56);   // zooms and scrolls with the ruler"));
    }

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
