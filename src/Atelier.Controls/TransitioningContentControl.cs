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
public class TransitioningContentControl : ContentControl
{
    private static AnimationClock? _globalClock;

    /// <summary>
    /// Sets the application-wide animation clock used by default when no local clock is configured.
    /// </summary>
    public static void SetGlobalAnimationClock(AnimationClock? clock) => _globalClock = clock;

    public static readonly BindableProperty<ITransition?> TransitionProperty =
        BindableProperty.Register<TransitioningContentControl, ITransition?>(
            nameof(Transition),
            null
        );

    public static readonly BindableProperty<TimeSpan?> DurationProperty =
        BindableProperty.Register<TransitioningContentControl, TimeSpan?>(
            nameof(Duration),
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
    /// Gets or sets an optional local <see cref="AnimationClock"/> driving this control's transitions.
    /// When null, falls back to the globally registered clock.
    /// </summary>
    public AnimationClock? Clock { get; set; }

    /// <summary>
    /// Gets a value indicating whether a transition animation is actively in progress.
    /// </summary>
    public bool IsTransitioning => _activeAnimation != null && _previousView != null;

    /// <summary>
    /// Occurs when a content transition begins.
    /// </summary>
    public event EventHandler? TransitionStarted;

    /// <summary>
    /// Occurs when a content transition finishes.
    /// </summary>
    public event EventHandler? TransitionCompleted;

    protected UIElement? _previousView;
    private IAnimation? _activeAnimation;
    private ITransition? _activeTransition;

    public TransitioningContentControl()
    {
        // Clip children by default so sliding or zooming visuals do not spill outside bounds
        ClipToBounds = true;
    }

    public TransitioningContentControl(object? content) : this()
    {
        Content = content;
    }

    public TransitioningContentControl(object? content, ITransition? transition) : this(content)
    {
        Transition = transition;
    }

    protected override void OnContentChanged(object? oldContent, object? newContent)
    {
        var clock = Clock ?? _globalClock;
        var transition = Transition;

        // If no transition is configured, no animation clock is ticking, or there is no existing view,
        // fall back to instantaneous content swap (same as standard ContentControl).
        if (transition == null || clock == null || _currentView == null)
        {
            CancelCurrentTransition();
            base.OnContentChanged(oldContent, newContent);
            return;
        }

        // Cancel and finalize any active transition currently in progress (e.g. rapid content switches)
        if (_activeAnimation != null)
        {
            if (_previousView != null)
            {
                _activeTransition?.Reset(_previousView, null);
                RemoveChild(_previousView);
                _previousView.IsHitTestVisible = true;
                _previousView = null;
            }
            _activeAnimation = null;
        }

        // Promote the current visual element to outgoing previous view
        _previousView = _currentView;
        _previousView.IsHitTestVisible = false;

        // Resolve and mount the new incoming view
        var newView = ResolveContentView(newContent);
        _currentView = newView;

        if (_currentView != null)
        {
            AddChild(_currentView);
        }
        else
        {
            // If new content resolved to null, finish immediately
            if (_previousView != null)
            {
                RemoveChild(_previousView);
                _previousView.IsHitTestVisible = true;
                _previousView = null;
            }
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        _activeTransition = transition;
        var effectiveDuration = Duration ?? transition.Duration;
        var effectiveEasing = transition.Easing ?? Atelier.Core.Animation.Easing.Emphasized;

        // Apply starting frame (progress = 0)
        var contentSize = Bounds.Size;
        if (contentSize.Width <= 0 || contentSize.Height <= 0)
        {
            contentSize = DesiredSize;
        }

        _activeTransition.Apply(_previousView, _currentView, 0.0f, contentSize);

        TransitionStarted?.Invoke(this, EventArgs.Empty);

        var anim = new FloatAnimation(
            from: 0.0f,
            to: 1.0f,
            duration: effectiveDuration,
            easing: effectiveEasing,
            onUpdate: progress =>
            {
                var currentSize = Bounds.Size;
                if (currentSize.Width <= 0 || currentSize.Height <= 0)
                {
                    currentSize = DesiredSize;
                }
                _activeTransition?.Apply(_previousView, _currentView, progress, currentSize);
                InvalidateVisual();
            },
            onCompleted: EndTransition
        );

        _activeAnimation = anim;
        clock.Add(anim);

        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>
    /// Cancels any running transition and removes the outgoing visual immediately.
    /// </summary>
    public void CancelCurrentTransition()
    {
        if (_activeAnimation != null)
        {
            _activeAnimation = null;
        }

        if (_previousView != null)
        {
            _activeTransition?.Reset(_previousView, null);
            RemoveChild(_previousView);
            _previousView.IsHitTestVisible = true;
            _previousView = null;
        }

        if (_currentView != null)
        {
            _activeTransition?.Reset(null, _currentView);
        }

        _activeTransition = null;
    }

    private void EndTransition()
    {
        _activeAnimation = null;

        if (_previousView != null)
        {
            _activeTransition?.Reset(_previousView, null);
            RemoveChild(_previousView);
            _previousView.IsHitTestVisible = true;
            _previousView = null;
        }

        if (_currentView != null)
        {
            _activeTransition?.Reset(null, _currentView);
        }

        _activeTransition = null;

        TransitionCompleted?.Invoke(this, EventArgs.Empty);

        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var contentArea = availableSize.Deflate(padding);
        Size maxDesired = Size.Zero;

        if (_currentView != null && _currentView.Visibility != Visibility.Collapsed)
        {
            _currentView.Measure(contentArea);
            maxDesired = _currentView.DesiredSize;
        }

        if (_previousView != null && _previousView.Visibility != Visibility.Collapsed)
        {
            _previousView.Measure(contentArea);
            maxDesired = new Size(
                Math.Max(maxDesired.Width, _previousView.DesiredSize.Width),
                Math.Max(maxDesired.Height, _previousView.DesiredSize.Height)
            );
        }

        return maxDesired.Inflate(padding.Horizontal, padding.Vertical);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = Padding;
        var contentRect = new Rect(Point.Zero, finalSize).Deflate(padding);

        if (_currentView != null && _currentView.Visibility != Visibility.Collapsed)
        {
            _currentView.Arrange(contentRect);
        }

        if (_previousView != null && _previousView.Visibility != Visibility.Collapsed)
        {
            _previousView.Arrange(contentRect);
        }

        return finalSize;
    }
}
