using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Markup;

/// <summary>Fluent methods for <see cref="TabControl"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class TabControlMarkup
{
    /// <summary>Adds tabs.</summary>
    public static T Tabs<T>(this T tabControl, params TabItem[] tabs) where T : TabControl
    {
        foreach (var tab in tabs)
        {
            tabControl.Items.Add(tab);
        }
        return tabControl;
    }

    /// <summary>Selects the tab at <paramref name="index"/>.</summary>
    public static T SelectedIndex<T>(this T tabControl, int index) where T : TabControl => tabControl.Set(TabControl.SelectedIndexProperty, index);

    /// <summary>Sets the look of the tabs: MD3 primary (the default) or secondary, or browser tabs.</summary>
    public static T TabStyle<T>(this T tabControl, TabStyle style) where T : TabControl => tabControl.Set(TabControl.TabStyleProperty, style);

    /// <summary>Gives the tabs close buttons (a tab's IsCloseable overrides it).</summary>
    public static T AreTabsCloseable<T>(this T tabControl, bool closeable = true) where T : TabControl => tabControl.Set(TabControl.AreTabsCloseableProperty, closeable);

    /// <summary>Shows a "+" button after the tabs; handle it with <see cref="OnAddTabRequested{T}(T, EventHandler{AddTabRequestedEventArgs})"/>.</summary>
    public static T ShowAddButton<T>(this T tabControl, bool show = true) where T : TabControl => tabControl.Set(TabControl.ShowAddButtonProperty, show);

    /// <summary>Sets whether tabs can be dragged to a new place (on by default).</summary>
    public static T CanReorderTabs<T>(this T tabControl, bool canReorder = true) where T : TabControl => tabControl.Set(TabControl.CanReorderTabsProperty, canReorder);

    /// <summary>Sets the factory of the header content for each data item, e.g. an icon and a title.</summary>
    public static T WithHeaderTemplate<T, TItem>(this T tabControl, Func<TItem, UIElement> template) where T : TabControl
    {
        tabControl.HeaderTemplate = ItemsMarkupHelpers.TypedTemplate(template);
        return tabControl;
    }

    /// <summary>Sets the factory of the page for each data item; without one the view locator finds a view.</summary>
    public static T WithContentTemplate<T, TItem>(this T tabControl, Func<TItem, UIElement> template) where T : TabControl
    {
        ArgumentNullException.ThrowIfNull(template);
        tabControl.ContentTemplate = item => item is TItem typed ? template(typed) : null;
        return tabControl;
    }

    /// <summary>Handles <see cref="TabControl.SelectionChanged"/>, raised with the new selected item.</summary>
    public static T OnSelectionChanged<T>(this T tabControl, EventHandler<object?> handler) where T : TabControl
    {
        tabControl.SelectionChanged += handler;
        return tabControl;
    }

    /// <summary>Handles <see cref="TabControl.TabClosing"/>, raised before a tab closes; cancel it to keep the tab.</summary>
    public static T OnTabClosing<T>(this T tabControl, EventHandler<TabClosingEventArgs> handler) where T : TabControl
    {
        tabControl.TabClosing += handler;
        return tabControl;
    }

    /// <summary>Runs <paramref name="action"/> with the item of each closed tab (<see cref="TabControl.TabClosed"/>).</summary>
    public static T OnTabClosed<T>(this T tabControl, Action<object> action) where T : TabControl
    {
        tabControl.TabClosed += MarkupExtensions.ToHandler(action);
        return tabControl;
    }

    /// <summary>Handles <see cref="TabControl.AddTabRequested"/>, raised by the "+" button; set the new item or add one to the bound collection.</summary>
    public static T OnAddTabRequested<T>(this T tabControl, EventHandler<AddTabRequestedEventArgs> handler) where T : TabControl
    {
        tabControl.AddTabRequested += handler;
        return tabControl;
    }

    /// <summary>Handles <see cref="TabControl.TabMoved"/>, raised when a dragged tab was dropped at a new place.</summary>
    public static T OnTabMoved<T>(this T tabControl, EventHandler<TabMovedEventArgs> handler) where T : TabControl
    {
        tabControl.TabMoved += handler;
        return tabControl;
    }

    /// <summary>Binds the selected index to <paramref name="source"/>. With a <paramref name="setter"/>, selecting a tab writes it back.</summary>
    public static T BindSelectedIndex<T, TSource>(this T tabControl, TSource source, Func<TSource, int> getter, Action<TSource, int>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TabControl where TSource : class =>
        tabControl.BindToSource(TabControl.SelectedIndexProperty, source, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>Fluent methods for <see cref="TabItem"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class TabItemMarkup
{
    /// <summary>Sets the header: a string or an element.</summary>
    public static T Header<T>(this T tab, object? header) where T : TabItem => tab.Set(TabItem.HeaderProperty, header);

    /// <summary>Sets an icon shown with the header.</summary>
    public static T Icon<T>(this T tab, MaterialIconKind icon) where T : TabItem => tab.Set(TabItem.IconProperty, icon);

    /// <summary>Sets the content shown while the tab is selected: an element or a view model.</summary>
    public static T Content<T>(this T tab, object? content) where T : TabItem => tab.Set(TabItem.ContentProperty, content);

    /// <summary>Gives the tab a close button (or removes it with <c>false</c>), overriding the tab control's setting.</summary>
    public static T IsCloseable<T>(this T tab, bool? closeable = true) where T : TabItem => tab.Set(TabItem.IsCloseableProperty, closeable);
}
