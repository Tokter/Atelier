using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Markup;

/// <summary>
/// Fluent methods for giving any element a tooltip (see <see cref="ToolTipService"/>). See
/// <see cref="MarkupExtensions"/> for the conventions.
/// </summary>
public static class ToolTipMarkup
{
    /// <summary>
    /// Sets the element's tooltip. A string shows a plain tooltip; an element (such as a <see cref="RichToolTip"/>)
    /// shows a rich tooltip whose buttons and links can be clicked; <c>null</c> removes the tooltip.
    /// </summary>
    /// <example><c>new Button().Content(icon).ToolTip("Save (Ctrl+S)")</c></example>
    public static T ToolTip<T>(this T element, object? toolTip) where T : UIElement => element.Set(ToolTipService.ToolTipProperty, toolTip);

    /// <summary>
    /// Sets where the tooltip appears: below the element (<see cref="PlacementMode.Bottom"/>, the default), on another
    /// side, or below the pointer (<see cref="PlacementMode.Pointer"/>).
    /// </summary>
    public static T ToolTipPlacement<T>(this T element, PlacementMode placement) where T : UIElement =>
        element.Set(ToolTipService.PlacementProperty, placement);

    /// <summary>Sets how long the pointer must rest on the element before its tooltip opens. <c>null</c> uses the global delay (500 ms).</summary>
    public static T ToolTipShowDelay<T>(this T element, TimeSpan? delay) where T : UIElement => element.Set(ToolTipService.ShowDelayProperty, delay);

    /// <summary>Turns the element's tooltip on (the default) or off without removing it.</summary>
    public static T ToolTipIsEnabled<T>(this T element, bool isEnabled = true) where T : UIElement =>
        element.Set(ToolTipService.IsEnabledProperty, isEnabled);

    /// <summary>Shows the tooltip while the element is disabled too, for example to explain why it is disabled.</summary>
    public static T ToolTipShowOnDisabled<T>(this T element, bool show = true) where T : UIElement =>
        element.Set(ToolTipService.ShowOnDisabledProperty, show);

    /// <summary>
    /// Sets whether the tooltip receives pointer input and stays open while the pointer is over it. <c>null</c> (the
    /// default) makes rich tooltips interactive and plain ones not.
    /// </summary>
    public static T ToolTipIsInteractive<T>(this T element, bool? isInteractive = true) where T : UIElement =>
        element.Set(ToolTipService.IsInteractiveProperty, isInteractive);

    /// <summary>Binds the tooltip to a value of <paramref name="source"/>, e.g. a status text. An open tooltip updates in place.</summary>
    public static T BindToolTip<T, TSource>(this T element, TSource source, Func<TSource, object?> getter, Action<TSource, object?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : UIElement where TSource : class =>
        element.BindToSource(ToolTipService.ToolTipProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the tooltip to a value of the DataContext.</summary>
    public static T BindToolTip<T, TDataContext>(this T element, Func<TDataContext, object?> getter, Action<TDataContext, object?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : UIElement where TDataContext : class =>
        element.BindToDataContext(ToolTipService.ToolTipProperty, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>
/// Fluent methods for the <see cref="Controls.ToolTip"/> popup, when used directly. See <see cref="MarkupExtensions"/>
/// for the conventions.
/// </summary>
public static class ToolTipPopupMarkup
{
    /// <summary>Sets what the tooltip shows: a string (plain tooltip), an element or another object (rich tooltip).</summary>
    public static T Content<T>(this T toolTip, object? content) where T : Controls.ToolTip => toolTip.Set(Controls.ToolTip.ContentProperty, content);

    /// <summary>Sets whether the tooltip receives pointer input, so its buttons work and it stays open under the pointer.</summary>
    public static T IsInteractive<T>(this T toolTip, bool isInteractive = true) where T : Controls.ToolTip =>
        toolTip.Set(Controls.ToolTip.IsInteractiveProperty, isInteractive);

    /// <summary>Sets the space between the tooltip and the element (or pointer) it points at. The default is 4.</summary>
    public static T Gap<T>(this T toolTip, float gap) where T : Controls.ToolTip => toolTip.Set(Controls.ToolTip.GapProperty, gap);
}

/// <summary>Fluent methods for <see cref="RichToolTip"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class RichToolTipMarkup
{
    /// <summary>Sets the heading, shown in medium weight above the text. <c>null</c> or empty hides it.</summary>
    public static T Title<T>(this T toolTip, string? title) where T : RichToolTip => toolTip.Set(RichToolTip.TitleProperty, title);

    /// <summary>Sets the supporting text. <c>null</c> or empty hides it.</summary>
    public static T Text<T>(this T toolTip, string? text) where T : RichToolTip => toolTip.Set(RichToolTip.TextProperty, text);

    /// <summary>Adds actions to the row at the bottom, usually text buttons: <c>.Actions(new Button("Retry").Variant(ButtonVariant.Text))</c>.</summary>
    public static T Actions<T>(this T toolTip, params UIElement[] actions) where T : RichToolTip
    {
        foreach (var action in actions)
        {
            toolTip.Actions.Add(action);
        }
        return toolTip;
    }

    /// <summary>Binds the heading to a value of <paramref name="source"/>.</summary>
    public static T BindTitle<T, TSource>(this T toolTip, TSource source, Func<TSource, string?> getter, Action<TSource, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : RichToolTip where TSource : class =>
        toolTip.BindToSource(RichToolTip.TitleProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the heading to a value of the DataContext (a tooltip gets its element's DataContext).</summary>
    public static T BindTitle<T, TDataContext>(this T toolTip, Func<TDataContext, string?> getter, Action<TDataContext, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : RichToolTip where TDataContext : class =>
        toolTip.BindToDataContext(RichToolTip.TitleProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the supporting text to a value of the DataContext (a tooltip gets its element's DataContext).</summary>
    public static T BindText<T, TDataContext>(this T toolTip, Func<TDataContext, string?> getter, Action<TDataContext, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : RichToolTip where TDataContext : class =>
        toolTip.BindToDataContext(RichToolTip.TextProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the supporting text to a value of <paramref name="source"/>.</summary>
    public static T BindText<T, TSource>(this T toolTip, TSource source, Func<TSource, string?> getter, Action<TSource, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : RichToolTip where TSource : class =>
        toolTip.BindToSource(RichToolTip.TextProperty, source, getter, setter, updateSourceTrigger, getterExpression);
}
