using System.Linq;
using System.Numerics;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Tests;

public class GridSplitterTests
{
    private const float Width = 1000, Height = 400;

    // A grid "a,Auto,b" with a splitter in the middle column, laid out at 1000×400.
    private static (Grid Grid, GridSplitter Splitter) ThreeColumns(string columns = "*,Auto,*")
    {
        var splitter = new GridSplitter().Column(1);
        var grid = new Grid().Columns(columns).Children(new Border(), splitter, new Border().Column(2));
        Layout(grid);
        return (grid, splitter);
    }

    private static void Layout(UIElement root)
    {
        root.Measure(new Size(Width, Height));
        root.Arrange(new Rect(0, 0, Width, Height));
    }

    // Drags the splitter by dx/dy pixels (in window coordinates) and releases it.
    private static void Drag(GridSplitter splitter, float dx, float dy = 0, bool release = true)
    {
        var start = splitter.PointToScreen(new Point(splitter.Bounds.Width / 2, splitter.Bounds.Height / 2));
        var end = new Point(start.X + dx, start.Y + dy);
        splitter.OnPointerPressed(new PointerEventArgs(start, start, PointerButtons.Left, clickCount: 1));
        splitter.OnPointerMoved(new PointerEventArgs(end, end));
        if (release)
        {
            splitter.OnPointerReleased(new PointerEventArgs(end, end, PointerButtons.Left));
        }
    }

    private static float W(Grid grid, int column) => grid.ColumnDefinitions[column].ActualWidth;

    [Fact]
    public void Splitter_IsEightPixelsWide_InAnAutoColumn()
    {
        var (grid, splitter) = ThreeColumns();

        Assert.Equal(GridResizeDirection.Columns, splitter.ActualDirection);
        Assert.Equal(GridSplitter.DefaultThickness, W(grid, 1));
        Assert.Equal((Width - 8) / 2, W(grid, 0), 1);
    }

    [Fact]
    public void StarColumns_KeepTheirTotalWeight_AndGetNewProportions()
    {
        var (grid, splitter) = ThreeColumns();
        Drag(splitter, 100);
        Layout(grid);

        var first = grid.ColumnDefinitions[0].Width;
        var second = grid.ColumnDefinitions[2].Width;
        Assert.True(first.IsStar && second.IsStar);
        Assert.Equal(2f, first.Value + second.Value, 3);
        Assert.Equal((Width - 8) / 2 + 100, W(grid, 0), 1);
        Assert.Equal((Width - 8) / 2 - 100, W(grid, 2), 1);
    }

    [Fact]
    public void NextToAStarColumn_OnlyTheFixedColumnChanges()
    {
        var (grid, splitter) = ThreeColumns("200,Auto,*");
        Drag(splitter, 50);
        Layout(grid);

        Assert.Equal(GridLength.Pixels(250), grid.ColumnDefinitions[0].Width);
        Assert.True(grid.ColumnDefinitions[2].Width.IsStar);
        Assert.Equal(250, W(grid, 0), 1);
    }

    [Fact]
    public void AutoAndPixelColumns_BecomePixelSizes()
    {
        var content = new Border().Width(120);
        var splitter = new GridSplitter().Column(1);
        var grid = new Grid().Columns("Auto,Auto,300").Children(content, splitter, new Border().Column(2));
        Layout(grid);

        Drag(splitter, 30);
        Layout(grid);

        Assert.Equal(GridLength.Pixels(150), grid.ColumnDefinitions[0].Width);
        Assert.Equal(GridLength.Pixels(270), grid.ColumnDefinitions[2].Width);
    }

    [Fact]
    public void Limits_StopTheDrag()
    {
        var (grid, splitter) = ThreeColumns();
        grid.ColumnDefinitions[2].MinWidth = 400;
        grid.ColumnDefinitions[0].MinWidth = 300;
        Layout(grid);

        Drag(splitter, 1000);
        Layout(grid);
        Assert.Equal(400, W(grid, 2), 1);

        Drag(splitter, -1000);
        Layout(grid);
        Assert.Equal(300, W(grid, 0), 1);
    }

