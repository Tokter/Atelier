using System;
using Xunit;
using Atelier.Core.Animation;
using Atelier.Core.Tree;

namespace Atelier.Tests;

public class AnimationStopTests
{
    [Fact]
    public void StoppedAnimation_NoLongerUpdates_AndIsDroppedByTheClock()
    {
        var clock = new AnimationClock();
        int updates = 0, completions = 0;
        var animation = new FloatAnimation(0, 1, TimeSpan.FromSeconds(1), _ => updates++, onCompleted: () => completions++);
        clock.Add(animation);
        clock.Update(0.1);

        animation.Stop();
        clock.Update(2);

        Assert.Equal(1, updates);
        Assert.Equal(0, completions);
        Assert.Equal(0, clock.ActiveAnimationCount);
    }
}

public class LostPointerCaptureTests
{
    private sealed class CaptureProbe : UIElement
    {
        public int LostCount { get; private set; }

        protected override void OnLostPointerCapture()
        {
            LostCount++;
            base.OnLostPointerCapture();
        }
    }

    [Fact]
    public void LosingCapture_NotifiesTheElement_ForEveryWayItCanHappen()
    {
        var a = new CaptureProbe();
        var b = new CaptureProbe();
        int events = 0;
        a.LostPointerCapture += (_, _) => events++;
        try
        {
            a.CapturePointer();
            b.CapturePointer();          // another element takes over
            Assert.Equal(1, a.LostCount);

            b.ReleasePointerCapture();   // released by the element itself
            Assert.Equal(1, b.LostCount);

            a.CapturePointer();
            UIElement.ReleaseCurrentPointerCapture();
            Assert.Equal(2, a.LostCount);
            Assert.Equal(2, events);

            a.ReleasePointerCapture();   // not captured: nothing happens
            Assert.Equal(2, a.LostCount);
        }
        finally
        {
            UIElement.ReleaseCurrentPointerCapture();
        }
    }
}
