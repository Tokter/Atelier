using System.Globalization;

namespace Atelier.Audio;

/// <summary>
/// A time signature: <see cref="Numerator"/> beats of a <see cref="Denominator"/> note per bar, like 4/4 or 7/8.
/// </summary>
/// <remarks>
/// Lengths are in quarter notes, the unit of <see cref="TimelineUnit.Beats"/>: a 6/8 bar is 3 quarter notes long and its
/// beats are eighth notes, half a quarter note each. <c>default</c> is 4/4.
/// </remarks>
public readonly record struct TimeSignature
{
    private readonly int _numerator;
    private readonly int _denominator;

    /// <summary>Creates a time signature.</summary>
    /// <param name="numerator">The beats per bar, 1 to 99.</param>
    /// <param name="denominator">The note value of a beat: 1, 2, 4, 8, 16, 32 or 64.</param>
    public TimeSignature(int numerator, int denominator)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numerator, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(numerator, 99);
        if (denominator < 1 || denominator > 64 || (denominator & (denominator - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), denominator, "The denominator must be a power of two from 1 to 64.");
        }
        _numerator = numerator;
        _denominator = denominator;
    }

    /// <summary>Gets 4/4.</summary>
    public static TimeSignature Common => new(4, 4);

    /// <summary>Gets the beats per bar.</summary>
    public int Numerator => _numerator == 0 ? 4 : _numerator;

    /// <summary>Gets the note value of a beat (4 for quarter notes, 8 for eighth notes).</summary>
    public int Denominator => _denominator == 0 ? 4 : _denominator;

    /// <summary>Gets the length of a beat in quarter notes: 1 for x/4, 0.5 for x/8.</summary>
    public double BeatLength => 4.0 / Denominator;

    /// <summary>Gets the length of a bar in quarter notes.</summary>
    public double BarLength => Numerator * BeatLength;

    /// <summary>Gets whether both signatures have the same numerator and denominator (so <c>default</c> equals 4/4).</summary>
    public bool Equals(TimeSignature other) => Numerator == other.Numerator && Denominator == other.Denominator;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    /// <summary>Returns the signature as <c>"numerator/denominator"</c>.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Numerator}/{Denominator}");
}

/// <summary>A tempo that applies from <paramref name="Beat"/> (in quarter notes from the start) until the next change.</summary>
/// <param name="Beat">Where the tempo starts, in quarter notes.</param>
/// <param name="Bpm">The tempo in quarter notes per minute.</param>
public readonly record struct TempoChange(double Beat, double Bpm);

/// <summary>A time signature that applies from bar <paramref name="Bar"/> (1-based) until the next change.</summary>
/// <param name="Bar">The first bar with the signature, 1-based.</param>
/// <param name="Signature">The time signature.</param>
public readonly record struct MeterChange(int Bar, TimeSignature Signature);

/// <summary>A musical position: a 1-based bar and beat, and how far into the beat (0 to 1).</summary>
/// <param name="Bar">The bar, 1-based.</param>
/// <param name="Beat">The beat within the bar, 1-based.</param>
/// <param name="Fraction">How far into the beat, from 0 (on the beat) to just under 1.</param>
public readonly record struct BarPosition(int Bar, int Beat, double Fraction);

/// <summary>
/// Maps between seconds and musical time: the song's tempo and time signature, each of which can change along the way.
/// </summary>
/// <remarks>
/// <para>
/// Musical time is counted in quarter notes from the start ("beats", like <see cref="TimelineUnit.Beats"/> and MIDI),
/// independent of the time signature. A tempo applies from its <see cref="TempoChange.Beat"/> until the next change; a
/// time signature from its <see cref="MeterChange.Bar"/>, so meters change on bar lines. Before the first tempo the
/// first tempo applies, and bars before the first meter change are 4/4.
/// </para>
/// <para>A tempo map is immutable: a song that changes tempo gets a new map.</para>
/// </remarks>
/// <example>
/// <code>
/// var map = new TempoMap(
///     [new TempoChange(0, 120), new TempoChange(32, 140)],   // 140 BPM from bar 9
///     [new MeterChange(1, new TimeSignature(4, 4)), new MeterChange(17, new TimeSignature(7, 8))]);
/// double seconds = map.BeatsToSeconds(map.BarToBeats(9));    // 16 s
/// </code>
/// </example>
public sealed class TempoMap
{
    private const double Epsilon = 1e-9;

