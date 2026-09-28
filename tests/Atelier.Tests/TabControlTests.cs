using System;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Tests;

public class TabControlTests
{
    private sealed record Doc(string Title);

    private static TabControl Layout(TabControl tabs, float width = 800)
    {
        tabs.AttachToHost();
        tabs.Measure(new Size(width, 400));
        tabs.Arrange(new Rect(0, 0, width, 400));
        return tabs;
    }

    private static TabControl ThreeTabs() => Layout(new TabControl().Tabs(
        new TabItem("One", new TextBlock("Page 1")),
        new TabItem("Two", new TextBlock("Page 2")),
        new TabItem("Three", new TextBlock("Page 3"))));

    private static PointerEventArgs At(float x, float y, PointerButtons button = PointerButtons.Left) =>
        new(new Point(x, y), new Point(x, y), button);

    private static Point Center(UIElement element) =>
        element.PointToScreen(new Point(element.Bounds.Width / 2, element.Bounds.Height / 2));

    [Fact]
    public void SelectsTheFirstTab_AndShowsTheSelectedPage()
    {
        var tabs = ThreeTabs();
        Assert.Equal(0, tabs.SelectedIndex);
        Assert.True(tabs.GetTab(0)!.IsSelected);
        Assert.Equal("Page 1", ((TextBlock)tabs.ContentPresenter.Content!).Text);

        object? changed = null;
        tabs.SelectionChanged += (_, item) => changed = item;
        tabs.SelectedIndex = 2;
        Assert.Equal("Page 3", ((TextBlock)tabs.ContentPresenter.Content!).Text);
        Assert.Same(tabs.GetTab(2), changed);
        Assert.False(tabs.GetTab(0)!.IsSelected);

        // One tab stop: only the selected tab is focusable.
        Assert.Equal(new[] { false, false, true }, Enumerable.Range(0, 3).Select(i => tabs.GetTab(i)!.IsFocusable));
    }

    [Fact]
    public void DataItems_UseTheHeaderAndContentTemplates()
    {
        var docs = new ObservableCollection<Doc> { new("a.txt"), new("b.txt") };
        var tabs = Layout(new TabControl()
            .WithHeaderTemplate((Doc d) => new StackPanel().Children(new Icon(MaterialIconKind.Description), new TextBlock(d.Title)))
            .WithContentTemplate((Doc d) => new TextBlock("Content of " + d.Title)));
        tabs.ItemsSource = docs;

        var header = tabs.GetTab(1)!;
        Assert.Same(docs[1], header.Item);
        Assert.Same(docs[1], header.DataContext);
        Assert.Equal("Content of a.txt", ((TextBlock)tabs.ContentPresenter.CurrentView!).Text);

        tabs.SelectedItem = docs[1];
        Assert.Equal(1, tabs.SelectedIndex);
        Assert.Equal("Content of b.txt", ((TextBlock)tabs.ContentPresenter.CurrentView!).Text);
    }

    [Fact]
    public void ClosingTheSelectedTab_SelectsTheNextOne_OrThePreviousForTheLast()
    {
        var tabs = ThreeTabs().AreTabsCloseable();
        var two = tabs.GetTab(1)!;
        Assert.Equal(Visibility.Visible, two.CloseButton.Visibility);

        tabs.SelectedIndex = 1;
        object? closed = null;
        tabs.TabClosed += (_, item) => closed = item;
        Assert.True(tabs.CloseTab(1));
        Assert.Same(two, closed);
        Assert.Null(two.Owner);
        Assert.Equal("Three", tabs.GetTab(tabs.SelectedIndex)!.Header);

        Assert.True(tabs.CloseTab(1));
        Assert.Equal("One", tabs.GetTab(tabs.SelectedIndex)!.Header);

        Assert.True(tabs.CloseTab(0));
        Assert.Equal(-1, tabs.SelectedIndex);
        Assert.Null(tabs.ContentPresenter.Content);
    }

