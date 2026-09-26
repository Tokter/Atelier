using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Xunit;

namespace Atelier.Tests;

/// <summary>
/// A list that raises whatever notification a test hands it (including ones with -1 indices) and counts subscribers.
/// </summary>
public class ManualNotifyingList : IEnumerable, INotifyCollectionChanged
{
    public List<object> Items { get; } = [];
    private NotifyCollectionChangedEventHandler? _handler;

    public int SubscriberCount { get; private set; }

    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add { _handler += value; SubscriberCount++; }
        remove { _handler -= value; SubscriberCount--; }
    }

    public void Raise(NotifyCollectionChangedEventArgs e) => _handler?.Invoke(this, e);

    public IEnumerator GetEnumerator() => Items.GetEnumerator();
}

/// <summary>An observable collection that counts its CollectionChanged subscribers.</summary>
public class CountingObservableCollection<T> : ObservableCollection<T>
{
    public CountingObservableCollection() { }
    public CountingObservableCollection(IEnumerable<T> items) : base(items) { }

    public int SubscriberCount { get; private set; }

    public override event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add { base.CollectionChanged += value; SubscriberCount++; }
        remove { base.CollectionChanged -= value; SubscriberCount--; }
    }

    /// <summary>Replaces all items and raises a single Reset.</summary>
    public void ResetTo(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (var item in items) Items.Add(item);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}

public class ListBoxTests
{
    private static ListBoxItem Container(ListBox listBox, int index) => (ListBoxItem)listBox.ContainerFromIndex(index)!;

