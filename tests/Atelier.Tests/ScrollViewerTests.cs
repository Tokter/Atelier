using System;
using System.Collections.Generic;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Xunit;

namespace Atelier.Tests;

public class ScrollViewerTests
{
    private static (ScrollViewer Viewer, StackPanel Content) Create(float contentHeight, ScrollBarVisibility vertical = ScrollBarVisibility.Auto)
    {
        var content = new StackPanel { Height = contentHeight };
        var viewer = new ScrollViewer { Content = content, VerticalScrollBarVisibility = vertical };
        Layout(viewer);
        return (viewer, content);
    }

    private static void Layout(UIElement element)
    {
        element.Measure(new Size(200, 100));
        element.Arrange(new Rect(0, 0, 200, 100));
    }

    [Fact]
    public void ScrollOffset_IsCoercedToScrollableRange()
    {
        var (viewer, content) = Create(400);

        viewer.ScrollOffsetY = 99999;
        Assert.Equal(300, viewer.ScrollOffsetY);
        Assert.Equal(-300, content.Bounds.Y);

        viewer.ScrollOffsetY = -50;
        Assert.Equal(0, viewer.ScrollOffsetY);

        viewer.ScrollOffsetY = float.NaN;
        Assert.Equal(0, viewer.ScrollOffsetY);
    }

    [Fact]
    public void ContentShrinkingAndGrowingBack_DoesNotRestoreStaleOffset()
    {
        var (viewer, content) = Create(400);
        viewer.ScrollOffsetY = 250;

        content.Height = 150; // max offset 50
        Layout(viewer);
        Assert.Equal(50, viewer.ScrollOffsetY);

        content.Height = 400;
        Layout(viewer);
        Assert.Equal(50, viewer.ScrollOffsetY);
        Assert.Equal(-50, content.Bounds.Y);
    }

    [Fact]
    public void HiddenScrollBar_StillScrollsWithWheel_ButIsNotShown()
    {
        var (viewer, _) = Create(400, ScrollBarVisibility.Hidden);

        Assert.True(viewer.CanScrollVertically);
        Assert.False(viewer.IsVerticalScrollBarVisible);

        var wheel = new PointerWheelEventArgs(new Point(50, 50), 0, -1f);
        viewer.OnPointerWheel(wheel);

        Assert.True(wheel.Handled);
        Assert.Equal(ScrollViewer.WheelScrollAmount, viewer.ScrollOffsetY);
    }

    [Fact]
    public void DisabledAxis_DoesNotScroll()
    {
        var (viewer, _) = Create(400, ScrollBarVisibility.Disabled);

        var wheel = new PointerWheelEventArgs(new Point(50, 50), 0, -1f);
        viewer.OnPointerWheel(wheel);

        Assert.False(wheel.Handled);
        Assert.Equal(0, viewer.ScrollOffsetY);
    }

    [Fact]
    public void Wheel_AtEnd_IsLeftUnhandledForOuterViewers()
    {
        var (viewer, _) = Create(400);
        viewer.ScrollToBottom();

        var wheel = new PointerWheelEventArgs(new Point(50, 50), 0, -1f);
        viewer.OnPointerWheel(wheel);

        Assert.False(wheel.Handled);
    }

