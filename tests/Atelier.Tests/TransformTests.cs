using System;
using System.Numerics;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Tests;

public class TransformTests
{
    [Fact]
    public void IdentityTransform_PreservesCoordinates()
    {
        var panel = new Canvas().Size(400, 400);
        var button = new Button("Click").Size(100, 50);
        Canvas.SetLeft(button, 50);
        Canvas.SetTop(button, 50);
        panel.AddChild(button);

        panel.Measure(new Size(400, 400));
        panel.Arrange(new Rect(0, 0, 400, 400));

        var screenPoint = button.PointToScreen(new Point(10, 10));
        Assert.Equal(60, screenPoint.X);
        Assert.Equal(60, screenPoint.Y);

        var localPoint = button.PointToClient(new Point(60, 60));
        Assert.Equal(10, localPoint.X);
        Assert.Equal(10, localPoint.Y);
    }

    [Fact]
    public void ElementScale_ScalesPointToScreenAndPointToClient()
    {
        var panel = new Canvas().Size(500, 500);
        var button = new Button("Click").Size(100, 50).Scale(2f);
        Canvas.SetLeft(button, 50);
        Canvas.SetTop(button, 50);
        panel.AddChild(button);

        panel.Measure(new Size(500, 500));
        panel.Arrange(new Rect(0, 0, 500, 500));

        // Local (10, 10) scaled by 2 -> (20, 20) in parent -> + (50, 50) -> screen (70, 70)
        var screenPoint = button.PointToScreen(new Point(10, 10));
        Assert.Equal(70, screenPoint.X);
        Assert.Equal(70, screenPoint.Y);

        // Screen (70, 70) -> local (10, 10)
        var localPoint = button.PointToClient(new Point(70, 70));
        Assert.Equal(10, localPoint.X);
        Assert.Equal(10, localPoint.Y);
    }

    [Fact]
    public void RootScale_ScalesWholeTreeCoordinateSpace()
    {
        var root = new Canvas().Size(500, 500).Scale(1.5f);
        var button = new Button("Click").Size(100, 50);
        Canvas.SetLeft(button, 100);
        Canvas.SetTop(button, 100);
        root.AddChild(button);

        root.Measure(new Size(500, 500));
        root.Arrange(new Rect(0, 0, 500, 500));

        // Button local (0, 0) -> in root (100, 100) -> scaled by 1.5 -> screen (150, 150)
        var screenPoint = button.PointToScreen(Point.Zero);
        Assert.Equal(150, screenPoint.X);
        Assert.Equal(150, screenPoint.Y);

        // Screen (150, 150) -> in root (100, 100) -> button local (0, 0)
        var localPoint = button.PointToClient(new Point(150, 150));
        Assert.Equal(0, localPoint.X, 1e-4f);
        Assert.Equal(0, localPoint.Y, 1e-4f);
    }

    [Fact]
    public void NestedTransforms_AccumulateHierarchically()
    {
        var root = new Canvas().Size(800, 800).Scale(2f);
        var container = new Canvas().Size(300, 300).Scale(1.5f);
        Canvas.SetLeft(container, 50);
        Canvas.SetTop(container, 50);
        root.AddChild(container);

        var button = new Button("Click").Size(50, 50);
        Canvas.SetLeft(button, 20);
        Canvas.SetTop(button, 20);
        container.AddChild(button);

        root.Measure(new Size(800, 800));
        root.Arrange(new Rect(0, 0, 800, 800));

        // Button local (10, 10):
        // In container: (20 + 10, 20 + 10) = (30, 30)
        // Container scale: (30 * 1.5, 30 * 1.5) = (45, 45)
        // In root: (50 + 45, 50 + 45) = (95, 95)
        // Root scale: (95 * 2, 95 * 2) = (190, 190)
        var screenPoint = button.PointToScreen(new Point(10, 10));
        Assert.Equal(190, screenPoint.X);
        Assert.Equal(190, screenPoint.Y);

        var localPoint = button.PointToClient(new Point(190, 190));
        Assert.Equal(10, localPoint.X, 1e-4f);
        Assert.Equal(10, localPoint.Y, 1e-4f);
    }

