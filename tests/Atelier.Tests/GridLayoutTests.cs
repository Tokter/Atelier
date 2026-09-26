using System;
using Xunit;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Tests;

/// <summary>A child with a fixed desired size that records the constraint it was measured with.</summary>
public sealed class LayoutProbe(float width, float height) : UIElement
{
    public Size LastAvailable { get; private set; }
    public int MeasureCount { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        LastAvailable = availableSize;
        MeasureCount++;
        return new Size(width, height);
    }
}

/// <summary>A child whose height depends on its width, like wrapping text: it keeps a constant area.</summary>
public sealed class WrappingProbe(float area, float preferredWidth) : UIElement
{
    protected override Size MeasureOverride(Size availableSize)
    {
        float width = Math.Min(preferredWidth, availableSize.Width);
        return new Size(width, width > 0 ? area / width : 0);
    }
}

internal static class LayoutAssert
{
    public static void Rect(float x, float y, float width, float height, Rect actual)
    {
        const float tolerance = 0.01f;
        Assert.True(
            MathF.Abs(actual.X - x) < tolerance && MathF.Abs(actual.Y - y) < tolerance &&
            MathF.Abs(actual.Width - width) < tolerance && MathF.Abs(actual.Height - height) < tolerance,
            $"Expected ({x}, {y}, {width}, {height}) but was ({actual.X}, {actual.Y}, {actual.Width}, {actual.Height}).");
    }

    public static void Near(float expected, float actual) =>
        Assert.True(MathF.Abs(expected - actual) < 0.01f, $"Expected {expected} but was {actual}.");

    public static void Layout(UIElement element, float width, float height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
    }
}

public class GridLayoutTests
{
    [Fact]
    public void PixelAutoAndWeightedStarColumns_ShareTheWidth()
    {
        var grid = new Grid().Columns(GridLength.Pixels(100), GridLength.Auto, GridLength.Star, GridLength.Stars(2));
        var pixel = new LayoutProbe(10, 10).Column(0);
        var auto = new LayoutProbe(50, 10).Column(1);
        var star = new LayoutProbe(10, 10).Column(2);
        var doubleStar = new LayoutProbe(10, 10).Column(3);
        grid.Children(pixel, auto, star, doubleStar);

        LayoutAssert.Layout(grid, 400, 100);

        // 400 - 100 - 50 = 250 for the stars, split 1:2.
        LayoutAssert.Rect(0, 0, 100, 100, pixel.Bounds);
        LayoutAssert.Rect(100, 0, 50, 100, auto.Bounds);
        LayoutAssert.Rect(150, 0, 83.333f, 100, star.Bounds);
        LayoutAssert.Rect(233.333f, 0, 166.667f, 100, doubleStar.Bounds);
        LayoutAssert.Near(83.333f, grid.ColumnDefinitions[2].ActualWidth);
        LayoutAssert.Near(233.333f, grid.ColumnDefinitions[3].Offset);
    }

    [Fact]
    public void AutoRows_SizeToTheirTallestChild_AndStarRowsGetTheRest()
    {
        var grid = new Grid().Rows(GridLength.Auto, GridLength.Star);
        var header = new LayoutProbe(10, 30).Row(0);
        var tallerHeader = new LayoutProbe(10, 45).Row(0);
        var body = new LayoutProbe(10, 10).Row(1);
        grid.Children(header, tallerHeader, body);

        LayoutAssert.Layout(grid, 200, 300);

        LayoutAssert.Near(45, grid.RowDefinitions[0].ActualHeight);
        LayoutAssert.Rect(0, 45, 200, 255, body.Bounds);
    }

    [Fact]
    public void StarRowChildren_AreMeasuredWithTheirRowHeight_NotTheWholeGrid()
    {
        var grid = new Grid().Rows(GridLength.Auto, GridLength.Star);
        var header = new LayoutProbe(10, 100).Row(0);
        var body = new LayoutProbe(10, 10).Row(1);
        grid.Children(header, body);

        grid.Measure(new Size(200, 300));

        Assert.Equal(new Size(200, 200), body.LastAvailable);
    }

