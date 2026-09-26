using System;
using Xunit;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming.Material;
using Atelier.Theming.Material.Renderers;

namespace Atelier.Tests;

public class StackPanelLayoutTests
{
    [Fact]
    public void Horizontal_StacksLeftToRight_WithSpacing_AndStretchesHeight()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        var a = new LayoutProbe(30, 10);
        var b = new LayoutProbe(40, 20);
        panel.Children(a, b);

        LayoutAssert.Layout(panel, 200, 50);

        Assert.Equal(new Size(75, 20), panel.DesiredSize);
        LayoutAssert.Rect(0, 0, 30, 50, a.Bounds);
        LayoutAssert.Rect(35, 0, 40, 50, b.Bounds);
    }

    [Fact]
    public void CollapsedChildren_TakeNoSpaceAndNoSpacing()
    {
        var panel = new StackPanel { Spacing = 10 };
        var a = new LayoutProbe(10, 20);
        var hidden = new LayoutProbe(10, 20) { Visibility = Visibility.Collapsed };
        var b = new LayoutProbe(10, 20);
        panel.Children(a, hidden, b);

        LayoutAssert.Layout(panel, 100, 200);

        LayoutAssert.Near(50, panel.DesiredSize.Height); // 20 + 10 + 20
        LayoutAssert.Rect(0, 30, 100, 20, b.Bounds);
        Assert.Equal(Rect.Zero, hidden.Bounds);
    }

    [Fact]
    public void Vertical_MeasuresChildrenWithInfiniteHeight_AndThePanelWidth()
    {
        var panel = new StackPanel();
        var child = new LayoutProbe(10, 10);
        panel.Add(child);

        panel.Measure(new Size(120, 80));

        Assert.Equal(120, child.LastAvailable.Width);
        Assert.True(float.IsPositiveInfinity(child.LastAvailable.Height));
    }

    [Fact]
    public void ChildAlignment_PositionsTheChildAcrossTheStack()
    {
        var panel = new StackPanel();
        var centered = new LayoutProbe(40, 10) { HorizontalAlignment = HorizontalAlignment.Center };
        panel.Add(centered);

        LayoutAssert.Layout(panel, 100, 100);

        LayoutAssert.Rect(30, 0, 40, 10, centered.Bounds);
    }

    [Fact]
    public void RepeatedLayout_DoesNotAllocate()
    {
        var panel = new StackPanel { Spacing = 3 };
        panel.Children(new LayoutProbe(10, 10), new LayoutProbe(20, 20) { Visibility = Visibility.Collapsed }, new LayoutProbe(30, 30));

        Assert.Equal(0, LayoutAllocations.Measure(panel));
    }
}

public class WrapPanelLayoutTests
{
    [Fact]
    public void ChildrenThatExactlyFill_StayOnOneLine_DespiteRounding()
    {
        var panel = new WrapPanel();
        for (int i = 0; i < 3; i++)
        {
            panel.Add(new LayoutProbe(100f / 3f, 10));
        }

        LayoutAssert.Layout(panel, 100, 100);

        LayoutAssert.Near(10, panel.DesiredSize.Height);
    }

    [Fact]
    public void ZeroWidth_PutsEveryChildOnItsOwnLine()
    {
        var panel = new WrapPanel();
        panel.Children(new LayoutProbe(10, 10), new LayoutProbe(10, 10));

        panel.Measure(new Size(0, 100));

        LayoutAssert.Near(20, panel.DesiredSize.Height);
    }

    [Fact]
    public void Vertical_MeasuresChildrenWithTheAvailableWidth()
    {
        var panel = new WrapPanel { Orientation = Orientation.Vertical };
        var child = new LayoutProbe(10, 10);
        panel.Add(child);

        panel.Measure(new Size(150, 90));

        Assert.Equal(new Size(150, 90), child.LastAvailable);
    }

