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
    /// <summary>
    /// Initializes a new instance of the <see cref="FadeTransition"/> class.
    /// </summary>
    /// <param name="duration">The duration; defaults to 250 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Linear"/>.</param>
    public FadeTransition(TimeSpan? duration = null, Func<float, float>? easing = null)
        : base(duration ?? TimeSpan.FromMilliseconds(250), easing ?? Atelier.Core.Animation.Easing.Linear)
    {
    }

    /// <summary>
    /// Creates a <see cref="FadeTransition"/>; equivalent to calling the constructor.
    /// </summary>
    /// <param name="duration">The duration; defaults to 250 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Linear"/>.</param>
    /// <returns>A new fade transition.</returns>
    public static FadeTransition Default(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(duration, easing);

    /// <inheritdoc/>
    /// <remarks>Sets the outgoing element's opacity to <c>1 - progress</c> and the incoming element's to <c>progress</c>.</remarks>
    public override void Apply(UIElement? from, UIElement? to, float progress, Size bounds)
    {
        if (from != null)
        {
            from.SetAnimatedValue(UIElement.OpacityProperty, Math.Clamp(1.0f - progress, 0.0f, 1.0f));
        }

        if (to != null)
        {
            to.SetAnimatedValue(UIElement.OpacityProperty, Math.Clamp(progress, 0.0f, 1.0f));
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
    /// <summary>
    /// Gets or sets the direction the content moves in.
    /// </summary>
    public SlideDirection Direction { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SlideTransition"/> class.
    /// </summary>
    /// <param name="direction">The direction the content moves in.</param>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    public SlideTransition(SlideDirection direction = SlideDirection.Left, TimeSpan? duration = null, Func<float, float>? easing = null)
        : base(duration ?? TimeSpan.FromMilliseconds(300), easing ?? Atelier.Core.Animation.Easing.Emphasized)
    {
        Direction = direction;
    }

    /// <summary>
    /// Creates a slide transition in the <see cref="SlideDirection.Left"/> direction.
    /// </summary>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    /// <returns>A new slide transition.</returns>
    public static SlideTransition Left(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Left, duration, easing);

    /// <summary>
    /// Creates a slide transition in the <see cref="SlideDirection.Right"/> direction.
    /// </summary>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    /// <returns>A new slide transition.</returns>
    public static SlideTransition Right(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Right, duration, easing);

    /// <summary>
    /// Creates a slide transition in the <see cref="SlideDirection.Up"/> direction.
    /// </summary>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    /// <returns>A new slide transition.</returns>
    public static SlideTransition Up(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Up, duration, easing);

    /// <summary>
    /// Creates a slide transition in the <see cref="SlideDirection.Down"/> direction.
    /// </summary>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    /// <returns>A new slide transition.</returns>
    public static SlideTransition Down(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Down, duration, easing);

    /// <inheritdoc/>
    /// <remarks>
    /// Translates both elements by the full width or height of <paramref name="bounds"/> through the render transform.
    /// A non-positive width or height falls back to 400 or 300 pixels.
    /// </remarks>
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
            from.SetAnimatedValue(VisualNode.RenderTransformProperty, Matrix3x2.CreateTranslation(fromX, fromY));
        }

        if (to != null)
        {
            to.SetAnimatedValue(VisualNode.RenderTransformProperty, Matrix3x2.CreateTranslation(toX, toY));
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
    /// <summary>
    /// Gets or sets whether the content zooms in or out.
    /// </summary>
    public ZoomMode Mode { get; set; }
    /// <summary>
    /// Gets or sets the scale difference between the start and end of the transition; defaults to 0.2 (20%).
    /// </summary>
    public float ScaleOffset { get; set; } = 0.20f;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZoomTransition"/> class.
    /// </summary>
    /// <param name="mode">Whether the content zooms in or out.</param>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    public ZoomTransition(ZoomMode mode = ZoomMode.In, TimeSpan? duration = null, Func<float, float>? easing = null)
        : base(duration ?? TimeSpan.FromMilliseconds(300), easing ?? Atelier.Core.Animation.Easing.Emphasized)
    {
        Mode = mode;
    }

    /// <summary>
    /// Creates a zoom transition in <see cref="ZoomMode.In"/> mode.
    /// </summary>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    /// <returns>A new zoom transition.</returns>
    public static ZoomTransition In(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(ZoomMode.In, duration, easing);

    /// <summary>
    /// Creates a zoom transition in <see cref="ZoomMode.Out"/> mode.
    /// </summary>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    /// <returns>A new zoom transition.</returns>
    public static ZoomTransition Out(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(ZoomMode.Out, duration, easing);

    /// <inheritdoc/>
    /// <remarks>
    /// Scales both elements around their center through the render transform while cross-fading their opacity.
    /// In <see cref="ZoomMode.In"/> mode the outgoing element grows to <c>1 + ScaleOffset</c> and the incoming element
    /// grows from <c>1 - ScaleOffset</c>; <see cref="ZoomMode.Out"/> reverses both.
    /// </remarks>
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
            from.SetAnimatedValue(VisualNode.RenderTransformOriginProperty, new Point(0.5f, 0.5f));
            from.SetAnimatedValue(VisualNode.RenderTransformProperty, Matrix3x2.CreateScale(fromScale));
            from.SetAnimatedValue(UIElement.OpacityProperty, Math.Clamp(1.0f - progress, 0.0f, 1.0f));
        }

        if (to != null)
        {
            to.SetAnimatedValue(VisualNode.RenderTransformOriginProperty, new Point(0.5f, 0.5f));
            to.SetAnimatedValue(VisualNode.RenderTransformProperty, Matrix3x2.CreateScale(toScale));
            to.SetAnimatedValue(UIElement.OpacityProperty, Math.Clamp(progress, 0.0f, 1.0f));
        }
    }
}

/// <summary>
/// A transition combining directional translation with simultaneous opacity cross-fading for Material Design 3 motion.
/// </summary>
public class SlideFadeTransition : TransitionBase
{
    /// <summary>
    /// Gets or sets the direction the content moves in.
    /// </summary>
    public SlideDirection Direction { get; set; }
    /// <summary>
    /// Gets or sets the slide distance as a fraction of the host's width or height; defaults to 0.35.
    /// </summary>
    public float OffsetFraction { get; set; } = 0.35f;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlideFadeTransition"/> class.
    /// </summary>
    /// <param name="direction">The direction the content moves in.</param>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    public SlideFadeTransition(SlideDirection direction = SlideDirection.Left, TimeSpan? duration = null, Func<float, float>? easing = null)
        : base(duration ?? TimeSpan.FromMilliseconds(300), easing ?? Atelier.Core.Animation.Easing.Emphasized)
    {
        Direction = direction;
    }

    /// <summary>
    /// Creates a slide-fade transition in the <see cref="SlideDirection.Left"/> direction.
    /// </summary>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    /// <returns>A new slide-fade transition.</returns>
    public static SlideFadeTransition Left(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Left, duration, easing);

    /// <summary>
    /// Creates a slide-fade transition in the <see cref="SlideDirection.Right"/> direction.
    /// </summary>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    /// <returns>A new slide-fade transition.</returns>
    public static SlideFadeTransition Right(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Right, duration, easing);

    /// <summary>
    /// Creates a slide-fade transition in the <see cref="SlideDirection.Up"/> direction.
    /// </summary>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    /// <returns>A new slide-fade transition.</returns>
    public static SlideFadeTransition Up(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Up, duration, easing);

    /// <summary>
    /// Creates a slide-fade transition in the <see cref="SlideDirection.Down"/> direction.
    /// </summary>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    /// <returns>A new slide-fade transition.</returns>
    public static SlideFadeTransition Down(TimeSpan? duration = null, Func<float, float>? easing = null) =>
        new(SlideDirection.Down, duration, easing);

    /// <inheritdoc/>
    /// <remarks>
    /// Translates both elements by <see cref="OffsetFraction"/> of the width or height of <paramref name="bounds"/>
    /// while cross-fading their opacity. A non-positive width or height falls back to 400 or 300 pixels.
    /// </remarks>
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
            from.SetAnimatedValue(VisualNode.RenderTransformProperty, Matrix3x2.CreateTranslation(fromX, fromY));
            from.SetAnimatedValue(UIElement.OpacityProperty, Math.Clamp(1.0f - progress, 0.0f, 1.0f));
        }

        if (to != null)
        {
            to.SetAnimatedValue(VisualNode.RenderTransformProperty, Matrix3x2.CreateTranslation(toX, toY));
            to.SetAnimatedValue(UIElement.OpacityProperty, Math.Clamp(progress, 0.0f, 1.0f));
        }
    }
}

/// <summary>
/// A composite transition that runs multiple <see cref="ITransition"/> instances simultaneously.
/// </summary>
public class CompositeTransition : TransitionBase
{
    private readonly List<ITransition> _transitions = new();

    /// <summary>
    /// Gets the child transitions, in the order they are applied.
    /// </summary>
    public IReadOnlyList<ITransition> Transitions => _transitions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeTransition"/> class that takes its duration and easing
    /// from the first child transition.
    /// </summary>
    /// <remarks>
    /// With no children the duration is 300 ms and the easing is <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.
    /// The durations and easings of the other children are ignored; all children receive the same progress.
    /// </remarks>
    /// <param name="transitions">The child transitions.</param>
    public CompositeTransition(params ITransition[] transitions)
        : base(transitions.Length > 0 ? transitions[0].Duration : TimeSpan.FromMilliseconds(300),
               transitions.Length > 0 ? transitions[0].Easing : Atelier.Core.Animation.Easing.Emphasized)
    {
        _transitions.AddRange(transitions);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeTransition"/> class with an explicit duration and easing.
    /// </summary>
    /// <remarks>The durations and easings of the children are ignored; all children receive the same progress.</remarks>
    /// <param name="transitions">The child transitions.</param>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    public CompositeTransition(IEnumerable<ITransition> transitions, TimeSpan? duration = null, Func<float, float>? easing = null)
        : base(duration ?? TimeSpan.FromMilliseconds(300), easing ?? Atelier.Core.Animation.Easing.Emphasized)
    {
        _transitions.AddRange(transitions);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Applies each child in order with the same arguments; when children write the same property, the last one wins.
    /// </remarks>
    public override void Apply(UIElement? from, UIElement? to, float progress, Size bounds)
    {
        for (int i = 0; i < _transitions.Count; i++)
        {
            _transitions[i].Apply(from, to, progress, bounds);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Clears the built-in animated values and then calls <see cref="ITransition.Reset"/> on each child.</remarks>
    public override void Reset(UIElement? from, UIElement? to)
    {
        base.Reset(from, to);
        for (int i = 0; i < _transitions.Count; i++)
        {
            _transitions[i].Reset(from, to);
        }
    }
}
