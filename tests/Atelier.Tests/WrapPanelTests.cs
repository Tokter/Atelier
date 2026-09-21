using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Xunit;

namespace Atelier.Tests;

public class WrapPanelTests
{
    private class FixedSizeElement(float width, float height) : UIElement
    {
        protected override Size MeasureOverride(Size availableSize) => new(width, height);
    }

    [Fact]
    public void WrapPanel_Horizontal_WrapsChildrenWhenExceedingWidth()
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };

        var c1 = new FixedSizeElement(40, 20);
        var c2 = new FixedSizeElement(40, 20);
        var c3 = new FixedSizeElement(40, 20);

        panel.Add(c1);
        panel.Add(c2);
        panel.Add(c3);

        panel.Measure(new Size(100, 1000));
        Assert.Equal(80, panel.DesiredSize.Width);
        Assert.Equal(40, panel.DesiredSize.Height);

        panel.Arrange(new Rect(0, 0, 100, 1000));

        Assert.Equal(new Rect(0, 0, 40, 20), c1.Bounds);
        Assert.Equal(new Rect(40, 0, 40, 20), c2.Bounds);
        Assert.Equal(new Rect(0, 20, 40, 20), c3.Bounds);
    }

    [Fact]
    public void WrapPanel_Horizontal_SpacingAppliedCorrectly()
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalSpacing = 10,
            VerticalSpacing = 5
        };

        var c1 = new FixedSizeElement(40, 20);
        var c2 = new FixedSizeElement(40, 20);
        var c3 = new FixedSizeElement(40, 20);

        panel.Add(c1);
        panel.Add(c2);
        panel.Add(c3);

        // Line 1: c1 (40) + spacing (10) + c2 (40) = 90.
        // Line 2: wraps. c3 (40)
        panel.Measure(new Size(100, 1000));
        Assert.Equal(90, panel.DesiredSize.Width);
        Assert.Equal(45, panel.DesiredSize.Height); // 20 + 5 + 20 = 45

        panel.Arrange(new Rect(0, 0, 100, 1000));

        Assert.Equal(new Rect(0, 0, 40, 20), c1.Bounds);
        Assert.Equal(new Rect(50, 0, 40, 20), c2.Bounds);
        Assert.Equal(new Rect(0, 25, 40, 20), c3.Bounds);
    }

    [Fact]
    public void WrapPanel_Vertical_WrapsChildrenWhenExceedingHeight()
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalSpacing = 8,
            VerticalSpacing = 6
        };

        var c1 = new FixedSizeElement(30, 40);
        var c2 = new FixedSizeElement(30, 40);
        var c3 = new FixedSizeElement(30, 40);

        panel.Add(c1);
        panel.Add(c2);
        panel.Add(c3);

        // Column 1: c1 (40) + vSpacing (6) + c2 (40) = 86 <= 100
        // Column 2: c3 (40)
        panel.Measure(new Size(1000, 100));
        Assert.Equal(68, panel.DesiredSize.Width); // 30 + 8 + 30 = 68
        Assert.Equal(86, panel.DesiredSize.Height);

        panel.Arrange(new Rect(0, 0, 1000, 100));

        Assert.Equal(new Rect(0, 0, 30, 40), c1.Bounds);
        Assert.Equal(new Rect(0, 46, 30, 40), c2.Bounds);
        Assert.Equal(new Rect(38, 0, 30, 40), c3.Bounds);
    }

    [Fact]
    public void WrapPanel_FixedItemWidthAndHeight_ConstrainsChildren()
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemWidth = 50,
            ItemHeight = 30
        };

        var c1 = new FixedSizeElement(10, 10);
        var c2 = new FixedSizeElement(10, 10);

        panel.Add(c1);
        panel.Add(c2);

        panel.Measure(new Size(200, 200));
        Assert.Equal(100, panel.DesiredSize.Width);
        Assert.Equal(30, panel.DesiredSize.Height);

        panel.Arrange(new Rect(0, 0, 200, 200));

        Assert.Equal(new Rect(0, 0, 50, 30), c1.Bounds);
        Assert.Equal(new Rect(50, 0, 50, 30), c2.Bounds);
    }

    [Fact]
    public void WrapPanel_CollapsedChildren_AreIgnored()
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalSpacing = 10
        };

        var c1 = new FixedSizeElement(40, 20);
        var c2 = new FixedSizeElement(40, 20) { Visibility = Visibility.Collapsed };
        var c3 = new FixedSizeElement(40, 20);

        panel.Add(c1);
        panel.Add(c2);
        panel.Add(c3);

        panel.Measure(new Size(200, 200));
        Assert.Equal(90, panel.DesiredSize.Width); // 40 + 10 + 40
        Assert.Equal(20, panel.DesiredSize.Height);

        panel.Arrange(new Rect(0, 0, 200, 200));

        Assert.Equal(new Rect(0, 0, 40, 20), c1.Bounds);
        Assert.Equal(Rect.Zero, c2.Bounds);
        Assert.Equal(new Rect(50, 0, 40, 20), c3.Bounds);
    }

    [Fact]
    public void WrapPanel_FluentMarkup_ChainsCorrectly()
    {
        var panel = new WrapPanel()
            .Orientation(Orientation.Vertical)
            .ItemWidth(60)
            .ItemHeight(40)
            .HorizontalSpacing(12)
            .VerticalSpacing(16);

        Assert.Equal(Orientation.Vertical, panel.Orientation);
        Assert.Equal(60, panel.ItemWidth);
        Assert.Equal(40, panel.ItemHeight);
        Assert.Equal(12, panel.HorizontalSpacing);
        Assert.Equal(16, panel.VerticalSpacing);

        panel.Spacing(8);
        Assert.Equal(8, panel.HorizontalSpacing);
        Assert.Equal(8, panel.VerticalSpacing);

        panel.Spacing(14, 22);
        Assert.Equal(14, panel.HorizontalSpacing);
        Assert.Equal(22, panel.VerticalSpacing);
    }
}
