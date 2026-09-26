using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Xunit;

namespace Atelier.Tests;

public class PopupBehaviorTests
{
    private sealed class TestTarget : Control
    {
        public TestTarget(float x, float y, float w, float h)
        {
            Arrange(new Rect(x, y, w, h));
        }
    }

    private sealed class HoverProbe : Border
    {
        public int Entered, Exited;
        public override void OnPointerEntered(PointerEventArgs e) { Entered++; base.OnPointerEntered(e); }
        public override void OnPointerExited(PointerEventArgs e) { Exited++; base.OnPointerExited(e); }
    }

    private static Popup CreatePopup(UIElement target, PlacementMode placement, float childWidth, float childHeight)
    {
        return new Popup
        {
            PlacementTarget = target,
            Placement = placement,
            Child = new Border { Width = childWidth, Height = childHeight }
        };
    }

    [Theory]
    [InlineData(PlacementMode.Bottom, 110f, 145f)]
    [InlineData(PlacementMode.BottomLeft, 110f, 145f)]
    [InlineData(PlacementMode.BottomRight, 230f, 145f)]
    [InlineData(PlacementMode.Top, 110f, 55f)]
    [InlineData(PlacementMode.Left, 30f, 105f)]
    [InlineData(PlacementMode.Right, 310f, 105f)]
    [InlineData(PlacementMode.Center, 370f, 280f)]
    public void Placement_AddsOffsetsInScreenDirection(PlacementMode placement, float expectedX, float expectedY)
    {
        var target = new TestTarget(100, 100, 200, 40);
        var popup = CreatePopup(target, placement, 80, 50);
        popup.HorizontalOffset = 10;
        popup.VerticalOffset = 5;

        var bounds = popup.ComputeSmartPlacement(new Size(800, 600));

        Assert.Equal(new Rect(expectedX, expectedY, 80, 50), bounds);
    }

    [Fact]
    public void Placement_TopWithoutRoom_FlipsBelow_KeepingOffsetDirection()
    {
        var target = new TestTarget(100, 10, 200, 40);
        var popup = CreatePopup(target, PlacementMode.Top, 80, 50);
        popup.VerticalOffset = 5;

        var bounds = popup.ComputeSmartPlacement(new Size(800, 600));

        Assert.Equal(55f, bounds.Y); // below the target (50) plus the offset
    }

    [Fact]
    public void Placement_OversizedPopup_IsShrunkToViewport()
    {
        var target = new TestTarget(100, 100, 200, 40);
        var popup = CreatePopup(target, PlacementMode.Bottom, 1000, 900);

        var bounds = popup.ComputeSmartPlacement(new Size(800, 600));

        Assert.Equal(new Rect(0, 0, 800, 600), bounds);
    }

    [Fact]
    public void Placement_MatchTargetWidth_ArrangesChildAtTargetWidth()
    {
        PopupManager.CloseAllPopups();
        var target = new TestTarget(100, 100, 200, 40);
        var child = new Border { Height = 50 };
        var popup = new Popup { PlacementTarget = target, MatchTargetWidth = true, Child = child };

        popup.IsOpen = true;
        popup.UpdatePlacement(new Size(800, 600));

        Assert.Equal(new Rect(100, 140, 200, 50), popup.ActualBounds);
        Assert.Equal(200f, child.Bounds.Width);
        popup.IsOpen = false;
    }

    [Fact]
    public void Placement_WithoutTarget_UsesParent()
    {
        var owner = new TestTarget(300, 200, 120, 30);
        var popup = new Popup { Child = new Border { Width = 60, Height = 40 } };
        owner.AddChild(popup);

        var bounds = popup.ComputeSmartPlacement(new Size(800, 600));

        Assert.Equal(new Rect(300, 230, 60, 40), bounds);
    }

