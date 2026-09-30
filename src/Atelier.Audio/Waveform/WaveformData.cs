namespace Atelier.Audio;

/// <summary>The lowest and highest sample value in a stretch of audio.</summary>
/// <param name="Min">The lowest value (−1 to 1 for full scale).</param>
/// <param name="Max">The highest value.</param>
public readonly record struct WaveformPeak(float Min, float Max);

/// <summary>
/// Audio samples prepared for drawing: the samples of each channel plus a pyramid of min/max peaks, so a waveform of any
/// length draws in time proportional to its width in pixels, at any zoom.
/// </summary>
/// <remarks>
/// <para>
/// The peak pyramid holds the min and max of blocks of <see cref="FirstBlockSize"/> samples, then of blocks twice as
/// long, and so on up to the whole sound: about a quarter as many values again as there are samples. It's built once,
/// when the data is created; share one <see cref="WaveformData"/> between every <see cref="WaveformView"/> that shows the
/// same sound, such as the copies of a sample placed along a song.
/// </para>
/// <para>
/// The data is immutable: it keeps the sample arrays it's given, so don't change them afterwards. Decoding audio files is
/// up to the app; pass the decoded samples as floats (−1 to 1 for full scale).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var kick = WaveformData.FromInterleaved(decodedStereo, channelCount: 2, sampleRate: 48000);
/// new WaveformView().Source(kick);
/// </code>
/// </example>
public sealed class WaveformData
{
    /// <summary>The number of samples per peak of the finest pyramid level.</summary>
    public const int FirstBlockSize = 16;

    private readonly float[][] _channels;
    // _levels[channel][level] holds min/max pairs of blocks of FirstBlockSize << level samples.
    private readonly WaveformPeak[][][] _levels;

    /// <summary>Creates the data from one sample array per channel, all equally long.</summary>
    /// <param name="channels">The samples of each channel (at least one); the arrays are kept, not copied.</param>
    /// <param name="sampleRate">The samples per second.</param>
    public WaveformData(float[][] channels, double sampleRate)
    {
        ArgumentNullException.ThrowIfNull(channels);
        if (channels.Length == 0) throw new ArgumentException("There must be at least one channel.", nameof(channels));
        if (!double.IsFinite(sampleRate) || sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "The sample rate must be finite and positive.");
        int length = channels[0]?.Length ?? throw new ArgumentException("A channel is null.", nameof(channels));
        foreach (var channel in channels)
        {
            if (channel == null) throw new ArgumentException("A channel is null.", nameof(channels));
            if (channel.Length != length) throw new ArgumentException("All channels must have the same length.", nameof(channels));
        }

        _channels = channels;
        SampleRate = sampleRate;
        _levels = new WaveformPeak[channels.Length][][];
        for (int c = 0; c < channels.Length; c++) _levels[c] = BuildLevels(channels[c]);
    }

    /// <summary>Creates the data from interleaved samples (frame by frame: left, right, left, right, …).</summary>
    public static WaveformData FromInterleaved(ReadOnlySpan<float> samples, int channelCount, double sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(channelCount, 1);
        int frames = samples.Length / channelCount;
        var channels = new float[channelCount][];
        for (int c = 0; c < channelCount; c++)
        {
            var channel = new float[frames];
            for (int i = 0; i < frames; i++) channel[i] = samples[i * channelCount + c];
            channels[c] = channel;
        }
        return new WaveformData(channels, sampleRate);
    }

    /// <summary>Gets the number of channels.</summary>
    public int ChannelCount => _channels.Length;

    /// <summary>Gets the number of samples per channel.</summary>
    public int SampleCount => _channels[0].Length;

    /// <summary>Gets the samples per second.</summary>
    public double SampleRate { get; }

    /// <summary>Gets the length in seconds.</summary>
    public double Duration => SampleCount / SampleRate;

    /// <summary>Gets a sample, or 0 outside the sound.</summary>
    public float GetSample(int channel, long index) =>
        index >= 0 && index < SampleCount ? _channels[channel][index] : 0f;

    /// <summary>
    /// Gets the lowest and highest sample of a channel from sample <paramref name="start"/> up to (not including)
    /// <paramref name="end"/>, clipped to the sound; (0, 0) when nothing of it is in the sound.
    /// </summary>
    /// <remarks>
    /// The result is exact. The middle of the range is covered by the largest aligned pyramid blocks that fit, like a
    /// segment tree, and the ends (up to <see cref="FirstBlockSize"/> − 1 samples each) are read sample by sample, so it
    /// takes a few dozen steps whatever the range's length.
    /// </remarks>
    public WaveformPeak GetPeak(int channel, long start, long end)
    {
        start = Math.Max(0, start);
        end = Math.Min(SampleCount, end);
        if (end <= start) return default;

        var samples = _channels[channel];
        var levels = _levels[channel];
        float min = float.MaxValue, max = float.MinValue;
        long i = start;
        // Samples up to the first block boundary.
        for (; i < end && (i & (FirstBlockSize - 1)) != 0; i++) Include(samples[i], ref min, ref max);
        // The largest aligned blocks that fit.
        while (i + FirstBlockSize <= end)
        {
            int level = 0;
            while (level + 1 < levels.Length)
            {
                long size = (long)FirstBlockSize << (level + 1);
                if ((i & (size - 1)) != 0 || i + size > end) break;
                level++;
            }
            var peak = levels[level][i >> (level + BlockShift)];
            if (peak.Min < min) min = peak.Min;
            if (peak.Max > max) max = peak.Max;
            i += (long)FirstBlockSize << level;
        }
        // The samples after the last whole block.
        for (; i < end; i++) Include(samples[i], ref min, ref max);
        return new WaveformPeak(min, max);
    }

    private const int BlockShift = 4; // log2(FirstBlockSize)

    private static void Include(float value, ref float min, ref float max)
    {
        if (value < min) min = value;
        if (value > max) max = value;
    }

    private static WaveformPeak[][] BuildLevels(float[] samples)
    {
        var levels = new List<WaveformPeak[]>();
        int count = (samples.Length + FirstBlockSize - 1) / FirstBlockSize;
        if (count == 0) return [];
        var first = new WaveformPeak[count];
        for (int b = 0; b < count; b++)
        {
            int from = b * FirstBlockSize;
            int to = Math.Min(samples.Length, from + FirstBlockSize);
            float min = float.MaxValue, max = float.MinValue;
            for (int i = from; i < to; i++)
            {
                float value = samples[i];
                if (value < min) min = value;
                if (value > max) max = value;
            }
            first[b] = new WaveformPeak(min, max);
        }
        levels.Add(first);
        var previous = first;
        while (previous.Length > 1)
        {
            var next = new WaveformPeak[(previous.Length + 1) / 2];
            for (int b = 0; b < next.Length; b++)
            {
                var a = previous[2 * b];
                var c = 2 * b + 1 < previous.Length ? previous[2 * b + 1] : a;
                next[b] = new WaveformPeak(Math.Min(a.Min, c.Min), Math.Max(a.Max, c.Max));
            }
            levels.Add(next);
            previous = next;
        }
        return [.. levels];
    }
}
