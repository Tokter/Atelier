using System;
using System.Numerics;
using Atelier.Controls;
using Atelier.Core.Animation;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Xunit;

namespace Atelier.Tests;

public class TransitioningContentControlTests
{
    [Fact]
    public void Defaults_ClipToBoundsIsTrue_AndTransitionIsNull()
    {
        var control = new TransitioningContentControl();

        Assert.True(control.ClipToBounds);
        Assert.Null(control.Transition);
        Assert.Null(control.Duration);
        Assert.False(control.IsTransitioning);
    }

    [Fact]
    public void WithoutTransition_InstantlySwapsContent()
    {
        var control = new TransitioningContentControl();
        control.Content = "Initial Content";

        Assert.NotNull(control.CurrentView);
        Assert.Single(control.Children);

        control.Content = "Updated Content";

        Assert.NotNull(control.CurrentView);
        Assert.Single(control.Children);
        Assert.False(control.IsTransitioning);
    }

    [Fact]
    public void WithTransitionAndClock_MountsBothChildrenDuringTransition()
    {
        var clock = new AnimationClock();
        var control = new TransitioningContentControl
        {
            Clock = clock,
            Transition = new FadeTransition(TimeSpan.FromMilliseconds(200)),
            Content = "First"
        };

        Assert.Single(control.Children);
        var firstView = control.CurrentView;
        Assert.NotNull(firstView);

        // Switch content
        control.Content = "Second";

        Assert.True(control.IsTransitioning);
        Assert.Equal(2, control.Children.Count);
        Assert.False(firstView.IsHitTestVisible);
        Assert.Equal("Second", (control.CurrentView as TextBlock)?.Text);
    }

    [Fact]
    public void AnimationClockProgression_UpdatesAndFinalizesTransition()
    {
        var clock = new AnimationClock();
        bool startedFired = false;
        bool completedFired = false;

        var control = new TransitioningContentControl
        {
            Clock = clock,
            Transition = new FadeTransition(TimeSpan.FromMilliseconds(200), Easing.Linear),
            Content = "Page A"
        };

        control.TransitionStarted += (s, e) => startedFired = true;
        control.TransitionCompleted += (s, e) => completedFired = true;

        var viewA = control.CurrentView;
        control.Content = "Page B";

        Assert.True(startedFired);
        Assert.False(completedFired);
        Assert.True(control.IsTransitioning);

        // Advance halfway (100ms)
        clock.Update(0.100);

        Assert.True(control.IsTransitioning);
        Assert.True(viewA!.Opacity < 1.0f && viewA.Opacity > 0.0f);
        Assert.True(control.CurrentView!.Opacity > 0.0f && control.CurrentView.Opacity < 1.0f);

        // Advance to end (another 150ms)
        clock.Update(0.150);

        Assert.True(completedFired);
        Assert.False(control.IsTransitioning);
        Assert.Single(control.Children);
        Assert.Equal(1.0f, control.CurrentView.Opacity);
        Assert.True(viewA.IsHitTestVisible); // Reset upon completion
    }

    [Fact]
    public void RapidContentSwitches_CleanlyAbortsAndLeavesNoOrphanedChildren()
    {
        var clock = new AnimationClock();
        var control = new TransitioningContentControl
        {
            Clock = clock,
            Transition = new SlideTransition(SlideDirection.Left, TimeSpan.FromMilliseconds(300)),
            Content = "Step 1"
        };

        // Fire 4 rapid switches without advancing clock to completion
        control.Content = "Step 2";
        control.Content = "Step 3";
        control.Content = "Step 4";

        Assert.Equal(2, control.Children.Count);
        Assert.True(control.IsTransitioning);

        // Complete the animation
        clock.Update(0.500);

        Assert.False(control.IsTransitioning);
        Assert.Single(control.Children);
        Assert.Equal("Step 4", (control.CurrentView as TextBlock)?.Text);
    }

