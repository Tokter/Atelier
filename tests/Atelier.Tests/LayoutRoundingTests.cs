using Xunit;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Tests;

public class FractionalElement : UIElement
{
    private readonly Size _size;

    public FractionalElement(float width, float height) => _size = new Size(width, height);

    protected override Size MeasureOverride(Size availableSize) => _size;
}

public class LayoutRoundingTests
{
    [Fact]
    public void Default_KeepsFractionalLayout()
    {
        var element = new FractionalElement(10.3f, 4.6f);
        element.Measure(Size.Infinity);

        Assert.False(element.UseLayoutRounding);
        Assert.Equal(new Size(10.3f, 4.6f), element.DesiredSize);
    }

    [Fact]
    public void DesiredSize_RoundsUp()
    {
        var element = new FractionalElement(10.3f, 4.6f) { UseLayoutRounding = true };
        element.Measure(Size.Infinity);

        Assert.Equal(new Size(11, 5), element.DesiredSize);
    }

    [Fact]
    public void ArrangedChildren_HaveWholePixelEdges_AndStayFlush()
    {
        var panel = new StackPanel { UseLayoutRounding = true, Orientation = Orientation.Horizontal };
        var first = new FractionalElement(10.4f, 10) { HorizontalAlignment = HorizontalAlignment.Left };
        var second = new FractionalElement(10.4f, 10) { HorizontalAlignment = HorizontalAlignment.Left };
        panel.Add(first);
        panel.Add(second);

        panel.Measure(new Size(100.7f, 50.5f));
        panel.Arrange(new Rect(0.4f, 0.6f, 100.7f, 50.5f));

        foreach (var bounds in new[] { panel.Bounds, first.Bounds, second.Bounds })
        {
            Assert.Equal(bounds.X, System.MathF.Round(bounds.X));
            Assert.Equal(bounds.Width, System.MathF.Round(bounds.Width));
        }
        Assert.Equal(first.Bounds.Right, second.Bounds.X);
    }

    [Fact]
    public void Setting_IsInherited()
    {
        var panel = new StackPanel { UseLayoutRounding = true };
        var child = new FractionalElement(1.5f, 1.5f);
        panel.Add(child);

        Assert.True(child.UseLayoutRounding);
    }
}
