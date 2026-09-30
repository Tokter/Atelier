namespace Atelier.Audio;

/// <summary>The shape of a fade.</summary>
public enum FadeCurve
{
    /// <summary>The level changes evenly: a straight line.</summary>
    Linear,

    /// <summary>
    /// Equal power: a quarter sine, so a fade out and an overlapping fade in keep the loudness steady (a smooth cross
    /// fade).
    /// </summary>
    Smooth,
}

/// <summary>
/// The level of a sound over time: a gain, a fade in over <see cref="FadeInStart"/> to <see cref="FadeInEnd"/> and a
/// fade out over <see cref="FadeOutStart"/> to <see cref="FadeOutEnd"/> (seconds into the sound), each shaped by
/// <see cref="Curve"/>; silent before the fade in and after the fade out.
/// </summary>
/// <remarks>
/// The <see cref="AudioWaveformEditor"/> draws the waveform scaled by it; apply the same <see cref="At"/> to the samples
/// when playing or rendering the sound, so what's heard matches what's shown.
/// </remarks>
/// <param name="Gain">The linear gain (1 for 0 dB; see <see cref="FromDecibels"/>).</param>
/// <param name="FadeInStart">Where the sound starts (and the fade in begins), in seconds into the sound.</param>
/// <param name="FadeInEnd">Where the fade in reaches full level.</param>
/// <param name="FadeOutStart">Where the fade out begins.</param>
/// <param name="FadeOutEnd">Where the sound ends (and the fade out reaches silence).</param>
/// <param name="Curve">The shape of both fades.</param>
public readonly record struct WaveformGain(float Gain, double FadeInStart, double FadeInEnd, double FadeOutStart, double FadeOutEnd, FadeCurve Curve)
{
    /// <summary>Gets full level everywhere: no gain, no fades.</summary>
    public static WaveformGain Identity { get; } =
        new(1f, double.NegativeInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity, FadeCurve.Linear);

    /// <summary>Converts decibels to a linear gain: 0 dB is 1, −6 dB about 0.5.</summary>
    public static float FromDecibels(float decibels) => MathF.Pow(10, decibels / 20);

    /// <summary>Converts a linear gain to decibels (−∞ for 0).</summary>
    public static float ToDecibels(float gain) => 20 * MathF.Log10(gain);

    /// <summary>Gets the linear level at a time in seconds into the sound.</summary>
    public float At(double time)
    {
        if (time < FadeInStart || time > FadeOutEnd) return 0;
        float fade = 1;
        if (time < FadeInEnd && FadeInEnd > FadeInStart) fade = Shape((time - FadeInStart) / (FadeInEnd - FadeInStart));
        if (time > FadeOutStart && FadeOutEnd > FadeOutStart) fade = Math.Min(fade, Shape((FadeOutEnd - time) / (FadeOutEnd - FadeOutStart)));
        return Gain * fade;
    }

    // The level after a share `t` (0 to 1) of a fade in.
    private float Shape(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Curve == FadeCurve.Smooth ? (float)Math.Sin(t * Math.PI / 2) : (float)t;
    }
}
