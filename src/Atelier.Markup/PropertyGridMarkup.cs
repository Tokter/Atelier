using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Markup;

/// <summary>Fluent methods for <see cref="PropertyGrid"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class PropertyGridMarkup
{
    #region Properties

    /// <summary>
    /// Sets the object whose properties are listed and edited. Objects implementing <see cref="Core.Inspection.IInspectableObject"/>
    /// describe their own properties.
    /// </summary>
    public static T SelectedObject<T>(this T grid, object? selectedObject) where T : PropertyGrid => grid.Set(PropertyGrid.SelectedObjectProperty, selectedObject);

    /// <summary>Sets whether properties are grouped by category (the default) or listed alphabetically.</summary>
    public static T SortMode<T>(this T grid, PropertySortMode sortMode) where T : PropertyGrid => grid.Set(PropertyGrid.SortModeProperty, sortMode);

    /// <summary>Filters the rows to properties whose display name, name or category matches <paramref name="filterText"/> (case-insensitive).</summary>
    public static T FilterText<T>(this T grid, string? filterText) where T : PropertyGrid => grid.Set(PropertyGrid.FilterTextProperty, filterText ?? string.Empty);

    /// <summary>Shows or hides the toolbar with the sort buttons and the filter box (shown by default).</summary>
    public static T IsToolbarVisible<T>(this T grid, bool isVisible = true) where T : PropertyGrid => grid.Set(PropertyGrid.IsToolbarVisibleProperty, isVisible);

    /// <summary>Sets the shadow depth of the toolbar. The default is 2.</summary>
    public static T ToolbarElevation<T>(this T grid, float elevation) where T : PropertyGrid => grid.Set(PropertyGrid.ToolbarElevationProperty, elevation);

    /// <summary>Sets the width of the label column in pixels. The default is 160.</summary>
    public static T LabelWidth<T>(this T grid, float labelWidth) where T : PropertyGrid => grid.Set(PropertyGrid.LabelWidthProperty, labelWidth);

    /// <summary>
    /// Shows or hides the panel below the rows that describes the current property (shown by default, when the
    /// properties have descriptions).
    /// </summary>
    public static T IsDescriptionVisible<T>(this T grid, bool isVisible = true) where T : PropertyGrid => grid.Set(PropertyGrid.IsDescriptionVisibleProperty, isVisible);

    #endregion

    #region Editors

    /// <summary>Uses editors created by <paramref name="factory"/> for properties of type <typeparamref name="TValue"/> in this grid.</summary>
    public static T RegisterCustomEditor<T, TValue>(this T grid, Func<PropertyEditorContext, UIElement> factory) where T : PropertyGrid
    {
        grid.RegisterEditor<TValue>(factory);
        return grid;
    }

    /// <summary>Uses editors created by <paramref name="factory"/> for properties of type <paramref name="type"/> in this grid.</summary>
    public static T RegisterCustomEditor<T>(this T grid, Type type, Func<PropertyEditorContext, UIElement> factory) where T : PropertyGrid
    {
        grid.RegisterEditor(type, factory);
        return grid;
    }

    /// <summary>Uses editors created by <paramref name="factory"/> for the properties <paramref name="predicate"/> accepts, e.g. by name or attribute.</summary>
    public static T RegisterCustomEditor<T>(this T grid, Func<PropertyEditorContext, bool> predicate, Func<PropertyEditorContext, UIElement> factory)
        where T : PropertyGrid
    {
        grid.RegisterEditor(predicate, factory);
        return grid;
    }

    #endregion

    #region Events

    /// <summary>Handles <see cref="PropertyGrid.PropertyValueChanged"/>, raised after the user changed a property.</summary>
    public static T OnPropertyValueChanged<T>(this T grid, EventHandler<PropertyValueChangedEventArgs> handler) where T : PropertyGrid
    {
        grid.PropertyValueChanged += handler;
        return grid;
    }

    /// <summary>Handles <see cref="PropertyGrid.PropertyValueChanging"/>, raised before a property changes; the change can be canceled.</summary>
    public static T OnPropertyValueChanging<T>(this T grid, EventHandler<PropertyValueChangingEventArgs> handler) where T : PropertyGrid
    {
        grid.PropertyValueChanging += handler;
        return grid;
    }

    /// <summary>Handles <see cref="PropertyGrid.PropertyValueError"/>, raised when a property getter or setter throws; the message is shown on the row.</summary>
    public static T OnPropertyValueError<T>(this T grid, EventHandler<PropertyValueErrorEventArgs> handler) where T : PropertyGrid
    {
        grid.PropertyValueError += handler;
        return grid;
    }

    /// <summary>Handles <see cref="PropertyGrid.SelectedObjectChanged"/>, raised with the new object when the inspected object changes.</summary>
    public static T OnSelectedObjectChanged<T>(this T grid, EventHandler<object?> handler) where T : PropertyGrid
    {
        grid.SelectedObjectChanged += handler;
        return grid;
    }

    /// <summary>Handles <see cref="PropertyGrid.SortModeChanged"/>, raised when the sort mode changes, e.g. from the toolbar.</summary>
    public static T OnSortModeChanged<T>(this T grid, EventHandler<PropertySortMode> handler) where T : PropertyGrid
    {
        grid.SortModeChanged += handler;
        return grid;
    }

    /// <summary>Handles <see cref="PropertyGrid.FilterTextChanged"/>, raised when the filter text changes, e.g. from the toolbar.</summary>
    public static T OnFilterTextChanged<T>(this T grid, EventHandler<string> handler) where T : PropertyGrid
    {
        grid.FilterTextChanged += handler;
        return grid;
    }

    #endregion

    #region Bindings

    /// <summary>Binds the inspected object to a value of <paramref name="source"/>, such as the current selection of an editor.</summary>
    public static T BindSelectedObject<T, TSource>(this T grid, TSource source, Func<TSource, object?> getter, Action<TSource, object?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : PropertyGrid where TSource : class =>
        grid.BindToSource(PropertyGrid.SelectedObjectProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the inspected object to a value of the DataContext.</summary>
    public static T BindSelectedObject<T, TDataContext>(this T grid, Func<TDataContext, object?> getter, Action<TDataContext, object?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : PropertyGrid where TDataContext : class =>
        grid.BindToDataContext(PropertyGrid.SelectedObjectProperty, getter, setter, updateSourceTrigger, getterExpression);

    #endregion
}
