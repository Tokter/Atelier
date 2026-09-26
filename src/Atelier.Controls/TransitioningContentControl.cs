using System;
using Atelier.Core.Animation;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// A <see cref="ContentControl"/> that animates transitions between outgoing and incoming content
/// using an <see cref="ITransition"/> (such as slide, fade, zoom, or composite motions).
/// </summary>
/// <remarks>
/// A transition runs when <see cref="ContentControl.Content"/> changes while a view is shown, a <see cref="Transition"/>
/// is set and an <see cref="AnimationClock"/> is available (<see cref="Clock"/> or the global clock); otherwise the
/// content is swapped instantly. Changing the content during a transition stops the running one (the outgoing view is
/// removed at once) and starts a new transition from the view that was coming in. Changing
/// <see cref="ContentControl.ContentTemplate"/> or <see cref="ContentControl.ViewLocator"/> cancels the running transition.
/// </remarks>
public class TransitioningContentControl : ContentControl
{
    private static AnimationClock? _globalClock;

    /// <summary>
    /// Sets the application-wide animation clock used by default when no local clock is configured.
    /// </summary>
    /// <param name="clock">The clock, or <c>null</c> to disable transitions for controls without a local clock.</param>
    public static void SetGlobalAnimationClock(AnimationClock? clock) => _globalClock = clock;

    /// <summary>Identifies the <see cref="Transition"/> property.</summary>
    public static readonly BindableProperty<ITransition?> TransitionProperty =
        BindableProperty.Register<TransitioningContentControl, ITransition?>(
            nameof(Transition),
            null
        );

    /// <summary>Identifies the <see cref="Duration"/> property.</summary>
    public static readonly BindableProperty<TimeSpan?> DurationProperty =
        BindableProperty.Register<TransitioningContentControl, TimeSpan?>(
            nameof(Duration),
            null
        );

    /// <summary>Identifies the <see cref="Easing"/> property.</summary>
    public static readonly BindableProperty<Func<float, float>?> EasingProperty =
        BindableProperty.Register<TransitioningContentControl, Func<float, float>?>(
            nameof(Easing),
            null
        );