    private readonly TempoChange[] _tempos;
    private readonly double[] _tempoSeconds;
    private readonly MeterChange[] _meters;
    private readonly double[] _meterBeats;

    /// <summary>Creates a tempo map.</summary>
    /// <param name="tempos">The tempos, at least one, at distinct non-negative beats; any order. The first one also applies before its beat.</param>
    /// <param name="meters">The time signatures, at distinct bars from 1; any order. Bars before the first are 4/4.</param>
    public TempoMap(IEnumerable<TempoChange> tempos, IEnumerable<MeterChange>? meters = null)
    {
        ArgumentNullException.ThrowIfNull(tempos);
        var sortedTempos = tempos.OrderBy(t => t.Beat).ToList();
        if (sortedTempos.Count == 0) throw new ArgumentException("A tempo map needs at least one tempo.", nameof(tempos));
        for (int i = 0; i < sortedTempos.Count; i++)
        {
            var tempo = sortedTempos[i];
            if (!double.IsFinite(tempo.Beat) || tempo.Beat < 0) throw new ArgumentException($"Tempo changes must be at finite, non-negative beats (got {tempo.Beat}).", nameof(tempos));
            if (!double.IsFinite(tempo.Bpm) || tempo.Bpm <= 0) throw new ArgumentException($"Tempos must be finite and positive (got {tempo.Bpm}).", nameof(tempos));
            if (i > 0 && tempo.Beat == sortedTempos[i - 1].Beat) throw new ArgumentException($"Two tempo changes at beat {tempo.Beat}.", nameof(tempos));
        }
        if (sortedTempos[0].Beat > 0) sortedTempos.Insert(0, sortedTempos[0] with { Beat = 0 });
        _tempos = [.. sortedTempos];
        _tempoSeconds = new double[_tempos.Length];
        for (int i = 1; i < _tempos.Length; i++)
        {
            _tempoSeconds[i] = _tempoSeconds[i - 1] + (_tempos[i].Beat - _tempos[i - 1].Beat) * 60 / _tempos[i - 1].Bpm;
        }

        var sortedMeters = (meters ?? []).OrderBy(m => m.Bar).ToList();
        for (int i = 0; i < sortedMeters.Count; i++)
        {
            if (sortedMeters[i].Bar < 1) throw new ArgumentException($"Meter changes must be at bar 1 or later (got {sortedMeters[i].Bar}).", nameof(meters));
            if (i > 0 && sortedMeters[i].Bar == sortedMeters[i - 1].Bar) throw new ArgumentException($"Two meter changes at bar {sortedMeters[i].Bar}.", nameof(meters));
        }
        if (sortedMeters.Count == 0 || sortedMeters[0].Bar > 1) sortedMeters.Insert(0, new MeterChange(1, TimeSignature.Common));
        _meters = [.. sortedMeters];
        _meterBeats = new double[_meters.Length];
        for (int i = 1; i < _meters.Length; i++)
        {
            _meterBeats[i] = _meterBeats[i - 1] + (_meters[i].Bar - _meters[i - 1].Bar) * _meters[i - 1].Signature.BarLength;
        }
    }

    /// <summary>Gets a map of 120 BPM in 4/4.</summary>
    public static TempoMap Default { get; } = Constant(120);

    /// <summary>Creates a map with one tempo and one time signature (4/4 by default).</summary>
    public static TempoMap Constant(double bpm, TimeSignature signature = default) =>
        new([new TempoChange(0, bpm)], [new MeterChange(1, signature)]);

    /// <summary>Gets the tempo changes in order; the first is at beat 0.</summary>
    public IReadOnlyList<TempoChange> Tempos => _tempos;