    [Fact]
    public void DragIncrement_SnapsTheChange()
    {
        var (grid, splitter) = ThreeColumns("200,Auto,*");
        splitter.DragIncrement(25);
        Drag(splitter, 37);

        Assert.Equal(GridLength.Pixels(225), grid.ColumnDefinitions[0].Width);
    }

    [Fact]
    public void Escape_CancelsTheDrag()
    {
        var (grid, splitter) = ThreeColumns("200,Auto,*");
        GridSplitterDragCompletedEventArgs? completed = null;
        splitter.OnDragCompleted((_, e) => completed = e);

        Drag(splitter, 80, release: false);
        Assert.Equal(GridLength.Pixels(280), grid.ColumnDefinitions[0].Width);
        splitter.OnKeyDown(new KeyEventArgs(Key.Escape, 0, ModifierKeys.None, true));

        Assert.Equal(GridLength.Pixels(200), grid.ColumnDefinitions[0].Width);
        Assert.True(completed!.Canceled);
        Assert.False(splitter.IsDragging);
    }

    [Fact]
    public void ShowsPreview_ResizesOnlyOnRelease()
    {
        var (grid, splitter) = ThreeColumns("200,Auto,*");
        splitter.ShowsPreview();

        Drag(splitter, 60, release: false);
        Assert.Equal(GridLength.Pixels(200), grid.ColumnDefinitions[0].Width);
        Assert.Equal(Matrix3x2.CreateTranslation(60, 0), splitter.RenderTransform);

        var end = splitter.PointToScreen(new Point(4, 4));
        splitter.OnPointerReleased(new PointerEventArgs(end, end, PointerButtons.Left));
        Assert.Equal(GridLength.Pixels(260), grid.ColumnDefinitions[0].Width);
        Assert.True(splitter.RenderTransform.IsIdentity);
    }

    [Fact]
    public void ArrowKeys_ResizeByTheKeyboardIncrement()
    {
        var (grid, splitter) = ThreeColumns("200,Auto,*");
        splitter.OnKeyDown(new KeyEventArgs(Key.Right, 0, ModifierKeys.None, true));
        Layout(grid);
        Assert.Equal(GridLength.Pixels(210), grid.ColumnDefinitions[0].Width);

        splitter.KeyboardIncrement(5);
        splitter.OnKeyDown(new KeyEventArgs(Key.Left, 0, ModifierKeys.None, true));
        Assert.Equal(GridLength.Pixels(205), grid.ColumnDefinitions[0].Width);
    }

    [Fact]
    public void DoubleClick_RestoresTheOriginalSizes()
    {
        var (grid, splitter) = ThreeColumns("200,Auto,*");
        int resized = 0;
        splitter.OnResized(() => resized++);
        Drag(splitter, 50);
        Layout(grid);
        Drag(splitter, 50);
        Assert.Equal(GridLength.Pixels(300), grid.ColumnDefinitions[0].Width);

        var point = splitter.PointToScreen(new Point(4, 4));
        splitter.OnPointerPressed(new PointerEventArgs(point, point, PointerButtons.Left, clickCount: 2));

        Assert.Equal(GridLength.Pixels(200), grid.ColumnDefinitions[0].Width);
        Assert.Equal(3, resized);
    }

    [Fact]
    public void RowSplitter_ResizesRows()
    {
        var splitter = new GridSplitter().Row(1);
        var grid = new Grid().Rows("*,Auto,100").Children(new Border(), splitter, new Border().Row(2));
        Layout(grid);

        Assert.Equal(GridResizeDirection.Rows, splitter.ActualDirection);
        Drag(splitter, 0, -40);
        Assert.Equal(GridLength.Pixels(140), grid.RowDefinitions[2].Height);
    }

