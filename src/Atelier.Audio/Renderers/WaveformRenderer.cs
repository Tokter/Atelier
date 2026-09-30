using Atelier.Core.Primitives;
using Atelier.Rendering;
using Atelier.Theming;

namespace Atelier.Audio.Renderers;

/// <summary>How <see cref="WaveformDrawing.Draw"/> draws a waveform.</summary>
/// <param name="Color">The waveform's color.</param>
/// <param name="Layout">How several channels are shown.</param>
/// <param name="Scale">The vertical scale: 1 draws full scale to the edges.</param>
/// <param name="ShowRms">Whether the zoomed-out envelope shows the RMS inside it.</param>
/// <param name="PeakOpacity">The opacity of the envelope while the RMS is shown.</param>
/// <param name="CenterLineColor">The color of each channel's zero line; transparent for none.</param>
internal readonly record struct WaveformStyle(Color Color, WaveformChannelLayout Layout, float Scale, bool ShowRms, float PeakOpacity, Color CenterLineColor);

/// <summary>
/// Draws waveforms for <see cref="WaveformView"/> and <see cref="AudioWaveformEditor"/>: from
/// <see cref="WaveformView.LineSamplesPerPixel"/> samples per pixel the peak envelope with the RMS inside, else an
/// antialiased line through the samples (with dots from <see cref="WaveformView.SampleDotSpacing"/> pixels per sample),
/// scaled by a <see cref="WaveformGain"/>.
/// </summary>
/// <remarks>
/// To avoid aliasing and moiré, each column of the envelope is filled through zero from the exact peaks of its samples
/// (see <see cref="WaveformData.GetPeak"/>), reaching the next column's first sample so neighbors always touch; zoomed
/// out, columns take their peaks from <see cref="WaveformView.EnvelopeWindow"/> around them. The columns sit on a pixel
/// grid fixed to the sound, so scrolling doesn't change which samples a column shows (the waveform moves in whole
/// pixels instead).
/// </remarks>
internal static class WaveformDrawing
{
    private const float Headroom = 0.92f;

    /// <summary>
    /// Draws the part of <paramref name="source"/> from <paramref name="soundStart"/> to <paramref name="soundEnd"/>
    /// (seconds into it) between <paramref name="top"/> and <paramref name="top"/> + <paramref name="height"/>, where
    /// x = 0 is <paramref name="timeAtZero"/> seconds into the sound; only columns from <paramref name="from"/> to
    /// <paramref name="to"/> are drawn.
    /// </summary>
    public static void Draw(ref DrawingContext context, WaveformData source, double timeAtZero, double pixelsPerSecond,
        double soundStart, double soundEnd, double from, double to, float top, float height, in WaveformStyle style, in WaveformGain gain)
    {
        if (source.SampleCount == 0 || !(pixelsPerSecond > 0) || height <= 0) return;
        soundStart = Math.Max(0, soundStart);
        soundEnd = Math.Min(source.Duration, soundEnd);
        from = Math.Max(from, (soundStart - timeAtZero) * pixelsPerSecond);
        to = Math.Min(to, (soundEnd - timeAtZero) * pixelsPerSecond);
        if (to <= from) return;

        bool combined = style.Layout == WaveformChannelLayout.Combined || source.ChannelCount == 1;
        int rows = combined ? 1 : source.ChannelCount;
        float rowHeight = height / rows;
        float scale = rowHeight * 0.5f * Headroom * style.Scale;
        double sampleRate = source.SampleRate;
        long firstSample = (long)Math.Floor(soundStart * sampleRate);
        long endSample = (long)Math.Ceiling(soundEnd * sampleRate);
        var mapping = new Mapping(timeAtZero, pixelsPerSecond, from, to, firstSample, endSample);

        for (int row = 0; row < rows; row++)
        {
            float center = top + rowHeight * (row + 0.5f);
            if (style.CenterLineColor.A > 0)
            {
                float y = MathF.Round(center) + 0.5f;
                context.DrawLine(new Point((float)from, y), new Point((float)to, y), style.CenterLineColor, 1f);
            }

            if (sampleRate / pixelsPerSecond >= WaveformView.LineSamplesPerPixel)
            {
                DrawEnvelope(ref context, source, combined ? -1 : row, mapping, center, scale, style, gain);
            }
            else
            {
                DrawSamples(ref context, source, combined ? -1 : row, mapping, center, scale, style.Color, gain);
            }
        }
    }

    // Where the sound is: x = 0 is TimeAtZero seconds into it; columns From to To show samples FirstSample to EndSample.
    private readonly record struct Mapping(double TimeAtZero, double PixelsPerSecond, double From, double To, long FirstSample, long EndSample);

