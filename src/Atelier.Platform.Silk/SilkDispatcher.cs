using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Atelier.Core.Threading;

namespace Atelier.Platform.Silk;

/// <summary>
/// Implements <see cref="IDispatcher"/> by queueing work to the <see cref="SilkApplication"/> UI loop, which serves all windows.
/// </summary>
public sealed class SilkDispatcher : IDispatcher
{
    /// <summary>Initializes a dispatcher for the <see cref="SilkApplication"/> UI loop.</summary>
    public SilkDispatcher()
    {
    }

    /// <summary>Initializes a dispatcher for the <see cref="SilkApplication"/> UI loop.</summary>
    /// <param name="window">Ignored: all windows share one UI thread and dispatcher.</param>
    [Obsolete("All windows share one dispatcher; use the parameterless constructor.")]
    public SilkDispatcher(SilkWindow window)
    {
    }

    /// <inheritdoc/>
    public bool CheckAccess() => SilkApplication.CheckAccess();

    /// <inheritdoc/>
    public void Post(Action action) => SilkApplication.Dispatch(action);

    /// <inheritdoc/>
    public void Post(Action action, DispatcherPriority priority) => SilkApplication.Dispatch(action, priority);

    /// <inheritdoc/>
    public void Send(Action action)
    {
        if (CheckAccess())
        {
            action();
            return;
        }

        SendAndWait(action);
    }

    internal static void SendAndWait(Action action)
    {
        using var evt = new ManualResetEventSlim();
        Exception? error = null;
        SilkApplication.Dispatch(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                evt.Set();
            }
        });

        evt.Wait();
        if (error != null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}

/// <summary>
/// Synchronization context that posts continuations back to the <see cref="SilkApplication"/> UI loop.
/// </summary>
public sealed class SilkSynchronizationContext : SynchronizationContext
{
    /// <summary>Initializes a context for the <see cref="SilkApplication"/> UI loop.</summary>
    public SilkSynchronizationContext()
    {
    }

    /// <summary>Initializes a context for the <see cref="SilkApplication"/> UI loop.</summary>
    /// <param name="window">Ignored: all windows share one UI thread.</param>
    [Obsolete("All windows share one UI thread; use the parameterless constructor.")]
    public SilkSynchronizationContext(SilkWindow window)
    {
    }

    /// <inheritdoc/>
    public override void Post(SendOrPostCallback d, object? state)
    {
        SilkApplication.Dispatch(() => d(state));
    }

    /// <inheritdoc/>
    public override void Send(SendOrPostCallback d, object? state)
    {
        if (SilkApplication.CheckAccess())
        {
            d(state);
            return;
        }

        SilkDispatcher.SendAndWait(() => d(state));
    }

    /// <inheritdoc/>
    public override SynchronizationContext CreateCopy() => this;
}
