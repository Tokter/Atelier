using System;
using System.Collections.Specialized;

namespace Atelier.Controls;

/// <summary>
/// Subscribes to <see cref="INotifyCollectionChanged.CollectionChanged"/> without keeping the listener alive.
/// </summary>
/// <remarks>
/// The collection only references this small proxy, which holds the listener through a weak reference. A long-lived
/// view-model collection therefore cannot keep a discarded control (and its element tree) alive. If the listener has
/// been collected, the proxy unsubscribes itself on the next notification; <see cref="Dispose"/> unsubscribes eagerly.
/// The handler must be a static (non-capturing) delegate, otherwise it would root the listener again.
/// </remarks>
/// <typeparam name="TListener">The listener type.</typeparam>
internal sealed class WeakCollectionChangedSubscription<TListener> : IDisposable where TListener : class
{
    private readonly WeakReference<TListener> _listener;
    private readonly Action<TListener, NotifyCollectionChangedEventArgs> _handler;
    private INotifyCollectionChanged? _source;

    public WeakCollectionChangedSubscription(
        INotifyCollectionChanged source,
        TListener listener,
        Action<TListener, NotifyCollectionChangedEventArgs> handler)
    {
        _listener = new WeakReference<TListener>(listener);
        _handler = handler;
        _source = source;
        source.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_listener.TryGetTarget(out var listener))
        {
            _handler(listener, e);
        }
        else
        {
            Dispose();
        }
    }

    /// <summary>Unsubscribes from the collection. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (_source != null)
        {
            _source.CollectionChanged -= OnCollectionChanged;
            _source = null;
        }
    }
}
