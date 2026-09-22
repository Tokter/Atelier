using System;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;

namespace Atelier.Core.Animation;

/// <summary>
/// Defines a transition strategy that animates visual changes between an outgoing element and an incoming element.
/// </summary>
public interface ITransition
{
    /// <summary>
    /// Gets the default duration of the transition.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Gets the easing function applied across the normalized progress.
    /// </summary>
    Func<float, float>? Easing { get; }

    /// <summary>
    /// Applies animation transforms, positions, or opacity values to the outgoing (<paramref name="from"/>)
    /// and incoming (<paramref name="to"/>) visual elements at normalized progress <paramref name="progress"/> (0.0 to 1.0).
    /// </summary>
    /// <param name="from">The outgoing element transitioning out, if any.</param>
    /// <param name="to">The incoming element transitioning in, if any.</param>
    /// <param name="progress">Normalized animation progress from 0.0 (start) to 1.0 (end).</param>
    /// <param name="bounds">The available content size of the host container.</param>
    void Apply(UIElement? from, UIElement? to, float progress, Size bounds);

    /// <summary>
    /// Restores any modified element properties (such as Opacity or RenderTransform) to their clean baseline states.
    /// </summary>
    /// <param name="from">The outgoing element, if any.</param>
    /// <param name="to">The incoming element, if any.</param>
    void Reset(UIElement? from, UIElement? to);
}

/// <summary>
/// Base class providing duration and easing properties for transition implementations.
/// </summary>
public abstract class TransitionBase : ITransition
{
    public TimeSpan Duration { get; set; }
    public Func<float, float>? Easing { get; set; }

    protected TransitionBase(TimeSpan? duration = null, Func<float, float>? easing = null)
    {
        Duration = duration ?? TimeSpan.FromMilliseconds(300);
        Easing = easing ?? Atelier.Core.Animation.Easing.Emphasized;
    }

    public abstract void Apply(UIElement? from, UIElement? to, float progress, Size bounds);

    public virtual void Reset(UIElement? from, UIElement? to)
    {
        if (from != null)
        {
            from.Opacity = 1.0f;
            from.RenderTransform = System.Numerics.Matrix3x2.Identity;
        }

        if (to != null)
        {
            to.Opacity = 1.0f;
            to.RenderTransform = System.Numerics.Matrix3x2.Identity;
        }
    }
}