    [Fact]
    public void AutoColumnChildren_AreMeasuredWithInfiniteWidth()
    {
        var grid = new Grid().Columns(GridLength.Auto, GridLength.Star);
        var label = new LayoutProbe(40, 10).Column(0);
        grid.Add(label);

        grid.Measure(new Size(300, 100));

        Assert.True(float.IsPositiveInfinity(label.LastAvailable.Width));
    }

    [Fact]
    public void CollapsedChildren_DoNotSizeAutoColumns()
    {
        var grid = new Grid().Columns(GridLength.Auto, GridLength.Star);
        var hidden = new LayoutProbe(80, 10) { Visibility = Visibility.Collapsed }.Column(0);
        var visible = new LayoutProbe(20, 10).Column(0);
        grid.Children(hidden, visible);

        LayoutAssert.Layout(grid, 200, 50);

        LayoutAssert.Near(20, grid.ColumnDefinitions[0].ActualWidth);
        Assert.Equal(Core.Primitives.Rect.Zero, hidden.Bounds);
    }

    [Fact]
    public void SpanningChild_GrowsTheAutoColumnsItSpans()
    {
        var grid = new Grid().Columns(GridLength.Auto, GridLength.Auto, GridLength.Star);
        var wide = new LayoutProbe(100, 10).Column(0).ColumnSpan(2);
        var a = new LayoutProbe(20, 10).Column(0).Row(0);
        var b = new LayoutProbe(20, 10).Column(1).Row(0);
        grid.Children(wide, a, b);

        LayoutAssert.Layout(grid, 400, 50);

        // The spanning child needs 60 more than the two columns have; each auto column gets half.
        LayoutAssert.Near(50, grid.ColumnDefinitions[0].ActualWidth);
        LayoutAssert.Near(50, grid.ColumnDefinitions[1].ActualWidth);
        LayoutAssert.Rect(0, 0, 100, 50, wide.Bounds);
    }

    [Fact]
    public void Spacing_SeparatesColumnsAndRows_AndIsIncludedInSpans()
    {
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 5 }
            .Columns(GridLength.Star, GridLength.Star, GridLength.Star)
            .Rows(GridLength.Pixels(20), GridLength.Pixels(20));
        var spanning = new LayoutProbe(10, 10).Column(1).ColumnSpan(2).Row(1);
        grid.Add(spanning);

        LayoutAssert.Layout(grid, 320, 100);

