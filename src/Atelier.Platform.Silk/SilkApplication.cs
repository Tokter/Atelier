using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Atelier.Controls;
using Atelier.Core.Animation;
using Atelier.Core.Threading;
using Atelier.Core.Tree;

namespace Atelier.Platform.Silk;

/// <summary>
/// Specifies when <see cref="SilkApplication.Run"/> returns.
/// </summary>
public enum ShutdownMode
{
    /// <summary>The application ends when the last open window closes.</summary>
    OnLastWindowClose,

    /// <summary>The application ends when the main window (the first shown) closes; other windows are closed with it.</summary>
    OnMainWindowClose,
}

/// <summary>
/// Runs the UI thread for all <see cref="SilkWindow"/>s: one loop that processes OS events, dispatched work and
/// animations, then updates and renders each window.
/// </summary>
/// <remarks>
/// <para>
/// All windows share the UI thread, the dispatcher (<see cref="Dispatcher.UIThread"/>) and one
/// <see cref="Core.Animation.AnimationClock"/>. The loop sleeps until OS input arrives or work is dispatched while no
/// window needs a new frame and nothing is animating. When several windows render in the same iteration, only the first
/// waits for vertical sync, so animating windows don't slow each other down.
/// </para>
/// <para>
/// Single-window apps can keep calling <see cref="SilkWindow.Run"/>, which shows the window and runs this loop. Open
/// more windows with <see cref="SilkWindow.Show"/>, before or while the application runs.
/// </para>
/// </remarks>
public static class SilkApplication
{
    private static readonly List<SilkWindow> s_windows = [];
    private static readonly List<SilkWindow> s_pendingWindows = [];
    private static readonly ConcurrentQueue<Action> s_dispatchQueue = new();
    private static readonly ConcurrentQueue<Action> s_backgroundQueue = new();
    private static readonly Stopwatch s_clock = Stopwatch.StartNew();

    // Longest animation step per iteration, so a stalled frame (e.g. while the OS drags a window) doesn't skip animations.
    private const double MaxAnimationStepSeconds = 0.1;

    private static volatile bool s_isRunning;
    private static int s_mainThreadId;
    private static bool s_vsyncClaimed;
    private static SilkWindow? s_activeWindow;

    // The window used to wake the event loop; read from any thread, so it is a single volatile reference instead of the list.
    private static volatile SilkWindow? s_wakeWindow;

    /// <summary>Gets the animation clock shared by all windows.</summary>
    public static AnimationClock AnimationClock { get; } = new();

    /// <summary>Gets the open windows, in the order they were shown.</summary>
    public static IReadOnlyList<SilkWindow> Windows => s_windows;

    /// <summary>Gets the first window that was shown, or <c>null</c>.</summary>
    public static SilkWindow? MainWindow { get; private set; }

    /// <summary>Gets the window that most recently received OS focus (or the main window), or <c>null</c>.</summary>
    public static SilkWindow? ActiveWindow => s_activeWindow ?? MainWindow;

    /// <summary>Gets whether <see cref="Run"/> is executing.</summary>
    public static bool IsRunning => s_isRunning;

    /// <summary>Gets the managed thread id of the UI thread (valid while running).</summary>
    public static int MainThreadId => s_mainThreadId;

    /// <summary>Gets or sets when <see cref="Run"/> returns. The default is <see cref="ShutdownMode.OnLastWindowClose"/>.</summary>
    public static ShutdownMode ShutdownMode { get; set; } = ShutdownMode.OnLastWindowClose;

    /// <summary>Occurs on the UI thread after a window has been created and shown.</summary>
    public static event Action<SilkWindow>? WindowOpened;

    /// <summary>Occurs on the UI thread after a window has closed.</summary>
    public static event Action<SilkWindow>? WindowClosed;

