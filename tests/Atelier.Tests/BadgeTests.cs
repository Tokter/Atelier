using Xunit;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Tests;

public class BadgeTests
{
    private static Badge Layout(Badge badge)
    {
        badge.Measure(new Size(400, 400));
        badge.Arrange(new Rect(0, 0, badge.DesiredSize.Width, badge.DesiredSize.Height));
        return badge;
    }

    [Fact]
    public void TakesTheSizeOfItsContent_AndHostsIt()
    {
        var content = new Border().Size(40, 24);
        var badge = Layout(new Badge(content));

        Assert.Equal(new Size(40, 24), badge.DesiredSize);
        Assert.Same(badge, content.Parent);
        Assert.Equal(new Rect(0, 0, 40, 24), content.Bounds);

        var other = new Border().Size(10, 10);
        badge.Content = other;
        Assert.Null(content.Parent);
        Assert.Single(badge.Children);
    }

    [Fact]
    public void WithoutTextOrCount_ItIsTheSmallDot_InsideTheCorner()
    {
        var badge = Layout(new Badge(new Border().Size(24, 24)));

        Assert.True(badge.IsBadgeShown);
        Assert.True(badge.IsSmall);
        Assert.Null(badge.Label);
        Assert.Equal(new Rect(18, 0, Badge.SmallSize, Badge.SmallSize), badge.BadgeBounds);
    }

    [Fact]
    public void Counts_ShowTheLargeBadge_CappedAtMaxCount()
    {
        var badge = Layout(new Badge(new Border().Size(24, 24)).Count(3));
        Assert.False(badge.IsSmall);
        Assert.Equal("3", badge.Label);
        Assert.Equal(new Rect(12, -4, Badge.LargeHeight, Badge.LargeHeight), badge.BadgeBounds);

        badge.Count = 1234;
        Assert.Equal("999+", badge.Label);
        Assert.True(badge.BadgeBounds.Width > Badge.LargeHeight); // grows with the label
        Assert.Equal(12, badge.BadgeBounds.X);                     // anchored at its left edge

        badge.MaxCount = 99;
        badge.Count = 100;
        Assert.Equal("99+", badge.Label);
    }

    [Fact]
    public void ZeroCount_HidesTheBadge_UnlessShowZero()
    {
        var badge = Layout(new Badge(new Border().Size(24, 24)).Count(0));
        Assert.False(badge.IsBadgeShown);

        badge.ShowZero = true;
        Assert.True(badge.IsBadgeShown);
        Assert.Equal("0", badge.Label);

        badge.Count = null;
        Assert.True(badge.IsSmall);
    }

    [Fact]
    public void Text_TakesPrecedenceOverTheCount()
    {
        var badge = Layout(new Badge(new Border().Size(24, 24)).Count(0).Text("New"));
        Assert.True(badge.IsBadgeShown);
        Assert.Equal("New", badge.Label);

        badge.Text = "";
        Assert.False(badge.IsBadgeShown); // back to the hidden zero count
    }

    [Fact]
    public void IsBadgeVisible_AndOffsets()
    {
        var badge = Layout(new Badge(new Border().Size(24, 24)).BadgeOffset(-6, 6));
        Assert.Equal(new Rect(12, 6, Badge.SmallSize, Badge.SmallSize), badge.BadgeBounds);

        badge.IsBadgeVisible = false;
        Assert.False(badge.IsBadgeShown);
    }

    [Fact]
    public void FollowsTheContentsWidth()
    {
        var content = new Border().Size(100, 40);
        var badge = Layout(new Badge(content).Count(5));
        Assert.Equal(88, badge.BadgeBounds.X);

        content.Width = 60;
        Layout(badge);
        Assert.Equal(48, badge.BadgeBounds.X);
    }
}