    // The peak envelope, a column per pixel filled from its lowest to its highest sample through zero, with the RMS inside.
    private static void DrawEnvelope(ref DrawingContext context, WaveformData source, int channel, in Mapping mapping,
        float center, float scale, in WaveformStyle style, in WaveformGain gain)
    {
        double pixelsPerSecond = mapping.PixelsPerSecond;
        double samplesPerPixel = source.SampleRate / pixelsPerSecond;
        // The columns sit on a grid fixed to the sound, its first sample on a whole pixel: scrolling by a fraction of a
        // pixel moves the waveform a whole pixel at times instead of changing which samples each column shows.
        double origin = Math.Round(-mapping.TimeAtZero * pixelsPerSecond);
        // How many columns on each side the smoothed values come from, so they span at least the envelope window.
        double windowSamples = WaveformView.EnvelopeWindow * source.SampleRate;
        int smoothing = (int)Math.Max(0, Math.Ceiling((windowSamples / samplesPerPixel - 1) / 2));
        // Zooming out, the smoothed envelope and the RMS fade in over an octave up to EnvelopeSamplesPerPixel: closer in,
        // the columns show their own peaks (the waveform's shape) in full color, which the RMS would hide.
        double envelope = Math.Clamp(Math.Log2(samplesPerPixel / WaveformView.EnvelopeSamplesPerPixel) + 1, 0, 1);
        int peakSmoothing = (int)Math.Round(smoothing * envelope);
        var color = style.Color;
        bool rms = style.ShowRms && envelope > 0;
        var peakColor = rms ? color.WithAlpha(color.A / 255f * (1 - (1 - style.PeakOpacity) * (float)envelope)) : color;
        var rmsColor = color.WithAlpha(color.A / 255f * (float)envelope);

        for (int x = (int)Math.Floor(mapping.From); x < (int)Math.Ceiling(mapping.To); x++)
        {
            // Up to and including the next column's first sample, so neighboring columns always touch.
            long start = Math.Max(mapping.FirstSample, Bin(x - peakSmoothing));
            long end = Math.Min(mapping.EndSample, Bin(x + 1 + peakSmoothing) + 1);
            if (end <= start) continue;
            float level = gain.At(mapping.TimeAtZero + (x + 0.5) / pixelsPerSecond);
            if (level <= 0) continue;
            var peak = channel < 0 ? CombinedPeak(source, start, end) : source.GetPeak(channel, start, end);
            // Through zero, so a column that only sees part of a cycle doesn't leave a hole in the middle.
            float top = center - Math.Clamp(Math.Max(peak.Max, 0) * level, 0, 1) * scale;
            float bottom = center - Math.Clamp(Math.Min(peak.Min, 0) * level, -1, 0) * scale;
            context.DrawRect(new Rect(x, top, 1, Math.Max(1, bottom - top)), peakColor);

            if (!rms) continue;
            long rmsStart = Math.Max(mapping.FirstSample, Bin(x - smoothing));
            long rmsEnd = Math.Min(mapping.EndSample, Bin(x + 1 + smoothing));
            float average = Math.Min(1, (channel < 0 ? CombinedRms(source, rmsStart, rmsEnd) : source.GetRms(channel, rmsStart, rmsEnd)) * level) * scale;
            if (average > 0.25f) context.DrawRect(new Rect(x, center - average, 1, 2 * average), rmsColor);
        }

        long Bin(int column) => (long)Math.Floor((column - origin) * samplesPerPixel);
    }

    // A line through the samples in view (and one beyond each side), with dots when they're far enough apart.
    private static void DrawSamples(ref DrawingContext context, WaveformData source, int channel, in Mapping mapping,
        float center, float scale, Color color, in WaveformGain gain)
    {
        double sampleRate = source.SampleRate;
        double pixelsPerSecond = mapping.PixelsPerSecond;
        long first = Math.Max(mapping.FirstSample, (long)Math.Floor((mapping.TimeAtZero + mapping.From / pixelsPerSecond) * sampleRate) - 1);
        long last = Math.Min(mapping.EndSample - 1, (long)Math.Ceiling((mapping.TimeAtZero + mapping.To / pixelsPerSecond) * sampleRate) + 1);
        bool dots = pixelsPerSecond / sampleRate >= WaveformView.SampleDotSpacing;
        Point previous = default;
        for (long i = first; i <= last; i++)
        {
            double time = i / sampleRate;
            float value = (channel < 0 ? CombinedSample(source, i) : source.GetSample(channel, i)) * gain.At(time);
            var point = new Point((float)((time - mapping.TimeAtZero) * pixelsPerSecond), center - Math.Clamp(value, -1, 1) * scale);
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

/// <summary>
/// Draws a <see cref="WaveformView"/>: its background, a zero line per channel, and the waveform in
/// <see cref="Atelier.Controls.Control.Foreground"/> (the peak envelope with the RMS inside, or a line through the samples
/// when zoomed in far). Only columns that are both visible (inside the canvas clip) and inside the sound are drawn.
/// </summary>
public sealed class WaveformViewRenderer : ControlRenderer<WaveformView>
{
    /// <inheritdoc/>
    public override void Render(WaveformView view, ref DrawingContext context)
    {
        float width = view.Bounds.Width;
        float height = view.Bounds.Height;
        if (width <= 0 || height <= 0) return;

        using var clip = context.PushClip(new Rect(0, 0, width, height));
        context.DrawRect(new Rect(0, 0, width, height), view.Background);
        if (view.Source is not { } source) return;
        var (timeAtZero, pixelsPerSecond) = view.GetMapping();
        var visible = context.Canvas.LocalClipBounds;
        var style = new WaveformStyle(view.Foreground, view.ChannelLayout, view.Gain, view.ShowRms, view.PeakOpacity, view.CenterLineColor);
        WaveformDrawing.Draw(ref context, source, timeAtZero, pixelsPerSecond, view.SourceStart, view.SourceStart + view.ShownLength,
            Math.Max(0, visible.Left), Math.Min(width, visible.Right), 0, height, style, WaveformGain.Identity);
    }
}
