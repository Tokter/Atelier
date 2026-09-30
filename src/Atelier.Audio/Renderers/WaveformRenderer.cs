using Atelier.Core.Primitives;
using Atelier.Rendering;
using Atelier.Theming;

namespace Atelier.Audio.Renderers;

/// <summary>
/// Draws a <see cref="WaveformView"/>: its background, a zero line per channel, and the waveform in
/// <see cref="Atelier.Controls.Control.Foreground"/>: from <see cref="WaveformView.LineSamplesPerPixel"/> samples per
/// pixel the peak envelope with the RMS inside, else an antialiased line through the samples (with dots from
/// <see cref="WaveformView.SampleDotSpacing"/> pixels per sample). Only columns that are both visible (inside the canvas
/// clip) and inside the sound are drawn.
/// </summary>
/// <remarks>
/// To avoid aliasing and moiré, each column of the envelope is filled through zero from the exact peaks of its samples
/// (see <see cref="WaveformData.GetPeak"/>), reaching the next column's first sample so neighbors always touch; zoomed
/// out, columns take their peaks from <see cref="WaveformView.EnvelopeWindow"/> around them. The columns sit on a pixel
/// grid fixed to the sound, so scrolling doesn't change which samples a column shows (the waveform moves in whole
/// pixels instead).
/// </remarks>
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

            if (samplesPerPixel >= WaveformView.LineSamplesPerPixel)
            {
                DrawEnvelope(ref context, view, source, combined ? -1 : row, timeAtZero, pixelsPerSecond, from, to, firstSample, endSample, center, scale, color);
            }
            else
            {
                DrawSamples(ref context, source, combined ? -1 : row, timeAtZero, pixelsPerSecond, from, to, firstSample, endSample, center, scale, color);
            }
        }
    }

    // The peak envelope, a column per pixel filled from its lowest to its highest sample through zero, with the RMS inside.
    private static void DrawEnvelope(ref DrawingContext context, WaveformView view, WaveformData source, int channel, double timeAtZero,
        double pixelsPerSecond, double from, double to, long firstSample, long endSample, float center, float scale, Color color)
    {
        double samplesPerPixel = source.SampleRate / pixelsPerSecond;
        // The columns sit on a grid fixed to the sound, its first sample on a whole pixel: scrolling by a fraction of a
        // pixel moves the waveform a whole pixel at times instead of changing which samples each column shows.
        double origin = Math.Round(-timeAtZero * pixelsPerSecond);
        // How many columns on each side the smoothed values come from, so they span at least the envelope window.
        double windowSamples = WaveformView.EnvelopeWindow * source.SampleRate;
        int smoothing = (int)Math.Max(0, Math.Ceiling((windowSamples / samplesPerPixel - 1) / 2));
        // Zooming out, the smoothed envelope and the RMS fade in over an octave up to EnvelopeSamplesPerPixel: closer in,
        // the columns show their own peaks (the waveform's shape) in full color, which the RMS would hide.
        double envelope = Math.Clamp(Math.Log2(samplesPerPixel / WaveformView.EnvelopeSamplesPerPixel) + 1, 0, 1);
        int peakSmoothing = (int)Math.Round(smoothing * envelope);
        bool rms = view.ShowRms && envelope > 0;
        var peakColor = rms ? color.WithAlpha(color.A / 255f * (1 - (1 - view.PeakOpacity) * (float)envelope)) : color;
        var rmsColor = color.WithAlpha(color.A / 255f * (float)envelope);

        for (int x = (int)Math.Floor(from); x < (int)Math.Ceiling(to); x++)
        {
            // Up to and including the next column's first sample, so neighboring columns always touch.
            long start = Math.Max(firstSample, Bin(x - peakSmoothing));
            long end = Math.Min(endSample, Bin(x + 1 + peakSmoothing) + 1);
            if (end <= start) continue;
            var peak = channel < 0 ? CombinedPeak(source, start, end) : source.GetPeak(channel, start, end);
            // Through zero, so a column that only sees part of a cycle doesn't leave a hole in the middle.
            float top = center - Math.Clamp(Math.Max(peak.Max, 0), 0, 1) * scale;
            float bottom = center - Math.Clamp(Math.Min(peak.Min, 0), -1, 0) * scale;
            context.DrawRect(new Rect(x, top, 1, Math.Max(1, bottom - top)), peakColor);

            if (!rms) continue;
            long rmsStart = Math.Max(firstSample, Bin(x - smoothing));
            long rmsEnd = Math.Min(endSample, Bin(x + 1 + smoothing));
            float level = Math.Min(1, channel < 0 ? CombinedRms(source, rmsStart, rmsEnd) : source.GetRms(channel, rmsStart, rmsEnd)) * scale;
            if (level > 0.25f) context.DrawRect(new Rect(x, center - level, 1, 2 * level), rmsColor);
        }

        long Bin(int column) => (long)Math.Floor((column - origin) * samplesPerPixel);
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

    // The loudest channel's RMS.
    private static float CombinedRms(WaveformData source, long start, long end)
    {
        float level = 0;
        for (int c = 0; c < source.ChannelCount; c++) level = Math.Max(level, source.GetRms(c, start, end));
        return level;
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
