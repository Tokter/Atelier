using System;
using Atelier.Audio;

namespace Atelier.Gallery.ViewModels;

/// <summary>
/// Synthesized sounds for the timeline page, at 120 BPM: a one-bar drum beat, a four-bar bass line and an eight-bar
/// stereo pad. Real apps decode audio files instead.
/// </summary>
public static class TimelineSounds
{
    private const int SampleRate = 48000;
    private const double Beat = 0.5; // seconds at 120 BPM

    /// <summary>Gets a bar of drums: kicks on 1 and 3, snares on 2 and 4, hi-hats on the eighths.</summary>
    public static WaveformData Drums { get; } = CreateDrums();

    /// <summary>Gets four bars of bass, a note per beat.</summary>
    public static WaveformData Bass { get; } = CreateBass();

    /// <summary>Gets eight bars of a stereo pad: four chords of two bars each.</summary>
    public static WaveformData Pad { get; } = CreatePad();

    private static WaveformData CreateDrums()
    {
        var samples = new float[(int)(4 * Beat * SampleRate)];
        var random = new Random(7);
        for (int eighth = 0; eighth < 8; eighth++)
        {
            int at = (int)(eighth * Beat / 2 * SampleRate);
            bool kick = eighth is 0 or 4;
            bool snare = eighth is 2 or 6;
            for (int i = 0; at + i < samples.Length && i < SampleRate / 3; i++)
            {
                double t = (double)i / SampleRate;
                double value = 0;
                if (kick) value += Math.Sin(2 * Math.PI * (50 * t + 60 * (1 - Math.Exp(-t * 30)) / 30)) * Math.Exp(-t * 9);
                if (snare) value += (random.NextDouble() * 2 - 1) * 0.7 * Math.Exp(-t * 18) + Math.Sin(2 * Math.PI * 190 * t) * 0.3 * Math.Exp(-t * 25);
                value += (random.NextDouble() * 2 - 1) * 0.18 * Math.Exp(-t * 90); // hi-hat
                samples[at + i] += (float)value;
            }
        }
        Normalize(samples, 0.95f);
        return new WaveformData([samples], SampleRate);
    }

    private static WaveformData CreateBass()
    {
        double[] notes = [55, 55, 65.4, 73.4, 55, 55, 82.4, 73.4, 49, 49, 55, 65.4, 55, 73.4, 65.4, 55];
        var samples = new float[(int)(notes.Length * Beat * SampleRate)];
        for (int n = 0; n < notes.Length; n++)
        {
            int at = (int)(n * Beat * SampleRate);
            double phase = 0;
            for (int i = 0; i < Beat * SampleRate; i++)
            {
                double t = (double)i / SampleRate;
                phase += notes[n] / SampleRate;
                double saw = 2 * (phase - Math.Floor(phase + 0.5));
                double envelope = Math.Min(1, t * 200) * Math.Exp(-t * 3.5);
                samples[at + i] = (float)(saw * envelope * 0.8);
            }
        }
        return new WaveformData([samples], SampleRate);
    }

    private static WaveformData CreatePad()
    {
        double[][] chords = [[220, 261.6, 329.6], [196, 246.9, 293.7], [174.6, 220, 261.6], [196, 246.9, 329.6]];
        double chordLength = 8 * Beat;
        int length = (int)(chords.Length * chordLength * SampleRate);
        var left = new float[length];
        var right = new float[length];
        for (int c = 0; c < chords.Length; c++)
        {
            int at = (int)(c * chordLength * SampleRate);
            for (int i = 0; i < chordLength * SampleRate; i++)
            {
                double t = (double)i / SampleRate;
                double envelope = Math.Min(1, t / 0.6) * Math.Min(1, (chordLength - t) / 0.4);
                double l = 0, r = 0;
                for (int k = 0; k < 3; k++)
                {
                    l += Math.Sin(2 * Math.PI * chords[c][k] * t);
                    r += Math.Sin(2 * Math.PI * chords[c][k] * 1.003 * t + k);
                }
                double tremolo = 0.75 + 0.25 * Math.Sin(2 * Math.PI * 0.5 * (at / (double)SampleRate + t));
                left[at + i] = (float)(l / 3 * envelope * tremolo * 0.8);
                right[at + i] = (float)(r / 3 * envelope * (1.5 - tremolo) * 0.8);
            }
        }
        return new WaveformData([left, right], SampleRate);
    }

    private static void Normalize(float[] samples, float peak)
    {
        float max = 0;
        foreach (float s in samples) max = Math.Max(max, Math.Abs(s));
        if (max <= 0) return;
        for (int i = 0; i < samples.Length; i++) samples[i] *= peak / max;
    }
}