        // (320 - 2 * 10) / 3 = 100 per column.
        LayoutAssert.Rect(110, 25, 210, 20, spanning.Bounds);
        LayoutAssert.Near(45, grid.DesiredSize.Height); // 20 + 5 + 20

    }

    [Fact]
    public void StarColumns_GetNothing_WhenFixedColumnsTakeAllSpace_AndRecoverAfterwards()
    {
        var grid = new Grid().Columns(GridLength.Pixels(300), GridLength.Star);
        var star = new LayoutProbe(10, 10).Column(1);
        grid.Add(star);

        LayoutAssert.Layout(grid, 500, 50);
        LayoutAssert.Near(200, star.Bounds.Width);

        LayoutAssert.Layout(grid, 200, 50);
        LayoutAssert.Near(0, star.Bounds.Width);
        LayoutAssert.Near(0, star.LastAvailable.Width); // not the stale 200 from the previous pass

        LayoutAssert.Layout(grid, 500, 50);
        LayoutAssert.Near(200, star.Bounds.Width);
    }

    [Fact]
    public void InfiniteWidth_StarColumnsSizeToContent_KeepingTheirProportions()
    {
        var grid = new Grid().Columns(GridLength.Star, GridLength.Stars(2));
        grid.Children(new LayoutProbe(60, 10).Column(0), new LayoutProbe(60, 10).Column(1));

        grid.Measure(new Size(float.PositiveInfinity, 100));

        // Column 0 needs 60 per unit of weight, so column 1 gets 120: arranging at the desired size never clips.
        LayoutAssert.Near(180, grid.DesiredSize.Width);
    }

    [Fact]
    public void DesiredSize_IsBasedOnContent_SoANonStretchedGridShrinks()
    {
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left }.Columns(GridLength.Star);
        var child = new LayoutProbe(50, 20);
        grid.Add(child);

        LayoutAssert.Layout(grid, 400, 100);

        LayoutAssert.Near(50, grid.DesiredSize.Width);
        LayoutAssert.Near(50, grid.Bounds.Width);
    }

    [Fact]
    public void WrappingChildInStarColumn_GetsItsHeightFromTheColumnWidth()
    {
        var grid = new Grid().Columns(GridLength.Star, GridLength.Star);
        var text = new WrappingProbe(area: 10000, preferredWidth: 1000).Column(0);
        grid.Add(text);

        grid.Measure(new Size(400, float.PositiveInfinity));

        Assert.Equal(new Size(200, 50), text.DesiredSize);
        LayoutAssert.Near(50, grid.DesiredSize.Height);
    }

    [Fact]
    public void MinAndMaxLimits_ApplyToStarColumns()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MaxWidth = 100 });
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 150 });

        LayoutAssert.Layout(grid, 400, 50);

        // An even split would be 133.33 each; the first column is capped at 100 and the last raised to 150.
        LayoutAssert.Near(100, grid.ColumnDefinitions[0].ActualWidth);
        LayoutAssert.Near(150, grid.ColumnDefinitions[1].ActualWidth);
        LayoutAssert.Near(150, grid.ColumnDefinitions[2].ActualWidth);
    }

    [Fact]
    public void MinAndMaxLimits_ApplyToAutoAndPixelRows()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto) { MaxHeight = 40 });
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Pixels(10)) { MinHeight = 25 });
        grid.Add(new LayoutProbe(10, 100).Row(0));

        LayoutAssert.Layout(grid, 100, 300);

        LayoutAssert.Near(40, grid.RowDefinitions[0].ActualHeight);
        LayoutAssert.Near(25, grid.RowDefinitions[1].ActualHeight);
    }

    [Fact]
    public void WithoutDefinitions_ChildrenShareTheSingleCell()
    {
        var grid = new Grid();
        var a = new LayoutProbe(10, 10);
        var b = new LayoutProbe(20, 20).Row(3).Column(2); // clamped to the only cell
        grid.Children(a, b);

        LayoutAssert.Layout(grid, 150, 80);

        LayoutAssert.Rect(0, 0, 150, 80, a.Bounds);
        LayoutAssert.Rect(0, 0, 150, 80, b.Bounds);
    }

    [Fact]
    public void OutOfRangeIndexesAndSpans_AreClamped()
    {
        var grid = new Grid().Columns(GridLength.Pixels(50), GridLength.Pixels(60));
        var beyond = new LayoutProbe(10, 10).Column(5);
        var longSpan = new LayoutProbe(10, 10).Column(1).ColumnSpan(4);
        grid.Children(beyond, longSpan);

        LayoutAssert.Layout(grid, 200, 50);

        LayoutAssert.Rect(50, 0, 60, 50, beyond.Bounds);
        LayoutAssert.Rect(50, 0, 60, 50, longSpan.Bounds);
    }

    [Fact]
    public void NegativeIndexOrZeroSpan_IsRejected()
    {
        var child = new LayoutProbe(1, 1);

        Assert.Throws<ArgumentException>(() => Grid.SetRow(child, -1));
        Assert.Throws<ArgumentException>(() => Grid.SetColumnSpan(child, 0));
        Assert.Throws<ArgumentException>(() => new Grid().ColumnSpacing = -1);
    }

    [Fact]
    public void ChangingDefinitions_InvalidatesTheLayout()
    {
        var grid = new Grid().Columns(GridLength.Star);
        var child = new LayoutProbe(10, 10);
        grid.Add(child);
        LayoutAssert.Layout(grid, 200, 50);
        Assert.True(grid.IsMeasureValid);

        grid.ColumnDefinitions[0].Width = GridLength.Pixels(80);
        Assert.False(grid.IsMeasureValid);
        LayoutAssert.Layout(grid, 200, 50);
        LayoutAssert.Near(80, child.Bounds.Width);

        grid.ColumnDefinitions.Insert(0, new ColumnDefinition(GridLength.Pixels(30)));
        Assert.False(grid.IsMeasureValid);
        LayoutAssert.Layout(grid, 200, 50);
        LayoutAssert.Rect(0, 0, 30, 50, child.Bounds); // the child is now in the new first column

        grid.ColumnDefinitions.Clear();
        LayoutAssert.Layout(grid, 200, 50);
        LayoutAssert.Rect(0, 0, 200, 50, child.Bounds);
    }

    [Fact]
    public void Definition_CanBelongToOnlyOneGrid()
    {
        var column = new ColumnDefinition();
        var first = new Grid();
        first.ColumnDefinitions.Add(column);

        Assert.Throws<InvalidOperationException>(() => new Grid().ColumnDefinitions.Add(column));

        first.ColumnDefinitions.Remove(column);
        new Grid().ColumnDefinitions.Add(column); // free again
    }

    [Fact]
    public void RepeatedLayout_DoesNotAllocate()
    {
        var grid = new Grid { ColumnSpacing = 4, RowSpacing = 4 }
            .Columns(GridLength.Auto, GridLength.Star, GridLength.Pixels(50))
            .Rows(GridLength.Auto, GridLength.Star);
        grid.Children(
            new LayoutProbe(30, 20).Column(0).Row(0),
            new LayoutProbe(30, 20).Column(1).Row(1),
            new LayoutProbe(30, 20).Column(0).ColumnSpan(2).Row(1),
            new LayoutProbe(30, 20).Column(2).RowSpan(2));
        var implicitGrid = new Grid();
        implicitGrid.Add(new LayoutProbe(10, 10));

        Assert.Equal(0, LayoutAllocations.Measure(grid));
        Assert.Equal(0, LayoutAllocations.Measure(implicitGrid));
    }
}