    [Fact]
    public void TabClosing_CanKeepTheTab_AndIsCloseableOverridesTheDefault()
    {
        var tabs = ThreeTabs().AreTabsCloseable();
        tabs.GetTab(0)!.IsCloseable = false;
        Assert.Equal(Visibility.Collapsed, tabs.GetTab(0)!.CloseButton.Visibility);

        tabs.TabClosing += (_, e) => e.Cancel = e.Index == 2;
        Assert.False(tabs.CloseTab(2));
        Assert.Equal(3, tabs.Items.Count);
    }

    [Fact]
    public void Closing_RemovesTheItemFromABoundList()
    {
        var docs = new ObservableCollection<Doc> { new("a"), new("b"), new("c") };
        var tabs = Layout(new TabControl { ItemsSource = docs, AreTabsCloseable = true });

        // A middle click closes a closeable tab.
        var b = tabs.GetTab(1)!;
        b.OnPointerPressed(At(5, 5, PointerButtons.Middle));
        Assert.Equal(new[] { "a", "c" }, docs.Select(d => d.Title));
    }

    [Fact]
    public void AddButton_AddsTheNewItem_AndSelectsIt()
    {
        var docs = new ObservableCollection<Doc> { new("a") };
        var tabs = Layout(new TabControl { ItemsSource = docs, ShowAddButton = true });
        Assert.Equal(Visibility.Visible, tabs.AddButton.Visibility);

        tabs.AddTabRequested += (_, e) => e.NewItem = new Doc("new");
        tabs.RequestAddTab();
        Assert.Equal(2, docs.Count);
        Assert.Equal(1, tabs.SelectedIndex);

        // A handler that adds to the bound collection itself: the new tab is selected too.
        var direct = Layout(new TabControl { ItemsSource = docs });
        direct.AddTabRequested += (_, _) => docs.Insert(0, new Doc("first"));
        direct.RequestAddTab();
        Assert.Equal(0, direct.SelectedIndex);
        Assert.Equal("first", ((Doc)direct.SelectedItem!).Title);
    }

    [Fact]
    public void Keyboard_MovesBetweenTabs_AndDeleteCloses()
    {
        var tabs = ThreeTabs().AreTabsCloseable();
        var first = tabs.GetTab(0)!;
        first.Focus();

        void Press(Key key, ModifierKeys modifiers = ModifierKeys.None) =>
            FocusManager.GetFocusedElement(tabs)!.DispatchKeyEvent(new KeyEventArgs(key, 0, modifiers, true),
                static (el, a) => el.OnPreviewKeyDown(a), static (el, a) => el.OnKeyDown(a));

        Press(Key.Right);
        Assert.Equal(1, tabs.SelectedIndex);
        Assert.True(tabs.GetTab(1)!.IsFocused);
        Press(Key.End);
        Assert.Equal(2, tabs.SelectedIndex);
        Press(Key.PageDown, ModifierKeys.Control);
        Assert.Equal(0, tabs.SelectedIndex); // wraps
        Press(Key.Delete);
        Assert.Equal(new object?[] { "Two", "Three" }, tabs.Items.Cast<TabItem>().Select(t => t.Header));
        Assert.True(tabs.GetTab(0)!.IsFocused);
        tabs.DetachFromHost();
    }

