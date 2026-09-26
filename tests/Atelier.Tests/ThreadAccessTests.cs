using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Core.Threading;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Tests;

/// <summary>
/// A dispatcher whose UI thread is the thread that created it; posted work is queued until <see cref="RunPending"/>.
/// </summary>
public sealed class QueueingTestDispatcher : IDispatcher, IDisposable
{
    private readonly int _uiThreadId = Environment.CurrentManagedThreadId;
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly IDispatcher _previous;

    public QueueingTestDispatcher()
    {
        _previous = Dispatcher.UIThread;
        Dispatcher.UIThread = this;
    }

    public int PostCount { get; private set; }

    public bool CheckAccess() => Environment.CurrentManagedThreadId == _uiThreadId;

    public void Post(Action action)
    {
        PostCount++;
        _queue.Enqueue(action);
    }

    public void Send(Action action) => throw new NotSupportedException();

    public void RunPending()
    {
        while (_queue.TryDequeue(out var action))
        {
            action();
        }
    }

    public void Dispose() => Dispatcher.UIThread = _previous;
}

public class ThreadAccessTests
{
    // A dedicated thread: blocking on Task.Run could run the task inline on this (UI) thread.
    private static Exception? OnBackgroundThread(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.Start();
        thread.Join();
        return error;
    }

    [Fact]
    public void SetValue_FromBackgroundThread_Throws()
    {
        var tb = new TextBlock("x");
        using var dispatcher = new QueueingTestDispatcher();

        var error = OnBackgroundThread(() => tb.FontSize = 20f);

        var invalid = Assert.IsType<InvalidOperationException>(error);
        Assert.Contains("TextBlock.SetValue(Control.FontSize)", invalid.Message);
        Assert.Equal(14f, tb.FontSize);
    }

    [Fact]
    public void Changes_OnTheUIThread_AreAllowed()
    {
        using var dispatcher = new QueueingTestDispatcher();
        var panel = new StackPanel();
        var tb = new TextBlock("x");

        panel.Add(tb);
        tb.FontSize = 20f;
        tb.InvalidateVisual();
        FocusManager.SetFocus(null);

        Assert.Equal(20f, tb.FontSize);
    }

    [Fact]
    public void TreeAndFocusChanges_FromBackgroundThread_Throw()
    {
        var panel = new StackPanel();
        var child = new TextBlock("x");
        var focusable = new FocusableTestElement();
        panel.Add(focusable);
        using var dispatcher = new QueueingTestDispatcher();

        Assert.IsType<InvalidOperationException>(OnBackgroundThread(() => panel.Add(child)));
        Assert.IsType<InvalidOperationException>(OnBackgroundThread(() => panel.RemoveChild(focusable)));
        Assert.IsType<InvalidOperationException>(OnBackgroundThread(() => focusable.InvalidateMeasure()));
        Assert.IsType<InvalidOperationException>(OnBackgroundThread(() => focusable.InvalidateVisual()));
        Assert.IsType<InvalidOperationException>(OnBackgroundThread(() => FocusManager.SetFocus(focusable)));
        Assert.Null(child.Parent);
    }

    [Fact]
    public void TraceMode_ReportsButAllowsTheChange()
    {
        var tb = new TextBlock("x");
        using var dispatcher = new QueueingTestDispatcher();
        Dispatcher.ThreadCheckMode = ThreadCheckMode.Trace;
        try
        {
            Assert.Null(OnBackgroundThread(() => tb.FontSize = 21f));
            Assert.Equal(21f, tb.FontSize);
        }
        finally
        {
            Dispatcher.ThreadCheckMode = ThreadCheckMode.Throw;
        }
    }

    [Fact]
    public void OffMode_DoesNotCheck()
    {
        var tb = new TextBlock("x");
        using var dispatcher = new QueueingTestDispatcher();
        Dispatcher.ThreadCheckMode = ThreadCheckMode.Off;
        try
        {
            Assert.Null(OnBackgroundThread(() => tb.FontSize = 22f));
            Assert.Equal(22f, tb.FontSize);
        }
        finally
        {
            Dispatcher.ThreadCheckMode = ThreadCheckMode.Throw;
        }
    }

    [Fact]
    public void WithoutAUIThread_EveryThreadIsAllowed()
    {
        // Headless use and unit tests: no platform dispatcher is installed.
        var tb = new TextBlock("x");

        Assert.Null(OnBackgroundThread(() => tb.FontSize = 23f));
        Assert.Equal(23f, tb.FontSize);
    }

    [Fact]
    public void Binding_SourceChangedOnBackgroundThread_UpdatesTargetOnUIThread()
    {
        using var dispatcher = new QueueingTestDispatcher();
        var vm = new BindingTestViewModel { Title = "A" };
        var tb = new TextBlock();
        using var binding = new PropertyBinding<string, BindingTestViewModel>(
            tb, TextBlock.TextProperty, vm, x => x.Title, null, sourcePropertyName: nameof(vm.Title));

        Assert.Null(OnBackgroundThread(() =>
        {
            vm.Title = "B";
            vm.Title = "C";
            vm.Title = "D";
        }));

        Assert.Equal("A", tb.Text);          // not touched off the UI thread
        Assert.Equal(1, dispatcher.PostCount); // the three changes were coalesced

        dispatcher.RunPending();
        Assert.Equal("D", tb.Text);

        // After the queued update ran, the next background change queues a new one.
        Assert.Null(OnBackgroundThread(() => vm.Title = "E"));
        dispatcher.RunPending();
        Assert.Equal("E", tb.Text);
    }

    [Fact]
    public void MultiBinding_SourceChangedOnBackgroundThread_UpdatesTargetOnUIThread()
    {
        using var dispatcher = new QueueingTestDispatcher();
        var vm = new BindingTestViewModel { Title = "A" };
        var tb = new TextBlock();
        tb.SetMultiBinding(TextBlock.TextProperty, () => vm.Title + "!", vm);

        Assert.Null(OnBackgroundThread(() => vm.Title = "B"));
        Assert.Equal("A!", tb.Text);

        dispatcher.RunPending();
        Assert.Equal("B!", tb.Text);
    }

    [Fact]
    public void ItemsSourceChangedOnBackgroundThread_Throws()
    {
        var items = new ObservableCollection<string> { "a" };
        var list = new ItemsControl { ItemsSource = items };
        using var dispatcher = new QueueingTestDispatcher();

        var error = OnBackgroundThread(() => items.Add("b"));

        var invalid = Assert.IsType<InvalidOperationException>(error);
        Assert.Contains("ItemsControl.ItemsSource collection change", invalid.Message);
    }
}
