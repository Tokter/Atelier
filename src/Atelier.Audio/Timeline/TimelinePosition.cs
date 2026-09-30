using System.Globalization;

namespace Atelier.Audio;

/// <summary>The unit a <see cref="TimelinePosition"/> is anchored in.</summary>
public enum TimelineUnit
{
    /// <summary>Seconds: the position stays at the same time when the tempo changes.</summary>
    Seconds,

    /// <summary>Quarter notes from the start: the position moves with the music when the tempo changes.</summary>
    Beats,
}

/// <summary>
/// A position or length on a timeline, anchored either to a point in time (<see cref="TimelineUnit.Seconds"/>) or to the
/// musical grid (<see cref="TimelineUnit.Beats"/>, quarter notes); a <see cref="TempoMap"/> converts between the two.
/// </summary>
/// <remarks>
/// <para>
/// Clips, markers, loop ranges and the play start marker use it, so each can be pinned to a time or to the music: when the
/// tempo changes, beat-anchored items move and second-anchored ones stay. A length can be in either unit independently of
/// its start: an audio clip that isn't stretched starts on a beat but keeps its length in seconds
/// (see <see cref="GetEndSeconds"/>).
/// </para>
/// <para><c>default</c> is 0 seconds, the start of the timeline.</para>
/// </remarks>
/// <param name="Value">The position, in <paramref name="Unit"/>.</param>
/// <param name="Unit">The unit.</param>
public readonly record struct TimelinePosition(double Value, TimelineUnit Unit)
{
    /// <summary>Gets the start of the timeline.</summary>
    public static TimelinePosition Zero => default;

    /// <summary>Creates a position anchored in time.</summary>
    public static TimelinePosition Seconds(double seconds) => new(seconds, TimelineUnit.Seconds);

    /// <summary>Creates a position anchored to the music, in quarter notes from the start.</summary>
    public static TimelinePosition Beats(double beats) => new(beats, TimelineUnit.Beats);

    /// <summary>Creates a beat-anchored position at a bar (1-based) and a 1-based, possibly fractional beat into it.</summary>
    public static TimelinePosition Bar(TempoMap tempoMap, int bar, double beat = 1)
    {
        ArgumentNullException.ThrowIfNull(tempoMap);
        return Beats(tempoMap.BarToBeats(bar, beat));
    }

    /// <summary>Gets the position in seconds.</summary>
    public double ToSeconds(TempoMap tempoMap)
    {
        ArgumentNullException.ThrowIfNull(tempoMap);
        return Unit == TimelineUnit.Seconds ? Value : tempoMap.BeatsToSeconds(Value);
    }

    /// <summary>Gets the position in quarter notes.</summary>
    public double ToBeats(TempoMap tempoMap)
    {
        ArgumentNullException.ThrowIfNull(tempoMap);
        return Unit == TimelineUnit.Beats ? Value : tempoMap.SecondsToBeats(Value);
    }

    /// <summary>Gets the same position anchored in <paramref name="unit"/>.</summary>
    public TimelinePosition In(TimelineUnit unit, TempoMap tempoMap) =>
        unit == Unit ? this : unit == TimelineUnit.Seconds ? Seconds(ToSeconds(tempoMap)) : Beats(ToBeats(tempoMap));

    /// <summary>
    /// Gets where something that starts here and is <paramref name="length"/> long ends, in seconds: a length in seconds
    /// is added to the start time, a length in beats to the start's position in beats.
    /// </summary>
    public double GetEndSeconds(TimelinePosition length, TempoMap tempoMap)
    {
        ArgumentNullException.ThrowIfNull(tempoMap);
        return length.Unit == TimelineUnit.Seconds
            ? ToSeconds(tempoMap) + length.Value
            : tempoMap.BeatsToSeconds(ToBeats(tempoMap) + length.Value);
    }

    /// <summary>Returns the value and unit, like <c>"12.5 s"</c> or <c>"16 beats"</c>.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Value} {(Unit == TimelineUnit.Seconds ? "s" : "beats")}");
}