    [Fact]
    public void Rotation_90DegreesClockwise_TransformsCoordinates()
    {
        var panel = new Canvas().Size(500, 500);
        var button = new Button("Click").Size(100, 50).Rotate(90f);
        Canvas.SetLeft(button, 100);
        Canvas.SetTop(button, 100);
        panel.AddChild(button);

        panel.Measure(new Size(500, 500));
        panel.Arrange(new Rect(0, 0, 500, 500));

        // In 90-deg clockwise rotation around (0, 0):
        // (x, y) -> (-y, x)
        // Local (100, 0) -> (0, 100).
        // Since local geometry has X in [-50, 0], layout compensates with Bounds.X = 150
        // so the visual bounding box aligns at Canvas.Left = 100 (spanning [100, 150]).
        // Parent screen pos for (100, 0): (0, 100) + (150, 100) = (150, 200)
        var screenPoint = button.PointToScreen(new Point(100, 0));
        Assert.Equal(150, screenPoint.X, 1e-4f);
        Assert.Equal(200, screenPoint.Y, 1e-4f);

        // Screen (150, 200) -> local (100, 0)
        var localPoint = button.PointToClient(new Point(150, 200));
        Assert.Equal(100, localPoint.X, 1e-4f);
        Assert.Equal(0, localPoint.Y, 1e-4f);
    }

    [Fact]
    public void TransformOrigin_CenterRotation_PreservesCenterPosition()
    {
        var panel = new Canvas().Size(500, 500);
        var button = new Button("Click").Size(100, 100).RotateCenter(90f);
        Canvas.SetLeft(button, 100);
        Canvas.SetTop(button, 100);
        panel.AddChild(button);

        panel.Measure(new Size(500, 500));
        panel.Arrange(new Rect(0, 0, 500, 500));

        // Center is at local (50, 50).
        // Since rotation is centered, local (50, 50) should remain at parent (150, 150).
        var screenCenter = button.PointToScreen(new Point(50, 50));
        Assert.Equal(150, screenCenter.X, 1e-4f);
        Assert.Equal(150, screenCenter.Y, 1e-4f);

        var localCenter = button.PointToClient(new Point(150, 150));
        Assert.Equal(50, localCenter.X, 1e-4f);
        Assert.Equal(50, localCenter.Y, 1e-4f);
    }

    [Fact]
    public void HitTest_ScaledElement_AccuratelyDetectsHits()
    {
        var panel = new Canvas().Size(500, 500);
        var button = new Button("Click").Size(100, 50).Scale(2f);
        Canvas.SetLeft(button, 100);
        Canvas.SetTop(button, 100);
        panel.AddChild(button);

        panel.Measure(new Size(500, 500));
        panel.Arrange(new Rect(0, 0, 500, 500));

        // Button visually spans:
        // X: 100 to 100 + 100*2 = 300
        // Y: 100 to 100 + 50*2 = 200

        // Inside scaled button (250, 150) -> should hit button
        var hit1 = panel.HitTest(new Point(250, 150));
        Assert.Equal(button, hit1);

        // Outside original bounds (250, 150 is X=250 which was > original 200) -> hits because scaled to 300!
        var hitEdge = panel.HitTest(new Point(290, 190));
        Assert.Equal(button, hitEdge);

        // Outside scaled bounds (310, 150) -> should not hit button
        var hitMiss = panel.HitTest(new Point(310, 150));
        Assert.NotEqual(button, hitMiss);
    }

    [Fact]
    public void HitTest_RotatedElement_AccuratelyDetectsHits()
    {
        var panel = new Canvas().Size(500, 500);
        // Size 100x40 at (100, 100), rotated 90 degrees clockwise around (0, 0)
        // Layout compensates for the -40 X extent by setting Bounds.X = 140,
        // placing the visual bounding box cleanly at X in [100, 140], Y in [100, 200] without leaking outside the slot.
        var button = new Button("Click").Size(100, 40).Rotate(90f);
        Canvas.SetLeft(button, 100);
        Canvas.SetTop(button, 100);
        panel.AddChild(button);

        panel.Measure(new Size(500, 500));
        panel.Arrange(new Rect(0, 0, 500, 500));

        // Test point inside rotated rectangle: X=120, Y=150 (fits in [100, 140] x [100, 200])
        var hitInside = panel.HitTest(new Point(120, 150));
        Assert.Equal(button, hitInside);

        // Test point outside the allocated Canvas slot: X=80, Y=150 (outside the [100, 140] bounds)
        var hitOutside = panel.HitTest(new Point(80, 150));
        Assert.NotEqual(button, hitOutside);
    }

