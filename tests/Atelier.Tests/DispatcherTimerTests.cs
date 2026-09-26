using System;
using System.Threading;
using Xunit;
using Atelier.Core.Threading;

namespace Atelier.Tests;

public class DispatcherTimerTests
{
    [Fact]
    public void Timer_TicksRepeatedly_UntilStopped()
    {
        int ticks = 0;
        using var twoTicks = new CountdownEvent(2);
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(15), (s, e) =>
        {
            Interlocked.Increment(ref ticks);
            if (!twoTicks.IsSet) twoTicks.Signal();
        });

        timer.Start();
        Assert.True(timer.IsEnabled);
        Assert.True(twoTicks.Wait(TimeSpan.FromSeconds(5)), "timer did not tick twice");

        timer.Stop();
        Assert.False(timer.IsEnabled);
        int afterStop = Volatile.Read(ref ticks);
        Thread.Sleep(80);

        // A tick already in flight when Stop ran may still complete, but no new ones start.
        Assert.InRange(Volatile.Read(ref ticks), afterStop, afterStop + 1);
    }

    [Fact]
    public void Interval_MustBePositive()
    {
        var timer = new DispatcherTimer();
        Assert.Throws<ArgumentOutOfRangeException>(() => timer.Interval = TimeSpan.Zero);
    }

    [Fact]
    public void StartTwice_DoesNotCreateSecondTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
        timer.Start();
        timer.Start();
        Assert.True(timer.IsEnabled);
        timer.Stop();
        Assert.False(timer.IsEnabled);
    }

    [Fact]
    public void PostWithPriority_FallsBackToPost_ForDispatchersWithoutPriorities()
    {
        bool ran = false;
        Dispatcher.Post(() => ran = true, DispatcherPriority.Background);
        Assert.True(ran); // headless dispatcher runs work immediately
    }
}
