using System;
using System.Collections.Generic;
using System.Numerics;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;

namespace Atelier.Core.Animation;

/// <summary>
/// A transition that smoothly cross-fades the opacity between outgoing and incoming content.
/// </summary>
public class FadeTransition : TransitionBase
{
    public FadeTransition(TimeSpan? duration = null, Func<float, float>? easing = null)
        : base(duration ?? TimeSpan.FromMilliseconds(250), easing ?? Atelier.Core.Animation.Easing.Linear)
    {
    }

    public static FadeTransition Default(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(duration, easing);

    public override void Apply(UIElement? from, UIElement? to, float progress, Size bounds)
    {
        if (from != null)
        {
            from.Opacity = Math.Clamp(1.0f - progress, 0.0f, 1.0f);
        }

        if (to != null)
        {
            to.Opacity = Math.Clamp(progress, 0.0f, 1.0f);
        }
    }
}

/// <summary>
/// Direction for directional slide transitions.
/// </summary>
public enum SlideDirection
{
    /// <summary>Content moves towards the left (incoming enters from the right).</summary>
    Left,
    /// <summary>Content moves towards the right (incoming enters from the left).</summary>
    Right,
    /// <summary>Content moves upwards (incoming enters from the bottom).</summary>
    Up,
    /// <summary>Content moves downwards (incoming enters from the top).</summary>
    Down
}

/// <summary>
/// A transition that slides content along horizontal or vertical axes.
/// </summary>
public class SlideTransition : TransitionBase
{
    public SlideDirection Direction { get; set; }

    public SlideTransition(SlideDirection direction = SlideDirection.Left, TimeSpan? duration = null, Func<float, float>? easing = null)
        : base(duration ?? TimeSpan.FromMilliseconds(300), easing ?? Atelier.Core.Animation.Easing.Emphasized)
    {
        Direction = direction;
    }

    public static SlideTransition Left(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Left, duration, easing);

    public static SlideTransition Right(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Right, duration, easing);

    public static SlideTransition Up(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Up, duration, easing);

    public static SlideTransition Down(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Down, duration, easing);

    public override void Apply(UIElement? from, UIElement? to, float progress, Size bounds)
    {
        float w = bounds.Width > 0 ? bounds.Width : 400f;
        float h = bounds.Height > 0 ? bounds.Height : 300f;

        float fromX = 0f, fromY = 0f;
        float toX = 0f, toY = 0f;

        switch (Direction)
        {
            case SlideDirection.Left:
                fromX = -w * progress;
                toX = w * (1.0f - progress);
                break;
            case SlideDirection.Right:
                fromX = w * progress;
                toX = -w * (1.0f - progress);
                break;
            case SlideDirection.Up:
                fromY = -h * progress;
                toY = h * (1.0f - progress);
                break;
            case SlideDirection.Down:
                fromY = h * progress;
                toY = -h * (1.0f - progress);
                break;
        }

        if (from != null)
        {
            from.RenderTransform = Matrix3x2.CreateTranslation(fromX, fromY);
        }

        if (to != null)
        {
            to.RenderTransform = Matrix3x2.CreateTranslation(toX, toY);
        }
    }
}

/// <summary>
/// Zoom scaling mode for <see cref="ZoomTransition"/>.
/// </summary>
public enum ZoomMode
{
    /// <summary>Incoming content scales up from a smaller size (zoom in).</summary>
    In,
    /// <summary>Incoming content scales down from a larger size (zoom out).</summary>
    Out
}

/// <summary>
/// A transition that scales content while simultaneously fading opacity.
/// </summary>
public class ZoomTransition : TransitionBase
{
    public ZoomMode Mode { get; set; }
    public float ScaleOffset { get; set; } = 0.20f;

    public ZoomTransition(ZoomMode mode = ZoomMode.In, TimeSpan? duration = null, Func<float, float>? easing = null)
        : base(duration ?? TimeSpan.FromMilliseconds(300), easing ?? Atelier.Core.Animation.Easing.Emphasized)
    {
        Mode = mode;
    }