    /// <summary>
    /// Runs the UI loop on the calling thread until the application shuts down (see <see cref="ShutdownMode"/>).
    /// At least one window must have been shown with <see cref="SilkWindow.Show"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The application is already running, or no window was shown.</exception>
    public static void Run()
    {
        if (s_isRunning)
        {
            throw new InvalidOperationException("The application is already running.");
        }

        if (s_pendingWindows.Count == 0 && s_windows.Count == 0)
        {
            throw new InvalidOperationException("Show a window before running the application.");
        }

        s_isRunning = true;
        s_mainThreadId = Environment.CurrentManagedThreadId;
        Dispatcher.UIThread = new SilkDispatcher();
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new SilkSynchronizationContext());
        InstallGlobals();

        try
        {
            RunLoop();
        }
        finally
        {
            foreach (var window in s_windows.ToArray())
            {
                CloseWindowNow(window);
            }
            foreach (var window in s_pendingWindows.ToArray())
            {
                window.Dispose();
            }
            s_windows.Clear();
            s_wakeWindow = null;
            s_pendingWindows.Clear();
            MainWindow = null;
            s_activeWindow = null;

            UninstallGlobals();
            SynchronizationContext.SetSynchronizationContext(previousContext);
            Dispatcher.UIThread = Dispatcher.ImmediateDispatcher.Instance;
            s_isRunning = false;
        }
    }

    /// <summary>
    /// Queues <paramref name="action"/> to run on the UI thread: <see cref="DispatcherPriority.Normal"/> at the start of the
    /// next iteration, <see cref="DispatcherPriority.Background"/> after the windows have rendered. Wakes the loop if idle.
    /// Safe to call from any thread.
    /// </summary>
    public static void Dispatch(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        ArgumentNullException.ThrowIfNull(action);
        (priority == DispatcherPriority.Background ? s_backgroundQueue : s_dispatchQueue).Enqueue(action);
        Wake();
    }

    /// <summary>Closes all windows, which ends <see cref="Run"/>.</summary>
    public static void Shutdown()
    {
        foreach (var window in s_windows.ToArray())
        {
            window.Close();
        }
        s_pendingWindows.Clear();
    }

    /// <summary>Determines whether the calling thread is the UI thread (always <c>true</c> before the application runs).</summary>
    public static bool CheckAccess() => !s_isRunning || Environment.CurrentManagedThreadId == s_mainThreadId;

    internal static void Show(SilkWindow window)
    {
        if (!CheckAccess())
        {
            Dispatch(() => Show(window));
            return;
        }

        if (s_windows.Contains(window) || s_pendingWindows.Contains(window))
        {
            return;
        }

        s_pendingWindows.Add(window);
        MainWindow ??= window;
        Wake();
    }

    internal static void SetActive(SilkWindow window)
    {
        s_activeWindow = window;
        FocusManager.ActivateRoot(window.Content);
    }

    /// <summary>
    /// Called by a window right before presenting: returns <c>true</c> for the first window per loop iteration, which
    /// waits for vertical sync; the others present immediately.
    /// </summary>
    internal static bool ClaimVSync()
    {
        if (s_vsyncClaimed)
        {
            return false;
        }
        s_vsyncClaimed = true;
        return true;
    }

    /// <summary>Wakes the loop if it is waiting for OS events. Safe to call from any thread.</summary>
    internal static void Wake()
    {
        if (!s_isRunning)
        {
            return;
        }

        // GLFW's event wait is global: posting an empty event through any initialized window wakes it.
        s_wakeWindow?.TryWakeEventLoop();
    }

    private static void UpdateWakeWindow() => s_wakeWindow = s_windows.Count > 0 ? s_windows[0] : null;

    private static void RunLoop()
    {
        double lastTime = s_clock.Elapsed.TotalSeconds;

        while (true)
        {
            InitializePendingWindows();
            RemoveClosedWindows();
            if (s_windows.Count == 0)
            {
                break;
            }

            // Wait for OS events only when nothing needs the loop; the first iteration after a wait advances animations by
            // zero, so an animation started by the input that woke the loop doesn't jump ahead by the whole sleep.
            bool busy = HasWork();
            PumpEvents(wait: !busy);

            RunQueue(s_dispatchQueue);

            double now = s_clock.Elapsed.TotalSeconds;
            double delta = busy ? Math.Min(now - lastTime, MaxAnimationStepSeconds) : 0;
            lastTime = now;
            AnimationClock.Update(delta);

            s_vsyncClaimed = false;
            bool rendered = false;
            for (int i = 0; i < s_windows.Count; i++)
            {
                rendered |= s_windows[i].RunFrame();
            }

            RunQueue(s_backgroundQueue);

            // Without a rendered frame nothing throttles the loop to the display; don't spin while waiting for animations.
            if (!rendered && busy)
            {
                Thread.Sleep(1);
            }
        }
    }

    private static bool HasWork()
    {
        if (AnimationClock.ActiveAnimationCount > 0 || !s_dispatchQueue.IsEmpty || !s_backgroundQueue.IsEmpty || s_pendingWindows.Count > 0)
        {
            return true;
        }

        for (int i = 0; i < s_windows.Count; i++)
        {
            if (s_windows[i].NeedsLoop)
            {
                return true;
            }
        }
        return false;
    }

    private static void PumpEvents(bool wait)
    {
        // GLFW processes the events of all windows in one call, so only the first window may wait; the rest poll.
        for (int i = 0; i < s_windows.Count; i++)
        {
            s_windows[i].PumpEvents(wait && i == 0);
        }
    }

    // Runs the actions queued before this call; actions they queue run in the next iteration.
    private static void RunQueue(ConcurrentQueue<Action> queue)
    {
        int count = queue.Count;
        for (int i = 0; i < count && queue.TryDequeue(out var action); i++)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Atelier.Dispatch] Error: {ex}");
            }
        }
    }

    private static void InitializePendingWindows()
    {
        while (s_pendingWindows.Count > 0)
        {
            var window = s_pendingWindows[0];
            s_pendingWindows.RemoveAt(0);
            s_windows.Add(window);
            window.InitializeNative();
            UpdateWakeWindow();
            WindowOpened?.Invoke(window);
        }
    }

    private static void RemoveClosedWindows()
    {
        for (int i = s_windows.Count - 1; i >= 0; i--)
        {
            var window = s_windows[i];
            if (!window.IsClosed)
            {
                continue;
            }

            CloseWindowNow(window);

            if (window == MainWindow && ShutdownMode == ShutdownMode.OnMainWindowClose)
            {
                Shutdown();
            }
        }
    }

    private static void CloseWindowNow(SilkWindow window)
    {
        s_windows.Remove(window);
        UpdateWakeWindow();
        window.Dispose();

        if (s_activeWindow == window)
        {
            s_activeWindow = null;
            var next = s_windows.Count > 0 ? s_windows[^1] : null;
            if (next != null)
            {
                SetActive(next);
            }
            else
            {
                FocusManager.ActivateRoot(null);
            }
        }

        if (MainWindow == window && ShutdownMode == ShutdownMode.OnLastWindowClose)
        {
            MainWindow = s_windows.Count > 0 ? s_windows[0] : null;
        }

        WindowClosed?.Invoke(window);
    }

    private static void InstallGlobals()
    {
        Button.SetGlobalAnimationClock(AnimationClock);
        CheckBox.SetGlobalAnimationClock(AnimationClock);
        Atelier.Controls.Switch.SetGlobalAnimationClock(AnimationClock);
        TextBox.SetGlobalAnimationClock(AnimationClock);
        Slider.SetGlobalAnimationClock(AnimationClock);
        ScrollViewer.SetGlobalAnimationClock(AnimationClock);
        TransitioningContentControl.SetGlobalAnimationClock(AnimationClock);
        DialogHost.RootVisualProvider = () => ActiveWindow?.Content;
    }

    private static void UninstallGlobals()
    {
        Button.SetGlobalAnimationClock(null!);
        CheckBox.SetGlobalAnimationClock(null!);
        Atelier.Controls.Switch.SetGlobalAnimationClock(null!);
        TextBox.SetGlobalAnimationClock(null!);
        Slider.SetGlobalAnimationClock(null!);
        ScrollViewer.SetGlobalAnimationClock(null!);
        TransitioningContentControl.SetGlobalAnimationClock(null!);
        DialogHost.RootVisualProvider = null;
    }
}
