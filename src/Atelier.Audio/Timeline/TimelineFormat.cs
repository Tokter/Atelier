using System.Globalization;

namespace Atelier.Audio;

/// <summary>Formats timeline positions the way the ruler labels them: <c>01:05.250</c>, <c>5.2.3</c> and sample counts.</summary>
public static class TimelineFormat
{
    private const long NanosecondsPerSecond = 1_000_000_000;

    /// <summary>
    /// Formats seconds as <c>mm:ss</c> with <paramref name="decimals"/> digits of the second (0 to 9), and hours
    /// (<c>h:mm:ss</c>) from an hour on: <c>Time(65.25)</c> is <c>"01:05.250"</c>. Negative times get a minus sign.
    /// </summary>
    public static string Time(double seconds, int decimals = 3)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decimals, 9);
        if (!double.IsFinite(seconds)) return seconds.ToString(CultureInfo.InvariantCulture);
        long unit = Pow10(9 - decimals);
        long ns = (long)Math.Round(Math.Abs(seconds) * NanosecondsPerSecond / unit) * unit;
        string text = Time(ns, decimals, ns >= 3600 * NanosecondsPerSecond);
        return seconds < 0 && ns != 0 ? "-" + text : text;
    }

    /// <summary>Formats a musical position: <c>"5"</c> for a bar, <c>"5.2"</c> with a beat, <c>"5.2.3"</c> with a sixteenth of the beat (all 1-based; 0 leaves a part out).</summary>
    public static string Bar(int bar, int beat = 0, int sixteenth = 0) =>
        beat <= 0 ? bar.ToString(CultureInfo.InvariantCulture)
        : sixteenth <= 0 ? string.Create(CultureInfo.InvariantCulture, $"{bar}.{beat}")
        : string.Create(CultureInfo.InvariantCulture, $"{bar}.{beat}.{sixteenth}");

    /// <summary>Formats a sample count.</summary>
    public static string Samples(long samples) => samples.ToString(CultureInfo.InvariantCulture);

    /// <summary>Formats a non-negative time in nanoseconds with <paramref name="decimals"/> digits of the second, as <c>h:mm:ss</c> when <paramref name="hours"/>.</summary>
    internal static string Time(long nanoseconds, int decimals, bool hours)
    {
        long totalSeconds = nanoseconds / NanosecondsPerSecond;
        long fraction = nanoseconds % NanosecondsPerSecond;
        long seconds = totalSeconds % 60;
        long minutes = totalSeconds / 60;
        Span<char> buffer = stackalloc char[40];
        int length;
        if (hours)
        {
            buffer.TryWrite(CultureInfo.InvariantCulture, $"{minutes / 60}:{minutes % 60:00}:{seconds:00}", out length);
        }
        else
        {
            buffer.TryWrite(CultureInfo.InvariantCulture, $"{minutes:00}:{seconds:00}", out length);
        }
        if (decimals > 0)
        {
            buffer[length++] = '.';
            long digits = fraction / Pow10(9 - decimals);
            for (int i = decimals - 1; i >= 0; i--)
            {
                buffer[length + i] = (char)('0' + digits % 10);
                digits /= 10;
            }
            length += decimals;
        }
        return new string(buffer[..length]);
    }

    /// <summary>Gets 10 to the power of <paramref name="exponent"/> (0 to 18).</summary>
    internal static long Pow10(int exponent)
    {
        long value = 1;
        for (int i = 0; i < exponent; i++) value *= 10;
        return value;
    }

    /// <summary>Gets the number of decimal digits of a non-negative number (1 for 0).</summary>
    internal static int Digits(long value)
    {
        int digits = 1;
        while (value >= 10)
        {
            value /= 10;
            digits++;
        }
        return digits;
    }
}