    private static void Layout(UIElement element, float width = 300, float height = 400)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
    }

    private static void Click(ListBoxItem item)
    {
        item.OnPointerEntered(new PointerEventArgs(Point.Zero));
        item.OnPointerPressed(new PointerEventArgs(Point.Zero, PointerButtons.Left));
        item.OnPointerReleased(new PointerEventArgs(Point.Zero, PointerButtons.Left));
    }

    private static string ContainerText(ListBox listBox, int index) =>
        ((TextBlock)Container(listBox, index).Content!).Text;

    [Fact]
    public void ItemsSource_Replace_UpdatesItemAndContainer()
    {
        var source = new ObservableCollection<string> { "a", "b", "c" };
        var listBox = new ListBox { ItemsSource = source };
        var untouched = Container(listBox, 0);

        source[1] = "B";

        Assert.Equal(new object[] { "a", "B", "c" }, listBox.Items);
        Assert.Equal("B", Container(listBox, 1).ItemValue);
        Assert.Equal("B", ContainerText(listBox, 1));
        Assert.Same(untouched, Container(listBox, 0));
    }

    [Fact]
    public void ItemsSource_Move_MovesItemAndReusesContainer()
    {
        var source = new ObservableCollection<string> { "a", "b", "c" };
        var listBox = new ListBox { ItemsSource = source };
        var containerA = Container(listBox, 0);

        source.Move(0, 2);

        Assert.Equal(new object[] { "b", "c", "a" }, listBox.Items);
        Assert.Same(containerA, Container(listBox, 2));
        Assert.Equal(2, containerA.Index);
        Assert.Equal(0, Container(listBox, 0).Index);
    }

    [Fact]
    public void ItemsSource_Reset_ResyncsItems()
    {
        var source = new CountingObservableCollection<string>(["a", "b"]);
        var listBox = new ListBox { ItemsSource = source };

        source.ResetTo(["x", "y", "z"]);

        Assert.Equal(new object[] { "x", "y", "z" }, listBox.Items);
        Assert.Equal("z", ContainerText(listBox, 2));
    }

    [Fact]
    public void ItemsSource_NotificationsWithoutIndex_FallBackToReset()
    {
        var source = new ManualNotifyingList();
        source.Items.AddRange(["a", "b"]);
        var listBox = new ListBox { ItemsSource = source };

        source.Items.Add("c");
        source.Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (object)"c")); // index -1
        Assert.Equal(new object[] { "a", "b", "c" }, listBox.Items);

        source.Items.Remove("a");
        source.Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, (object)"a")); // index -1
        Assert.Equal(new object[] { "b", "c" }, listBox.Items);
        Assert.Equal(2, listBox.ScrollViewer.Content is Panel p ? p.Children.Count : -1);
    }

    [Fact]
    public void AddingOneItem_ToLargeSource_KeepsExistingContainers()
    {
        var source = new ObservableCollection<string>();
        for (int i = 0; i < 1000; i++) source.Add($"Item {i}");
        var listBox = new ListBox { ItemsSource = source };

        var before = new UIElement[1000];
        for (int i = 0; i < 1000; i++) before[i] = listBox.ContainerFromIndex(i)!;

        source.Insert(500, "New");

        Assert.Equal(1001, listBox.Items.Count);
        for (int i = 0; i < 500; i++) Assert.Same(before[i], listBox.ContainerFromIndex(i));
        for (int i = 500; i < 1000; i++) Assert.Same(before[i], listBox.ContainerFromIndex(i + 1));
        Assert.Equal("New", Container(listBox, 500).ItemValue);
        Assert.Equal(1000, Container(listBox, 1000).Index);
    }

    [Fact]
    public void RemovingSelectedItem_ClearsSelection()
    {
        var source = new ObservableCollection<string> { "a", "b", "c" };
        var listBox = new ListBox { ItemsSource = source, SelectedIndex = 1 };
        var raised = new List<object?>();
        listBox.SelectionChanged += (s, item) => raised.Add(item);

        source.RemoveAt(1);

        Assert.Null(listBox.SelectedItem);
        Assert.Equal(-1, listBox.SelectedIndex);
        Assert.Equal(new object?[] { null }, raised);
        Assert.False(Container(listBox, 0).IsSelected);
        Assert.False(Container(listBox, 1).IsSelected);
    }

    [Fact]
    public void InsertingBeforeSelection_ShiftsIndex_WithoutSelectionChanged()
    {
        var source = new ObservableCollection<string> { "a", "b", "c" };
        var listBox = new ListBox { ItemsSource = source, SelectedIndex = 2 };
        int raised = 0;
        listBox.SelectionChanged += (s, item) => raised++;

        source.Insert(0, "first");

        Assert.Equal(3, listBox.SelectedIndex);
        Assert.Equal("c", listBox.SelectedItem);
        Assert.True(Container(listBox, 3).IsSelected);
        Assert.Equal(0, raised);

        source.RemoveAt(0);
        Assert.Equal(2, listBox.SelectedIndex);
        Assert.Equal("c", listBox.SelectedItem);
    }

    [Fact]
    public void SelectedIndex_SetBeforeItemsSource_IsApplied()
    {
        var listBox = new ListBox { SelectedIndex = 1 };
        listBox.ItemsSource = new[] { "a", "b", "c" };

        Assert.Equal(1, listBox.SelectedIndex);
        Assert.Equal("b", listBox.SelectedItem);
        Assert.True(Container(listBox, 1).IsSelected);
    }

    [Fact]
    public void SelectedItem_SetBeforeItemsSource_IsApplied()
    {
        var listBox = new ListBox { SelectedItem = "c" };
        listBox.ItemsSource = new[] { "a", "b", "c" };

        Assert.Equal(2, listBox.SelectedIndex);
        Assert.Equal("c", listBox.SelectedItem);
    }

    [Fact]
    public void OutOfRangeSelectedIndex_IsCoercedToMinusOne()
    {
        var listBox = new ListBox { ItemsSource = new[] { "a", "b", "c" } };

        listBox.SelectedIndex = 5;

        Assert.Equal(-1, listBox.SelectedIndex);
        Assert.Null(listBox.SelectedItem);
    }

    [Fact]
    public void SelectedItem_NotInItems_IsCoercedToNull()
    {
        var listBox = new ListBox { ItemsSource = new[] { "a", "b" }, SelectedIndex = 0 };

        listBox.SelectedItem = "zzz";

        Assert.Null(listBox.SelectedItem);
        Assert.Equal(-1, listBox.SelectedIndex);
    }

    [Fact]
    public void DuplicateItems_SelectByIndex_HighlightsOnlyThatContainer()
    {
        var listBox = new ListBox { ItemsSource = new[] { "a", "b", "a" } };

        listBox.SelectedIndex = 2;

        Assert.Equal(2, listBox.SelectedIndex);
        Assert.Equal("a", listBox.SelectedItem);
        Assert.False(Container(listBox, 0).IsSelected);
        Assert.True(Container(listBox, 2).IsSelected);

        // Setting the equal item again keeps the selected position.
        listBox.SelectedItem = "a";
        Assert.Equal(2, listBox.SelectedIndex);
    }

    [Fact]
    public void ClickingDuplicate_SelectsClickedPosition()
    {
        var listBox = new ListBox { ItemsSource = new[] { "a", "b", "a" } };
        var root = new StackPanel().Children(listBox);
        Layout(root);

        Click(Container(listBox, 2));

        Assert.Equal(2, listBox.SelectedIndex);
        Assert.True(Container(listBox, 2).IsSelected);
        Assert.False(Container(listBox, 0).IsSelected);
    }

    [Fact]
    public void Click_WithoutPriorSelection_RaisesSelectionChangedOnce()
    {
        var listBox = new ListBox { ItemsSource = new[] { "a", "b", "c" } };
        var root = new StackPanel().Children(listBox);
        Layout(root);
        var raised = new List<object?>();
        listBox.SelectionChanged += (s, item) => raised.Add(item);

        Click(Container(listBox, 2));

        Assert.Equal(new object?[] { "c" }, raised);
        Assert.Same(listBox, FocusManager.CurrentFocused);
    }

    [Fact]
    public void RightClick_DoesNotSelect()
    {
        var listBox = new ListBox { ItemsSource = new[] { "a", "b" } };
        var item = Container(listBox, 1);
        item.OnPointerEntered(new PointerEventArgs(Point.Zero));
        item.OnPointerPressed(new PointerEventArgs(Point.Zero, PointerButtons.Right));
        item.OnPointerReleased(new PointerEventArgs(Point.Zero, PointerButtons.Right));

        Assert.Equal(-1, listBox.SelectedIndex);
    }

    [Fact]
    public void PageDown_UsesRealContainerHeights()
    {
        var listBox = new ListBox
        {
            Height = 200,
            ItemTemplate = _ => new Border { Height = 50 } // container: 50 + 2 * 8 padding = 66
        };
        for (int i = 0; i < 20; i++) listBox.Items.Add($"Item {i}");
        Layout(listBox, 300, 200);
        listBox.SelectedIndex = 0;

        listBox.OnKeyDown(new KeyEventArgs(Key.PageDown));

        // 3 more rows (198px) fit into the 200px viewport; a 36px row assumption would have jumped to 5.
        Assert.Equal(3, listBox.SelectedIndex);

        listBox.OnKeyDown(new KeyEventArgs(Key.PageUp));
        Assert.Equal(0, listBox.SelectedIndex);
    }

    [Fact]
    public void TextSearch_SelectsByPrefix_AndCyclesOnRepeatedCharacter()
    {
        var listBox = new ListBox { ItemsSource = new[] { "Apple", "Banana", "Blueberry", "Cherry" } };

        listBox.OnTextInput(new TextInputEventArgs("b"));
        Assert.Equal("Banana", listBox.SelectedItem);

        listBox.OnTextInput(new TextInputEventArgs("l"));
        Assert.Equal("Blueberry", listBox.SelectedItem);
    }

    [Fact]
    public void TextSearch_RepeatedCharacter_Cycles()
    {
        var listBox = new ListBox { ItemsSource = new[] { "Apple", "Banana", "Blueberry", "Cherry" } };

        listBox.OnTextInput(new TextInputEventArgs("b"));
        listBox.OnTextInput(new TextInputEventArgs("b"));
        Assert.Equal("Blueberry", listBox.SelectedItem);

        listBox.OnTextInput(new TextInputEventArgs("b"));
        Assert.Equal("Banana", listBox.SelectedItem);
    }

    [Fact]
    public void TextSearch_CanBeDisabled()
    {
        var listBox = new ListBox { ItemsSource = new[] { "Apple", "Banana" }, IsTextSearchEnabled = false };

        listBox.OnTextInput(new TextInputEventArgs("b"));

        Assert.Null(listBox.SelectedItem);
    }

    [Fact]
    public void ScrollIntoView_BeforeLayout_IsAppliedAfterArrange()
    {
        var listBox = new ListBox();
        for (int i = 0; i < 30; i++) listBox.Items.Add($"Item {i}");

        listBox.SelectedIndex = 25; // scrolls into view once layout has run
        Layout(listBox, 300, 150);

        // Container bounds are in content (item panel) coordinates.
        var container = Container(listBox, 25);
        float offset = listBox.ScrollViewer.ScrollOffsetY;
        Assert.True(offset > 0);
        Assert.True(container.Bounds.Y >= offset - 0.01f);
        Assert.True(container.Bounds.Bottom - offset <= 150.01f);
    }

    [Fact]
    public void DetachedListBox_IsCollectable_WhileItemsSourceLivesOn()
    {
        var source = new CountingObservableCollection<string>(["a", "b", "c"]);

        var weak = CreateAttachAndDetach(source);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weak.TryGetTarget(out _));

        // The dead listener's proxy unsubscribes on the next notification.
        source.Add("d");
        Assert.Equal(0, source.SubscriberCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<ListBox> CreateAttachAndDetach(CountingObservableCollection<string> source)
    {
        var root = new StackPanel();
        var listBox = new ListBox { ItemsSource = source, SelectedIndex = 1 };
        root.Add(listBox);
        root.AttachToHost();
        Layout(root);
        root.DetachFromHost();
        root.Remove(listBox);
        return new WeakReference<ListBox>(listBox);
    }

    [Fact]
    public void ChangingItemsSource_UnsubscribesFromOldSource()
    {
        var first = new CountingObservableCollection<string>(["a"]);
        var second = new CountingObservableCollection<string>(["b"]);
        var listBox = new ListBox { ItemsSource = first };
        Assert.Equal(1, first.SubscriberCount);

        listBox.ItemsSource = second;

        Assert.Equal(0, first.SubscriberCount);
        Assert.Equal(1, second.SubscriberCount);
    }
}
