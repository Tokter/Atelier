using System;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Tests;

public class SliderTests
{
    // A 220 wide slider: the thumb travels 200 px, from x = 10 to x = 210.
    private static Slider CreateArranged(float min = 0, float max = 100)
    {
        var slider = new Slider { Minimum = min, Maximum = max, Width = 220, Height = 32 };
        slider.Measure(new Size(500, 500));
        slider.Arrange(new Rect(0, 0, 220, 32));
        return slider;
    }

    private static PointerEventArgs At(float x, PointerButtons button = PointerButtons.Left) => new(new Point(x, 16), button);

    private static void Key(Slider slider, Key key) => slider.OnKeyDown(new KeyEventArgs(key));

    [Fact]
    public void Value_SetBeforeMaximum_IsRestoredWhenTheRangeAllowsIt()
    {
        var slider = new Slider { Value = 150, Maximum = 200 };
        Assert.Equal(150, slider.Value);

        slider.Maximum = 120;
        Assert.Equal(120, slider.Value);

        slider.Maximum = 300;
        Assert.Equal(150, slider.Value);
    }

    [Fact]
    public void RaisingMinimum_MovesValueIntoRange_EvenWhenValueWasNeverSet()
    {
        var slider = new Slider { Minimum = 10 };
        Assert.Equal(10, slider.Value);

        var withValue = new Slider { Value = 5 };
        withValue.Minimum = 20;
        Assert.Equal(20, withValue.Value);
    }

    [Fact]
    public void MinimumAboveMaximum_DoesNotThrow_AndPinsValueToMinimum()
    {
        var slider = new Slider { Value = 30 };
        slider.Minimum = 50;
        slider.Maximum = 10;

        Assert.Equal(50, slider.Value);
        slider.Value = 70;
        Assert.Equal(50, slider.Value);
        Assert.Equal(0, slider.NormalizedValue);
    }

    [Fact]
    public void NaN_IsRejected()
    {
        var slider = new Slider();
        Assert.Throws<ArgumentException>(() => slider.Value = float.NaN);
        Assert.Throws<ArgumentException>(() => slider.Minimum = float.NaN);
        Assert.Throws<ArgumentException>(() => slider.Maximum = float.NaN);
        Assert.Throws<ArgumentException>(() => slider.SmallChange = -1);
    }

    [Fact]
    public void Keyboard_UsesSmallAndLargeChange_AndHomeEnd()
    {
        var slider = new Slider { Value = 50, SmallChange = 2, LargeChange = 20 };

        Key(slider, Core.Events.Key.Right);
        Assert.Equal(52, slider.Value);
        Key(slider, Core.Events.Key.Down);
        Assert.Equal(50, slider.Value);
        Key(slider, Core.Events.Key.PageUp);
        Assert.Equal(70, slider.Value);
        Key(slider, Core.Events.Key.PageDown);
        Assert.Equal(50, slider.Value);
        Key(slider, Core.Events.Key.End);
        Assert.Equal(100, slider.Value);
        Key(slider, Core.Events.Key.PageUp);
        Assert.Equal(100, slider.Value); // clamped
        Key(slider, Core.Events.Key.Home);
        Assert.Equal(0, slider.Value);
    }

    [Fact]
    public void DisabledSlider_IgnoresKeysAndPointer()
    {
        var slider = CreateArranged();
        slider.Value = 50;
        slider.IsEnabled = false;

        var key = new KeyEventArgs(Core.Events.Key.Right);
        slider.OnKeyDown(key);
        slider.OnPointerPressed(At(10));
        slider.OnPointerMoved(At(20));

        Assert.Equal(50, slider.Value);
        Assert.False(key.Handled);
        Assert.False(slider.IsPointerCaptured);
    }

    [Fact]
    public void RightButtonPressAndMove_DoesNotChangeValue()
    {
        var slider = CreateArranged();
        slider.Value = 50;

        slider.OnPointerPressed(At(10, PointerButtons.Right));
        slider.OnPointerMoved(At(200, PointerButtons.None));
        slider.OnPointerReleased(At(200, PointerButtons.Right));

        Assert.Equal(50, slider.Value);
    }

