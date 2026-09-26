using System;
using System.Collections.Generic;
using Atelier.Controls;
using Atelier.Core.Animation;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Core.ViewResolution;
using Atelier.Layout;
using Xunit;

namespace Atelier.Tests;

public class ContentControlBehaviorTests
{
    private sealed class PageViewModel(string name)
    {
        public string Name { get; } = name;
        public override string ToString() => Name;
    }

    // Returns the same view instance per view model, like a caching view locator.
    private sealed class CachingLocator : IViewLocator
    {
        public readonly Dictionary<object, UIElement> Views = [];

        public bool CanResolve(object? data) => data is PageViewModel;

        public UIElement? ResolveView(object? data)
        {
            if (data is not PageViewModel vm) return null;
            if (!Views.TryGetValue(vm, out var view))
            {
                view = new Border { Width = 50, Height = 20 };
                Views[vm] = view;
            }
            return view;
        }
    }

    [Fact]
    public void ContentChange_DetachesOldView_AndClearsDataContextItSet()
    {
        var locator = new CachingLocator();
        var vmA = new PageViewModel("A");
        var vmB = new PageViewModel("B");
        var control = new ContentControl { ViewLocator = locator, Content = vmA };

        var viewA = control.CurrentView!;
        Assert.Same(vmA, viewA.DataContext);

        control.Content = vmB;

        Assert.Null(viewA.Parent);
        Assert.Null(viewA.DataContext); // the cached view no longer references the old view model
        Assert.Same(vmB, control.CurrentView!.DataContext);
    }

    [Fact]
    public void ContentChange_KeepsDataContextOfElementContent()
    {
        var vm = new object();
        var element = new Border { DataContext = vm };
        var control = new ContentControl { Content = element };

        control.Content = "text";

        Assert.Null(element.Parent);
        Assert.Same(vm, element.DataContext); // set by the user, not by the control
    }

    [Fact]
    public void ContentTemplate_ReturningNull_FallsBackToText()
    {
        var control = new ContentControl
        {
            ContentTemplate = _ => null,
            Content = new PageViewModel("Fallback")
        };

        var text = Assert.IsType<TextBlock>(control.CurrentView);
        Assert.Equal("Fallback", text.Text);
    }