    [Fact]
    public void ChildrenAreStretchedAcrossTheirLine()
    {
        var panel = new WrapPanel();
        var small = new LayoutProbe(20, 10);
        var tall = new LayoutProbe(20, 30);
        panel.Children(small, tall);

        LayoutAssert.Layout(panel, 100, 100);

        LayoutAssert.Rect(0, 0, 20, 30, small.Bounds);
    }

    [Fact]
    public void InvalidItemSize_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new WrapPanel().ItemWidth = 0);
        Assert.Throws<ArgumentException>(() => new WrapPanel().HorizontalSpacing = -2);
    }

    [Fact]
    public void RepeatedLayout_DoesNotAllocate()
    {
        var panel = new WrapPanel { HorizontalSpacing = 2, VerticalSpacing = 2 };
        for (int i = 0; i < 12; i++)
        {
            panel.Add(new LayoutProbe(40, 20));
        }

        Assert.Equal(0, LayoutAllocations.Measure(panel));
    }
}

public class DockPanelLayoutTests
{
    [Fact]
    public void ChildrenDockInOrder_AndTheLastChildFills()
    {
        var panel = new DockPanel();
        var top = new LayoutProbe(10, 20).Dock(Dock.Top);
        var left = new LayoutProbe(30, 10).Dock(Dock.Left);
        var right = new LayoutProbe(40, 10).Dock(Dock.Right);
        var bottom = new LayoutProbe(10, 15).Dock(Dock.Bottom);
        var fill = new LayoutProbe(5, 5);
        panel.Children(top, left, right, bottom, fill);

        LayoutAssert.Layout(panel, 200, 100);

        LayoutAssert.Rect(0, 0, 200, 20, top.Bounds);
        LayoutAssert.Rect(0, 20, 30, 80, left.Bounds);
        LayoutAssert.Rect(160, 20, 40, 80, right.Bounds);
        LayoutAssert.Rect(30, 85, 130, 15, bottom.Bounds);
        LayoutAssert.Rect(30, 20, 130, 65, fill.Bounds);
    }

    [Fact]
    public void WithoutLastChildFill_TheLastChildIsDockedToo()
    {
        var panel = new DockPanel { LastChildFill = false };
        var left = new LayoutProbe(30, 10).Dock(Dock.Left);
        var last = new LayoutProbe(40, 10).Dock(Dock.Left);
        panel.Children(left, last);

        LayoutAssert.Layout(panel, 200, 50);

        LayoutAssert.Rect(30, 0, 40, 50, last.Bounds);
    }

    [Fact]
    public void DesiredSize_CombinesTheDockedStrips()
    {
        var panel = new DockPanel();
        panel.Children(
            new LayoutProbe(50, 100).Dock(Dock.Left),
            new LayoutProbe(80, 30).Dock(Dock.Top),
            new LayoutProbe(20, 20));

        panel.Measure(new Size(1000, 1000));

        // Width: left strip 50 + widest of top (80) and fill (20). Height: tallest of left (100) and top + fill (50).
        Assert.Equal(new Size(130, 100), panel.DesiredSize);
    }

    [Fact]
    public void Spacing_SeparatesDockedChildren()
    {
        var panel = new DockPanel { HorizontalSpacing = 8, VerticalSpacing = 4 };
        var top = new LayoutProbe(10, 20).Dock(Dock.Top);
        var left = new LayoutProbe(30, 10).Dock(Dock.Left);
        var fill = new LayoutProbe(5, 5);
        panel.Children(top, left, fill);

        LayoutAssert.Layout(panel, 200, 100);

        LayoutAssert.Rect(0, 24, 30, 76, left.Bounds);
        LayoutAssert.Rect(38, 24, 162, 76, fill.Bounds);
        // Width: 30 + 8 + 5. Height: 20 + 4 + the taller of left (10) and fill (5).
        Assert.Equal(new Size(43, 34), panel.DesiredSize);
    }

