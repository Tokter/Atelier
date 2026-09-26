using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Atelier.Core.Threading;

/// <summary>
/// Specifies how UI objects react when they are modified from a thread other than the UI thread
/// (see <see cref="Dispatcher.ThreadCheckMode"/>).
/// </summary>
public enum ThreadCheckMode
{
    /// <summary>Throw an <see cref="InvalidOperationException"/> at the offending call.</summary>
    Throw,

    /// <summary>Write a warning with a stack trace to <see cref="System.Diagnostics.Trace"/>, once per operation, and continue.</summary>
    Trace,

    /// <summary>Do not check. Cross-thread changes are unsafe and may corrupt the element tree.</summary>
    Off,
}

/// <summary>
/// Provides services for managing work items on a thread (typically the UI/render thread).
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Determines whether the calling thread is the thread associated with this dispatcher.
    /// </summary>
    bool CheckAccess();

    /// <summary>
    /// Executes the specified action asynchronously on the dispatcher thread.
    /// </summary>
    void Post(Action action);

    /// <summary>
    /// Executes the specified action asynchronously on the dispatcher thread with the given priority.
    /// </summary>
    /// <remarks>The default implementation ignores the priority and calls <see cref="Post(Action)"/>.</remarks>
    void Post(Action action, DispatcherPriority priority) => Post(action);

    /// <summary>
    /// Executes the specified action synchronously on the dispatcher thread.
    /// </summary>
    void Send(Action action);
}

/// <summary>
/// Provides access to the main UI thread dispatcher.
/// </summary>
public static class Dispatcher
{
    private static IDispatcher? _uiThread;

    /// <summary>
    /// Gets or sets the UI thread dispatcher instance.
    /// </summary>
    /// <remarks>
    /// The platform layer sets this while a window runs (see <c>SilkWindow.Run</c>). When nothing is set, for example in
    /// unit tests, headless use, or before the window starts, <see cref="ImmediateDispatcher"/> is used: it reports every
    /// thread as the UI thread and runs posted work immediately on the calling thread. Work posted from a background
    /// thread in that state therefore runs on that background thread.
    /// </remarks>
    public static IDispatcher UIThread
    {
        get => _uiThread ?? ImmediateDispatcher.Instance;
        set => _uiThread = value;
    }

    /// <summary>
    /// Determines whether the calling thread is the UI thread.
    /// </summary>
    public static bool CheckAccess() => UIThread.CheckAccess();

    /// <summary>
    /// Gets or sets how UI objects react when they are modified from a thread other than the UI thread.
    /// The default is <see cref="ThreadCheckMode.Throw"/>.
    /// </summary>
    /// <remarks>
    /// Element trees are not thread-safe: the UI thread lays out and renders them without locking, so a change made on
    /// another thread can corrupt state or be lost. Property writes, child changes, invalidation and focus changes
    /// therefore verify the calling thread. Reads are not checked. Use <see cref="Post(Action)"/> or
    /// <see cref="InvokeAsync(Action)"/> to run UI code from background work. Bindings already forward source changes
    /// raised on other threads to the UI thread.
    /// </remarks>
    public static ThreadCheckMode ThreadCheckMode { get; set; } = ThreadCheckMode.Throw;

    /// <summary>
    /// Gets a value indicating whether the calling thread may modify UI objects: it is the UI thread, or thread checks
    /// are turned off.
    /// </summary>
    internal static bool HasUIAccess => ThreadCheckMode == ThreadCheckMode.Off || UIThread.CheckAccess();

    /// <summary>
    /// Verifies that the calling thread may modify UI objects and reports a violation according to
    /// <see cref="ThreadCheckMode"/> otherwise.
    /// </summary>
    /// <param name="operation">A description of the operation, used in the error message.</param>
    /// <exception cref="InvalidOperationException">
    /// The caller is not on the UI thread and <see cref="ThreadCheckMode"/> is <see cref="ThreadCheckMode.Throw"/>.
    /// </exception>
    public static void VerifyAccess([CallerMemberName] string operation = "")
    {
        if (!HasUIAccess)
        {
            ReportWrongThread(operation);
        }
    }

    private static readonly HashSet<string> s_tracedOperations = new(StringComparer.Ordinal);

    /// <summary>
    /// Reports that <paramref name="operation"/> ran on the wrong thread: throws, or traces it once per operation.
    /// Callers check <see cref="HasUIAccess"/> first so the message is only built for actual violations.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ReportWrongThread(string operation)
    {
        string message =
            $"{operation} ran on thread {Environment.CurrentManagedThreadId}, but UI objects may only be changed on " +
            "the UI thread. Use Dispatcher.Post or Dispatcher.InvokeAsync to run this code on the UI thread.";

        if (ThreadCheckMode == ThreadCheckMode.Throw)
        {
            throw new InvalidOperationException(message);
        }

        // Trace: once per operation, since a misbehaving animation or timer would otherwise flood the output.
        lock (s_tracedOperations)
        {
            if (!s_tracedOperations.Add(operation))
            {
                return;
            }
        }
        Trace.TraceWarning($"[Atelier] {message}{Environment.NewLine}{Environment.StackTrace}");
    }

    /// <summary>
    /// Enqueues an action to be executed asynchronously on the UI thread.
    /// </summary>
    public static void Post(Action action) => UIThread.Post(action);

    /// <summary>
    /// Enqueues an action to be executed asynchronously on the UI thread with the given priority.
    /// </summary>
    public static void Post(Action action, DispatcherPriority priority) => UIThread.Post(action, priority);

    /// <summary>
    /// Executes an action synchronously on the UI thread, blocking until completion.
    /// </summary>
    public static void Send(Action action) => UIThread.Send(action);

    /// <summary>
    /// Executes an action on the UI thread asynchronously, returning a task that completes when the action finishes.
    /// </summary>
    public static Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (CheckAccess())
        {
            try
            {
                action();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    /// <summary>
    /// Executes a function on the UI thread asynchronously, returning a task with the result.
    /// </summary>
    public static Task<T> InvokeAsync<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        if (CheckAccess())
        {
            try
            {
                return Task.FromResult(func());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    /// <summary>
    /// Fallback dispatcher used in unit testing or headless environments where all actions run immediately on the caller thread.
    /// </summary>
    public sealed class ImmediateDispatcher : IDispatcher
    {
        /// <summary>The shared instance.</summary>
        public static readonly ImmediateDispatcher Instance = new();

        /// <summary>Always returns <c>true</c>: every thread is treated as the UI thread.</summary>
        public bool CheckAccess() => true;

        /// <summary>Runs <paramref name="action"/> immediately on the calling thread.</summary>
        public void Post(Action action) => action();

        /// <summary>Runs <paramref name="action"/> immediately on the calling thread.</summary>
        public void Send(Action action) => action();
    }
}
