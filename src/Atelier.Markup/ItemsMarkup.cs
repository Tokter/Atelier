using System;
using System.Collections;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Markup;

/// <summary>
/// Fluent methods for <see cref="ItemsControl"/> and <see cref="ListBox"/>: items and item templates. See
/// <see cref="MarkupExtensions"/> for the conventions.
/// </summary>
public static class ItemsControlMarkup
{
    /// <summary>
    /// Sets the collection the items come from. Collections that implement
    /// <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> (such as an <c>ObservableCollection</c>)
    /// update the list when they change. The items are copied into <see cref="ItemsControl.Items"/>.
    /// </summary>
    public static T ItemsSource<T>(this T itemsControl, IEnumerable? itemsSource) where T : ItemsControl => itemsControl.Set(ItemsControl.ItemsSourceProperty, itemsSource);

    /// <summary>Adds <paramref name="items"/> to <see cref="ItemsControl.Items"/>, for lists without an <c>ItemsSource</c>.</summary>
    public static T Items<T>(this T itemsControl, params object[] items) where T : ItemsControl
    {
        foreach (var item in items)
        {
            itemsControl.Items.Add(item);
        }
        return itemsControl;
    }

    /// <summary>
    /// Sets the <see cref="ItemsControl.ItemTemplate"/>, which builds the element that shows an item. Without a
    /// template, elements are shown as is and other items as their text.
    /// </summary>
    public static T WithItemTemplate<T>(this T itemsControl, Func<object, UIElement>? template) where T : ItemsControl
    {
        itemsControl.ItemTemplate = template;
        return itemsControl;
    }

    /// <summary>
    /// Sets an item template for items of type <typeparamref name="TItem"/>; other items are shown as their text. Write
    /// out the lambda parameter's type: <c>.WithItemTemplate((Person p) =&gt; new TextBlock(p.Name))</c>.
    /// </summary>
    public static T WithItemTemplate<T, TItem>(this T itemsControl, Func<TItem, UIElement> template) where T : ItemsControl
    {
        itemsControl.ItemTemplate = ItemsMarkupHelpers.TypedTemplate(template);
        return itemsControl;
    }

