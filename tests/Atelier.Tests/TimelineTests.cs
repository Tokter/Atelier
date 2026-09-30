using System.ComponentModel;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Rendering;
using Atelier.Theming;
using Atelier.Theming.Material;
using Atelier.Audio;

namespace Atelier.Tests;

public class TempoMapTests
{
    private static readonly TimeSignature ThreeFour = new(3, 4);
    private static readonly TimeSignature SixEight = new(6, 8);

    [Fact]
    public void AConstantTempo_ConvertsBeatsAndSeconds_BothWays()
    {
        var map = TempoMap.Constant(120);
        Assert.Equal(0.5, map.BeatsToSeconds(1));
        Assert.Equal(16, map.BeatsToSeconds(32));
        Assert.Equal(32, map.SecondsToBeats(16));
        Assert.Equal(4, map.BarToBeats(2));
        Assert.Equal(TimeSignature.Common, map.SignatureAt(100));
    }

    [Fact]
    public void TempoChanges_ApplyFromTheirBeat()
    {
        var map = new TempoMap([new TempoChange(32, 140), new TempoChange(0, 120)]);
        Assert.Equal(16, map.BeatsToSeconds(32));
        Assert.Equal(19, map.BeatsToSeconds(39), 9);
        Assert.Equal(39, map.SecondsToBeats(19), 9);
        Assert.Equal(140, map.TempoAt(40));
        Assert.Equal(140, map.MaxTempo(0, 32));
        Assert.Equal(120, map.MaxTempo(0, 31));
    }

    [Fact]
    public void TheFirstTempo_AlsoAppliesBeforeItsBeat()
    {
        var map = new TempoMap([new TempoChange(8, 60)]);
        Assert.Equal(0, map.Tempos[0].Beat);
        Assert.Equal(8, map.BeatsToSeconds(8));
    }

    [Fact]
    public void MeterChanges_StartOnBarLines_AndBarsBeforeThemAreFourFour()
    {
        var map = new TempoMap([new TempoChange(0, 120)], [new MeterChange(3, ThreeFour), new MeterChange(5, SixEight)]);
        Assert.Equal(TimeSignature.Common, map.Meters[0].Signature);
        Assert.Equal(8, map.BarToBeats(3));
        Assert.Equal(14, map.BarToBeats(5));
        Assert.Equal(17, map.BarToBeats(6));
        Assert.Equal(15, map.BarToBeats(5, 3)); // eighth-note beats
        Assert.Equal(new BarPosition(5, 3, 0.5), map.GetBarPosition(15.25));
        Assert.Equal(new BarPosition(3, 1, 0), map.GetBarPosition(8));
        Assert.Equal(new BarPosition(4, 3, 0), map.GetBarPosition(13));
    }

