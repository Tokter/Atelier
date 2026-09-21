using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Atelier.Core.Threading;

namespace Atelier.Platform.Silk;

/// <summary>
/// Implements <see cref="IDispatcher"/> by enqueueing work to the <see cref="SilkWindow"/> dispatch queue.
/// </summary>
public sealed class SilkDispatcher : IDispatcher
{
    private readonly SilkWindow _window;

    public SilkDispatcher(SilkWindow window)
    {
        _window = window;
    }

    public bool CheckAccess() => Thread.CurrentThread.ManagedThreadId == _window.MainThreadId;

    public void Post(Action action) => _window.Dispatch(action);

    public void Send(Action action)
    {
        if (CheckAccess())
        {
            action();
            return;
        }

        using var evt = new ManualResetEventSlim();
        Exception? error = null;
        _window.Dispatch(() =>
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
/// Synchronization context that posts continuations back to the <see cref="SilkWindow"/> render loop.
/// </summary>
public sealed class SilkSynchronizationContext : SynchronizationContext
{
    private readonly SilkWindow _window;

    public SilkSynchronizationContext(SilkWindow window)
    {
        _window = window;
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        _window.Dispatch(() => d(state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (Thread.CurrentThread.ManagedThreadId == _window.MainThreadId)
        {
            d(state);
            return;
        }

        using var evt = new ManualResetEventSlim();
        Exception? error = null;
        _window.Dispatch(() =>
        {
            try
            {
                d(state);
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

    public override SynchronizationContext CreateCopy() => this;
}