    /// <summary>Gets the meter changes in order; the first is at bar 1.</summary>
    public IReadOnlyList<MeterChange> Meters => _meters;

    /// <summary>Converts a position in quarter notes to seconds.</summary>
    public double BeatsToSeconds(double beats)
    {
        int i = FindLast(_tempos, beats);
        return _tempoSeconds[i] + (beats - _tempos[i].Beat) * 60 / _tempos[i].Bpm;
    }

    /// <summary>Converts seconds to a position in quarter notes.</summary>
    public double SecondsToBeats(double seconds)
    {
        int i = FindLast(_tempoSeconds, seconds);
        return _tempos[i].Beat + (seconds - _tempoSeconds[i]) * _tempos[i].Bpm / 60;
    }

    /// <summary>Gets the tempo at a position in quarter notes, in quarter notes per minute.</summary>
    public double TempoAt(double beats) => _tempos[FindLast(_tempos, beats)].Bpm;

    /// <summary>Gets the fastest tempo between two positions in quarter notes (both included).</summary>
    public double MaxTempo(double fromBeats, double toBeats)
    {
        int first = FindLast(_tempos, fromBeats);
        double max = _tempos[first].Bpm;
        for (int i = first + 1; i < _tempos.Length && _tempos[i].Beat <= toBeats; i++) max = Math.Max(max, _tempos[i].Bpm);
        return max;
    }

    /// <summary>Gets the time signature at a position in quarter notes.</summary>
    public TimeSignature SignatureAt(double beats) => _meters[MeterIndexAt(beats)].Signature;

    /// <summary>Gets the position in quarter notes where a bar (1-based) starts, plus a 1-based, possibly fractional beat into it.</summary>
    public double BarToBeats(int bar, double beat = 1)
    {
        int i = _meters.Length - 1;
        while (i > 0 && _meters[i].Bar > bar) i--;
        var signature = _meters[i].Signature;
        return _meterBeats[i] + (bar - _meters[i].Bar) * signature.BarLength + (beat - 1) * signature.BeatLength;
    }

    /// <summary>Gets the bar, beat and fraction of a beat at a position in quarter notes.</summary>
    public BarPosition GetBarPosition(double beats)
    {
        int i = MeterIndexAt(beats);
        var signature = _meters[i].Signature;
        double offset = beats - _meterBeats[i];
        double bars = Math.Floor(offset / signature.BarLength + Epsilon);
        double inBar = Math.Max(0, offset - bars * signature.BarLength);
        double beatsInBar = inBar / signature.BeatLength;
        double beat = Math.Min(signature.Numerator - 1, Math.Floor(beatsInBar + Epsilon));
        double fraction = Math.Clamp(beatsInBar - beat, 0, 1 - Epsilon);
        return new BarPosition(_meters[i].Bar + (int)bars, (int)beat + 1, fraction < Epsilon ? 0 : fraction);
    }

    /// <summary>Gets the time signature changes' count, for walking the meter segments (see <see cref="GetMeterSegment"/>).</summary>
    internal int MeterCount => _meters.Length;

    /// <summary>Gets the index of the meter segment at a position in quarter notes.</summary>
    internal int MeterIndexAt(double beats) => FindLast(_meterBeats, beats);

    /// <summary>Gets a meter segment: its first bar, signature, and start and end in quarter notes (the last ends at infinity).</summary>
    internal (int Bar, TimeSignature Signature, double Start, double End) GetMeterSegment(int index) =>
        (_meters[index].Bar, _meters[index].Signature, _meterBeats[index], index + 1 < _meters.Length ? _meterBeats[index + 1] : double.PositiveInfinity);

    // The index of the last entry at or before `value`; 0 when value is before them all.
    private static int FindLast(double[] starts, double value)
    {
        int lo = 0, hi = starts.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (starts[mid] <= value + Epsilon) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }

    private static int FindLast(TempoChange[] tempos, double beats)
    {
        int lo = 0, hi = tempos.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (tempos[mid].Beat <= beats + Epsilon) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }
}
