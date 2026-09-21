using System;
using System.Threading.Tasks;

namespace Atelier.Core.Threading;

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
    /// Enqueues an action to be executed asynchronously on the UI thread.
    /// </summary>
    public static void Post(Action action) => UIThread.Post(action);

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
        public static readonly ImmediateDispatcher Instance = new();

        public bool CheckAccess() => true;
        public void Post(Action action) => action();
        public void Send(Action action) => action();
    }
}