    [Fact]
    public void HitTest_RootScaledWindow_AccuratelyRoutesPointerEvents()
    {
        var root = new Canvas().Size(500, 500).Scale(2f);
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 0 }.Size(200, 30);
        Canvas.SetLeft(slider, 50);
        Canvas.SetTop(slider, 50);
        root.AddChild(slider);

        root.Measure(new Size(500, 500));
        root.Arrange(new Rect(0, 0, 500, 500));

        // Slider in root is at (50, 50), width 200.
        // Center of slider is at local X=100 -> in root X=150 -> on screen X=150 * 2 = 300!
        // Y center is local Y=15 -> in root Y=65 -> on screen Y=65 * 2 = 130!
        var screenPoint = new Point(300, 130);

        var hit = root.HitTest(screenPoint);
        Assert.NotNull(hit);
        Assert.Equal(slider, hit);

        // Send a pointer pressed event with screenPoint
        var e = new PointerEventArgs(screenPoint, screenPoint, PointerButtons.Left);
        hit.DispatchBubblePointerEvent(e, (el, localE) => el.OnPointerPressed(localE));

        // Slider value should be set to 50% (50.0)
        Assert.Equal(50, slider.Value);
    }

    [Fact]
    public void PointToNode_DirectCoordinateMappingBetweenNodes()
    {
        var panel = new Canvas().Size(600, 600);
        var buttonA = new Button("A").Size(100, 50);
        Canvas.SetLeft(buttonA, 50);
        Canvas.SetTop(buttonA, 50);

        var buttonB = new Button("B").Size(100, 50).Scale(2f);
        Canvas.SetLeft(buttonB, 200);
        Canvas.SetTop(buttonB, 100);

        panel.AddChild(buttonA);
        panel.AddChild(buttonB);

        panel.Measure(new Size(600, 600));
        panel.Arrange(new Rect(0, 0, 600, 600));

        // Local (0, 0) in Button A is at screen (50, 50).
        // Button B is at (200, 100) scaled by 2.
        // Screen (50, 50) mapped into Button B:
        // (50 - 200, 50 - 100) = (-150, -50) / 2 = (-75, -25).
        var pointInB = buttonA.PointToNode(Point.Zero, buttonB);
        Assert.Equal(-75, pointInB.X, 1e-4f);
        Assert.Equal(-25, pointInB.Y, 1e-4f);
    }

    [Fact]
    public void ScreenBounds_ReturnsAxisAlignedBoundingBox()
    {
        var panel = new Canvas().Size(500, 500);
        var button = new Button("Click").Size(100, 50).Scale(2f);
        Canvas.SetLeft(button, 50);
        Canvas.SetTop(button, 50);
        panel.AddChild(button);

        panel.Measure(new Size(500, 500));
        panel.Arrange(new Rect(0, 0, 500, 500));

        var screenBounds = button.GetScreenBounds();
        Assert.Equal(50, screenBounds.X);
        Assert.Equal(50, screenBounds.Y);
        Assert.Equal(200, screenBounds.Width);
        Assert.Equal(100, screenBounds.Height);
    }

    [Fact]
    public void Measure_RotatedElement_ExpandsDesiredSize()
    {
        // A 100x100 button rotated 45 degrees around center has an axis-aligned bounding box of 100 * sqrt(2) ≈ 141.42
        var button = new Button("Click").Size(100, 100).RotateCenter(45f);
        var container = new Border { Child = button };

        container.Measure(new Size(500, 500));

        float expected = 100f * MathF.Sqrt(2f); // ≈ 141.421
        Assert.InRange(button.DesiredSize.Width, expected - 0.5f, expected + 0.5f);
        Assert.InRange(button.DesiredSize.Height, expected - 0.5f, expected + 0.5f);
    }

    [Fact]
    public void Arrange_RotatedElement_AlignsWithinParentSlot()
    {
        // Button 100x100 rotated 45 degrees, placed in a 200x200 container with Center alignment
        var button = new Button("Click").Size(100, 100).RotateCenter(45f);
        button.HorizontalAlignment = HorizontalAlignment.Center;
        button.VerticalAlignment = VerticalAlignment.Center;

        var container = new Border { Child = button }.Size(200, 200);
        container.Measure(new Size(200, 200));
        container.Arrange(new Rect(0, 0, 200, 200));

        var screenBounds = button.GetScreenBounds();
        float expectedSize = 100f * MathF.Sqrt(2f); // ≈ 141.421
        float expectedOffset = (200f - expectedSize) * 0.5f; // ≈ 29.289

        Assert.InRange(screenBounds.X, expectedOffset - 0.5f, expectedOffset + 0.5f);
        Assert.InRange(screenBounds.Y, expectedOffset - 0.5f, expectedOffset + 0.5f);
        Assert.InRange(screenBounds.Width, expectedSize - 0.5f, expectedSize + 0.5f);
        Assert.InRange(screenBounds.Height, expectedSize - 0.5f, expectedSize + 0.5f);

        // Entire visual must remain strictly inside the 200x200 container
        Assert.True(screenBounds.X >= 0);
        Assert.True(screenBounds.Y >= 0);
        Assert.True(screenBounds.Right <= 200.5f);
        Assert.True(screenBounds.Bottom <= 200.5f);
    }

    [Fact]
    public void Border_WithRotatedChild_ExpandsToFitRotatedGeometry()
    {
        // 320x170 element rotated 45 degrees
        // Transformed width & height: 320*cos(45) + 170*sin(45) = 490 / sqrt(2) ≈ 346.48
        var liveContainer = new Border
        {
            Width = 320,
            Height = 170
        }.TransformOrigin(0.5f, 0.5f).Rotate(45f);

        var border = new Border
        {
            Padding = new Thickness(16),
            Child = liveContainer
        };

        border.Measure(new Size(float.PositiveInfinity, float.PositiveInfinity));

        float expectedChildSize = (320f + 170f) * MathF.Sin(MathF.PI / 4f); // ≈ 346.48
        float expectedBorderSize = expectedChildSize + 32f; // Child + padding (16*2) ≈ 378.48

        Assert.InRange(liveContainer.DesiredSize.Width, expectedChildSize - 0.5f, expectedChildSize + 0.5f);
        Assert.InRange(liveContainer.DesiredSize.Height, expectedChildSize - 0.5f, expectedChildSize + 0.5f);

        Assert.InRange(border.DesiredSize.Width, expectedBorderSize - 0.5f, expectedBorderSize + 0.5f);
        Assert.InRange(border.DesiredSize.Height, expectedBorderSize - 0.5f, expectedBorderSize + 0.5f);
    }

    [Fact]
    public void StackPanel_WithRotatedChild_PushesDownSubsequentChildren()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 };

        // 100x100 rotated 45 degrees -> height ≈ 141.42
        var rotatedBox = new Border { Width = 100, Height = 100 }.RotateCenter(45f);
        var buttonBelow = new Button("Below").Size(100, 40);

        panel.Add(rotatedBox);
        panel.Add(buttonBelow);

        panel.Measure(new Size(500, 1000));
        panel.Arrange(new Rect(0, 0, 500, 1000));

        float expectedRotatedHeight = 100f * MathF.Sqrt(2f); // ≈ 141.42
        float expectedButtonTop = expectedRotatedHeight + 10f; // height + spacing ≈ 151.42

        Assert.InRange(buttonBelow.Bounds.Y, expectedButtonTop - 0.5f, expectedButtonTop + 0.5f);
        Assert.Equal(40, buttonBelow.Bounds.Height);

        // Visual bounding box of rotated box must not intersect button below
        var rotatedBounds = rotatedBox.GetScreenBounds();
        Assert.True(rotatedBounds.Bottom <= buttonBelow.Bounds.Y + 0.5f);
    }
}