using System;
using System.Collections.Generic;
using Atelier.Core.Primitives;

namespace Atelier.Core.Animation;

public static class Easing
{
    public static readonly Func<float, float> Linear = t => t;

    public static readonly Func<float, float> EaseInQuad = t => t * t;
    public static readonly Func<float, float> EaseOutQuad = t => t * (2 - t);
    public static readonly Func<float, float> EaseInOutQuad = t => t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;

    public static readonly Func<float, float> EaseInCubic = t => t * t * t;
    public static readonly Func<float, float> EaseOutCubic = t => (--t) * t * t + 1;
    public static readonly Func<float, float> EaseInOutCubic = t => t < 0.5f ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;

    // Material Design 3 Easing Standards
    // Emphasized (Standard motion curve for M3)
    public static readonly Func<float, float> Emphasized = CubicBezier(0.2f, 0.0f, 0.0f, 1.0f);
    // Emphasized Decelerate (Incoming elements)
    public static readonly Func<float, float> EmphasizedDecelerate = CubicBezier(0.05f, 0.7f, 0.1f, 1.0f);
    // Emphasized Accelerate (Outgoing elements)
    public static readonly Func<float, float> EmphasizedAccelerate = CubicBezier(0.3f, 0.0f, 0.8f, 0.15f);

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

public interface IAnimation
{
    bool IsRunning { get; }
    void Update(double deltaTimeSeconds);
}

public class FloatAnimation : IAnimation
{
    private readonly float _from;
    private readonly float _to;
    private readonly double _durationSeconds;
    private readonly Func<float, float> _easing;
    private readonly Action<float> _onUpdate;
    private readonly Action? _onCompleted;

    private double _elapsedSeconds = 0;
    public bool IsRunning { get; private set; } = true;

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

public class AnimationClock
{
    private readonly List<IAnimation> _animations = [];
    private readonly List<IAnimation> _pendingAdd = [];

    public int ActiveAnimationCount => _animations.Count + _pendingAdd.Count;

    public void Add(IAnimation animation)
    {
        _pendingAdd.Add(animation);
    }

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