public class GridLengthTests
{
    [Theory]
    [InlineData("Auto", GridUnitType.Auto, 1f)]
    [InlineData("auto", GridUnitType.Auto, 1f)]
    [InlineData("*", GridUnitType.Star, 1f)]
    [InlineData("2.5*", GridUnitType.Star, 2.5f)]
    [InlineData(" 120 ", GridUnitType.Pixel, 120f)]
    [InlineData("48px", GridUnitType.Pixel, 48f)]
    public void Parse_AcceptsTheCommonForms(string text, GridUnitType type, float value)
    {
        var length = GridLength.Parse(text);

        Assert.Equal(type, length.GridUnitType);
        Assert.Equal(value, length.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("**")]
    public void TryParse_RejectsInvalidText(string text)
    {
        Assert.False(GridLength.TryParse(text, out _));
    }

    [Fact]
    public void ToString_RoundTrips()
    {
        foreach (var length in new[] { GridLength.Auto, GridLength.Star, GridLength.Stars(3), GridLength.Pixels(42.5f) })
        {
            Assert.Equal(length, GridLength.Parse(length.ToString()));
        }
    }

    [Fact]
    public void AllAutoLengthsAreEqual_IncludingDefault()
    {
        Assert.Equal(GridLength.Auto, default(GridLength));
        Assert.True(GridLength.Auto == new GridLength(7, GridUnitType.Auto));
        Assert.Equal(GridLength.Auto.GetHashCode(), default(GridLength).GetHashCode());
        Assert.NotEqual(GridLength.Star, GridLength.Stars(2));
    }

    [Fact]
    public void Markup_AcceptsACommaSeparatedDefinitionList()
    {
        var grid = new Grid().Rows("Auto, *").Columns("48,2*,Auto");

        Assert.Equal(new[] { GridLength.Auto, GridLength.Star }, new[] { grid.RowDefinitions[0].Height, grid.RowDefinitions[1].Height });
        Assert.Equal(GridLength.Pixels(48), grid.ColumnDefinitions[0].Width);
        Assert.Equal(GridLength.Stars(2), grid.ColumnDefinitions[1].Width);
        Assert.Equal(GridLength.Auto, grid.ColumnDefinitions[2].Width);
    }

    [Fact]
    public void InvalidValues_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridLength.Pixels(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridLength.Stars(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridLength.Pixels(float.PositiveInfinity));
    }
}

internal static class LayoutAllocations
{
    /// <summary>Bytes allocated by a measure and arrange pass after warm-up; alternating sizes forces real work.</summary>
    public static long Measure(UIElement element)
    {
        void Pass(float size)
        {
            element.Measure(new Size(size, size));
            element.Arrange(new Rect(0, 0, size, size));
        }

        Pass(300);
        Pass(301);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10; i++)
        {
            Pass(300 + (i % 2));
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
