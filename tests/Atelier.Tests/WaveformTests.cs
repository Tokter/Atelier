using Atelier.Audio;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Rendering;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Tests;

public class WaveformDataTests
{
    private static float[] Noise(int length, int seed)
    {
        var random = new Random(seed);
        var samples = new float[length];
        for (int i = 0; i < length; i++) samples[i] = (float)(random.NextDouble() * 2 - 1) * (float)Math.Sin(i * 0.001);
        return samples;
    }

    private static WaveformPeak Brute(float[] samples, long start, long end)
    {
        start = Math.Max(0, start);
        end = Math.Min(samples.Length, end);
        float min = float.MaxValue, max = float.MinValue;
        for (long i = start; i < end; i++)
        {
            min = Math.Min(min, samples[i]);
            max = Math.Max(max, samples[i]);
        }
        return end > start ? new WaveformPeak(min, max) : default;
    }

    [Fact]
    public void Peaks_AreExact_ForAnyRange()
    {
        var samples = Noise(100_000, 1);
        var data = new WaveformData([samples], 48000);
        var random = new Random(2);
        for (int n = 0; n < 2000; n++)
        {
            long start = random.Next(-100, samples.Length);
            long length = random.Next(1, (n % 3) switch { 0 => 40, 1 => 2000, _ => 120_000 });
            Assert.Equal(Brute(samples, start, start + length), data.GetPeak(0, start, start + length));
        }
        Assert.Equal(default, data.GetPeak(0, 200_000, 300_000));
    }

    [Fact]
    public void InterleavedSamples_SplitIntoChannels()
    {
        var data = WaveformData.FromInterleaved([0.1f, -0.1f, 0.2f, -0.2f, 0.3f, -0.3f], 2, 3);
        Assert.Equal((2, 3, 1.0), (data.ChannelCount, data.SampleCount, data.Duration));
        Assert.Equal(-0.2f, data.GetSample(1, 1));
        Assert.Equal(0f, data.GetSample(0, 5));
        Assert.Equal(new WaveformPeak(0.1f, 0.3f), data.GetPeak(0, 0, 3));
        Assert.Throws<ArgumentException>(() => new WaveformData([new float[3], new float[4]], 48000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaveformData([new float[3]], 0));
    }
}

public class TimelinePanelTests
{
    // A lane 800 × 60 at 100 px/s with a panel as its content.
    private static (TimelineContext Song, TimelinePanel Panel, TimelineLane Lane) Lane(params UIElement[] children)
    {
        var song = new TimelineContext { PixelsPerSecond = 100 };
        var panel = new TimelinePanel().Children(children);
        var lane = new TimelineLane { Timeline = song, Content = panel };
        lane.AttachToHost();
        Layout(lane);
        return (song, panel, lane);
    }

    private static void Layout(UIElement element)
    {
        element.Measure(new Size(800, 60));
        element.Arrange(new Rect(0, 0, 800, 60));
    }

    [Fact]
    public void Children_SitAtTheirStart_AsLongAsTheirLength_AndFollowTheView()
    {
        var clip = new Border().TimelineRange(TimelinePosition.Beats(4), TimelinePosition.Seconds(1.5));
        var (song, panel, lane) = Lane(clip);
        Assert.Same(song, panel.CurrentTimeline);
        Assert.Equal(new Rect(200, 0, 150, 60), clip.Bounds);

        song.Start = 1;
        Layout(lane);
        Assert.Equal(new Rect(100, 0, 150, 60), clip.Bounds);
        song.PixelsPerSecond = 200;
        Layout(lane);
        Assert.Equal(new Rect(200, 0, 300, 60), clip.Bounds);
        song.TempoMap = TempoMap.Constant(60); // the start is in beats: 4 s now
        Layout(lane);
        Assert.Equal(new Rect(600, 0, 300, 60), clip.Bounds);
    }

    [Fact]
    public void ChildrenOutOfView_AreEmpty_AndHugeOnesAreCutToAMargin()
    {
        var early = new Border().TimelineRange(TimelinePosition.Seconds(0), TimelinePosition.Seconds(1));
        var huge = new Border().TimelineRange(TimelinePosition.Seconds(0), TimelinePosition.Seconds(3600));
        var (song, _, lane) = Lane(early, huge);
        song.Start = 100;
        Layout(lane);
        Assert.Equal(Rect.Zero, early.Bounds);
        Assert.Equal(new Rect(-800, 0, 2400, 60), huge.Bounds);
    }

    [Fact]
    public void AWaveformInAClip_PlaysAtTheClipsStart_EvenWhereTheClipIsCut()
    {
        var sound = new WaveformData([new float[48000 * 100]], 48000); // 100 s
        var view = new WaveformView { Source = sound, SourceStart = 2 };
        var clip = new Border { Padding = new Thickness(5, 0, 0, 0), Child = view }.TimelineRange(TimelinePosition.Seconds(1), TimelinePosition.Seconds(98));
        var (song, panel, lane) = Lane(clip);

        // At 100 px/s the sound's 2 s mark is at the clip's start plus the padding.
        var (timeAtZero, pixelsPerSecond) = view.GetMapping();
        Assert.Equal(100, pixelsPerSecond);
        Assert.Equal(2, timeAtZero, 9);

        // Zoomed in to a sample per 10 px far into the clip: the panel cuts the clip, the mapping stays exact.
        song.PixelsPerSecond = 480_000;
        song.Start = 60;
        Layout(lane);
        Assert.True(clip.Bounds.X > -2000);
        (timeAtZero, pixelsPerSecond) = view.GetMapping();
        double panelXOfView = clip.Bounds.X + view.Bounds.X;
        double soundTimeAtPanelLeft = timeAtZero - panelXOfView / pixelsPerSecond;
        Assert.Equal(60 - 1 - 5.0 / 480_000 + 2, soundTimeAtPanelLeft, 9);
    }