    [Fact]
    public void HandledKeys_AreIgnored()
    {
        var slider = new Slider { Value = 50 };
        var key = new KeyEventArgs(Core.Events.Key.Right) { Handled = true };
        slider.OnKeyDown(key);
        Assert.Equal(50, slider.Value);
    }

    [Fact]
    public void SnapToTick_AppliesToPointerAndMovesKeysOneTick()
    {
        var slider = CreateArranged();
        slider.IsSnapToTickEnabled = true;
        slider.TickFrequency = 10;

        slider.OnPointerPressed(At(10 + 200 * 0.26f)); // 26 snaps to 30
        Assert.Equal(30, slider.Value);
        slider.OnPointerMoved(new PointerEventArgs(new Point(0, 0), new Point(10 + 200 * 0.26f - 200 * 0.12f, 16))); // 30 - 12 = 18 snaps to 20
        Assert.Equal(20, slider.Value);
        slider.OnPointerReleased(At(0));

        slider.Value = 23; // values from code are not snapped
        Assert.Equal(23, slider.Value);
        Key(slider, Core.Events.Key.Right);
        Assert.Equal(30, slider.Value);
        Key(slider, Core.Events.Key.Right);
        Assert.Equal(40, slider.Value);
        slider.Value = 23;
        Key(slider, Core.Events.Key.Left);
        Assert.Equal(20, slider.Value);
        Key(slider, Core.Events.Key.Left);
        Assert.Equal(10, slider.Value);
    }

    [Fact]
    public void SnapToTick_ReachesMaximumOnAPartialLastStep()
    {
        var slider = new Slider { Maximum = 25, IsSnapToTickEnabled = true, TickFrequency = 10, Value = 20 };
        Key(slider, Core.Events.Key.Right);
        Assert.Equal(25, slider.Value);
        Key(slider, Core.Events.Key.Left);
        Assert.Equal(20, slider.Value);
    }

    [Fact]
    public void ValueText_IsCached_UntilValueOrFormatChanges()
    {
        var slider = new Slider { Value = 42 };
        string first = slider.ValueText;
        Assert.Equal("42", first);
        Assert.Same(first, slider.ValueText);

        slider.Value = 43;
        Assert.Equal("43", slider.ValueText);

        slider.ValueFormat = "{0:0}%";
        Assert.Equal("43%", slider.ValueText);
    }

    [Fact]
    public void LosingCapture_EndsTheDrag()
    {
        var slider = CreateArranged();
        slider.OnPointerPressed(At(110));
        Assert.True(slider.IsPointerCaptured);
        Assert.Equal(50, slider.Value);

        var other = new Border();
        other.CapturePointer();
        slider.OnPointerMoved(new PointerEventArgs(new Point(0, 0), new Point(210, 16)));

        Assert.Equal(50, slider.Value);
        UIElement.ReleaseCurrentPointerCapture();
    }

    [Fact]
    public void DetachedSlider_CanBeCollected()
    {
        var weak = CreateAttachAndDetachSlider();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weak.TryGetTarget(out _));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<Slider> CreateAttachAndDetachSlider()
    {
        var root = new StackPanel();
        var slider = new Slider();
        root.Add(slider);
        root.AttachToHost();
        slider.CapturePointer();
        slider.ReleasePointerCapture();
        root.Remove(slider);
        root.DetachFromHost();
        return new WeakReference<Slider>(slider);
    }
}

public class ProgressBarTests
{
    [Fact]
    public void Value_IsKeptWithinTheRange_AndRecoercedWhenItChanges()
    {
        var bar = new ProgressBar { Value = 150 };
        Assert.Equal(100, bar.Value);

        bar.Maximum = 200;
        Assert.Equal(150, bar.Value);

        bar.Value = -5;
        Assert.Equal(0, bar.Value);

        bar.Minimum = 10;
        Assert.Equal(10, bar.Value);
    }

    [Fact]
    public void MinimumAboveMaximum_DoesNotThrow()
    {
        var bar = new ProgressBar { Minimum = 50, Maximum = 10, Value = 30 };
        Assert.Equal(50, bar.Value);
        Assert.Equal(0, bar.NormalizedValue);
        Assert.Throws<ArgumentException>(() => bar.Value = float.NaN);
    }
}
