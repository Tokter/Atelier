using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Core.ViewResolution;

namespace Atelier.Markup;

/// <summary>
/// Fluent methods for every <see cref="Control"/>: padding, corner radius, colors and font. See
/// <see cref="MarkupExtensions"/> for the conventions.
/// </summary>
/// <remarks>
/// The theme's default styles set these per control type. A value set here overrides the style for this control only.
/// </remarks>
public static class ControlMarkup
{
    /// <summary>Sets the same <see cref="Control.Padding"/> (space between the control's edge and its content) on all sides.</summary>
    public static T Padding<T>(this T control, float uniform) where T : Control => control.Set(Control.PaddingProperty, new Thickness(uniform));

    /// <summary>Sets the <see cref="Control.Padding"/>: <paramref name="horizontal"/> on the left and right, <paramref name="vertical"/> on the top and bottom.</summary>
    public static T Padding<T>(this T control, float horizontal, float vertical) where T : Control =>
        control.Set(Control.PaddingProperty, new Thickness(horizontal, vertical));

    /// <summary>Sets the <see cref="Control.Padding"/> of each side.</summary>
    public static T Padding<T>(this T control, float left, float top, float right, float bottom) where T : Control =>
        control.Set(Control.PaddingProperty, new Thickness(left, top, right, bottom));

    /// <summary>Sets the <see cref="Control.Padding"/>.</summary>
    public static T Padding<T>(this T control, Thickness padding) where T : Control => control.Set(Control.PaddingProperty, padding);

    /// <summary>Rounds all four corners of the control's container shape by <paramref name="uniform"/> pixels.</summary>
    public static T CornerRadius<T>(this T control, float uniform) where T : Control => control.Set(Control.CornerRadiusProperty, new CornerRadius(uniform));

    /// <summary>Rounds each corner of the control's container shape by its own radius, in pixels.</summary>
    public static T CornerRadius<T>(this T control, float topLeft, float topRight, float bottomRight, float bottomLeft) where T : Control =>
        control.Set(Control.CornerRadiusProperty, new CornerRadius(topLeft, topRight, bottomRight, bottomLeft));

    /// <summary>Sets the corner radii of the control's container shape.</summary>
    public static T CornerRadius<T>(this T control, CornerRadius cornerRadius) where T : Control => control.Set(Control.CornerRadiusProperty, cornerRadius);

    /// <summary>Sets the <see cref="Control.Background"/>, overriding the color the theme uses for the control's container.</summary>
    public static T Background<T>(this T control, Color background) where T : Control => control.Set(Control.BackgroundProperty, background);

    /// <summary>
    /// Sets the <see cref="Control.Foreground"/>: the color of the control's text and icons. Descendants inherit it.
    /// When it isn't set, the theme picks the color that fits the control's container.
    /// </summary>
    public static T Foreground<T>(this T control, Color foreground) where T : Control => control.Set(Control.ForegroundProperty, foreground);

    /// <summary>Sets the font size in pixels for the control's text. Descendants inherit it.</summary>
    public static T FontSize<T>(this T control, float fontSize) where T : Control => control.Set(Control.FontSizeProperty, fontSize);

    /// <summary>Sets the font family for the control's text, e.g. <c>"Segoe UI"</c>. <c>null</c> uses the default font. Descendants inherit it.</summary>
    public static T FontFamily<T>(this T control, string? fontFamily) where T : Control => control.Set(Control.FontFamilyProperty, fontFamily);

    /// <summary>Sets the font weight for the control's text, e.g. <see cref="Core.Primitives.FontWeight.Medium"/>. Descendants inherit it.</summary>
    public static T FontWeight<T>(this T control, FontWeight fontWeight) where T : Control => control.Set(Control.FontWeightProperty, fontWeight);

