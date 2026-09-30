using Atelier.Audio;
using Atelier.Core.Events;
using Atelier.Core.Primitives;

namespace Atelier.Tests;

public class KnobTests
{
    private static Knob Knob() => new() { Minimum = 0, Maximum = 100, Value = 50 };

    [Fact]
    public void DraggingUpOrRight_TurnsItUp_AndShiftMakesFineChanges()
    {
        var knob = Knob();
        knob.OnPointerPressed(new PointerEventArgs(new Point(10, 10), new Point(10, 10), PointerButtons.Left, clickCount: 1));
        Assert.True(knob.IsDragging);
        knob.OnPointerMoved(new PointerEventArgs(new Point(10, -10), new Point(10, -10))); // 20 px up of 200 for the range
        Assert.Equal(60f, knob.Value, 3);
        knob.OnPointerMoved(new PointerEventArgs(new Point(30, -10), new Point(30, -10), modifiers: ModifierKeys.Shift));
        Assert.Equal(61f, knob.Value, 3);
        knob.OnPointerMoved(new PointerEventArgs(new Point(30, 1000), new Point(30, 1000)));
        Assert.Equal(0f, knob.Value); // clamped
        knob.OnPointerReleased(new PointerEventArgs(new Point(30, 1000), new Point(30, 1000), PointerButtons.Left));
        Assert.False(knob.IsDragging);
    }

    [Fact]
    public void WheelKeysAndDoubleClick()
    {
        var knob = Knob();
        knob.OnPointerWheel(new PointerWheelEventArgs(Point.Zero, 0, 1));
        Assert.Equal(51f, knob.Value);
        knob.OnKeyDown(new KeyEventArgs(Key.PageDown));
        Assert.Equal(41f, knob.Value);
        knob.OnKeyDown(new KeyEventArgs(Key.End));
        Assert.Equal(100f, knob.Value);

        knob.OnPointerPressed(new PointerEventArgs(Point.Zero, Point.Zero, PointerButtons.Left, clickCount: 2));
        Assert.Equal(100f, knob.Value); // no default value
        knob.DefaultValue = 25;
        knob.OnPointerPressed(new PointerEventArgs(Point.Zero, Point.Zero, PointerButtons.Left, clickCount: 2));
        Assert.Equal(25f, knob.Value);
    }

    [Fact]
    public void TheAngle_SweepsFromTheBottomLeftToTheBottomRight()
    {
        var knob = Knob();
        knob.Value = 0;
        Assert.Equal(Audio.Knob.StartAngle, knob.Angle);
        knob.Value = 100;
        Assert.Equal(Audio.Knob.StartAngle + Audio.Knob.SweepAngle, knob.Angle);
        knob.Maximum = 40;
        Assert.Equal(40f, knob.Value);
    }
}