    [Fact]
    public void Opened_SeesComputedBounds_WhenTreeIsLaidOut()
    {
        PopupManager.CloseAllPopups();
        var root = new StackPanel();
        var owner = new Border { Height = 30 };
        root.Add(owner);
        var popup = new Popup { Child = new Border { Width = 60, Height = 40 } };
        root.Add(popup);
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));

        Rect boundsInOpened = Rect.Zero;
        popup.Opened += (s, e) => boundsInOpened = popup.ActualBounds;
        popup.IsOpen = true;

        Assert.False(boundsInOpened.IsEmpty);
        Assert.Equal(new Size(60, 40), boundsInOpened.Size);
        popup.IsOpen = false;
    }

    [Fact]
    public void PropertyChangeWhileOpen_RequestsRender_AndMovesPopup()
    {
        PopupManager.CloseAllPopups();
        var target = new TestTarget(100, 100, 200, 40);
        var popup = CreatePopup(target, PlacementMode.Bottom, 80, 50);
        popup.IsOpen = true;
        popup.UpdatePlacement(new Size(800, 600));

        int renderRequests = 0;
        popup.NeedsVisualUpdate += () => renderRequests++;
        popup.Placement = PlacementMode.Top;
        Assert.True(renderRequests > 0);

        popup.UpdatePlacement(new Size(800, 600));
        Assert.Equal(50f, popup.ActualBounds.Y);

        popup.HorizontalOffset = 7;
        popup.UpdatePlacement(new Size(800, 600));
        Assert.Equal(107f, popup.ActualBounds.X);
        popup.IsOpen = false;
    }

    [Fact]
    public void Popup_ClosesWhenOwnerLeavesDisplayedTree()
    {
        PopupManager.CloseAllPopups();
        var root = new StackPanel();
        var owner = new StackPanel();
        var popup = new Popup { Child = new Border { Width = 50, Height = 50 } };
        owner.Add(popup);
        root.Add(owner);
        root.AttachToHost();
        try
        {
            int closed = 0;
            popup.Closed += (s, e) => closed++;
            popup.IsOpen = true;
            Assert.Contains(popup, PopupManager.ActivePopups);

            root.RemoveChild(owner);

            Assert.False(popup.IsOpen);
            Assert.Equal(1, closed);
            Assert.Empty(PopupManager.ActivePopups);
        }
        finally
        {
            root.DetachFromHost();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference OpenPopupAndRemoveOwner(StackPanel root)
    {
        var owner = new StackPanel();
        var popup = new Popup { Child = new Button("Item") };
        owner.Add(popup);
        root.Add(owner);
        popup.IsOpen = true;
        popup.UpdatePlacement(new Size(800, 600));
        root.RemoveChild(owner);
        return new WeakReference(popup);
    }

    [Fact]
    public void RemovedOwner_PopupIsCollectable()
    {
        PopupManager.CloseAllPopups();
        var root = new StackPanel();
        root.AttachToHost();
        try
        {
            var weak = OpenPopupAndRemoveOwner(root);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(weak.IsAlive);
            Assert.Empty(PopupManager.ActivePopups);
        }
        finally
        {
            root.DetachFromHost();
        }
    }

    [Fact]
    public void ClickInsideParentPopup_DismissesChildPopupsAboveIt()
    {
        PopupManager.CloseAllPopups();
        var parentTarget = new TestTarget(100, 100, 200, 40);
        var parent = CreatePopup(parentTarget, PlacementMode.Bottom, 200, 200);
        var childTarget = new TestTarget(500, 100, 100, 40);
        var child = CreatePopup(childTarget, PlacementMode.Bottom, 100, 100);
        var pinned = CreatePopup(childTarget, PlacementMode.Right, 50, 50);
        pinned.StaysOpen = true;

        parent.IsOpen = true;
        child.IsOpen = true;
        pinned.IsOpen = true;
        PopupManager.UpdatePopups(new Size(800, 600));

        // Press inside the parent popup (100,140)-(300,340), outside the child.
        bool handled = PopupManager.HandleMouseDown(new Point(150, 200), PointerButtons.Left);

        Assert.True(handled);
        Assert.True(parent.IsOpen);
        Assert.False(child.IsOpen);
        Assert.True(pinned.IsOpen);

        // With no popup above it any more, the next press is routed into the parent popup.
        Assert.True(PopupManager.HandleMouseDown(new Point(150, 200), PointerButtons.Left));
        Assert.True(parent.IsOpen);

        PopupManager.CloseAllPopups();
    }

    [Fact]
    public void HoveredPopupElement_GetsExited_WhenPopupClosed()
    {
        PopupManager.CloseAllPopups();
        var target = new TestTarget(100, 100, 200, 40);
        var probe = new HoverProbe { Width = 100, Height = 100 };
        var popup = new Popup { PlacementTarget = target, Child = probe };
        popup.IsOpen = true;
        PopupManager.UpdatePopups(new Size(800, 600));

        UIElement? hovered = null;
        Assert.True(PopupManager.HandleMouseMove(new Point(150, 180), ref hovered));
        Assert.Same(probe, hovered);
        Assert.Equal(1, probe.Entered);

        popup.IsOpen = false;
        Assert.False(PopupManager.HandleMouseMove(new Point(150, 180), ref hovered));

        Assert.Null(hovered);
        Assert.Equal(1, probe.Exited);
    }

    [Fact]
    public void Wheel_PassesThrough_WithOnlyStaysOpenPopups()
    {
        PopupManager.CloseAllPopups();
        var target = new TestTarget(100, 100, 200, 40);
        var pinned = CreatePopup(target, PlacementMode.Bottom, 100, 100);
        pinned.StaysOpen = true;
        pinned.IsOpen = true;
        PopupManager.UpdatePopups(new Size(800, 600));

        Assert.False(PopupManager.HandleMouseScroll(new Point(600, 500), 0, 1));
        Assert.True(PopupManager.HandleMouseScroll(new Point(150, 180), 0, 1)); // over the popup

        var dismissable = CreatePopup(target, PlacementMode.Top, 50, 50);
        dismissable.IsOpen = true;
        Assert.True(PopupManager.HandleMouseScroll(new Point(600, 500), 0, 1));

        PopupManager.CloseAllPopups();
    }

    [Fact]
    public void CloseAllPopups_KeepsPopupReopenedByClosedHandler()
    {
        PopupManager.CloseAllPopups();
        var target = new TestTarget(100, 100, 200, 40);
        var a = CreatePopup(target, PlacementMode.Bottom, 10, 10);
        var b = CreatePopup(target, PlacementMode.Top, 10, 10);
        a.IsOpen = true;
        b.IsOpen = true;

        bool reopened = false;
        b.Closed += (s, e) =>
        {
            if (!reopened)
            {
                reopened = true;
                a.IsOpen = false; // closes another popup from the handler
                b.IsOpen = true;
            }
        };

        PopupManager.CloseAllPopups();

        Assert.True(b.IsOpen);
        Assert.False(a.IsOpen);
        Assert.Contains(b, PopupManager.ActivePopups);
        Assert.Single(PopupManager.ActivePopups);

        b.IsOpen = false;
    }

    [Fact]
    public void BorderThickness_DefaultsToOne()
    {
        Assert.Equal(new Thickness(1), new Popup().BorderThickness);
        Assert.Equal(new Thickness(1), new Dialog().BorderThickness);
    }
}