    public static ZoomTransition In(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(ZoomMode.In, duration, easing);

    public static ZoomTransition Out(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(ZoomMode.Out, duration, easing);

    public override void Apply(UIElement? from, UIElement? to, float progress, Size bounds)
    {
        float fromScale, toScale;

        if (Mode == ZoomMode.In)
        {
            // Outgoing zooms slightly larger while fading
            fromScale = 1.0f + (ScaleOffset * progress);
            // Incoming starts smaller and scales to 1.0
            toScale = (1.0f - ScaleOffset) + (ScaleOffset * progress);
        }
        else
        {
            // Outgoing shrinks while fading
            fromScale = 1.0f - (ScaleOffset * progress);
            // Incoming starts larger and scales to 1.0
            toScale = (1.0f + ScaleOffset) - (ScaleOffset * progress);
        }

        if (from != null)
        {
            from.RenderTransformOrigin = new Point(0.5f, 0.5f);
            from.RenderTransform = Matrix3x2.CreateScale(fromScale);
            from.Opacity = Math.Clamp(1.0f - progress, 0.0f, 1.0f);
        }

        if (to != null)
        {
            to.RenderTransformOrigin = new Point(0.5f, 0.5f);
            to.RenderTransform = Matrix3x2.CreateScale(toScale);
            to.Opacity = Math.Clamp(progress, 0.0f, 1.0f);
        }
    }
}

/// <summary>
/// A transition combining directional translation with simultaneous opacity cross-fading for Material Design 3 motion.
/// </summary>
public class SlideFadeTransition : TransitionBase
{
    public SlideDirection Direction { get; set; }
    public float OffsetFraction { get; set; } = 0.35f;

    public SlideFadeTransition(SlideDirection direction = SlideDirection.Left, TimeSpan? duration = null, Func<float, float>? easing = null)
        : base(duration ?? TimeSpan.FromMilliseconds(300), easing ?? Atelier.Core.Animation.Easing.Emphasized)
    {
        Direction = direction;
    }

    public static SlideFadeTransition Left(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Left, duration, easing);

    public static SlideFadeTransition Right(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Right, duration, easing);

    public static SlideFadeTransition Up(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Up, duration, easing);

    public static SlideFadeTransition Down(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Down, duration, easing);

    public override void Apply(UIElement? from, UIElement? to, float progress, Size bounds)
    {
        float w = bounds.Width > 0 ? bounds.Width : 400f;
        float h = bounds.Height > 0 ? bounds.Height : 300f;
        float distanceX = w * OffsetFraction;
        float distanceY = h * OffsetFraction;

        float fromX = 0f, fromY = 0f;
        float toX = 0f, toY = 0f;

        switch (Direction)
        {
            case SlideDirection.Left:
                fromX = -distanceX * progress;
                toX = distanceX * (1.0f - progress);
                break;
            case SlideDirection.Right:
                fromX = distanceX * progress;
                toX = -distanceX * (1.0f - progress);
                break;
            case SlideDirection.Up:
                fromY = -distanceY * progress;
                toY = distanceY * (1.0f - progress);
                break;
            case SlideDirection.Down:
                fromY = distanceY * progress;
                toY = -distanceY * (1.0f - progress);
                break;
        }

        if (from != null)
        {
            from.RenderTransform = Matrix3x2.CreateTranslation(fromX, fromY);
            from.Opacity = Math.Clamp(1.0f - progress, 0.0f, 1.0f);
        }

        if (to != null)
        {
            to.RenderTransform = Matrix3x2.CreateTranslation(toX, toY);
            to.Opacity = Math.Clamp(progress, 0.0f, 1.0f);
        }
    }
}

/// <summary>
/// A composite transition that runs multiple <see cref="ITransition"/> instances simultaneously.
/// </summary>
public class CompositeTransition : TransitionBase
{
    private readonly List<ITransition> _transitions = new();

    public IReadOnlyList<ITransition> Transitions => _transitions;

    public CompositeTransition(params ITransition[] transitions)
        : base(transitions.Length > 0 ? transitions[0].Duration : TimeSpan.FromMilliseconds(300),
               transitions.Length > 0 ? transitions[0].Easing : Atelier.Core.Animation.Easing.Emphasized)
    {
        _transitions.AddRange(transitions);
    }

    public CompositeTransition(IEnumerable<ITransition> transitions, TimeSpan? duration = null, Func<float, float>? easing = null)
        : base(duration ?? TimeSpan.FromMilliseconds(300), easing ?? Atelier.Core.Animation.Easing.Emphasized)
    {
        _transitions.AddRange(transitions);
    }

    public override void Apply(UIElement? from, UIElement? to, float progress, Size bounds)
    {
        for (int i = 0; i < _transitions.Count; i++)
        {
            _transitions[i].Apply(from, to, progress, bounds);
        }
    }

    public override void Reset(UIElement? from, UIElement? to)
    {
        base.Reset(from, to);
        for (int i = 0; i < _transitions.Count; i++)
        {
            _transitions[i].Reset(from, to);
        }
    }
}