    [Theory]
    [InlineData(HorizontalAlignment.Right, 0, 1)]
    [InlineData(HorizontalAlignment.Left, 0, 1)]
    public void CellEdgeSplitter_ResizesByAlignment(HorizontalAlignment alignment, int expectedFirst, int expectedSecond)
    {
        // A splitter at the right edge of column 0 resizes columns 0 and 1; at the left edge of column 1 the same pair.
        int column = alignment == HorizontalAlignment.Right ? 0 : 1;
        var splitter = new GridSplitter().Column(column).HorizontalAlignment(alignment);
        var grid = new Grid().Columns("300,*").Children(new Border(), new Border().Column(1), splitter);
        Layout(grid);

        Drag(splitter, 40);
        Layout(grid);

        Assert.Equal(GridLength.Pixels(340), grid.ColumnDefinitions[expectedFirst].Width);
        Assert.True(grid.ColumnDefinitions[expectedSecond].Width.IsStar);
    }

    [Fact]
    public void SplitterWithoutANeighbor_DoesNothing()
    {
        var splitter = new GridSplitter();
        var grid = new Grid().Columns("300").Children(splitter);
        Layout(grid);

        Drag(splitter, 50);

        Assert.Equal(GridLength.Pixels(300), grid.ColumnDefinitions[0].Width);
        Assert.False(splitter.IsDragging);
    }

    [Fact]
    public void Drag_InAScaledGrid_FollowsThePointer()
    {
        var (grid, splitter) = ThreeColumns("200,Auto,*");
        var root = new StackPanel().Children(grid);
        grid.RenderTransformOrigin(0, 0).RenderScale(2);
        Layout(root);

        // 100 window pixels at 2× zoom are 50 grid pixels.
        Drag(splitter, 100);
        Assert.Equal(GridLength.Pixels(250), grid.ColumnDefinitions[0].Width);
    }

    [Fact]
    public void Cursor_ShowsTheResizeArrowForTheDirection()
    {
        var (_, splitter) = ThreeColumns();
        Assert.Equal(CursorType.SizeWestEast, UIElement.GetEffectiveCursor(splitter));

        splitter.ResizeDirection(GridResizeDirection.Rows);
        Assert.Equal(CursorType.SizeNorthSouth, UIElement.GetEffectiveCursor(splitter));

        splitter.Cursor(CursorType.Hand);
        Assert.Equal(CursorType.Hand, UIElement.GetEffectiveCursor(splitter));
    }

    [Fact]
    public void Cursor_IsInheritedFromTheNearestAncestor()
    {
        var label = new TextBlock("Link");
        var card = new Border().Cursor(CursorType.Hand).Child(label);
        var textBox = new TextBox();
        new StackPanel().Children(card, textBox);

        Assert.Equal(CursorType.Hand, UIElement.GetEffectiveCursor(label));
        Assert.Equal(CursorType.IBeam, UIElement.GetEffectiveCursor(textBox));
        Assert.Equal(CursorType.Arrow, UIElement.GetEffectiveCursor(null));
    }

