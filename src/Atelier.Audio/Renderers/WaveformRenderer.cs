using Atelier.Core.Primitives;
using Atelier.Rendering;
using Atelier.Theming;

namespace Atelier.Audio.Renderers;

/// <summary>
/// Draws a <see cref="WaveformView"/>: its background, a zero line per channel, and the waveform in
/// <see cref="Atelier.Controls.Control.Foreground"/>: a min/max bar per pixel column while there is at least one sample
/// per pixel, else a line through the samples (with dots from <see cref="WaveformView.SampleDotSpacing"/> pixels per
/// sample). Only columns that are both visible (inside the canvas clip) and inside the sound are drawn.
/// </summary>
public sealed class WaveformViewRenderer : ControlRenderer<WaveformView>
{
    private const float Headroom = 0.92f;

    /// <inheritdoc/>
    public override void Render(WaveformView view, ref DrawingContext context)
    {
        float width = view.Bounds.Width;
        float height = view.Bounds.Height;
        if (width <= 0 || height <= 0) return;

        using var clip = context.PushClip(new Rect(0, 0, width, height));
        context.DrawRect(new Rect(0, 0, width, height), view.Background);
        if (view.Source is not { } source || source.SampleCount == 0) return;
        var (timeAtZero, pixelsPerSecond) = view.GetMapping();
        if (!(pixelsPerSecond > 0)) return;

        // The visible columns that show the sound.
        double soundStart = Math.Max(0, view.SourceStart);
        double soundEnd = Math.Min(source.Duration, view.SourceStart + view.ShownLength);
        var visible = context.Canvas.LocalClipBounds;
        double from = Math.Max(Math.Max(0, visible.Left), (soundStart - timeAtZero) * pixelsPerSecond);
        double to = Math.Min(Math.Min(width, visible.Right), (soundEnd - timeAtZero) * pixelsPerSecond);
        if (to <= from) return;

        bool combined = view.ChannelLayout == WaveformChannelLayout.Combined || source.ChannelCount == 1;
        int rows = combined ? 1 : source.ChannelCount;
        float rowHeight = height / rows;
        float scale = rowHeight * 0.5f * Headroom * view.Gain;
        var color = view.Foreground;
        double sampleRate = source.SampleRate;
        double samplesPerPixel = sampleRate / pixelsPerSecond;
        long firstSample = (long)Math.Floor(soundStart * sampleRate);
        long endSample = (long)Math.Ceiling(soundEnd * sampleRate);

        for (int row = 0; row < rows; row++)
        {
            float center = rowHeight * (row + 0.5f);
            if (view.CenterLineColor.A > 0)
            {
                context.DrawLine(new Point((float)from, MathF.Round(center) + 0.5f), new Point((float)to, MathF.Round(center) + 0.5f), view.CenterLineColor, 1f);
            }

            if (samplesPerPixel >= 1)
            {
                for (int x = (int)Math.Floor(from); x < (int)Math.Ceiling(to); x++)
                {
                    long start = Math.Max(firstSample, (long)Math.Floor((timeAtZero + x / pixelsPerSecond) * sampleRate));
                    long end = Math.Min(endSample, (long)Math.Ceiling((timeAtZero + (x + 1) / pixelsPerSecond) * sampleRate));
                    if (end <= start) continue;
                    var peak = combined ? CombinedPeak(source, start, end) : source.GetPeak(row, start, end);
                    float top = center - Math.Clamp(peak.Max, -1, 1) * scale;
                    float bottom = center - Math.Clamp(peak.Min, -1, 1) * scale;
                    context.DrawRect(new Rect(x, top, 1, Math.Max(1, bottom - top)), color);
                }
            }
            else
            {
                DrawSamples(ref context, source, combined ? -1 : row, timeAtZero, pixelsPerSecond, from, to, firstSample, endSample, center, scale, color);
            }
        }
    }

    // A line through the samples in view (and one beyond each side), with dots when they're far enough apart.
    private static void DrawSamples(ref DrawingContext context, WaveformData source, int channel, double timeAtZero, double pixelsPerSecond,
        double from, double to, long firstSample, long endSample, float center, float scale, Color color)
    {
        double sampleRate = source.SampleRate;
        long first = Math.Max(firstSample, (long)Math.Floor((timeAtZero + from / pixelsPerSecond) * sampleRate) - 1);
        long last = Math.Min(endSample - 1, (long)Math.Ceiling((timeAtZero + to / pixelsPerSecond) * sampleRate) + 1);
        bool dots = pixelsPerSecond / sampleRate >= WaveformView.SampleDotSpacing;
        Point previous = default;
        for (long i = first; i <= last; i++)
        {
            float value = channel < 0 ? CombinedSample(source, i) : source.GetSample(channel, i);
            var point = new Point((float)((i / sampleRate - timeAtZero) * pixelsPerSecond), center - Math.Clamp(value, -1, 1) * scale);
            if (i > first) context.DrawLine(previous, point, color, 1.5f);
            if (dots) context.DrawCircle(point, 2.5f, color);
            previous = point;
        }
    }

    private static WaveformPeak CombinedPeak(WaveformData source, long start, long end)
    {
        float min = float.MaxValue, max = float.MinValue;
        for (int c = 0; c < source.ChannelCount; c++)
        {
            var peak = source.GetPeak(c, start, end);
            min = Math.Min(min, peak.Min);
            max = Math.Max(max, peak.Max);
        }
        return new WaveformPeak(min, max);
    }

    // The channel with the largest magnitude, so a combined view shows the loudest signal.
    private static float CombinedSample(WaveformData source, long index)
    {
        float value = source.GetSample(0, index);
        for (int c = 1; c < source.ChannelCount; c++)
        {
            float other = source.GetSample(c, index);
            if (Math.Abs(other) > Math.Abs(value)) value = other;
        }
        return value;
    }
}
