using System;
using System.Threading;

namespace Atelier.Core.Threading;

/// <summary>
/// The order in which posted work runs relative to other work and to rendering.
/// </summary>
public enum DispatcherPriority
{
    /// <summary>Runs at the start of the next frame, before layout and rendering.</summary>
    Normal = 0,

    /// <summary>
    /// Runs after the current frame has been rendered. Use it for work that can wait until the screen is up to date,
    /// such as loading content that isn't visible yet.
    /// </summary>
    Background = 1,
}

/// <summary>
/// Raises <see cref="Tick"/> on the UI thread at a regular <see cref="Interval"/> while enabled.
/// </summary>
/// <remarks>
/// <para>
/// Timing runs on a background timer and each tick is posted through <see cref="Dispatcher.UIThread"/>, so it works while
/// the window is idle (posting wakes the render loop). If the UI thread is busy, ticks are not queued up: at most one
/// tick is pending at a time, so a slow handler causes skipped ticks rather than a backlog.
/// </para>
/// <para>
/// A running timer is kept alive by the system timer and keeps its <see cref="Tick"/> handlers alive; call
/// <see cref="Stop"/> when it is no longer needed. Without a UI dispatcher (tests, headless use) ticks run on a
/// thread-pool thread.
/// </para>
/// </remarks>
public sealed class DispatcherTimer
{
    private readonly Action _raiseTick;
    private Timer? _timer;
    private TimeSpan _interval = TimeSpan.FromSeconds(1);
    private int _tickPending;

    /// <summary>Initializes a stopped timer with a one-second interval.</summary>
    public DispatcherTimer()
    {
        _raiseTick = RaiseTick;
    }

    /// <summary>Initializes a stopped timer with the given interval and tick handler.</summary>
    /// <param name="interval">The time between ticks.</param>
    /// <param name="tick">The handler for <see cref="Tick"/>.</param>
    public DispatcherTimer(TimeSpan interval, EventHandler tick)
        : this()
    {
        Interval = interval;
        Tick += tick;
    }

    /// <summary>Occurs on the UI thread each time the interval elapses while the timer is enabled.</summary>
    public event EventHandler? Tick;

    /// <summary>Gets or sets the time between ticks. Changing it restarts the current interval.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The interval is not positive.</exception>
    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The interval must be positive.");
            }

            _interval = value;
            _timer?.Change(value, value);
        }
    }

    /// <summary>Gets or sets the priority ticks are posted with. The default is <see cref="DispatcherPriority.Normal"/>.</summary>
    public DispatcherPriority Priority { get; set; } = DispatcherPriority.Normal;

    /// <summary>Gets whether the timer is running.</summary>
    public bool IsEnabled => _timer != null;

    /// <summary>Starts the timer; the first tick occurs after one <see cref="Interval"/>. Does nothing if already running.</summary>
    public void Start()
    {
        _timer ??= new Timer(static state => ((DispatcherTimer)state!).OnElapsed(), this, _interval, _interval);
    }

    /// <summary>Stops the timer. A tick that was already posted is not raised.</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void OnElapsed()
    {
        if (Interlocked.Exchange(ref _tickPending, 1) == 0)
        {
            Dispatcher.Post(_raiseTick, Priority);
        }
    }

    private void RaiseTick()
    {
        Volatile.Write(ref _tickPending, 0);
        if (IsEnabled)
        {
            Tick?.Invoke(this, EventArgs.Empty);
        }
    }
}