    [Fact]
    public void PropertyGrid_LabelSplitter_ResizesAllRows()
    {
        var grid = new PropertyGrid().SelectedObject(new SettingsModel());
        grid.Measure(new Size(600, 800));
        grid.Arrange(new Rect(0, 0, 600, 800));

        // One splitter for the whole grid, as tall as the rows area (so it crosses the category headers too).
        var splitter = Assert.Single(Descendants(grid).OfType<GridSplitter>());
        var scrollViewer = Descendants(grid).OfType<ScrollViewer>().First();
        Assert.Equal(scrollViewer.Bounds.Height, splitter.Bounds.Height, 1);
        AssertNoEditorOverlaps(grid, splitter);

        Drag(splitter, 40);
        grid.Measure(new Size(600, 800));
        grid.Arrange(new Rect(0, 0, 600, 800));

        Assert.Equal(200f, grid.LabelWidth);
        var rowGrids = Descendants(grid).OfType<Grid>().Where(g => g.ColumnDefinitions.Count == 3 && g != splitter.Parent).ToList();
        Assert.NotEmpty(rowGrids);
        Assert.All(rowGrids, row => Assert.Equal(GridLength.Pixels(200), row.ColumnDefinitions[0].Width));
        AssertNoEditorOverlaps(grid, splitter);

        // Changing LabelWidth moves the splitter too.
        grid.LabelWidth = 120;
        grid.Measure(new Size(600, 800));
        grid.Arrange(new Rect(0, 0, 600, 800));
        AssertNoEditorOverlaps(grid, splitter);
    }

    [Fact]
    public void TextBlock_ShowsItsTextAsToolTip_OnlyWhileTrimmed()
    {
        var text = new TextBlock("A rather long property name").TextTrimming().ShowsToolTipWhenTrimmed();
        text.Measure(new Size(60, 40));
        text.Arrange(new Rect(0, 0, 60, 40));
        Assert.True(text.IsTextTrimmed);
        Assert.Equal("A rather long property name", ToolTipService.GetEffectiveToolTip(text));

        text.Measure(new Size(600, 40));
        text.Arrange(new Rect(0, 0, 600, 40));
        Assert.False(text.IsTextTrimmed);
        Assert.Null(ToolTipService.GetEffectiveToolTip(text));

        // An explicit tooltip wins.
        text.ToolTip("Explicit");
        Assert.Equal("Explicit", ToolTipService.GetEffectiveToolTip(text));
    }

    [Fact]
    public void PropertyGrid_NarrowLabels_TrimInsideTheirColumn_WithTheFullNameAsToolTip()
    {
        var grid = new PropertyGrid().SelectedObject(new SettingsModel());
        grid.LabelWidth = 60;
        grid.Measure(new Size(600, 800));
        grid.Arrange(new Rect(0, 0, 600, 800));

        var name = Descendants(grid).OfType<TextBlock>().First(t => t.Text == "Server Host");
        var column = (Grid)name.Parent!;
        Assert.True(column.ClipToBounds);
        Assert.True(name.IsTextTrimmed);
        Assert.True(name.Bounds.Right <= column.Bounds.Width + 0.5f, "The trimmed name must fit its label column.");
        Assert.Equal("Server Host", ToolTipService.GetEffectiveToolTip(name));

        grid.LabelWidth = 200;
        grid.Measure(new Size(600, 800));
        grid.Arrange(new Rect(0, 0, 600, 800));
        Assert.False(name.IsTextTrimmed);
        Assert.Null(ToolTipService.GetEffectiveToolTip(name));
    }

    // Every editor (the third column of a row) starts right of the splitter; every label ends left of it.
    private static void AssertNoEditorOverlaps(PropertyGrid grid, GridSplitter splitter)
    {
        float left = splitter.PointToScreen(Point.Zero).X;
        float right = left + splitter.Bounds.Width;
        foreach (var row in Descendants(grid).OfType<Grid>().Where(g => g.ColumnDefinitions.Count == 3 && g != splitter.Parent))
        {
            foreach (var child in row.Children.OfType<UIElement>())
            {
                float x = child.PointToScreen(Point.Zero).X;
                if (Grid.GetColumn(child) == 2)
                {
                    Assert.True(x >= right - 0.5f, $"An editor at {x} overlaps the splitter ending at {right}.");
                }
                else if (Grid.GetColumn(child) == 0)
                {
                    Assert.True(x + child.Bounds.Width <= left + 0.5f, $"A label ending at {x + child.Bounds.Width} overlaps the splitter at {left}.");
                }
            }
        }
    }

    private static System.Collections.Generic.IEnumerable<VisualNode> Descendants(VisualNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
