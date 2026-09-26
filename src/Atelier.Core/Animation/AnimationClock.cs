using System;
using System.Collections.Generic;
using Atelier.Core.Primitives;

namespace Atelier.Core.Animation;

/// <summary>
/// Standard easing functions that map normalized progress (0 to 1) to eased progress.
/// </summary>
public static class Easing
{
    /// <summary>Constant speed: returns the input unchanged.</summary>
    public static readonly Func<float, float> Linear = t => t;

    /// <summary>Quadratic ease-in: starts slow and accelerates.</summary>
    public static readonly Func<float, float> EaseInQuad = t => t * t;
    /// <summary>Quadratic ease-out: starts fast and decelerates.</summary>
    public static readonly Func<float, float> EaseOutQuad = t => t * (2 - t);
    /// <summary>Quadratic ease-in-out: accelerates in the first half and decelerates in the second.</summary>
    public static readonly Func<float, float> EaseInOutQuad = t => t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;

    /// <summary>Cubic ease-in: starts slow and accelerates.</summary>
    public static readonly Func<float, float> EaseInCubic = t => t * t * t;
    /// <summary>Cubic ease-out: starts fast and decelerates.</summary>
    public static readonly Func<float, float> EaseOutCubic = t => (--t) * t * t + 1;
    /// <summary>Cubic ease-in-out: accelerates in the first half and decelerates in the second.</summary>
    public static readonly Func<float, float> EaseInOutCubic = t => t < 0.5f ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;

    // Material Design 3 Easing Standards
    // Emphasized (Standard motion curve for M3)
    /// <summary>The Material Design 3 emphasized curve, <c>cubic-bezier(0.2, 0, 0, 1)</c>; the default for animations and transitions.</summary>
    public static readonly Func<float, float> Emphasized = CubicBezier(0.2f, 0.0f, 0.0f, 1.0f);
    // Emphasized Decelerate (Incoming elements)
    /// <summary>The Material Design 3 emphasized-decelerate curve, <c>cubic-bezier(0.05, 0.7, 0.1, 1)</c>, for incoming elements.</summary>
    public static readonly Func<float, float> EmphasizedDecelerate = CubicBezier(0.05f, 0.7f, 0.1f, 1.0f);
    // Emphasized Accelerate (Outgoing elements)
    /// <summary>The Material Design 3 emphasized-accelerate curve, <c>cubic-bezier(0.3, 0, 0.8, 0.15)</c>, for outgoing elements.</summary>
    public static readonly Func<float, float> EmphasizedAccelerate = CubicBezier(0.3f, 0.0f, 0.8f, 0.15f);

    /// <summary>
    /// Creates an easing function from a CSS-style cubic Bezier curve through (0, 0), (x1, y1), (x2, y2) and (1, 1).
    /// </summary>
    /// <remarks>
    /// Each call solves the curve for x with up to eight Newton-Raphson iterations, so the result is approximate.
    /// </remarks>
    /// <param name="x1">The x coordinate of the first control point.</param>
    /// <param name="y1">The y coordinate of the first control point.</param>
    /// <param name="x2">The x coordinate of the second control point.</param>
    /// <param name="y2">The y coordinate of the second control point.</param>
    /// <returns>A function mapping progress to eased progress.</returns>
    public static Func<float, float> CubicBezier(float x1, float y1, float x2, float y2)
    {
        return t =>
        {
            float cx = 3.0f * x1;
            float bx = 3.0f * (x2 - x1) - cx;
            float ax = 1.0f - cx - bx;

            float cy = 3.0f * y1;
            float by = 3.0f * (y2 - y1) - cy;
            float ay = 1.0f - cy - by;

            // Solve X for T using Newton-Raphson
            float sampleCurveX(float tVal) => ((ax * tVal + bx) * tVal + cx) * tVal;
            float sampleCurveY(float tVal) => ((ay * tVal + by) * tVal + cy) * tVal;
            float sampleCurveDerivativeX(float tVal) => (3.0f * ax * tVal + 2.0f * bx) * tVal + cx;

            float tGuess = t;
            for (int i = 0; i < 8; i++)
            {
                float x = sampleCurveX(tGuess) - t;
                if (MathF.Abs(x) < 1e-4f) break;
                float d = sampleCurveDerivativeX(tGuess);
                if (MathF.Abs(d) < 1e-4f) break;
                tGuess -= x / d;
            }

            return sampleCurveY(Math.Clamp(tGuess, 0f, 1f));
        };
    }
}