    [Fact]
    public void ContentAlignment_PlacesViewInsidePaddedArea()
    {
        var view = new Border { Width = 40, Height = 20 };
        var control = new ContentControl
        {
            Content = view,
            Padding = new Thickness(10),
            HorizontalContentAlignment = HorizontalAlignment.Right,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        control.Measure(new Size(200, 100));
        control.Arrange(new Rect(0, 0, 200, 100));

        // Content area: (10, 10, 180, 80). Right: x = 10 + 180 - 40 = 150. Center: y = 10 + (80 - 20) / 2 = 40.
        Assert.Equal(new Rect(150, 40, 40, 20), view.Bounds);

        control.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        control.VerticalContentAlignment = VerticalAlignment.Top;
        control.Measure(new Size(200, 100));
        control.Arrange(new Rect(0, 0, 200, 100));

        Assert.Equal(10f, view.Bounds.Y);
        Assert.Equal(20f, view.Bounds.Height);
    }

    [Fact]
    public void ContentAlignment_DefaultStretch_FillsContentArea()
    {
        var view = new Border();
        var control = new ContentControl { Content = view, Padding = new Thickness(5) };

        control.Measure(new Size(100, 60));
        control.Arrange(new Rect(0, 0, 100, 60));

        Assert.Equal(new Rect(5, 5, 90, 50), view.Bounds);
    }

    private static TransitioningContentControl CreateTransitioning(AnimationClock clock, int durationMs = 400)
    {
        var control = new TransitioningContentControl
        {
            Clock = clock,
            Transition = new FadeTransition(TimeSpan.FromMilliseconds(durationMs), Easing.Linear),
            Content = "A"
        };
        control.Measure(new Size(200, 100));
        control.Arrange(new Rect(0, 0, 200, 100));
        return control;
    }

    [Fact]
    public void Transition_InterruptedMidway_SecondRunsFullDuration_CompletesOnce()
    {
        var clock = new AnimationClock();
        var control = CreateTransitioning(clock);
        int completed = 0;
        control.TransitionCompleted += (s, e) => completed++;

        var viewA = control.CurrentView!;
        control.Content = "B";
        var viewB = control.CurrentView!;
        clock.Update(0.2);
        Assert.Equal(0.5f, viewB.Opacity, 3);

        control.Content = "C";
        var viewC = control.CurrentView!;

        // The first transition ended at once: A is gone and reset, B is now the outgoing view.
        Assert.Null(viewA.Parent);
        Assert.Equal(ValueSource.Default, viewA.GetValueSource(UIElement.OpacityProperty));
        Assert.Equal(2, control.Children.Count);

        // 200 ms later the first animation would have finished; the second one must still be running.
        clock.Update(0.2);
        Assert.True(control.IsTransitioning);
        Assert.Equal(0, completed);
        Assert.Equal(0.5f, viewC.Opacity, 3);
        Assert.Equal(0.5f, viewB.Opacity, 3); // only the second transition writes B (outgoing: 1 - progress)

        clock.Update(0.2);
        Assert.False(control.IsTransitioning);
        Assert.Equal(1, completed);
        Assert.Null(viewB.Parent);
        Assert.Single(control.Children);
        Assert.Equal(1f, viewC.Opacity);

        clock.Update(0.5);
        Assert.Equal(1, completed);
        Assert.Equal(0, clock.ActiveAnimationCount);
    }

    [Fact]
    public void CancelCurrentTransition_ThenClockUpdate_RaisesNothing()
    {
        var clock = new AnimationClock();
        var control = CreateTransitioning(clock);
        int completed = 0;
        control.TransitionCompleted += (s, e) => completed++;

        var viewA = control.CurrentView!;
        control.Content = "B";
        var viewB = control.CurrentView!;
        clock.Update(0.1);

        control.CancelCurrentTransition();
        Assert.False(control.IsTransitioning);
        Assert.Null(viewA.Parent);
        Assert.Equal(ValueSource.Default, viewB.GetValueSource(UIElement.OpacityProperty));

        clock.Update(1.0);
        Assert.Equal(0, completed);
        Assert.Equal(ValueSource.Default, viewB.GetValueSource(UIElement.OpacityProperty));
        Assert.Equal(0, clock.ActiveAnimationCount);
    }

    [Fact]
    public void CompleteCurrentTransition_JumpsToEnd_AndRaisesCompletedOnce()
    {
        var clock = new AnimationClock();
        var control = CreateTransitioning(clock);
        int completed = 0;
        control.TransitionCompleted += (s, e) => completed++;

        control.Content = "B";
        control.CompleteCurrentTransition();

        Assert.Equal(1, completed);
        Assert.Single(control.Children);

        clock.Update(1.0);
        Assert.Equal(1, completed);
    }

    [Fact]
    public void ContentTemplateChange_DuringTransition_ResetsAndRemovesOutgoingView()
    {
        var clock = new AnimationClock();
        var control = CreateTransitioning(clock);
        int completed = 0;
        control.TransitionCompleted += (s, e) => completed++;

        var viewA = control.CurrentView!;
        control.Content = "B";
        clock.Update(0.1);
        Assert.Equal(ValueSource.Animation, viewA.GetValueSource(UIElement.OpacityProperty));

        control.ContentTemplate = c => new TextBlock("templated " + c);

        Assert.Null(viewA.Parent);
        Assert.Equal(ValueSource.Default, viewA.GetValueSource(UIElement.OpacityProperty));
        Assert.True(viewA.IsHitTestVisible);
        Assert.False(control.IsTransitioning);
        Assert.Single(control.Children);
        Assert.Equal("templated B", ((TextBlock)control.CurrentView!).Text);

        clock.Update(1.0);
        Assert.Equal(0, completed);
    }

    [Fact]
    public void EasingProperty_OverridesTransitionEasing()
    {
        var clock = new AnimationClock();
        var control = CreateTransitioning(clock);
        control.Easing = t => t * t;

        control.Content = "B";
        var viewB = control.CurrentView!;
        clock.Update(0.2); // progress 0.5 -> eased 0.25

        Assert.Equal(0.25f, viewB.Opacity, 3);
    }
}