    [Fact]
    public void DockedChildren_AreClampedWhenThePanelIsTooSmall()
    {
        var panel = new DockPanel { LastChildFill = false };
        var left = new LayoutProbe(80, 10).Dock(Dock.Left);
        var right = new LayoutProbe(80, 10).Dock(Dock.Right);
        panel.Children(left, right);

        LayoutAssert.Layout(panel, 100, 50);

        LayoutAssert.Rect(0, 0, 80, 50, left.Bounds);
        LayoutAssert.Rect(80, 0, 20, 50, right.Bounds);
    }

    [Fact]
    public void RepeatedLayout_DoesNotAllocate()
    {
        var panel = new DockPanel { HorizontalSpacing = 2 };
        panel.Children(new LayoutProbe(10, 10).Dock(Dock.Top), new LayoutProbe(10, 10).Dock(Dock.Right), new LayoutProbe(10, 10));

        Assert.Equal(0, LayoutAllocations.Measure(panel));
    }
}

public class CanvasLayoutTests
{
    [Fact]
    public void ChildrenArePlacedAtTheirCoordinates_WithTheirDesiredSize()
    {
        var canvas = new Canvas();
        var child = new LayoutProbe(30, 20);
        Canvas.SetLeft(child, 15);
        Canvas.SetTop(child, 25);
        canvas.Add(child);

        LayoutAssert.Layout(canvas, 200, 100);

        LayoutAssert.Rect(15, 25, 30, 20, child.Bounds);
        Assert.Equal(Size.Zero, canvas.DesiredSize);
        Assert.True(float.IsPositiveInfinity(child.LastAvailable.Width));
    }

    [Fact]
    public void RightAndBottom_PositionFromTheFarEdges()
    {
        var canvas = new Canvas();
        var child = new LayoutProbe(30, 20);
        Canvas.SetRight(child, 10);
        Canvas.SetBottom(child, 5);
        canvas.Add(child);

        LayoutAssert.Layout(canvas, 200, 100);

        LayoutAssert.Rect(160, 75, 30, 20, child.Bounds);
    }

    [Fact]
    public void LeftAndTop_WinOverRightAndBottom_AndUnsetMeansZero()
    {
        var canvas = new Canvas();
        var both = new LayoutProbe(10, 10);
        Canvas.SetLeft(both, 5);
        Canvas.SetRight(both, 50);
        var unset = new LayoutProbe(10, 10);
        canvas.Children(both, unset);

        LayoutAssert.Layout(canvas, 200, 100);

        LayoutAssert.Rect(5, 0, 10, 10, both.Bounds);
        LayoutAssert.Rect(0, 0, 10, 10, unset.Bounds);
        Assert.True(float.IsNaN(Canvas.GetLeft(unset)));
    }

    [Fact]
    public void RepeatedLayout_DoesNotAllocate()
    {
        var canvas = new Canvas();
        var child = new LayoutProbe(10, 10);
        Canvas.SetRight(child, 4);
        canvas.Add(child);

        Assert.Equal(0, LayoutAllocations.Measure(canvas));
    }
}

public class BorderLayoutTests
{
    [Fact]
    public void Child_IsInsetByBorderThicknessAndPadding()
    {
        var child = new LayoutProbe(50, 20);
        var border = new Border { BorderThickness = new Thickness(2), Padding = new Thickness(10, 5, 10, 5), Child = child };

        LayoutAssert.Layout(border, 200, 100);

        Assert.Equal(new Size(74, 34), border.DesiredSize);
        LayoutAssert.Rect(12, 7, 176, 86, child.Bounds);
    }

    [Fact]
    public void WithoutAVisibleChild_TheBorderDesiresOnlyItsInset()
    {
        var border = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(4) };
        border.Measure(new Size(100, 100));
        Assert.Equal(new Size(10, 10), border.DesiredSize);

        var hidden = new LayoutProbe(50, 50) { Visibility = Visibility.Collapsed };
        border.Child = hidden;
        LayoutAssert.Layout(border, 100, 100);
        Assert.Equal(new Size(10, 10), border.DesiredSize);
        Assert.Equal(Rect.Zero, hidden.Bounds);
    }

    [Fact]
    public void SettingTheSameChild_KeepsIt()
    {
        var child = new LayoutProbe(1, 1);
        var border = new Border { Child = child };

        border.Child = child;

        Assert.Same(border, child.Parent);
        Assert.Single(border.Children);
    }
}