    [Fact]
    public void ScrollChanged_ReportsOffsetAndExtentChanges()
    {
        var (viewer, content) = Create(400);
        var events = new List<ScrollChangedEventArgs>();
        viewer.ScrollChanged += (s, e) => events.Add(e);

        viewer.ScrollOffsetY = 30;

        Assert.Single(events);
        Assert.Equal(30, events[0].VerticalOffset);
        Assert.Equal(30, events[0].VerticalChange);
        Assert.Equal(0, events[0].ExtentHeightChange);

        content.Height = 500;
        Layout(viewer);

        Assert.Equal(2, events.Count);
        Assert.Equal(500, events[1].ExtentHeight);
        Assert.Equal(100, events[1].ExtentHeightChange);
        Assert.Equal(0, events[1].VerticalChange);

        // Nothing changed: no event.
        viewer.ScrollOffsetY = 30;
        Layout(viewer);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void ScrollChanged_ReportsOffsetClampedByShrinkingContent()
    {
        var (viewer, content) = Create(400);
        viewer.ScrollOffsetY = 300;
        var events = new List<ScrollChangedEventArgs>();
        viewer.ScrollChanged += (s, e) => events.Add(e);

        content.Height = 200;
        Layout(viewer);

        var e = Assert.Single(events);
        Assert.Equal(100, e.VerticalOffset);
        Assert.Equal(-200, e.VerticalChange);
        Assert.Equal(-200, e.ExtentHeightChange);
    }

    [Fact]
    public void KeyboardScrolling_ArrowsPagesHomeEnd()
    {
        var (viewer, _) = Create(400);

        var down = new KeyEventArgs(Key.Down);
        viewer.OnKeyDown(down);
        Assert.True(down.Handled);
        Assert.Equal(ScrollViewer.LineScrollAmount, viewer.ScrollOffsetY);

        viewer.OnKeyDown(new KeyEventArgs(Key.PageDown));
        Assert.Equal(ScrollViewer.LineScrollAmount + 100, viewer.ScrollOffsetY);

        viewer.OnKeyDown(new KeyEventArgs(Key.End));
        Assert.Equal(300, viewer.ScrollOffsetY);

        var endAgain = new KeyEventArgs(Key.End);
        viewer.OnKeyDown(endAgain);
        Assert.False(endAgain.Handled); // nothing to scroll: left to outer handlers

        viewer.OnKeyDown(new KeyEventArgs(Key.Up));
        Assert.Equal(300 - ScrollViewer.LineScrollAmount, viewer.ScrollOffsetY);

        viewer.OnKeyDown(new KeyEventArgs(Key.Home));
        Assert.Equal(0, viewer.ScrollOffsetY);
    }

    [Fact]
    public void KeyFromFocusedDescendant_BubblesToViewerAndScrolls()
    {
        var button = new Button("Inside");
        var content = new StackPanel { Height = 400 };
        content.Add(button);
        var viewer = new ScrollViewer { Content = content };
        Layout(viewer);

        var e = new KeyEventArgs(Key.PageDown);
        button.DispatchBubbleKeyEvent(e, (el, args) => el.OnKeyDown(args));

        Assert.Equal(100, viewer.ScrollOffsetY);
    }

    [Fact]
    public void ScrollingMethods_MoveByLinesPagesAndToEdges()
    {
        var content = new StackPanel { Width = 600, Height = 400 };
        var viewer = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Layout(viewer);

        viewer.LineDown();
        Assert.Equal(16, viewer.ScrollOffsetY);
        viewer.PageDown();
        Assert.Equal(116, viewer.ScrollOffsetY);
        viewer.LineUp();
        Assert.Equal(100, viewer.ScrollOffsetY);
        viewer.PageUp();
        Assert.Equal(0, viewer.ScrollOffsetY);

        viewer.LineRight();
        Assert.Equal(16, viewer.ScrollOffsetX);
        viewer.PageRight();
        Assert.Equal(216, viewer.ScrollOffsetX);
        viewer.ScrollToRightEnd();
        Assert.Equal(400, viewer.ScrollOffsetX);
        viewer.LineLeft();
        Assert.Equal(384, viewer.ScrollOffsetX);

        viewer.ScrollToEnd();
        Assert.Equal(300, viewer.ScrollOffsetY);
        Assert.Equal(0, viewer.ScrollOffsetX);

        viewer.ScrollToHorizontalOffset(50);
        viewer.ScrollToVerticalOffset(60);
        Assert.Equal(50, viewer.ScrollOffsetX);
        Assert.Equal(60, viewer.ScrollOffsetY);

        viewer.ScrollToHome();
        Assert.Equal(0, viewer.ScrollOffsetX);
        Assert.Equal(0, viewer.ScrollOffsetY);
    }

    [Fact]
    public void ScrollToBottom_BeforeLayout_IsAppliedAtArrange()
    {
        var content = new StackPanel { Height = 400 };
        var viewer = new ScrollViewer { Content = content };

        viewer.ScrollToBottom();
        Layout(viewer);

        Assert.Equal(300, viewer.ScrollOffsetY);
        Assert.Equal(-300, content.Bounds.Y);
    }

    [Fact]
    public void ScrollToBottom_AfterContentGrew_ScrollsToNewEnd()
    {
        var (viewer, content) = Create(400);

        content.Height = 800; // layout pending
        viewer.ScrollToBottom();
        Layout(viewer);

        Assert.Equal(700, viewer.ScrollOffsetY);
    }

    [Fact]
    public void OffsetChange_RepositionsContentWithoutInvalidatingLayout()
    {
        var (viewer, content) = Create(400);

        viewer.ScrollOffsetY = 120;

        Assert.True(viewer.IsArrangeValid);
        Assert.True(viewer.IsMeasureValid);
        Assert.Equal(-120, content.Bounds.Y);
    }

    [Fact]
    public void ThumbDrag_EndsWhenCaptureIsLost()
    {
        UIElement.ReleaseCurrentPointerCapture();
        var (viewer, _) = Create(400);
        var thumb = viewer.GetVerticalThumbRect();

        viewer.OnPointerPressed(new PointerEventArgs(new Point(thumb.X + 2, thumb.Y + 2), PointerButtons.Left));
        Assert.True(viewer.IsVerticalThumbDragging);
        Assert.Same(viewer, UIElement.CapturedElement);

        var other = new Button("Other");
        other.CapturePointer();

        Assert.False(viewer.IsThumbDragging);

        // Moves no longer scroll.
        viewer.OnPointerMoved(new PointerEventArgs(new Point(thumb.X + 2, thumb.Y + 60)));
        Assert.Equal(0, viewer.ScrollOffsetY);

        UIElement.ReleaseCurrentPointerCapture();
    }

    [Fact]
    public void VisibleBar_WithoutOverflow_IsShownButCannotScroll()
    {
        var (viewer, _) = Create(50, ScrollBarVisibility.Visible);

        Assert.True(viewer.IsVerticalScrollBarVisible);
        Assert.False(viewer.CanScrollVertically);
    }
}
