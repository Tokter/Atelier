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
    /// <remarks>
    /// Transitions should write through <see cref="Properties.BindableObject.SetAnimatedValue{T}(Properties.BindableProperty{T}, T)"/>
    /// so that resetting only has to clear the animation layer, which restores whatever value the element had before.
    /// </remarks>
    /// <param name="from">The outgoing element, if any.</param>
    /// <param name="to">The incoming element, if any.</param>
    void Reset(UIElement? from, UIElement? to);
}

/// <summary>
/// Base class providing duration and easing properties for transition implementations.
/// </summary>
public abstract class TransitionBase : ITransition
{
    /// <inheritdoc/>
    public TimeSpan Duration { get; set; }
    /// <inheritdoc/>
    public Func<float, float>? Easing { get; set; }

    /// <summary>
    /// Initializes the transition with the given duration and easing.
    /// </summary>
    /// <param name="duration">The duration; defaults to 300 ms.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Atelier.Core.Animation.Easing.Emphasized"/>.</param>
    protected TransitionBase(TimeSpan? duration = null, Func<float, float>? easing = null)
    {
        Duration = duration ?? TimeSpan.FromMilliseconds(300);
        Easing = easing ?? Atelier.Core.Animation.Easing.Emphasized;
    }

    /// <inheritdoc/>
    public abstract void Apply(UIElement? from, UIElement? to, float progress, Size bounds);

    /// <inheritdoc/>
    /// <remarks>
    /// The base implementation clears the animated opacity, render transform and render transform origin of both
    /// elements (see <see cref="ClearAnimatedValues"/>).
    /// </remarks>
    public virtual void Reset(UIElement? from, UIElement? to)
    {
        ClearAnimatedValues(from);
        ClearAnimatedValues(to);
    }

    /// <summary>
    /// Clears the animated values the built-in transitions write, restoring the element's own values.
    /// </summary>
    /// <param name="element">The element to restore, if any.</param>
    protected static void ClearAnimatedValues(UIElement? element)
    {
        if (element == null)
        {
            return;
        }

        element.ClearAnimatedValue(UIElement.OpacityProperty);
        element.ClearAnimatedValue(VisualNode.RenderTransformProperty);
        element.ClearAnimatedValue(VisualNode.RenderTransformOriginProperty);
    }
}