/// <summary>
/// An animation advanced by an <see cref="AnimationClock"/>.
/// </summary>
public interface IAnimation
{
    /// <summary>
    /// Gets whether the animation is still running; the clock removes it once this is <see langword="false"/>.
    /// </summary>
    bool IsRunning { get; }
    /// <summary>
    /// Advances the animation by the given time.
    /// </summary>
    /// <param name="deltaTimeSeconds">The time elapsed since the previous update, in seconds.</param>
    void Update(double deltaTimeSeconds);
}

/// <summary>
/// Animates a <see cref="float"/> from one value to another over a fixed duration, reporting each value to a callback.
/// </summary>
public class FloatAnimation : IAnimation
{
    private readonly float _from;
    private readonly float _to;
    private readonly double _durationSeconds;
    private readonly Func<float, float> _easing;
    private readonly Action<float> _onUpdate;
    private readonly Action? _onCompleted;

    private double _elapsedSeconds = 0;
    /// <inheritdoc/>
    /// <remarks>Starts as <see langword="true"/> on construction; the animation does not need to be started.</remarks>
    public bool IsRunning { get; private set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="FloatAnimation"/> class.
    /// </summary>
    /// <param name="from">The start value.</param>
    /// <param name="to">The end value.</param>
    /// <param name="duration">The animation duration; values below one millisecond are treated as one millisecond.</param>
    /// <param name="onUpdate">Called with the current value on every update.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Easing.Emphasized"/>.</param>
    /// <param name="onCompleted">Called once when the animation finishes.</param>
    public FloatAnimation(
        float from,
        float to,
        TimeSpan duration,
        Action<float> onUpdate,
        Func<float, float>? easing = null,
        Action? onCompleted = null)
    {
        _from = from;
        _to = to;
        _durationSeconds = Math.Max(0.001, duration.TotalSeconds);
        _easing = easing ?? Easing.Emphasized;
        _onUpdate = onUpdate;
        _onCompleted = onCompleted;
    }

    /// <summary>
    /// Stops the animation where it is, without calling the update or completion callbacks again. The clock drops it
    /// on its next update.
    /// </summary>
    /// <remarks>
    /// Use this when a newer animation takes over the same value (for example a control toggled again before its
    /// previous animation finished), so the two don't alternately write it.
    /// </remarks>
    public void Stop()
    {
        IsRunning = false;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Calls the update callback with the eased value. On the update that reaches the duration, the animation stops,
    /// calls the update callback again with the exact end value, and then calls the completion callback.
    /// Does nothing once the animation has finished.
    /// </remarks>
    public void Update(double deltaTimeSeconds)
    {
        if (!IsRunning) return;

        _elapsedSeconds += deltaTimeSeconds;
        float progress = (float)Math.Clamp(_elapsedSeconds / _durationSeconds, 0.0, 1.0);
        float eased = _easing(progress);

        float current = _from + (_to - _from) * eased;
        _onUpdate(current);

        if (_elapsedSeconds >= _durationSeconds)
        {
            IsRunning = false;
            _onUpdate(_to);
            _onCompleted?.Invoke();
        }
    }
}

/// <summary>
/// Drives a set of <see cref="IAnimation"/>s from a per-frame update.
/// </summary>
public class AnimationClock
{
    private readonly List<IAnimation> _animations = [];
    private readonly List<IAnimation> _pendingAdd = [];

    /// <summary>
    /// Gets the number of animations being run, including those added since the last <see cref="Update"/>.
    /// </summary>
    public int ActiveAnimationCount => _animations.Count + _pendingAdd.Count;

    /// <summary>
    /// Schedules an animation to run.
    /// </summary>
    /// <remarks>
    /// The animation is picked up at the start of the next <see cref="Update"/> call, so it is safe to call from an
    /// animation callback during an update.
    /// </remarks>
    /// <param name="animation">The animation to run.</param>
    public void Add(IAnimation animation)
    {
        _pendingAdd.Add(animation);
    }

    /// <summary>
    /// Advances all animations by the given time.
    /// </summary>
    /// <remarks>
    /// First moves animations added through <see cref="Add"/> into the active set, then updates each active animation
    /// and removes those whose <see cref="IAnimation.IsRunning"/> is <see langword="false"/> afterwards.
    /// </remarks>
    /// <param name="deltaTimeSeconds">The time elapsed since the previous update, in seconds.</param>
    public void Update(double deltaTimeSeconds)
    {
        if (_pendingAdd.Count > 0)
        {
            _animations.AddRange(_pendingAdd);
            _pendingAdd.Clear();
        }

        for (int i = _animations.Count - 1; i >= 0; i--)
        {
            var anim = _animations[i];
            anim.Update(deltaTimeSeconds);
            if (!anim.IsRunning)
            {
                _animations.RemoveAt(i);
            }
        }
    }
}