public class UniformGridLayoutTests
{
    [Fact]
    public void WithoutRowsOrColumns_TheGridIsAsSquareAsPossible()
    {
        var grid = new UniformGrid();
        var children = new LayoutProbe[5];
        for (int i = 0; i < children.Length; i++)
        {
            grid.Add(children[i] = new LayoutProbe(10, 10));
        }

        LayoutAssert.Layout(grid, 300, 200);

        // 5 children: 3 columns, 2 rows of 100 x 100.
        LayoutAssert.Rect(0, 0, 100, 100, children[0].Bounds);
        LayoutAssert.Rect(200, 0, 100, 100, children[2].Bounds);
        LayoutAssert.Rect(100, 100, 100, 100, children[4].Bounds);
        Assert.Equal(new Size(30, 20), grid.DesiredSize);
    }

    [Fact]
    public void FixedColumns_WithFirstColumnAndSpacing()
    {
        var grid = new UniformGrid { Columns = 2, FirstColumn = 1, ColumnSpacing = 10, RowSpacing = 10 };
        var a = new LayoutProbe(10, 10);
        var b = new LayoutProbe(10, 10);
        grid.Children(a, b);

        LayoutAssert.Layout(grid, 210, 210);

        LayoutAssert.Rect(110, 0, 100, 100, a.Bounds);
        LayoutAssert.Rect(0, 110, 100, 100, b.Bounds);
    }

    [Fact]
    public void ChildrenBeyondTheFixedCells_AreNotShown()
    {
        var grid = new UniformGrid { Rows = 1, Columns = 2 };
        var a = new LayoutProbe(10, 10);
        var b = new LayoutProbe(10, 10);
        var extra = new LayoutProbe(10, 10);
        grid.Children(a, b, extra);

        LayoutAssert.Layout(grid, 200, 50);

        LayoutAssert.Rect(100, 0, 100, 50, b.Bounds);
        Assert.Equal(Rect.Zero, extra.Bounds);
    }

    [Fact]
    public void CellsAreAsLargeAsTheLargestChild()
    {
        var grid = new UniformGrid { Columns = 2 };
        grid.Children(new LayoutProbe(40, 10), new LayoutProbe(10, 25), new LayoutProbe(5, 5));

        grid.Measure(new Size(float.PositiveInfinity, float.PositiveInfinity));

        Assert.Equal(new Size(80, 50), grid.DesiredSize);
    }

    [Fact]
    public void CollapsedChildren_DoNotTakeACell()
    {
        var grid = new UniformGrid { Columns = 2 };
        var a = new LayoutProbe(10, 10);
        var hidden = new LayoutProbe(10, 10) { Visibility = Visibility.Collapsed };
        var b = new LayoutProbe(10, 10);
        grid.Children(a, hidden, b);

        LayoutAssert.Layout(grid, 200, 100);

        LayoutAssert.Rect(100, 0, 100, 100, b.Bounds);
    }

    [Fact]
    public void RepeatedLayout_DoesNotAllocate()
    {
        var grid = new UniformGrid { ColumnSpacing = 2 };
        for (int i = 0; i < 7; i++)
        {
            grid.Add(new LayoutProbe(10, 10));
        }

        Assert.Equal(0, LayoutAllocations.Measure(grid));
    }
}

public class PanelBackgroundTests
{
    [Fact]
    public void Background_IsTransparentByDefault_AndDrawnForEveryPanelType()
    {
        Assert.Equal(Color.Transparent, new StackPanel().Background);

        var theme = MaterialTheme.CreateLight();
        Assert.IsType<MaterialPanelRenderer>(theme.Renderers.GetRenderer(typeof(Grid)));
        Assert.IsType<MaterialPanelRenderer>(theme.Renderers.GetRenderer(typeof(UniformGrid)));
    }
}