    /// <summary>Binds the items source to a collection of <paramref name="source"/>.</summary>
    public static T BindItemsSource<T, TSource>(this T itemsControl, TSource source, Func<TSource, IEnumerable?> getter, Action<TSource, IEnumerable?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ItemsControl where TSource : class =>
        itemsControl.BindToSource(ItemsControl.ItemsSourceProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the items source to a collection of the DataContext: <c>.BindItemsSource((ContactsViewModel c) =&gt; c.People)</c>.</summary>
    public static T BindItemsSource<T, TDataContext>(this T itemsControl, Func<TDataContext, IEnumerable?> getter, Action<TDataContext, IEnumerable?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ItemsControl where TDataContext : class =>
        itemsControl.BindToDataContext(ItemsControl.ItemsSourceProperty, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>Fluent methods for <see cref="ListBox"/> selection. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ListBoxMarkup
{
    /// <summary>Selects <paramref name="item"/>; <c>null</c> clears the selection.</summary>
    public static T SelectedItem<T>(this T listBox, object? item) where T : ListBox => listBox.Set(ListBox.SelectedItemProperty, item);

    /// <summary>Selects the item at <paramref name="index"/>; -1 clears the selection.</summary>
    public static T SelectedIndex<T>(this T listBox, int index) where T : ListBox => listBox.Set(ListBox.SelectedIndexProperty, index);

    /// <summary>Sets whether typing selects the next item whose text starts with the typed characters (on by default).</summary>
    public static T IsTextSearchEnabled<T>(this T listBox, bool isEnabled = true) where T : ListBox => listBox.Set(ListBox.IsTextSearchEnabledProperty, isEnabled);

    /// <summary>Handles <see cref="ListBox.SelectionChanged"/>, raised with the newly selected item (or <c>null</c>).</summary>
    public static T OnSelectionChanged<T>(this T listBox, EventHandler<object?> handler) where T : ListBox
    {
        listBox.SelectionChanged += handler;
        return listBox;
    }

    /// <summary>Runs <paramref name="action"/> with the newly selected item (or <c>null</c>) whenever the selection changes.</summary>
    public static T OnSelectionChanged<T>(this T listBox, Action<object?> action) where T : ListBox => listBox.OnSelectionChanged(MarkupExtensions.ToHandler(action));

    /// <summary>
    /// Binds the selected item to a value of <paramref name="source"/>. With a <paramref name="setter"/>, the user's
    /// selection is written back; a selected item that isn't a <typeparamref name="TItem"/> is written as <c>default</c>.
    /// </summary>
    public static T BindSelectedItem<T, TSource, TItem>(this T listBox, TSource source, Func<TSource, TItem> getter, Action<TSource, TItem>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ListBox where TSource : class =>
        listBox.BindToSource(ListBox.SelectedItemProperty, source, s => (object?)getter(s), ItemsMarkupHelpers.ToObjectSetter(setter), updateSourceTrigger, getterExpression);

    /// <summary>
    /// Binds the selected item to a value of the DataContext. With a <paramref name="setter"/>, the user's selection is
    /// written back: <c>.BindSelectedItem((ContactsViewModel c) =&gt; c.Selected, (c, person) =&gt; c.Selected = person)</c>.
    /// </summary>
    public static T BindSelectedItem<T, TDataContext, TItem>(this T listBox, Func<TDataContext, TItem> getter, Action<TDataContext, TItem>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ListBox where TDataContext : class =>
        listBox.BindToDataContext(ListBox.SelectedItemProperty, (TDataContext s) => (object?)getter(s), ItemsMarkupHelpers.ToObjectSetter(setter), updateSourceTrigger, getterExpression);

    /// <summary>Binds the selected index to a value of <paramref name="source"/>. With a <paramref name="setter"/>, the user's selection is written back.</summary>
    public static T BindSelectedIndex<T, TSource>(this T listBox, TSource source, Func<TSource, int> getter, Action<TSource, int>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ListBox where TSource : class =>
        listBox.BindToSource(ListBox.SelectedIndexProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the selected index to a value of the DataContext. With a <paramref name="setter"/>, the user's selection is written back.</summary>
    public static T BindSelectedIndex<T, TDataContext>(this T listBox, Func<TDataContext, int> getter, Action<TDataContext, int>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ListBox where TDataContext : class =>
        listBox.BindToDataContext(ListBox.SelectedIndexProperty, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>Fluent methods for <see cref="ListBoxItem"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ListBoxItemMarkup
{
    /// <summary>Sets whether the item is drawn as selected.</summary>
    public static T IsSelected<T>(this T item, bool isSelected = true) where T : ListBoxItem => item.Set(ListBoxItem.IsSelectedProperty, isSelected);

    /// <summary>Handles <see cref="ListBoxItem.Clicked"/>, raised when the item is clicked.</summary>
    public static T OnClicked<T>(this T item, EventHandler handler) where T : ListBoxItem
    {
        item.Clicked += handler;
        return item;
    }

    /// <summary>Runs <paramref name="action"/> when the item is clicked (<see cref="ListBoxItem.Clicked"/>).</summary>
    public static T OnClicked<T>(this T item, Action action) where T : ListBoxItem => item.OnClicked(MarkupExtensions.ToHandler(action));
}

/// <summary>Fluent methods for <see cref="ComboBox"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ComboBoxMarkup
{
    #region Properties

    /// <summary>
    /// Sets the collection the options come from. Observable collections update the drop-down when they change. While
    /// it is set, its items replace <see cref="ComboBox.Items"/>.
    /// </summary>
    public static T ItemsSource<T>(this T comboBox, IEnumerable? itemsSource) where T : ComboBox => comboBox.Set(ComboBox.ItemsSourceProperty, itemsSource);

    /// <summary>Adds <paramref name="items"/> to <see cref="ComboBox.Items"/>, for options without an <c>ItemsSource</c>.</summary>
    public static T Items<T>(this T comboBox, params object[] items) where T : ComboBox
    {
        foreach (var item in items)
        {
            comboBox.Items.Add(item);
        }
        return comboBox;
    }

    /// <summary>
    /// Sets the <see cref="ComboBox.ItemTemplate"/>, which builds the element that shows an option in the drop-down.
    /// The closed box shows the selected item's text.
    /// </summary>
    public static T WithItemTemplate<T>(this T comboBox, Func<object, UIElement>? template) where T : ComboBox
    {
        comboBox.ItemTemplate = template;
        return comboBox;
    }

    /// <summary>
    /// Sets an item template for options of type <typeparamref name="TItem"/>; other options are shown as their text.
    /// Write out the lambda parameter's type: <c>.WithItemTemplate((Country c) =&gt; new TextBlock(c.Name))</c>.
    /// </summary>
    public static T WithItemTemplate<T, TItem>(this T comboBox, Func<TItem, UIElement> template) where T : ComboBox
    {
        comboBox.ItemTemplate = ItemsMarkupHelpers.TypedTemplate(template);
        return comboBox;
    }

    /// <summary>Selects <paramref name="item"/>; <c>null</c> clears the selection.</summary>
    public static T SelectedItem<T>(this T comboBox, object? item) where T : ComboBox => comboBox.Set(ComboBox.SelectedItemProperty, item);

    /// <summary>Selects the option at <paramref name="index"/>; -1 clears the selection.</summary>
    public static T SelectedIndex<T>(this T comboBox, int index) where T : ComboBox => comboBox.Set(ComboBox.SelectedIndexProperty, index);

    /// <summary>Sets the hint shown while nothing is selected.</summary>
    public static T Placeholder<T>(this T comboBox, string? placeholder) where T : ComboBox => comboBox.Set(ComboBox.PlaceholderProperty, placeholder ?? string.Empty);

    /// <summary>Sets the maximum height of the drop-down in pixels. Longer lists scroll.</summary>
    public static T MaxDropDownHeight<T>(this T comboBox, float height) where T : ComboBox => comboBox.Set(ComboBox.MaxDropDownHeightProperty, height);

    /// <summary>Opens or closes the drop-down.</summary>
    public static T IsDropDownOpen<T>(this T comboBox, bool isOpen = true) where T : ComboBox => comboBox.Set(ComboBox.IsDropDownOpenProperty, isOpen);

    /// <summary>Sets whether typing selects the next option whose text starts with the typed characters (on by default).</summary>
    public static T IsTextSearchEnabled<T>(this T comboBox, bool isEnabled = true) where T : ComboBox => comboBox.Set(ComboBox.IsTextSearchEnabledProperty, isEnabled);

    #endregion

    #region Events

    /// <summary>Handles <see cref="ComboBox.SelectionChanged"/>, raised with the newly selected item (or <c>null</c>).</summary>
    public static T OnSelectionChanged<T>(this T comboBox, EventHandler<object?> handler) where T : ComboBox
    {
        comboBox.SelectionChanged += handler;
        return comboBox;
    }

    /// <summary>Runs <paramref name="action"/> with the newly selected item (or <c>null</c>) whenever the selection changes.</summary>
    public static T OnSelectionChanged<T>(this T comboBox, Action<object?> action) where T : ComboBox => comboBox.OnSelectionChanged(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="ComboBox.DropDownOpened"/>, raised when the drop-down opens.</summary>
    public static T OnDropDownOpened<T>(this T comboBox, EventHandler handler) where T : ComboBox
    {
        comboBox.DropDownOpened += handler;
        return comboBox;
    }

    /// <summary>Runs <paramref name="action"/> when the drop-down opens (<see cref="ComboBox.DropDownOpened"/>).</summary>
    public static T OnDropDownOpened<T>(this T comboBox, Action action) where T : ComboBox => comboBox.OnDropDownOpened(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="ComboBox.DropDownClosed"/>, raised when the drop-down closes.</summary>
    public static T OnDropDownClosed<T>(this T comboBox, EventHandler handler) where T : ComboBox
    {
        comboBox.DropDownClosed += handler;
        return comboBox;
    }

    /// <summary>Runs <paramref name="action"/> when the drop-down closes (<see cref="ComboBox.DropDownClosed"/>).</summary>
    public static T OnDropDownClosed<T>(this T comboBox, Action action) where T : ComboBox => comboBox.OnDropDownClosed(MarkupExtensions.ToHandler(action));

    #endregion

    #region Bindings

    /// <summary>Binds the options to a collection of <paramref name="source"/>.</summary>
    public static T BindItemsSource<T, TSource>(this T comboBox, TSource source, Func<TSource, IEnumerable?> getter, Action<TSource, IEnumerable?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ComboBox where TSource : class =>
        comboBox.BindToSource(ComboBox.ItemsSourceProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the options to a collection of the DataContext.</summary>
    public static T BindItemsSource<T, TDataContext>(this T comboBox, Func<TDataContext, IEnumerable?> getter, Action<TDataContext, IEnumerable?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ComboBox where TDataContext : class =>
        comboBox.BindToDataContext(ComboBox.ItemsSourceProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>
    /// Binds the selected item to a value of <paramref name="source"/>. With a <paramref name="setter"/>, the user's
    /// choice is written back; a selected item that isn't a <typeparamref name="TItem"/> is written as <c>default</c>.
    /// </summary>
    public static T BindSelectedItem<T, TSource, TItem>(this T comboBox, TSource source, Func<TSource, TItem> getter, Action<TSource, TItem>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ComboBox where TSource : class =>
        comboBox.BindToSource(ComboBox.SelectedItemProperty, source, s => (object?)getter(s), ItemsMarkupHelpers.ToObjectSetter(setter), updateSourceTrigger, getterExpression);

    /// <summary>Binds the selected item to a value of the DataContext. With a <paramref name="setter"/>, the user's choice is written back.</summary>
    public static T BindSelectedItem<T, TDataContext, TItem>(this T comboBox, Func<TDataContext, TItem> getter, Action<TDataContext, TItem>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ComboBox where TDataContext : class =>
        comboBox.BindToDataContext(ComboBox.SelectedItemProperty, (TDataContext s) => (object?)getter(s), ItemsMarkupHelpers.ToObjectSetter(setter), updateSourceTrigger, getterExpression);

    /// <summary>Binds the selected index to a value of <paramref name="source"/>. With a <paramref name="setter"/>, the user's choice is written back.</summary>
    public static T BindSelectedIndex<T, TSource>(this T comboBox, TSource source, Func<TSource, int> getter, Action<TSource, int>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ComboBox where TSource : class =>
        comboBox.BindToSource(ComboBox.SelectedIndexProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the selected index to a value of the DataContext. With a <paramref name="setter"/>, the user's choice is written back.</summary>
    public static T BindSelectedIndex<T, TDataContext>(this T comboBox, Func<TDataContext, int> getter, Action<TDataContext, int>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ComboBox where TDataContext : class =>
        comboBox.BindToDataContext(ComboBox.SelectedIndexProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds whether the drop-down is open to a value of <paramref name="source"/>. With a <paramref name="setter"/>, opening and closing are written back.</summary>
    public static T BindIsDropDownOpen<T, TSource>(this T comboBox, TSource source, Func<TSource, bool> getter, Action<TSource, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ComboBox where TSource : class =>
        comboBox.BindToSource(ComboBox.IsDropDownOpenProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds whether the drop-down is open to a value of the DataContext. With a <paramref name="setter"/>, opening and closing are written back.</summary>
    public static T BindIsDropDownOpen<T, TDataContext>(this T comboBox, Func<TDataContext, bool> getter, Action<TDataContext, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ComboBox where TDataContext : class =>
        comboBox.BindToDataContext(ComboBox.IsDropDownOpenProperty, getter, setter, updateSourceTrigger, getterExpression);

    #endregion
}

/// <summary>Fluent methods for <see cref="TreeView"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class TreeViewMarkup
{
    #region Properties

    /// <summary>
    /// Sets the collection of root items. Each item's children come from the children selector, and each item is shown
    /// through the item template.
    /// </summary>
    public static T ItemsSource<T>(this T treeView, IEnumerable? itemsSource) where T : TreeView => treeView.Set(TreeView.ItemsSourceProperty, itemsSource);

    /// <summary>
    /// Sets the <see cref="TreeView.ChildrenSelector"/>, which returns the children of an item of the
    /// <c>ItemsSource</c>, or <c>null</c> for a leaf.
    /// </summary>
    public static T WithChildrenSelector<T>(this T treeView, Func<object, IEnumerable?>? selector) where T : TreeView
    {
        treeView.ChildrenSelector = selector;
        return treeView;
    }

    /// <summary>
    /// Sets a children selector for items of type <typeparamref name="TItem"/>; other items have no children. Write out
    /// the lambda parameter's type: <c>.WithChildrenSelector((Folder f) =&gt; f.SubFolders)</c>.
    /// </summary>
    public static T WithChildrenSelector<T, TItem>(this T treeView, Func<TItem, IEnumerable?> selector) where T : TreeView
    {
        ArgumentNullException.ThrowIfNull(selector);
        treeView.ChildrenSelector = item => item is TItem typed ? selector(typed) : null;
        return treeView;
    }

    /// <summary>Sets the <see cref="TreeView.ItemTemplate"/>, which builds the header element of an item. Without one, items are shown as their text.</summary>
    public static T WithItemTemplate<T>(this T treeView, Func<object, UIElement>? template) where T : TreeView
    {
        treeView.ItemTemplate = template;
        return treeView;
    }

    /// <summary>
    /// Sets an item template for items of type <typeparamref name="TItem"/>; other items are shown as their text. Write
    /// out the lambda parameter's type: <c>.WithItemTemplate((Folder f) =&gt; new TextBlock(f.Name))</c>.
    /// </summary>
    public static T WithItemTemplate<T, TItem>(this T treeView, Func<TItem, UIElement> template) where T : TreeView
    {
        treeView.ItemTemplate = ItemsMarkupHelpers.TypedTemplate(template);
        return treeView;
    }

    /// <summary>Adds <paramref name="items"/> as root items, for trees built from <see cref="TreeViewItem"/>s instead of an <c>ItemsSource</c>.</summary>
    public static T RootItems<T>(this T treeView, params TreeViewItem[] items) where T : TreeView
    {
        foreach (var item in items)
        {
            treeView.AddRootItem(item);
        }
        return treeView;
    }

    /// <summary>Selects the node that shows <paramref name="item"/>; <c>null</c> clears the selection.</summary>
    public static T SelectedItem<T>(this T treeView, object? item) where T : TreeView => treeView.Set(TreeView.SelectedItemProperty, item);

    /// <summary>Sets how far each level is indented, in pixels.</summary>
    public static T IndentSize<T>(this T treeView, float indentSize) where T : TreeView => treeView.Set(TreeView.IndentSizeProperty, indentSize);

    /// <summary>Sets the expander icon of expanded nodes (default: a downward chevron), shown while their children are visible.</summary>
    public static T ExpandIcon<T>(this T treeView, MaterialIconKind icon) where T : TreeView => treeView.Set(TreeView.ExpandIconProperty, icon);

    /// <summary>Sets the expander icon of collapsed nodes (default: a right chevron), clicked to show their children.</summary>
    public static T CollapseIcon<T>(this T treeView, MaterialIconKind icon) where T : TreeView => treeView.Set(TreeView.CollapseIconProperty, icon);

    /// <summary>Sets the size of the expand and collapse icons in pixels.</summary>
    public static T IconSize<T>(this T treeView, float iconSize) where T : TreeView => treeView.Set(TreeView.IconSizeProperty, iconSize);

    #endregion

    #region Events

    /// <summary>Handles <see cref="TreeView.SelectionChanged"/>, raised with the newly selected item (or <c>null</c>).</summary>
    public static T OnSelectionChanged<T>(this T treeView, EventHandler<object?> handler) where T : TreeView
    {
        treeView.SelectionChanged += handler;
        return treeView;
    }

    /// <summary>Runs <paramref name="action"/> with the newly selected item (or <c>null</c>) whenever the selection changes.</summary>
    public static T OnSelectionChanged<T>(this T treeView, Action<object?> action) where T : TreeView => treeView.OnSelectionChanged(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="TreeView.ItemExpanded"/>, raised with the node that was expanded.</summary>
    public static T OnItemExpanded<T>(this T treeView, EventHandler<TreeViewItem> handler) where T : TreeView
    {
        treeView.ItemExpanded += handler;
        return treeView;
    }

    /// <summary>Runs <paramref name="action"/> with the node that was expanded (<see cref="TreeView.ItemExpanded"/>).</summary>
    public static T OnItemExpanded<T>(this T treeView, Action<TreeViewItem> action) where T : TreeView => treeView.OnItemExpanded(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="TreeView.ItemCollapsed"/>, raised with the node that was collapsed.</summary>
    public static T OnItemCollapsed<T>(this T treeView, EventHandler<TreeViewItem> handler) where T : TreeView
    {
        treeView.ItemCollapsed += handler;
        return treeView;
    }

    /// <summary>Runs <paramref name="action"/> with the node that was collapsed (<see cref="TreeView.ItemCollapsed"/>).</summary>
    public static T OnItemCollapsed<T>(this T treeView, Action<TreeViewItem> action) where T : TreeView => treeView.OnItemCollapsed(MarkupExtensions.ToHandler(action));

    #endregion

    #region Bindings

    /// <summary>Binds the root items to a collection of <paramref name="source"/>.</summary>
    public static T BindItemsSource<T, TSource>(this T treeView, TSource source, Func<TSource, IEnumerable?> getter, Action<TSource, IEnumerable?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TreeView where TSource : class =>
        treeView.BindToSource(TreeView.ItemsSourceProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the root items to a collection of the DataContext.</summary>
    public static T BindItemsSource<T, TDataContext>(this T treeView, Func<TDataContext, IEnumerable?> getter, Action<TDataContext, IEnumerable?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TreeView where TDataContext : class =>
        treeView.BindToDataContext(TreeView.ItemsSourceProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>
    /// Binds the selected item to a value of <paramref name="source"/>. With a <paramref name="setter"/>, the user's
    /// selection is written back; a selected item that isn't a <typeparamref name="TItem"/> is written as <c>default</c>.
    /// </summary>
    public static T BindSelectedItem<T, TSource, TItem>(this T treeView, TSource source, Func<TSource, TItem> getter, Action<TSource, TItem>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TreeView where TSource : class =>
        treeView.BindToSource(TreeView.SelectedItemProperty, source, s => (object?)getter(s), ItemsMarkupHelpers.ToObjectSetter(setter), updateSourceTrigger, getterExpression);

    /// <summary>Binds the selected item to a value of the DataContext. With a <paramref name="setter"/>, the user's selection is written back.</summary>
    public static T BindSelectedItem<T, TDataContext, TItem>(this T treeView, Func<TDataContext, TItem> getter, Action<TDataContext, TItem>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TreeView where TDataContext : class =>
        treeView.BindToDataContext(TreeView.SelectedItemProperty, (TDataContext s) => (object?)getter(s), ItemsMarkupHelpers.ToObjectSetter(setter), updateSourceTrigger, getterExpression);

    #endregion
}

/// <summary>Fluent methods for <see cref="TreeViewItem"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class TreeViewItemMarkup
{
    /// <summary>Expands the node, showing its children.</summary>
    public static T IsExpanded<T>(this T item, bool isExpanded = true) where T : TreeViewItem => item.Set(TreeViewItem.IsExpandedProperty, isExpanded);

    /// <summary>Sets whether the node is drawn as selected.</summary>
    public static T IsSelected<T>(this T item, bool isSelected = true) where T : TreeViewItem => item.Set(TreeViewItem.IsSelectedProperty, isSelected);

    /// <summary>Adds <paramref name="children"/> as child nodes, after any it already has.</summary>
    public static T ChildrenItems<T>(this T item, params TreeViewItem[] children) where T : TreeViewItem
    {
        foreach (var child in children)
        {
            item.AddChildItem(child);
        }
        return item;
    }

    /// <summary>Handles <see cref="TreeViewItem.Expanded"/>, raised when the node is expanded.</summary>
    public static T OnExpanded<T>(this T item, EventHandler handler) where T : TreeViewItem
    {
        item.Expanded += handler;
        return item;
    }

    /// <summary>Runs <paramref name="action"/> when the node is expanded (<see cref="TreeViewItem.Expanded"/>).</summary>
    public static T OnExpanded<T>(this T item, Action action) where T : TreeViewItem => item.OnExpanded(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="TreeViewItem.Collapsed"/>, raised when the node is collapsed.</summary>
    public static T OnCollapsed<T>(this T item, EventHandler handler) where T : TreeViewItem
    {
        item.Collapsed += handler;
        return item;
    }

    /// <summary>Runs <paramref name="action"/> when the node is collapsed (<see cref="TreeViewItem.Collapsed"/>).</summary>
    public static T OnCollapsed<T>(this T item, Action action) where T : TreeViewItem => item.OnCollapsed(MarkupExtensions.ToHandler(action));

    /// <summary>Binds whether the node is expanded to a value of <paramref name="source"/>. With a <paramref name="setter"/>, expanding and collapsing are written back.</summary>
    public static T BindIsExpanded<T, TSource>(this T item, TSource source, Func<TSource, bool> getter, Action<TSource, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TreeViewItem where TSource : class =>
        item.BindToSource(TreeViewItem.IsExpandedProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds whether the node is expanded to a value of the DataContext. With a <paramref name="setter"/>, expanding and collapsing are written back.</summary>
    public static T BindIsExpanded<T, TDataContext>(this T item, Func<TDataContext, bool> getter, Action<TDataContext, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TreeViewItem where TDataContext : class =>
        item.BindToDataContext(TreeViewItem.IsExpandedProperty, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>Shared helpers of the item control markup.</summary>
internal static class ItemsMarkupHelpers
{
    /// <summary>Wraps a typed item template: items of another type are shown as their text.</summary>
    public static Func<object, UIElement> TypedTemplate<TItem>(Func<TItem, UIElement> template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return item => item is TItem typed ? template(typed) : new TextBlock(item?.ToString() ?? string.Empty);
    }

    /// <summary>Adapts a typed selection setter to the <see cref="object"/> selection property; other values are written as <c>default</c>.</summary>
    public static Action<TSource, object?>? ToObjectSetter<TSource, TItem>(Action<TSource, TItem>? setter) =>
        setter == null ? null : (source, value) => setter(source, value is TItem item ? item : default!);
}