    [Fact]
    public void DraggingATab_MovesItPastItsNeighbors_AndReportsTheMove()
    {
        var tabs = ThreeTabs();
        var one = tabs.GetTab(0)!;
        TabMovedEventArgs? moved = null;
        tabs.TabMoved += (_, e) => moved = e;

        var start = Center(one);
        one.OnPointerPressed(At(start.X, start.Y));
        Assert.True(one.IsPointerCaptured);

        // Below the threshold nothing moves.
        one.OnPointerMoved(At(start.X + 3, start.Y));
        Assert.Null(tabs.DraggedTab);

        // Past the middle of the second tab: the dragged tab takes its place.
        var three = Center(tabs.GetTab(2)!);
        one.OnPointerMoved(At(three.X + 5, start.Y));
        Assert.Same(one, tabs.DraggedTab);
        Assert.Equal(1, one.ZIndex);
        Assert.Same(one, tabs.Children.OfType<ScrollViewer>().Single().Content is StackPanel panel ? panel.Children[2] : null);

        one.OnPointerReleased(At(three.X + 5, start.Y));
        Assert.Null(tabs.DraggedTab);
        Assert.Equal(new object?[] { "Two", "Three", "One" }, tabs.Items.Cast<TabItem>().Select(t => t.Header));
        Assert.NotNull(moved);
        Assert.Equal((0, 2), (moved!.OldIndex, moved.NewIndex));
        Assert.Same(one, tabs.SelectedItem);
        tabs.DetachFromHost();
    }

    [Fact]
    public void DraggingATab_MovesTheItemInABoundList_KeepingTheSelection()
    {
        var docs = new ObservableCollection<Doc> { new("a"), new("b"), new("c") };
        var tabs = Layout(new TabControl { ItemsSource = docs, SelectedIndex = 2 });
        int selectionChanges = 0;
        tabs.SelectionChanged += (_, _) => selectionChanges++;

        var c = tabs.GetTab(2)!;
        var start = Center(c);
        c.OnPointerPressed(At(start.X, start.Y));
        var a = Center(tabs.GetTab(0)!);
        c.OnPointerMoved(At(a.X - 5, start.Y));
        c.OnPointerReleased(At(a.X - 5, start.Y));

        Assert.Equal(new[] { "c", "a", "b" }, docs.Select(d => d.Title));
        Assert.Equal("c", ((Doc)tabs.SelectedItem!).Title);
        Assert.Equal(0, tabs.SelectedIndex);
        Assert.Equal(0, selectionChanges);
        tabs.DetachFromHost();
    }

    [Fact]
    public void CanReorderTabs_False_OnlySelects()
    {
        var tabs = ThreeTabs().CanReorderTabs(false);
        var one = tabs.GetTab(0)!;
        var start = Center(one);
        one.OnPointerPressed(At(start.X, start.Y));
        one.OnPointerMoved(At(start.X + 400, start.Y));
        one.OnPointerReleased(At(start.X + 400, start.Y));
        Assert.Equal("One", tabs.GetTab(0)!.Header);
        tabs.DetachFromHost();
    }

    [Fact]
    public void Indicator_SpansTheLabel_OrTheWholeTab()
    {
        var tabs = ThreeTabs();
        var tab = tabs.GetTab(0)!;
        var indicator = tabs.IndicatorBounds;
        Assert.Equal(3, indicator.Height);
        Assert.Equal(tab.Bounds.Bottom - 3, indicator.Y);
        Assert.True(indicator.Width < tab.Bounds.Width);

        tabs.TabStyle = TabStyle.Secondary;
        Layout(tabs);
        indicator = tabs.IndicatorBounds;
        Assert.Equal(2, indicator.Height);
        Assert.Equal(tabs.GetTab(0)!.Bounds.Width, indicator.Width);

        tabs.TabStyle = TabStyle.Browser;
        Assert.Equal(0, tabs.IndicatorBounds.Width);
        tabs.DetachFromHost();
    }

    [Fact]
    public void ZIndex_DecidesWhichSiblingIsHitFirst()
    {
        var below = new Border().Size(100, 100).ZIndex(1);
        var above = new Border().Size(100, 100);
        var root = new Grid().Children(below, above);
        root.Measure(new Size(100, 100));
        root.Arrange(new Rect(0, 0, 100, 100));

        Assert.Same(below, root.HitTest(new Point(50, 50)));
        below.ZIndex = 0;
        Assert.Same(above, root.HitTest(new Point(50, 50)));
    }
}