    /// <summary>
    /// Gets or sets the transition strategy applied when <see cref="ContentControl.Content"/> changes.
    /// </summary>
    public ITransition? Transition
    {
        get => GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional duration override for the transition.
    /// If null, the duration specified on the <see cref="Transition"/> is used.
    /// </summary>
    public TimeSpan? Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional easing override for the transition (see <see cref="Atelier.Core.Animation.Easing"/>).
    /// If null, the <see cref="Transition"/>'s easing is used, or <see cref="Atelier.Core.Animation.Easing.Emphasized"/>
    /// when it has none.
    /// </summary>
    public Func<float, float>? Easing
    {
        get => GetValue(EasingProperty);
        set => SetValue(EasingProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional local <see cref="AnimationClock"/> driving this control's transitions.
    /// When null, falls back to the globally registered clock.
    /// </summary>
    public AnimationClock? Clock { get; set; }

    /// <summary>
    /// Gets a value indicating whether a transition animation is actively in progress.
    /// </summary>
    public bool IsTransitioning => _activeAnimation != null && PreviousView != null;

    /// <summary>
    /// Occurs when a content transition begins.
    /// </summary>
    public event EventHandler? TransitionStarted;

    /// <summary>
    /// Occurs when a content transition finishes, or is completed early with <see cref="CompleteCurrentTransition"/>.
    /// Not raised for a transition that is interrupted by another content change or canceled with
    /// <see cref="CancelCurrentTransition"/>.
    /// </summary>
    public event EventHandler? TransitionCompleted;

    /// <summary>Gets the outgoing view while a transition runs, or <c>null</c>.</summary>
    protected UIElement? PreviousView { get; private set; }

    private object? _previousContent;
    private FloatAnimation? _activeAnimation;
    private ITransition? _activeTransition;

    // Incremented whenever a transition starts or ends, so callbacks of a stopped animation are ignored.
    private int _generation;

    static TransitioningContentControl()
    {
        // Clip children by default so sliding or zooming visuals do not spill outside bounds
        ClipToBoundsProperty.OverrideDefaultValue<TransitioningContentControl>(true);
    }

    /// <summary>Initializes a new, empty <see cref="TransitioningContentControl"/> without a transition.</summary>
    public TransitioningContentControl()
    {
    }

    /// <summary>Initializes a new <see cref="TransitioningContentControl"/> showing <paramref name="content"/>.</summary>
    /// <param name="content">The initial content (shown without a transition).</param>
    public TransitioningContentControl(object? content) : this()
    {
        Content = content;
    }

    /// <summary>Initializes a new <see cref="TransitioningContentControl"/> with initial content and a transition.</summary>
    /// <param name="content">The initial content (shown without a transition).</param>
    /// <param name="transition">The transition used for later content changes.</param>
    public TransitioningContentControl(object? content, ITransition? transition) : this(content)
    {
        Transition = transition;
    }

    /// <inheritdoc/>
    /// <remarks>Starts a transition from the current view to the new content's view, or swaps instantly (see the class remarks).</remarks>
    protected override void OnContentChanged(object? oldContent, object? newContent)
    {
        var clock = Clock ?? _globalClock;
        var transition = Transition;

        // If no transition is configured, no animation clock is ticking, or there is no existing view,
        // fall back to instantaneous content swap (same as standard ContentControl).
        if (transition == null || clock == null || CurrentView == null)
        {
            base.OnContentChanged(oldContent, newContent);
            return;
        }

        // Finish any transition in progress (rapid content switches): its outgoing view goes away now.
        CancelCurrentTransition();

        var outgoing = CurrentView;
        var outgoingContent = CurrentViewContent;
        var newView = ResolveContentView(newContent);

        if (newView == null || ReferenceEquals(newView, outgoing))
        {
            // Nothing to transition to (null content), or the same view shows the new content.
            if (newView == null)
            {
                SetCurrentView(null, null);
                ReleaseView(outgoing, outgoingContent);
            }
            else
            {
                SetCurrentView(newView, newContent);
            }
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        // Promote the current visual element to outgoing previous view and mount the incoming one
        PreviousView = outgoing;
        _previousContent = outgoingContent;
        outgoing.IsHitTestVisible = false;

        SetCurrentView(newView, newContent);
        AddChild(newView);

        _activeTransition = transition;
        var effectiveDuration = Duration ?? transition.Duration;
        var effectiveEasing = Easing ?? transition.Easing ?? Atelier.Core.Animation.Easing.Emphasized;

        // Apply starting frame (progress = 0)
        transition.Apply(outgoing, newView, 0.0f, GetTransitionSize());

        int generation = ++_generation;
        var anim = new FloatAnimation(
            from: 0.0f,
            to: 1.0f,
            duration: effectiveDuration,
            easing: effectiveEasing,
            onUpdate: progress =>
            {
                if (generation != _generation) return;
                _activeTransition?.Apply(PreviousView, CurrentView, progress, GetTransitionSize());
                InvalidateVisual();
            },
            onCompleted: () =>
            {
                if (generation == _generation)
                {
                    EndTransition(raiseCompleted: true);
                }
            }
        );

        _activeAnimation = anim;
        clock.Add(anim);

        InvalidateMeasure();
        InvalidateVisual();

        TransitionStarted?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    /// <remarks>Cancels a running transition first, so its outgoing view is reset and removed.</remarks>
    protected override void UpdateContentDisplay()
    {
        CancelCurrentTransition();
        base.UpdateContentDisplay();
    }

    private Size GetTransitionSize()
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            size = DesiredSize;
        }
        return size;
    }

    /// <summary>
    /// Cancels any running transition: stops its animation, removes the outgoing view immediately and resets both views
    /// to their untransformed state. Raises no event.
    /// </summary>
    public void CancelCurrentTransition()
    {
        if (_activeAnimation == null && PreviousView == null && _activeTransition == null)
        {
            return;
        }

        EndTransition(raiseCompleted: false);
    }

    /// <summary>
    /// Jumps a running transition to its end state: like <see cref="CancelCurrentTransition"/>, but raises
    /// <see cref="TransitionCompleted"/>. Does nothing when no transition is running.
    /// </summary>
    public void CompleteCurrentTransition()
    {
        if (_activeAnimation == null)
        {
            return;
        }

        EndTransition(raiseCompleted: true);
    }

    private void EndTransition(bool raiseCompleted)
    {
        _generation++;
        _activeAnimation?.Stop();
        _activeAnimation = null;

        var transition = _activeTransition;
        _activeTransition = null;

        var previous = PreviousView;
        if (previous != null)
        {
            var previousContent = _previousContent;
            PreviousView = null;
            _previousContent = null;
            transition?.Reset(previous, null);
            ReleaseView(previous, previousContent);
            previous.IsHitTestVisible = true;
        }

        var current = CurrentView;
        if (current != null)
        {
            transition?.Reset(null, current);
        }

        InvalidateMeasure();
        InvalidateVisual();

        if (raiseCompleted)
        {
            TransitionCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    /// <remarks>While a transition runs, the result covers both the incoming and the outgoing view.</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var contentArea = availableSize.Deflate(padding);
        Size maxDesired = Size.Zero;

        var current = CurrentView;
        if (current != null && current.Visibility != Visibility.Collapsed)
        {
            current.Measure(contentArea);
            maxDesired = current.DesiredSize;
        }

        var previous = PreviousView;
        if (previous != null && previous.Visibility != Visibility.Collapsed)
        {
            previous.Measure(contentArea);
            maxDesired = new Size(
                Math.Max(maxDesired.Width, previous.DesiredSize.Width),
                Math.Max(maxDesired.Height, previous.DesiredSize.Height)
            );
        }

        return maxDesired.Inflate(padding.Horizontal, padding.Vertical);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var contentRect = new Rect(Point.Zero, finalSize).Deflate(Padding);

        var current = CurrentView;
        if (current != null && current.Visibility != Visibility.Collapsed)
        {
            current.Arrange(GetContentSlot(current, contentRect));
        }

        var previous = PreviousView;
        if (previous != null && previous.Visibility != Visibility.Collapsed)
        {
            previous.Arrange(GetContentSlot(previous, contentRect));
        }

        return finalSize;
    }
}