    [Theory]
    [InlineData(SlideDirection.Left, -400f, 0f, 0f, 0f)]
    [InlineData(SlideDirection.Right, 400f, 0f, 0f, 0f)]
    [InlineData(SlideDirection.Up, 0f, -300f, 0f, 0f)]
    [InlineData(SlideDirection.Down, 0f, 300f, 0f, 0f)]
    public void SlideTransition_CalculatesExpectedTranslationMatrices(
        SlideDirection direction,
        float expectedFromX,
        float expectedFromY,
        float expectedToX,
        float expectedToY)
    {
        var slide = new SlideTransition(direction, TimeSpan.FromMilliseconds(300));
        var from = new TextBlock("From");
        var to = new TextBlock("To");
        var bounds = new Size(400, 300);

        // Progress = 1.0 (completed state)
        slide.Apply(from, to, 1.0f, bounds);

        Assert.Equal(expectedFromX, from.RenderTransform.M31, tolerance: 0.1f);
        Assert.Equal(expectedFromY, from.RenderTransform.M32, tolerance: 0.1f);
        Assert.Equal(expectedToX, to.RenderTransform.M31, tolerance: 0.1f);
        Assert.Equal(expectedToY, to.RenderTransform.M32, tolerance: 0.1f);

        // Reset
        slide.Reset(from, to);
        Assert.True(from.RenderTransform.IsIdentity);
        Assert.True(to.RenderTransform.IsIdentity);
    }

    [Fact]
    public void ZoomTransition_ScalesAndCentersTransformOrigin()
    {
        var zoom = ZoomTransition.In(TimeSpan.FromMilliseconds(300));
        var from = new TextBlock("From");
        var to = new TextBlock("To");
        var bounds = new Size(200, 200);

        // Progress = 0.5 (halfway)
        zoom.Apply(from, to, 0.5f, bounds);

        Assert.Equal(0.5f, from.RenderTransformOrigin.X);
        Assert.Equal(0.5f, from.RenderTransformOrigin.Y);
        Assert.Equal(0.5f, to.RenderTransformOrigin.X);
        Assert.Equal(0.5f, to.RenderTransformOrigin.Y);

        Assert.True(from.RenderTransform.M11 > 1.0f); // Zooming larger
        Assert.True(to.RenderTransform.M11 < 1.0f);   // Scaling up from smaller

        zoom.Reset(from, to);
        Assert.True(from.RenderTransform.IsIdentity);
        Assert.True(to.RenderTransform.IsIdentity);
        Assert.Equal(1.0f, from.Opacity);
        Assert.Equal(1.0f, to.Opacity);
    }

    [Fact]
    public void CompositeTransition_AppliesAllInnerTransitions()
    {
        var composite = new CompositeTransition(
            new FadeTransition(TimeSpan.FromMilliseconds(200)),
            new SlideTransition(SlideDirection.Left, TimeSpan.FromMilliseconds(200))
        );

        var from = new TextBlock("From");
        var to = new TextBlock("To");
        var bounds = new Size(500, 400);

        composite.Apply(from, to, 0.5f, bounds);

        // Opacity from FadeTransition
        Assert.Equal(0.5f, from.Opacity, tolerance: 0.01f);
        Assert.Equal(0.5f, to.Opacity, tolerance: 0.01f);

        // Translation from SlideTransition
        Assert.Equal(-250f, from.RenderTransform.M31, tolerance: 0.1f);
        Assert.Equal(250f, to.RenderTransform.M31, tolerance: 0.1f);

        composite.Reset(from, to);
        Assert.Equal(1.0f, from.Opacity);
        Assert.True(from.RenderTransform.IsIdentity);
    }

    [Fact]
    public void MeasureAndArrange_EnvelopesBothChildrenDuringTransition()
    {
        var clock = new AnimationClock();
        var control = new TransitioningContentControl
        {
            Clock = clock,
            Transition = new FadeTransition(),
            Padding = new Thickness(10)
        };

        var childA = new TextBlock("Short") { Width = 100, Height = 50 };
        var childB = new TextBlock("Taller") { Width = 80, Height = 120 };

        control.Content = childA;
        control.Measure(new Size(1000, 1000));
        control.Arrange(new Rect(0, 0, 120, 70));

        // Start transition
        control.Content = childB;
        control.Measure(new Size(1000, 1000));

        // Max width: 100 + 20 padding = 120
        // Max height: 120 + 20 padding = 140
        Assert.Equal(120, control.DesiredSize.Width);
        Assert.Equal(140, control.DesiredSize.Height);

        control.Arrange(new Rect(0, 0, 120, 140));

        // Both children should be arranged within the padded inner area
        Assert.Equal(100, childA.Bounds.Width);
        Assert.Equal(120, childB.Bounds.Height);
    }