    [Fact]
    public void AWaveformOnItsOwn_StretchesItsPartOfTheSoundOverItsWidth()
    {
        var view = new WaveformView { Source = new WaveformData([new float[48000 * 4]], 48000), SourceStart = 1, SourceLength = 2 };
        view.Measure(new Size(400, 50));
        view.Arrange(new Rect(0, 0, 400, 50));
        Assert.Equal((1.0, 200.0), view.GetMapping());
        view.SourceLength = double.NaN; // the rest: 3 s
        Assert.Equal(3, view.ShownLength);
    }

    [Fact]
    public void TheWaveform_DrawsWithinTheSound_AndThePlayheadOverTheClips()
    {
        using var _ = ActiveTheme.Use(MaterialTheme.CreateLight());
        var samples = new float[48000];
        for (int i = 0; i < samples.Length; i++) samples[i] = (float)Math.Sin(i * 0.05);
        var view = new WaveformView { Source = new WaveformData([samples], 48000), Foreground = Color.FromRgb(0, 0, 255) };
        var clip = new Border { Background = Color.White, Child = view }.TimelineRange(TimelinePosition.Seconds(1), TimelinePosition.Seconds(2));
        var (song, _, lane) = Lane(clip);
        song.Playhead = 1.5;
        lane.Measure(new Size(800, 60));
        lane.Arrange(new Rect(0, 0, 800, 60));

        using var bitmap = new SkiaSharp.SKBitmap(800, 60);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        using var paints = new PaintRegistry();
        var context = new DrawingContext(canvas, paints);
        VisualTreeRenderer.Render(lane, ref context, ThemeVisualPresenter.Instance);

        Assert.Equal(255, bitmap.GetPixel(120, 30).Blue); // the sound, 1 s long, from x = 100
        Assert.Equal(SkiaSharp.SKColors.White, bitmap.GetPixel(250, 30)); // silence after it in the 2 s clip
        var playhead = bitmap.GetPixel(150, 5);
        Assert.NotEqual(SkiaSharp.SKColors.White, playhead); // drawn over the clip
        Assert.NotEqual((byte)255, playhead.Blue);
    }
}

public class WaveformRenderingTests
{
    private static WaveformData Noise()
    {
        var random = new Random(3);
        var samples = new float[48000];
        for (int i = 0; i < samples.Length; i++) samples[i] = (float)((random.NextDouble() * 2 - 1) * 0.5 + 0.4 * Math.Sin(i * 0.004));
        return new WaveformData([samples], 48000);
    }

    // The waveform of `sound` drawn 300 × 60 at `samplesPerPixel`, scrolled to `start` seconds: which pixels are lit.
    private static bool[,] Render(WaveformData sound, double samplesPerPixel, double start)
    {
        using var _ = ActiveTheme.Use(MaterialTheme.CreateDark());
        var song = new TimelineContext { PixelsPerSecond = 48000 / samplesPerPixel };
        song.Start = start;
        var view = new WaveformView { Source = sound, Foreground = Color.White };
        var panel = new TimelinePanel { Timeline = song }.Children(new Border { Child = view }.TimelineRange(TimelinePosition.Seconds(0), TimelinePosition.Seconds(1)));
        panel.Measure(new Size(300, 60));
        panel.Arrange(new Rect(0, 0, 300, 60));
        using var bitmap = new SkiaSharp.SKBitmap(300, 60);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.Black);
        using var paints = new PaintRegistry();
        var context = new DrawingContext(canvas, paints);
        VisualTreeRenderer.Render(panel, ref context, ThemeVisualPresenter.Instance);
        var lit = new bool[300, 60];
        for (int x = 0; x < 300; x++)
        {
            for (int y = 0; y < 60; y++) lit[x, y] = bitmap.GetPixel(x, y).Red > 128;
        }
        return lit;
    }

    private static bool SameColumn(bool[,] a, int xa, bool[,] b, int xb)
    {
        for (int y = 0; y < 60; y++)
        {
            if (a[xa, y] != b[xb, y]) return false;
        }
        return true;
    }

    [Fact]
    public void ScrollingByAFractionOfAPixel_MovesThePeaks_WithoutChangingThem()
    {
        var sound = Noise();
        double samplesPerPixel = 20;
        var before = Render(sound, samplesPerPixel, 0.25);
        var after = Render(sound, samplesPerPixel, 0.25 + 0.4 * samplesPerPixel / 48000); // 0.4 px later
        bool Matches(int shift)
        {
            for (int x = 10; x < 290; x++)
            {
                if (!SameColumn(after, x, before, x + shift)) return false;
            }
            return true;
        }
        Assert.True(Matches(0) || Matches(1), "the columns should show the same samples, at most a pixel apart");
    }

    [Fact]
    public void NeighboringPeakColumns_AlwaysTouch()
    {
        var lit = Render(Noise(), WaveformView.LineSamplesPerPixel, 0.25);
        (int Top, int Bottom) Span(int x)
        {
            int top = -1, bottom = -1;
            for (int y = 0; y < 60; y++)
            {
                if (!lit[x, y]) continue;
                if (top < 0) top = y;
                bottom = y;
            }
            return (top, bottom);
        }
        for (int x = 1; x < 299; x++)
        {
            var (top, bottom) = Span(x);
            var (nextTop, nextBottom) = Span(x + 1);
            Assert.True(top >= 0 && nextTop >= 0, $"column {x} or {x + 1} is empty");
            Assert.True(top <= nextBottom + 1 && nextTop <= bottom + 1, $"columns {x} and {x + 1} don't touch");
        }
    }
}
