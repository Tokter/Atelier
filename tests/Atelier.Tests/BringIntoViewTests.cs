using System.Linq;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Tests;

public class BringIntoViewTests
{
    private static void Layout(UIElement root, float width = 400, float height = 300)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
    }

    // The element's top and bottom in the viewer's coordinates.
    private static (float Top, float Bottom) InViewer(UIElement element, ScrollViewer viewer)
    {
        var top = element.PointToScreen(Point.Zero).Y - viewer.PointToScreen(Point.Zero).Y;
        return (top, top + element.Bounds.Height);
    }

    // A 300 px high scroll viewer over ten 80 px buttons.
    private static (ScrollViewer Viewer, Button[] Buttons) TallList()
    {
        var buttons = Enumerable.Range(0, 10).Select(i => new Button($"Button {i}").Height(80)).ToArray();
        var viewer = new ScrollViewer().Content(new StackPanel().Children(buttons));
        viewer.AttachToHost();
        Layout(viewer);
        return (viewer, buttons);
    }

    [Fact]
    public void Tab_ScrollsTheNewFocusIntoView_AndBack()
    {
        var (viewer, buttons) = TallList();

        // Tab to the fifth button (below the viewport): scrolled so it is fully visible, with a margin.
        buttons[3].Focus();
        FocusManager.FocusNext(viewer);
        Assert.True(buttons[4].IsFocused);
        var (top, bottom) = InViewer(buttons[4], viewer);
        Assert.True(bottom <= 300 - ScrollViewer.BringIntoViewMargin + 0.5f, $"Bottom {bottom} is out of view.");
        Assert.True(top >= 0);
        float scrolled = viewer.ScrollOffsetY;
        Assert.True(scrolled > 0);

        // Shift+Tab back up to the first button scrolls back to the top.
        for (int i = 0; i < 4; i++) FocusManager.FocusPrevious(viewer);
        Assert.True(buttons[0].IsFocused);
        Assert.Equal(0f, viewer.ScrollOffsetY);
        viewer.DetachFromHost();
    }

    [Fact]
    public void VisibleElement_DoesNotScroll()
    {
        var (viewer, buttons) = TallList();
        buttons[1].BringIntoView();

        Assert.Equal(0f, viewer.ScrollOffsetY);
        viewer.DetachFromHost();
    }

    [Fact]
    public void NestedScrollViewers_AllScroll()
    {
        // An inner 200 px viewer at the bottom of a long page in a 300 px outer viewer.
        var buttons = Enumerable.Range(0, 8).Select(i => new Button($"Inner {i}").Height(60)).ToArray();
        var inner = new ScrollViewer().Height(200).Content(new StackPanel().Children(buttons));
        var outer = new ScrollViewer().Content(new StackPanel().Children(new Border().Height(600), inner));
        outer.AttachToHost();
        Layout(outer);

        buttons[6].BringIntoView();

        Assert.True(inner.ScrollOffsetY > 0);
        Assert.True(outer.ScrollOffsetY > 0);
        var windowTop = buttons[6].PointToScreen(Point.Zero).Y;
        Assert.InRange(windowTop, 0, 300 - buttons[6].Bounds.Height);
        outer.DetachFromHost();
    }

    [Fact]
    public void ElementTallerThanTheViewport_KeepsItsTopVisible()
    {
        var tall = new Border().Height(500);
        var viewer = new ScrollViewer().Content(new StackPanel().Children(new Border().Height(400), tall, new Border().Height(400)));
        viewer.AttachToHost();
        Layout(viewer);

        tall.BringIntoView();

        var (top, _) = InViewer(tall, viewer);
        Assert.Equal(ScrollViewer.BringIntoViewMargin, top, 1);
        viewer.DetachFromHost();
    }

    [Fact]
    public void WithAnimationClock_ScrollsSmoothly_AndUserScrollingTakesOver()
    {
        var clock = new Atelier.Core.Animation.AnimationClock();
        ScrollViewer.SetGlobalAnimationClock(clock);
        try
        {
            var (viewer, buttons) = TallList();
            buttons[9].BringIntoView();

            // The scroll starts where it was and eases towards the target.
            Assert.True(viewer.IsScrollAnimating);
            Assert.Equal(0f, viewer.ScrollOffsetY);
            clock.Update(0.05);
            float partway = viewer.ScrollOffsetY;
            Assert.InRange(partway, 1f, viewer.MaxScrollY - 1f);

            clock.Update(0.2);
            Assert.False(viewer.IsScrollAnimating);
            Assert.Equal(viewer.MaxScrollY, viewer.ScrollOffsetY);

            // Scrolling by other means during an animation stops it.
            buttons[0].BringIntoView();
            clock.Update(0.05);
            viewer.ScrollOffsetY = 100;
            Assert.False(viewer.IsScrollAnimating);
            clock.Update(0.2);
            Assert.Equal(100f, viewer.ScrollOffsetY);

            // Turned off, it jumps.
            viewer.IsScrollAnimationEnabled(false);
            buttons[9].BringIntoView();
            Assert.False(viewer.IsScrollAnimating);
            Assert.Equal(viewer.MaxScrollY, viewer.ScrollOffsetY);
            viewer.DetachFromHost();
        }
        finally
        {
            ScrollViewer.SetGlobalAnimationClock(null);
        }
    }

    [Fact]
    public void NestedSmoothScrolls_EndWithTheElementInView()
    {
        var clock = new Atelier.Core.Animation.AnimationClock();
        ScrollViewer.SetGlobalAnimationClock(clock);
        try
        {
            var buttons = Enumerable.Range(0, 8).Select(i => new Button($"Inner {i}").Height(60)).ToArray();
            var inner = new ScrollViewer().Height(200).Content(new StackPanel().Children(buttons));
            var outer = new ScrollViewer().Content(new StackPanel().Children(new Border().Height(600), inner));
            outer.AttachToHost();
            Layout(outer);

            buttons[6].BringIntoView();
            Assert.True(inner.IsScrollAnimating && outer.IsScrollAnimating);
            clock.Update(0.3);

            var windowTop = buttons[6].PointToScreen(Point.Zero).Y;
            Assert.InRange(windowTop, 0, 300 - buttons[6].Bounds.Height);
            outer.DetachFromHost();
        }
        finally
        {
            ScrollViewer.SetGlobalAnimationClock(null);
        }
    }

    [Fact]
    public void HorizontalScrolling_BringsElementsIntoView()
    {
        var target = new Border().Width(100);
        var row = new StackPanel().Orientation(Orientation.Horizontal).Children(new Border().Width(700), target);
        var viewer = new ScrollViewer()
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Auto)
            .Content(row);
        viewer.AttachToHost();
        Layout(viewer);

        target.BringIntoView();

        float left = target.PointToScreen(Point.Zero).X - viewer.PointToScreen(Point.Zero).X;
        Assert.InRange(left, 0, 400 - 100);
        viewer.DetachFromHost();
    }
}