    /// <summary>Binds the <see cref="Control.Background"/> to a value of <paramref name="source"/>.</summary>
    public static T BindBackground<T, TSource>(this T control, TSource source, Func<TSource, Color> getter, Action<TSource, Color>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Control where TSource : class =>
        control.BindToSource(Control.BackgroundProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the <see cref="Control.Background"/> to a value of the DataContext.</summary>
    public static T BindBackground<T, TDataContext>(this T control, Func<TDataContext, Color> getter, Action<TDataContext, Color>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Control where TDataContext : class =>
        control.BindToDataContext(Control.BackgroundProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the <see cref="Control.Foreground"/> to a value of <paramref name="source"/>.</summary>
    public static T BindForeground<T, TSource>(this T control, TSource source, Func<TSource, Color> getter, Action<TSource, Color>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Control where TSource : class =>
        control.BindToSource(Control.ForegroundProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the <see cref="Control.Foreground"/> to a value of the DataContext.</summary>
    public static T BindForeground<T, TDataContext>(this T control, Func<TDataContext, Color> getter, Action<TDataContext, Color>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Control where TDataContext : class =>
        control.BindToDataContext(Control.ForegroundProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the font size to a value of <paramref name="source"/>.</summary>
    public static T BindFontSize<T, TSource>(this T control, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Control where TSource : class =>
        control.BindToSource(Control.FontSizeProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the font size to a value of the DataContext.</summary>
    public static T BindFontSize<T, TDataContext>(this T control, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Control where TDataContext : class =>
        control.BindToDataContext(Control.FontSizeProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the font weight to a value of <paramref name="source"/>.</summary>
    public static T BindFontWeight<T, TSource>(this T control, TSource source, Func<TSource, FontWeight> getter, Action<TSource, FontWeight>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Control where TSource : class =>
        control.BindToSource(Control.FontWeightProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the font weight to a value of the DataContext.</summary>
    public static T BindFontWeight<T, TDataContext>(this T control, Func<TDataContext, FontWeight> getter, Action<TDataContext, FontWeight>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Control where TDataContext : class =>
        control.BindToDataContext(Control.FontWeightProperty, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>
/// Fluent methods for <see cref="ContentControl"/> and derived controls (buttons, check boxes, scroll viewers, ...). See
/// <see cref="MarkupExtensions"/> for the conventions.
/// </summary>
public static class ContentControlMarkup
{
    /// <summary>
    /// Sets the <see cref="ContentControl.Content"/>. An element is shown as is; a string is shown as text; any other
    /// object is shown through the content template or view locator, or as its <see cref="object.ToString"/> text.
    /// </summary>
    public static T Content<T>(this T control, object? content) where T : ContentControl => control.Set(ContentControl.ContentProperty, content);

    /// <summary>
    /// Sets the <see cref="ContentControl.ContentTemplate"/>, which builds the element that shows a non-element content
    /// (such as a view model). Returning <c>null</c> falls back to the default presentation.
    /// </summary>
    public static T WithContentTemplate<T>(this T control, Func<object?, UIElement?>? template) where T : ContentControl =>
        control.Set(ContentControl.ContentTemplateProperty, template);

    /// <summary>Sets a content template for contents of type <typeparamref name="TContent"/>. Other contents use the default presentation.</summary>
    public static T WithContentTemplate<T, TContent>(this T control, Func<TContent, UIElement?> template) where T : ContentControl
    {
        ArgumentNullException.ThrowIfNull(template);
        return control.Set(ContentControl.ContentTemplateProperty, content => content is TContent typed ? template(typed) : null);
    }

    /// <summary>Sets the <see cref="IViewLocator"/> that finds the view for a view model content when there is no content template.</summary>
    public static T ViewLocator<T>(this T control, IViewLocator? locator) where T : ContentControl => control.Set(ContentControl.ViewLocatorProperty, locator);

    /// <summary>Sets how the content is positioned horizontally inside the control.</summary>
    public static T HorizontalContentAlignment<T>(this T control, HorizontalAlignment alignment) where T : ContentControl =>
        control.Set(ContentControl.HorizontalContentAlignmentProperty, alignment);

    /// <summary>Sets how the content is positioned vertically inside the control.</summary>
    public static T VerticalContentAlignment<T>(this T control, VerticalAlignment alignment) where T : ContentControl =>
        control.Set(ContentControl.VerticalContentAlignmentProperty, alignment);

    /// <summary>Binds the content to a value of <paramref name="source"/>, such as the current page's view model.</summary>
    public static T BindContent<T, TSource>(this T control, TSource source, Func<TSource, object?> getter, Action<TSource, object?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ContentControl where TSource : class =>
        control.BindToSource(ContentControl.ContentProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the content to a value of the DataContext.</summary>
    public static T BindContent<T, TDataContext>(this T control, Func<TDataContext, object?> getter, Action<TDataContext, object?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ContentControl where TDataContext : class =>
        control.BindToDataContext(ContentControl.ContentProperty, getter, setter, updateSourceTrigger, getterExpression);
}
