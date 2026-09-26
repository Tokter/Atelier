using Xunit;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;

namespace Atelier.Tests;

public class InputEventTests
{
    private const int DoubleClickTime = 500;
    private const float DoubleClickDistance = 4f;

    [Fact]
    public void ClickCounter_CountsConsecutiveClicks()
    {
        var counter = new ClickCounter();

        Assert.Equal(1, counter.RegisterPress(PointerButtons.Left, new Point(10, 10), 1000, DoubleClickTime, DoubleClickDistance));
        Assert.Equal(2, counter.RegisterPress(PointerButtons.Left, new Point(11, 10), 1200, DoubleClickTime, DoubleClickDistance));
        Assert.Equal(3, counter.RegisterPress(PointerButtons.Left, new Point(11, 11), 1400, DoubleClickTime, DoubleClickDistance));
        Assert.Equal(3, counter.GetReleaseCount(PointerButtons.Left));
    }

    [Theory]
    [InlineData(1600, 10f, PointerButtons.Left)]   // too late
    [InlineData(1100, 30f, PointerButtons.Left)]   // too far
    [InlineData(1100, 10f, PointerButtons.Right)]  // other button
    public void ClickCounter_StartsNewSequence(long secondTime, float secondX, PointerButtons secondButton)
    {
        var counter = new ClickCounter();
        counter.RegisterPress(PointerButtons.Left, new Point(10, 10), 1000, DoubleClickTime, DoubleClickDistance);

        Assert.Equal(1, counter.RegisterPress(secondButton, new Point(secondX, 10), secondTime, DoubleClickTime, DoubleClickDistance));
    }

    [Fact]
    public void PointerEventArgs_CarryModifiersAndClickCount_ThroughWithPosition()
    {
        var e = new PointerEventArgs(new Point(1, 2), new Point(3, 4), PointerButtons.Left, 99, ModifierKeys.Shift | ModifierKeys.Control, 2);

        var copy = e.WithPosition(new Point(5, 6));

        Assert.Equal(ModifierKeys.Shift | ModifierKeys.Control, copy.Modifiers);
        Assert.Equal(2, copy.ClickCount);
        Assert.Equal(new Point(5, 6), copy.Position);
    }

    [Fact]
    public void PointerWheelEventArgs_CarryModifiers()
    {
        var e = new PointerWheelEventArgs(Point.Zero, Point.Zero, 0, 1, 0, ModifierKeys.Control);

        Assert.Equal(ModifierKeys.Control, e.Modifiers);
        Assert.Equal(ModifierKeys.Control, e.WithPosition(new Point(1, 1)).Modifiers);
    }

    [Fact]
    public void KeyEventArgs_ReportsRepeat()
    {
        Assert.True(new KeyEventArgs(Key.Down, isRepeat: true).IsRepeat);
        Assert.False(new KeyEventArgs(Key.Down).IsRepeat);
    }

    [Fact]
    public void KeyEnum_ExistingValuesAreUnchanged()
    {
        Assert.Equal(15, (int)Key.A);
        Assert.Equal(41, (int)Key.D0);
        Assert.Equal(62, (int)Key.F12);
        Assert.Equal(63, (int)Key.Insert);
    }

    [Theory]
    [InlineData("Ctrl+Minus", Key.Minus, ModifierKeys.Control)]
    [InlineData("Ctrl+Equal", Key.Equal, ModifierKeys.Control)]
    [InlineData("Shift+F13", Key.F13, ModifierKeys.Shift)]
    [InlineData("NumPad5", Key.NumPad5, ModifierKeys.None)]
    [InlineData("Alt+Insert", Key.Insert, ModifierKeys.Alt)]
    public void Gestures_AcceptNewKeys(string text, Key key, ModifierKeys modifiers)
    {
        Assert.True(KeybindingGesture.TryParse(text, out var gesture));
        Assert.Equal(key, gesture.Key);
        Assert.Equal(modifiers, gesture.Modifiers);
    }
}