    [Fact]
    public void TransitionsViewLayoutHierarchy_InspectBounds()
    {
        var root = new Grid();
        root.Rows(GridLength.Auto, GridLength.Star);
        root.RowSpacing(16);

        // Header
        var headerCard = new Card(CardVariant.Filled).Padding(20);
        var headerStack = new StackPanel { Spacing = 10 };
        headerStack.Add(new TextBlock("Header Title") { Height = 30 });
        headerStack.Add(new TextBlock("Header Description") { Height = 40 });
        headerCard.Child = headerStack;
        root.Add(headerCard.Row(0));

        // Main Grid
        var mainGrid = new Grid()
            .Columns(new GridLength(380, GridUnitType.Pixel), GridLength.Star)
            .ColumnSpacing(20);

        // Controls
        var controlsCard = new Card(CardVariant.Elevated).Padding(18);
        var controlsScroll = new ScrollViewer();
        var controlsStack = new StackPanel { Spacing = 16 };
        var transitionCombo = new ComboBox();
        transitionCombo.Items.Add("Slide Left");
        transitionCombo.Items.Add("Cross Fade");
        transitionCombo.SelectedIndex = 0;
        controlsStack.Add(transitionCombo);
        controlsStack.Add(new Slider { Height = 20 });
        var easingCombo = new ComboBox();
        easingCombo.Items.Add("Emphasized");
        easingCombo.Items.Add("Linear");
        easingCombo.SelectedIndex = 0;
        controlsStack.Add(easingCombo);
        controlsStack.Add(new Button("Stress"));
        controlsScroll.Content = controlsStack;
        controlsCard.Child = controlsScroll;
        mainGrid.Add(controlsCard.Column(0));

        // Stage
        var stageCard = new Card(CardVariant.Elevated).Padding(20);
        var stageLayout = new Grid()
            .Rows(GridLength.Auto, GridLength.Star, GridLength.Auto)
            .RowSpacing(16);

        var navBar = new Grid()
            .Columns(GridLength.Auto, GridLength.Star, GridLength.Auto);
        navBar.Add(new Button("Prev").Column(0));
        navBar.Add(new Button("Next").Column(2));
        stageLayout.Add(navBar.Row(0));

        var transitionHost = new TransitioningContentControl();
        var stageContainer = new Border
        {
            Padding = new Thickness(0),
            Child = transitionHost
        };
        stageLayout.Add(stageContainer.Row(1));

        var statusRow = new StackPanel();
        statusRow.Add(new TextBlock("Status") { Height = 20 });
        stageLayout.Add(statusRow.Row(2));

        stageCard.Child = stageLayout;
        mainGrid.Add(stageCard.Column(1));

        root.Add(mainGrid.Row(1));

        // Render card content into transitionHost
        var cardContent = new Card(CardVariant.Outlined).Padding(24);
        var cardStack = new StackPanel { Spacing = 16 };
        cardStack.Add(new TextBlock("Card Title") { Height = 40 });
        var detailsCard = new Card(CardVariant.Filled).Padding(16);
        var detailsStack = new StackPanel { Spacing = 12 };
        detailsStack.Add(new TextBlock("Details") { Height = 30 });
        detailsStack.Add(new Button("Action") { Height = 36 });
        detailsCard.Child = detailsStack;
        cardStack.Add(detailsCard);
        cardContent.Child = cardStack;

        transitionHost.Content = cardContent;

        // Measure & Arrange root with 890x716
        root.Measure(new Size(890, 716));
        root.Arrange(new Rect(0, 0, 890, 716));

        Assert.True(stageContainer.Bounds.Height > 300, $"StageContainer Height was {stageContainer.Bounds.Height}");
        Assert.True(transitionHost.Bounds.Height > 300, $"TransitionHost Height was {transitionHost.Bounds.Height}");
        Assert.True(cardContent.Bounds.Height > 300, $"CardContent Height was {cardContent.Bounds.Height}");
        Assert.True(stageContainer.Bounds.Y < 100, $"StageContainer Y was {stageContainer.Bounds.Y}");
        Assert.True(navBar.Bounds.Height < 50, $"NavBar Height was {navBar.Bounds.Height}");
        Assert.True(transitionCombo.Bounds.Height > 0, "Transition ComboBox should be measured and arranged with positive height");
        Assert.True(easingCombo.Bounds.Height > 0, "Easing ComboBox should be measured and arranged with positive height");
    }
}