    [Fact]
    public void InvalidTemposAndSignatures_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => new TempoMap([]));
        Assert.Throws<ArgumentException>(() => new TempoMap([new TempoChange(0, 0)]));
        Assert.Throws<ArgumentException>(() => new TempoMap([new TempoChange(0, 120), new TempoChange(0, 100)]));
        Assert.Throws<ArgumentException>(() => new TempoMap([new TempoChange(0, 120)], [new MeterChange(0, ThreeFour)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeSignature(4, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeSignature(0, 4));
        Assert.Equal("4/4", default(TimeSignature).ToString());
        Assert.Equal(TimeSignature.Common, default(TimeSignature));
        Assert.Equal(1.5, new TimeSignature(3, 8).BarLength);
    }
}

public class TimelinePositionTests
{
    [Fact]
    public void BeatAnchoredPositions_MoveWithTheTempo_AndTimeAnchoredOnesStay()
    {
        var beats = TimelinePosition.Beats(8);
        var seconds = TimelinePosition.Seconds(4);
        Assert.Equal(4, beats.ToSeconds(TempoMap.Constant(120)));
        Assert.Equal(8, beats.ToSeconds(TempoMap.Constant(60)));
        Assert.Equal(4, seconds.ToSeconds(TempoMap.Constant(60)));
        Assert.Equal(TimelinePosition.Beats(4), seconds.In(TimelineUnit.Beats, TempoMap.Constant(60)));
        Assert.Equal(TimelinePosition.Beats(16), TimelinePosition.Bar(TempoMap.Default, 5));
        Assert.Equal(TimelinePosition.Seconds(0), TimelinePosition.Zero);
    }

    [Fact]
    public void Lengths_InSecondsOrBeats_EndWhereTheirUnitSays()
    {
        var map = new TempoMap([new TempoChange(0, 120), new TempoChange(8, 60)]);
        var start = TimelinePosition.Beats(6); // 3 s
        Assert.Equal(5, start.GetEndSeconds(TimelinePosition.Seconds(2), map));
        Assert.Equal(6, start.GetEndSeconds(TimelinePosition.Beats(4), map)); // 2 beats at 120 (1 s), 2 at 60 (2 s)
    }
}

public class TimelineContextTests
{
    [Fact]
    public void Zooming_KeepsTheTimeUnderTheAnchor()
    {
        var context = new TimelineContext { PixelsPerSecond = 100, Start = 10 };
        Assert.Equal(13, context.XToTime(300));
        context.ZoomAt(2, 300);
        Assert.Equal(200, context.PixelsPerSecond);
        Assert.Equal(13, context.XToTime(300), 9);
        Assert.Equal(300, context.TimeToX(13), 9);
    }

    [Fact]
    public void TheView_StaysWithinTheSongAndTheZoomLimits()
    {
        var context = new TimelineContext { PixelsPerSecond = 100, Start = 1, Duration = 60 };
        context.ZoomAt(0.5, 400); // would scroll before 0
        Assert.Equal(0, context.Start);
        context.Start = 100;
        Assert.Equal(60, context.Start);
        context.PixelsPerSecond = 1e12;
        Assert.Equal(48000 * 32, context.PixelsPerSecond);
        context.SampleRate = 44100;
        Assert.Equal(44100 * 32, context.PixelsPerSecond);
        context.PixelsPerSecond = 0;
        Assert.Equal(context.MinPixelsPerSecond, context.PixelsPerSecond);
        context.Duration = 10;
        Assert.Equal(10, context.Start);
    }

    [Fact]
    public void ZoomToFit_ShowsTheWholeSong()
    {
        var context = new TimelineContext { Duration = 60, Start = 30 };
        context.ZoomToFit(600);
        Assert.Equal((0.0, 10.0), (context.Start, context.PixelsPerSecond));
    }

    [Fact]
    public void ChangingTheView_ReportsEachProperty_AndTheViewOnce()
    {
        var context = new TimelineContext { PixelsPerSecond = 100, Start = 10 };
        var properties = new List<string?>();
        int views = 0;
        ((INotifyPropertyChanged)context).PropertyChanged += (_, e) => properties.Add(e.PropertyName);
        context.ViewChanged += (_, _) => views++;
        context.ZoomAt(2, 100);
        Assert.Equal([nameof(TimelineContext.Start), nameof(TimelineContext.PixelsPerSecond)], properties);
        Assert.Equal(1, views);
        context.ScrollBy(0);
        Assert.Equal(1, views);
    }
}

public class TimelineGridTests
{
    private static List<TimelineTick> Labels(TimelineGrid grid) => grid.Ticks.Where(t => t.Level == TimelineGrid.LabelLevel).ToList();

    private static TimelineGrid Grid(TimelineRulerMode mode, TimelineContext context, float width = 1000, double? left = null)
    {
        var grid = new TimelineGrid { Mode = mode };
        grid.Update(context, left ?? context.Start, width);
        return grid;
    }

    [Fact]
    public void TimeLabels_ShowSeconds_WithMillisecondTicksBetween()
    {
        var grid = Grid(TimelineRulerMode.Time, new TimelineContext { PixelsPerSecond = 100 });
        var labels = Labels(grid);
        Assert.Equal(Enumerable.Range(0, 11).Select(s => $"00:{s:00}"), labels.Select(t => t.Label));
        Assert.Equal(100f, labels[1].X);
        Assert.Equal(101, grid.Ticks.Count); // every 100 ms
        Assert.All(grid.Ticks.Where(t => t.Level == TimelineGrid.MediumLevel), t => Assert.Equal(0.5, t.Time % 1, 9));
        Assert.All(labels, t => Assert.Equal(t.Time == 0, t.IsEmphasized)); // whole minutes
    }

    [Fact]
    public void TimeLabels_ShowMilliseconds_OnlyWhenZoomedIn()
    {
        var grid = Grid(TimelineRulerMode.Time, new TimelineContext { PixelsPerSecond = 100_000, Start = 1 });
        var labels = Labels(grid);
        Assert.Equal(["00:01.000", "00:01.001", "00:01.002"], labels.Take(3).Select(t => t.Label));
        Assert.True(labels[0].IsEmphasized); // a whole second
        Assert.False(labels[1].IsEmphasized);
    }

    [Fact]
    public void TimeLabels_ShowMinutes_AndHours_WhenZoomedOut()
    {
        var minutes = Labels(Grid(TimelineRulerMode.Time, new TimelineContext { PixelsPerSecond = 1 }));
        Assert.Equal(["00:00", "01:00", "02:00"], minutes.Take(3).Select(t => t.Label));
        var hours = Labels(Grid(TimelineRulerMode.Time, new TimelineContext { PixelsPerSecond = 2, Start = 3600 }));
        Assert.Equal(["1:00:00", "1:01:00"], hours.Take(2).Select(t => t.Label));
    }

    [Fact]
    public void BeatLabels_ShowBarsAndBeats_LikeBitwig()
    {
        var grid = Grid(TimelineRulerMode.Beats, new TimelineContext { PixelsPerSecond = 100 }); // a beat is 50 px
        var labels = Labels(grid);
        Assert.Equal(["1", "1.2", "1.3", "1.4", "2", "2.2"], labels.Take(6).Select(t => t.Label));
        Assert.Equal(200f, labels[4].X);
        Assert.True(labels[4].IsEmphasized);
        Assert.False(labels[5].IsEmphasized);
        Assert.Contains(grid.Ticks, t => t.Level == TimelineGrid.MinorLevel && t.Time == 0.0625); // eighths of a beat

        grid.AbbreviateBarLabels = false;
        grid.Update(new TimelineContext { PixelsPerSecond = 100 }, 1000);
        Assert.Equal(["1.1", "1.2", "1.3", "1.4", "2.1"], Labels(grid).Take(5).Select(t => t.Label));
    }

    [Fact]
    public void BeatLabels_ShowSixteenths_ZoomedIn_AndBarGroups_ZoomedOut()
    {
        var zoomedIn = Labels(Grid(TimelineRulerMode.Beats, new TimelineContext { PixelsPerSecond = 800 }));
        Assert.Equal(["1", "1.1.2", "1.1.3", "1.1.4", "1.2"], zoomedIn.Take(5).Select(t => t.Label));

        var zoomedOut = Labels(Grid(TimelineRulerMode.Beats, new TimelineContext { PixelsPerSecond = 1 })); // a bar is 2 px
        Assert.Equal(["1", "33", "65"], zoomedOut.Take(3).Select(t => t.Label));
    }

    [Fact]
    public void BeatTicks_FollowMeterAndTempoChanges()
    {
        var map = new TempoMap([new TempoChange(0, 120), new TempoChange(8, 60)], [new MeterChange(3, new TimeSignature(3, 4))]);
        var labels = Labels(Grid(TimelineRulerMode.Beats, new TimelineContext { PixelsPerSecond = 100, TempoMap = map }));
        var bar3 = labels.Single(t => t.Label == "3");
        var bar4 = labels.Single(t => t.Label == "4");
        Assert.Equal(4, bar3.Time, 9);
        Assert.Equal(7, bar4.Time, 9); // three beats at 60 BPM
        Assert.Contains(labels, t => t.Label == "3.3");
        Assert.DoesNotContain(labels, t => t.Label == "3.4");
    }

    [Fact]
    public void SampleLabels_CountSamples_DownToEachSample()
    {
        var context = new TimelineContext { SampleRate = 48000, PixelsPerSecond = 480_000 }; // 10 px a sample
        var grid = Grid(TimelineRulerMode.Samples, context);
        Assert.Equal(["0", "5", "10"], Labels(grid).Take(3).Select(t => t.Label));
        Assert.Equal(101, grid.Ticks.Count);
        Assert.Equal(1 / 48000.0, grid.Ticks[1].Time);
    }

    [Fact]
    public void Snapping_UsesTheFinestVisibleTicks()
    {
        var context = new TimelineContext { PixelsPerSecond = 100 };
        Assert.Equal(1.2, Grid(TimelineRulerMode.Time, context).Snap(1.234), 9);
        Assert.Equal(0.0625, Grid(TimelineRulerMode.Beats, context).Snap(0.07), 9);
        context.PixelsPerSecond = 1; // snaps to 4-bar groups (bar 17 at 32 s)
        Assert.Equal(32, Grid(TimelineRulerMode.Beats, context).Snap(30), 9);
        Assert.Equal(1.234, new TimelineGrid().Snap(1.234));
    }

    [Fact]
    public void TicksBeforeTheStart_AreLeftOut_AndControlsCanStartAnywhere()
    {
        var context = new TimelineContext { PixelsPerSecond = 100 };
        var grid = Grid(TimelineRulerMode.Time, context, 500, left: -2);
        Assert.Equal(0, grid.Ticks[0].Time);
        Assert.Equal(200f, grid.Ticks[0].X);
        grid.Update(context, 12, 100); // a clip-sized control pinned at 12 s
        Assert.Equal("00:12", grid.Ticks[0].Label);
        Assert.Equal(0f, grid.Ticks[0].X);
    }

    [Fact]
    public void Scrolling_DoesNotAllocate_OnceTheLabelsAreKnown()
    {
        foreach (var mode in new[] { TimelineRulerMode.Beats, TimelineRulerMode.Time, TimelineRulerMode.Samples })
        {
            var context = new TimelineContext { PixelsPerSecond = 100 };
            var grid = new TimelineGrid { Mode = mode };
            grid.Update(context, 1000);
            grid.Update(context, 1000);
            long before = GC.GetAllocatedBytesForCurrentThread();
            grid.Update(context, 1000);
            grid.Snap(1.5);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }

    [Fact]
    public void TheFormat_WritesTimesBarsAndSamples()
    {
        Assert.Equal("01:05.250", TimelineFormat.Time(65.25));
        Assert.Equal("01:05", TimelineFormat.Time(65.25, 0));
        Assert.Equal("1:00:01.5", TimelineFormat.Time(3601.5, 1));
        Assert.Equal("-00:02.000", TimelineFormat.Time(-2));
        Assert.Equal("5.2.3", TimelineFormat.Bar(5, 2, 3));
        Assert.Equal("48000", TimelineFormat.Samples(48000));
    }
}

public class TimelineControlTests
{
    // A control laid out at the origin, `width` wide.
    private static T Laid<T>(T control, float width = 800, float height = 26) where T : TimelineControl
    {
        control.Measure(new Size(width, height));
        control.Arrange(new Rect(0, 0, width, height));
        return control;
    }

    [Fact]
    public void ConnectedControls_ZoomAndScrollTogether()
    {
        var song = new TimelineContext { PixelsPerSecond = 100 };
        var ruler = Laid(new TimelineRuler { Timeline = song });
        var lane = Laid(new TimelineLane { Timeline = song }, 400, 60);

        TimelineCommands.ZoomIn.Execute(ruler); // around the ruler's middle
        Assert.Equal(125, lane.CurrentTimeline.PixelsPerSecond);
        Assert.Equal(4, ruler.XToTime(400), 9);
        TimelineCommands.ScrollForward.Execute(lane);
        Assert.Equal(ruler.XToTime(0), lane.XToTime(0));
        Assert.True(ruler.XToTime(0) > 0);
    }

    [Fact]
    public void AControlWithoutAContext_HasItsOwn()
    {
        var a = Laid(new TimelineRuler());
        var b = Laid(new TimelineRuler());
        Assert.Same(a.CurrentTimeline, a.CurrentTimeline);
        Assert.NotSame(a.CurrentTimeline, b.CurrentTimeline);
        TimelineCommands.ZoomIn.Execute(a);
        Assert.NotEqual(a.CurrentTimeline.PixelsPerSecond, b.CurrentTimeline.PixelsPerSecond);
    }

    [Fact]
    public void DraggingTheRuler_ZoomsAroundTheGrabbedTime_AndScrollsWithThePointer()
    {
        var song = new TimelineContext { PixelsPerSecond = 100 };
        var ruler = Laid(new TimelineRuler { Timeline = song });
        var drag = TimelineCommands.ZoomDrag.BeginDrag(new DragStart(ruler, ruler, ruler, new Point(200, 10), PointerButtons.Left, ModifierKeys.None))!;

        drag.Update(new Point(200, -90), ModifierKeys.None); // 100 px up
        Assert.Equal(100 * Math.E, song.PixelsPerSecond, 6);
        Assert.Equal(2, ruler.XToTime(200), 9);
        drag.Update(new Point(300, -90), ModifierKeys.None);
        Assert.Equal(2, ruler.XToTime(300), 9);

        drag.Cancel();
        Assert.Equal((0.0, 100.0), (song.Start, song.PixelsPerSecond));
    }

    [Fact]
    public void TheWheel_ZoomsOverTheRuler_ButOverLanesOnlyWithCtrlAlt()
    {
        var song = new TimelineContext { PixelsPerSecond = 100, Start = 10 };
        var ruler = Laid(new TimelineRuler { Timeline = song });
        var lane = Laid(new TimelineLane { Timeline = song });

        var plain = new PointerWheelEventArgs(new Point(400, 30), new Point(400, 30), 0, 1);
        lane.OnPointerWheel(plain);
        Assert.False(plain.Handled); // left to the page's scroll viewer
        Assert.Equal(100, song.PixelsPerSecond);

        lane.OnPointerWheel(new PointerWheelEventArgs(new Point(400, 30), new Point(400, 30), 0, 1, modifiers: ModifierKeys.Control | ModifierKeys.Alt));
        Assert.Equal(125, song.PixelsPerSecond);
        ruler.OnPointerWheel(new PointerWheelEventArgs(new Point(400, 10), 0, -1));
        Assert.Equal(100, song.PixelsPerSecond, 9);
        Assert.Equal(10, song.Start, 9); // zoomed back around the same point

        ruler.OnPointerWheel(new PointerWheelEventArgs(new Point(400, 10), 1, 0)); // a sideways swipe
        Assert.Equal(10 - TimelineCommands.WheelScrollPixels / 100.0, song.Start, 5);
    }

    [Fact]
    public void TheRuler_IsAsHighAsItsParts_AndItsMenuChoosesTheUnits()
    {
        var ruler = new TimelineRuler();
        ruler.Measure(new Size(500, 100));
        Assert.Equal(TimelineRuler.MarkerLaneHeight + TimelineRuler.RowHeight + TimelineRuler.LoopBarHeight, ruler.DesiredSize.Height);
        ruler.ShowMarkers = false;
        ruler.ShowLoopBar = false;
        ruler.SecondaryMode = TimelineRulerMode.Time;
        ruler.Measure(new Size(500, 100));
        Assert.Equal(TimelineRuler.RowHeight + TimelineRuler.SecondaryRowHeight, ruler.DesiredSize.Height);
        ruler.Arrange(new Rect(0, 0, 500, ruler.DesiredSize.Height));
        Assert.Equal(new Rect(0, 0, 500, TimelineRuler.RowHeight), ruler.RowBounds);
        Assert.Equal(0, ruler.LoopBarBounds.Height);

        ruler.Mode = TimelineRulerMode.Samples;
        var items = ruler.CreateContextMenu().Items.OfType<MenuItem>().ToList();
        Assert.Equal(["Beats", "Time", "Samples"], items.Take(3).Select(i => i.Header as string));
        Assert.True(items[2].IsChecked);
        var secondRow = items.Single(i => i.Header as string == "Second row").Items.OfType<MenuItem>().ToList();
        Assert.Equal(4, secondRow.Count);
        Assert.True(secondRow[2].IsChecked); // Time
    }

    [Fact]
    public void RenderingTheRulerAndALane_DrawsTheSameTicks()
    {
        using var _ = ActiveTheme.Use(MaterialTheme.CreateLight());
        var song = new TimelineContext { PixelsPerSecond = 100 };
        var ruler = Laid(new TimelineRuler { Timeline = song }, 400);
        var lane = Laid(new TimelineLane { Timeline = song }, 400, 50);

        using var bitmap = new SkiaSharp.SKBitmap(400, 50);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        using var paints = new PaintRegistry();
        var context = new DrawingContext(canvas, paints);
        VisualTreeRenderer.Render(lane, ref context, ThemeVisualPresenter.Instance);
        Assert.NotEqual(bitmap.GetPixel(210, 25), bitmap.GetPixel(200, 25)); // the bar line at 2 s

        canvas.Clear();
        VisualTreeRenderer.Render(ruler, ref context, ThemeVisualPresenter.Instance);
        Assert.Contains(ruler.Grid.Ticks, t => t.Label == "2" && t.X == 200);
        Assert.Equal(ruler.Grid.Ticks.Select(t => t.X), lane.Grid.Ticks.Select(t => t.X));
    }
}

public class TimelineMarkerAndLoopTests
{
    // A ruler 800 px wide at 100 px/s (a beat is 50 px at 120 BPM): marker lane 0-18, row 18-44, loop bar 44-56.
    private static (TimelineRuler Ruler, TimelineContext Song) Ruler()
    {
        var song = new TimelineContext { PixelsPerSecond = 100 };
        var ruler = new TimelineRuler { Timeline = song };
        ruler.Measure(new Size(800, 100));
        ruler.Arrange(new Rect(0, 0, 800, ruler.DesiredSize.Height));
        return (ruler, song);
    }

    private static float RowY(TimelineRuler ruler) => ruler.RowBounds.Y + 10;

    private static float LoopY(TimelineRuler ruler) => ruler.LoopBarBounds.Y + 6;

    private static void Press(TimelineRuler ruler, float x, float y, int clicks = 1) =>
        ruler.OnPreviewPointerPressed(new PointerEventArgs(new Point(x, y), new Point(x, y), PointerButtons.Left, clickCount: clicks));

    private static IDragOperation Drag(DragCommand command, TimelineRuler ruler, float x, float y) =>
        command.BeginDrag(new DragStart(ruler, ruler, ruler, new Point(x, y), PointerButtons.Left, ModifierKeys.None))!;

    [Fact]
    public void Markers_ReportChangesToTheirProperties()
    {
        var markers = new TimelineMarkerCollection();
        int changes = 0;
        markers.Changed += (_, _) => changes++;
        var marker = new TimelineMarker(TimelinePosition.Seconds(1), "Intro");
        markers.Add(marker);
        marker.Label = "Verse";
        markers.Remove(marker);
        marker.Label = "Chorus"; // no longer in the collection
        Assert.Equal(3, changes);
        Assert.Null(markers.FindAt(1, TempoMap.Default));
    }

    [Fact]
    public void ClickingTheRuler_SetsThePlayStart_SnappedAndInBeats()
    {
        var (ruler, song) = Ruler();
        Press(ruler, 104, RowY(ruler));
        TimelineCommands.RulerClick.Execute(ruler);
        Assert.Equal(TimelinePosition.Beats(2.125), song.PlayStart); // the eighth of a beat nearest 1.04 s

        ruler.Mode = TimelineRulerMode.Time;
        ruler.SnapToGrid = false;
        Press(ruler, 104, RowY(ruler));
        TimelineCommands.RulerClick.Execute(ruler);
        Assert.Equal(TimelineUnit.Seconds, song.PlayStart.Unit);
        Assert.Equal(1.04, song.PlayStart.Value, 5);
    }

    [Fact]
    public void Flags_AreHitInTheMarkerLane_AndClickingOneMovesThePlayStartThere()
    {
        var (ruler, song) = Ruler();
        var shared = new TimelineMarker(TimelinePosition.Beats(4), "Verse");
        var own = new TimelineMarker(TimelinePosition.Seconds(5), "Mine");
        song.Markers.Add(shared);
        ruler.Markers.Add(own);

        Assert.Same(shared, ruler.MarkerAt(new Point(205, 9)));
        Assert.Same(own, ruler.MarkerAt(new Point(505, 9)));
        Assert.Null(ruler.MarkerAt(new Point(205, RowY(ruler)))); // the row below
        Assert.Null(ruler.MarkerAt(new Point(150, 9)));
        ruler.ShowSharedMarkers = false;
        Assert.Null(ruler.MarkerAt(new Point(205, 9)));
        ruler.ShowSharedMarkers = true;

        Press(ruler, 205, 9);
        TimelineCommands.RulerClick.Execute(ruler);
        Assert.Same(shared, ruler.SelectedMarker);
        Assert.Equal(TimelinePosition.Beats(4), song.PlayStart);

        TimelineCommands.DeleteMarker.Execute(ruler);
        Assert.Empty(song.Markers);
        Assert.Null(ruler.SelectedMarker);
    }

    [Fact]
    public void DraggingAFlag_MovesItsMarker_KeepingItsUnit_AndShiftInvertsSnapping()
    {
        var (ruler, song) = Ruler();
        var marker = new TimelineMarker(TimelinePosition.Beats(4), "Verse");
        song.Markers.Add(marker);

        var drag = Drag(TimelineCommands.MoveMarker, ruler, 205, 9);
        drag.Update(new Point(305, 9), ModifierKeys.None);
        Assert.Equal(TimelinePosition.Beats(6), marker.Position);
        drag.Update(new Point(308, 9), ModifierKeys.Shift);
        Assert.Equal(6.06, marker.Position.Value, 6); // 3 px unsnapped
        drag.Cancel();
        Assert.Equal(TimelinePosition.Beats(4), marker.Position);

        Assert.Null(TimelineCommands.MoveMarker.BeginDrag(new DragStart(ruler, ruler, ruler, new Point(150, 9), PointerButtons.Left, ModifierKeys.None)));
    }

    [Fact]
    public void DraggingOnTheLoopBar_DrawsResizesAndMovesTheLoop()
    {
        var (ruler, song) = Ruler();
        var drag = Drag(TimelineCommands.EditLoop, ruler, 100, LoopY(ruler));
        drag.Update(new Point(300, LoopY(ruler)), ModifierKeys.None);
        drag.Complete(new Point(300, LoopY(ruler)), ModifierKeys.None);
        Assert.Equal(new TimelineRange(TimelinePosition.Beats(2), TimelinePosition.Beats(6)), song.Loop);
        Assert.True(song.IsLoopEnabled);

        Assert.Equal(LoopBarPart.Start, ruler.LoopBarPartAt(new Point(102, LoopY(ruler))));
        Assert.Equal(LoopBarPart.End, ruler.LoopBarPartAt(new Point(298, LoopY(ruler))));
        Assert.Equal(LoopBarPart.Body, ruler.LoopBarPartAt(new Point(200, LoopY(ruler))));
        Assert.Equal(LoopBarPart.Empty, ruler.LoopBarPartAt(new Point(500, LoopY(ruler))));
        Assert.Equal(LoopBarPart.None, ruler.LoopBarPartAt(new Point(200, RowY(ruler))));

        drag = Drag(TimelineCommands.EditLoop, ruler, 298, LoopY(ruler)); // the end
        drag.Update(new Point(398, LoopY(ruler)), ModifierKeys.None);
        Assert.Equal(TimelinePosition.Beats(8), song.Loop.End);
        drag.Update(new Point(0, LoopY(ruler)), ModifierKeys.None); // not before the start
        Assert.Equal(TimelinePosition.Beats(2), song.Loop.End);
        drag.Cancel();
        Assert.Equal(TimelinePosition.Beats(6), song.Loop.End);

        drag = Drag(TimelineCommands.EditLoop, ruler, 200, LoopY(ruler)); // the body
        drag.Update(new Point(250, LoopY(ruler)), ModifierKeys.None);
        Assert.Equal(new TimelineRange(TimelinePosition.Beats(3), TimelinePosition.Beats(7)), song.Loop);
    }

    [Fact]
    public void DoubleClicks_AddAMarker_ToggleTheLoop_OrFitTheSong()
    {
        var (ruler, song) = Ruler();
        song.Duration = 16;
        ruler.MarkerAdding += (_, e) => e.Marker.Label = "Added";
        Press(ruler, 400, 9, clicks: 2);
        TimelineCommands.RulerDoubleClick.Execute(ruler);
        var marker = Assert.Single(song.Markers);
        Assert.Equal(("Added", TimelinePosition.Beats(8)), (marker.Label, marker.Position));
        Assert.Equal(TimelineMarker.Palette[0].Color, marker.Color);
        Assert.Same(marker, ruler.SelectedMarker);

        ruler.MarkerAdding += (_, e) => e.Cancel = true;
        Assert.Null(ruler.AddMarkerAt(100));
        Assert.Single(song.Markers);

        Press(ruler, 400, LoopY(ruler), clicks: 2); // an empty loop: loops four bars from the play start
        TimelineCommands.RulerDoubleClick.Execute(ruler);
        Assert.True(song.IsLoopEnabled);
        Assert.Equal(new TimelineRange(TimelinePosition.Beats(0), TimelinePosition.Beats(16)), song.Loop);
        TimelineCommands.RulerDoubleClick.Execute(ruler);
        Assert.False(song.IsLoopEnabled);

        Press(ruler, 400, RowY(ruler), clicks: 2);
        TimelineCommands.RulerDoubleClick.Execute(ruler);
        Assert.Equal(50, song.PixelsPerSecond); // 16 s in 800 px
    }

    [Fact]
    public void AMarkersMenu_RenamesRecolorsAndDeletesIt()
    {
        var (ruler, song) = Ruler();
        var marker = new TimelineMarker(TimelinePosition.Beats(4), "Verse");
        song.Markers.Add(marker);
        var items = ruler.CreateMarkerMenu(marker).Items.OfType<MenuItem>().ToList();
        Assert.Equal(3, items.Count);
        Assert.Equal("Color", items[1].Header as string);
        Assert.Equal(TimelineMarker.Palette.Count, items[1].Items.Count);

        var menu = ruler.CreateContextMenu(400).Items.OfType<MenuItem>().ToList();
        Assert.Contains(menu, i => i.Header as string == "Insert marker");
        Assert.Contains(menu, i => i.Command == TimelineCommands.ToggleLoop);
    }

    [Fact]
    public void RenderingTheSong_DrawsFlagsTheLoopAndThePlayhead()
    {
        using var _ = ActiveTheme.Use(MaterialTheme.CreateLight());
        var (ruler, song) = Ruler();
        song.Markers.Add(new TimelineMarker(TimelinePosition.Beats(4), "Verse", TimelineMarker.Palette[5].Color));
        song.Loop = new TimelineRange(TimelinePosition.Beats(2), TimelinePosition.Beats(6));
        song.IsLoopEnabled = true;
        song.Playhead = 3.5;
        var lane = new TimelineLane { Timeline = song };
        lane.Measure(new Size(800, 40));
        lane.Arrange(new Rect(0, 0, 800, 40));

        using var bitmap = new SkiaSharp.SKBitmap(800, 60);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        using var paints = new PaintRegistry();
        var context = new DrawingContext(canvas, paints);
        VisualTreeRenderer.Render(ruler, ref context, ThemeVisualPresenter.Instance);
        var blue = TimelineMarker.Palette[5].Color;
        var flag = bitmap.GetPixel(202, 4);
        Assert.Equal((blue.R, blue.G, blue.B), (flag.Red, flag.Green, flag.Blue));
        Assert.NotEqual(bitmap.GetPixel(600, (int)LoopY(ruler)), bitmap.GetPixel(200, (int)LoopY(ruler))); // the loop

        canvas.Clear();
        VisualTreeRenderer.Render(lane, ref context, ThemeVisualPresenter.Instance);
        Assert.NotEqual(bitmap.GetPixel(253, 20), bitmap.GetPixel(253 + 400, 20)); // shaded inside the loop (1 to 3 s) only
        Assert.NotEqual(bitmap.GetPixel(350, 20), bitmap.GetPixel(450, 20)); // the playhead at 3.5 s, not a beat line
    }
}

public class TimelineRulerFeedbackTests
{
    [Fact]
    public void TheCursor_ShowsWhetherADragResizesOrMovesTheLoop()
    {
        var song = new TimelineContext { PixelsPerSecond = 100 };
        song.Loop = new TimelineRange(TimelinePosition.Seconds(1), TimelinePosition.Seconds(3));
        song.Markers.Add(new TimelineMarker(TimelinePosition.Seconds(5), "Verse"));
        var ruler = new TimelineRuler { Timeline = song };
        ruler.Measure(new Size(800, 100));
        ruler.Arrange(new Rect(0, 0, 800, ruler.DesiredSize.Height));
        float loopY = ruler.LoopBarBounds.Y + 6;

        Assert.Equal(CursorType.SizeWestEast, ruler.CursorAt(new Point(101, loopY)));
        Assert.Equal(CursorType.SizeWestEast, ruler.CursorAt(new Point(299, loopY)));
        Assert.Equal(CursorType.SizeAll, ruler.CursorAt(new Point(200, loopY)));
        Assert.Equal(CursorType.Crosshair, ruler.CursorAt(new Point(500, loopY)));
        Assert.Equal(CursorType.Hand, ruler.CursorAt(new Point(505, 9)));
        Assert.Equal(CursorType.Default, ruler.CursorAt(new Point(200, ruler.RowBounds.Y + 10)));

        ruler.OnPreviewPointerMoved(new PointerEventArgs(new Point(101, loopY), new Point(101, loopY)));
        Assert.Equal(CursorType.SizeWestEast, ruler.Cursor);
        ruler.OnPreviewPointerMoved(new PointerEventArgs(new Point(200, loopY), new Point(200, loopY)));
        Assert.Equal(CursorType.SizeAll, ruler.Cursor);

        // The part under the pointer is highlighted too, until the pointer leaves.
        Assert.Equal(LoopBarPart.Body, ruler.HighlightedLoopPart);
        ruler.OnPreviewPointerMoved(new PointerEventArgs(new Point(299, loopY), new Point(299, loopY)));
        Assert.Equal(LoopBarPart.End, ruler.HighlightedLoopPart);
        ruler.OnPreviewPointerMoved(new PointerEventArgs(new Point(500, loopY), new Point(500, loopY)));
        Assert.Equal(LoopBarPart.None, ruler.HighlightedLoopPart); // outside the loop
        ruler.OnPreviewPointerMoved(new PointerEventArgs(new Point(101, loopY), new Point(101, loopY)));
        ruler.OnPointerExited(new PointerEventArgs(new Point(101, loopY), new Point(101, loopY)));
        Assert.Equal(LoopBarPart.None, ruler.HighlightedLoopPart);
    }

    [Fact]
    public void TheRulerAndLanes_DrawOnlyWithinTheirBounds()
    {
        using var _ = ActiveTheme.Use(MaterialTheme.CreateLight());
        var song = new TimelineContext { PixelsPerSecond = 100 };
        song.Markers.Add(new TimelineMarker(TimelinePosition.Seconds(0.9), "A long marker label", TimelineMarker.Palette[5].Color));
        var ruler = new TimelineRuler { Timeline = song, Background = Color.White };
        ruler.Measure(new Size(100, 100));
        ruler.Arrange(new Rect(0, 0, 100, ruler.DesiredSize.Height));

        using var bitmap = new SkiaSharp.SKBitmap(300, 60);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        using var paints = new PaintRegistry();
        var context = new DrawingContext(canvas, paints);
        VisualTreeRenderer.Render(ruler, ref context, ThemeVisualPresenter.Instance);
        Assert.NotEqual(0, bitmap.GetPixel(95, 6).Alpha); // the flag inside
        for (int x = 101; x < 300; x += 3)
        {
            Assert.Equal(0, bitmap.GetPixel(x, 6).Alpha); // the flag and its label end at the ruler's edge
            Assert.Equal(0, bitmap.GetPixel(x, (int)ruler.RowBounds.Y + 12).Alpha);
        }
    }
}
