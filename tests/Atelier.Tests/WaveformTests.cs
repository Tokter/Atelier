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
        return new WaveformPeak(min, max);
    }

    [Fact]
    public void Peaks_CoverTheRange_WidenedByAtMostHalfItsLength()
    {
        var samples = Noise(100_000, 1);
        var data = new WaveformData([samples], 48000);
        var random = new Random(2);
        for (int n = 0; n < 500; n++)
        {
            long start = random.Next(0, samples.Length);
            long length = random.Next(1, n % 2 == 0 ? 200 : 50_000);
            long end = start + length;
            var peak = data.GetPeak(0, start, end);
            var exact = Brute(samples, start, end);
            var widened = Brute(samples, start - length / 2 - 1, end + length / 2 + 1);
            Assert.InRange(peak.Min, widened.Min, exact.Min);
            Assert.InRange(peak.Max, exact.Max, widened.Max);
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
