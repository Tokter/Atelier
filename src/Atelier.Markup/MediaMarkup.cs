using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Core.Primitives;
using SkiaSharp;

namespace Atelier.Markup;

/// <summary>
/// Fluent methods for <see cref="Icon"/>: Material Symbols glyphs and custom vector paths. The icon color is the
/// control <c>Foreground</c>. See <see cref="MarkupExtensions"/> for the conventions.
/// </summary>
public static class IconMarkup
{
    #region Glyph and geometry

    /// <summary>Sets the Material Symbols glyph to show.</summary>
    public static T Kind<T>(this T icon, MaterialIconKind kind) where T : Icon => icon.Set(Icon.KindProperty, kind);

    /// <summary>Shows custom vector geometry instead of a glyph. The icon doesn't dispose <paramref name="path"/>.</summary>
    public static T Data<T>(this T icon, SKPath? path) where T : Icon => icon.Set(Icon.DataProperty, path);

    /// <summary>Shows custom vector geometry given as SVG path data, e.g. <c>"M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"</c>. Invalid data shows nothing.</summary>
    public static T PathData<T>(this T icon, string? svgPathData) where T : Icon => icon.Set(Icon.PathDataProperty, svgPathData);

    /// <summary>
    /// Sets the icon as text: a <see cref="MaterialIconKind"/> name (<c>"DarkMode"</c>), SVG path data or a whole SVG
    /// document (see <see cref="IconSource"/>).
    /// </summary>
    public static T Source<T>(this T icon, string? source) where T : Icon => icon.Set(Icon.SourceProperty, source);

    /// <summary>Sets the area of the geometry's coordinates the icon shows, like an SVG <c>viewBox</c>; <c>null</c> fits the geometry's bounds.</summary>
    public static T ViewBox<T>(this T icon, Rect? viewBox) where T : Icon => icon.Set(Icon.ViewBoxProperty, viewBox);

    /// <summary>Strokes custom geometry with lines of <paramref name="strokeWidth"/> pixels instead of filling it. 0 (the default) fills it.</summary>
    public static T StrokeWidth<T>(this T icon, float strokeWidth) where T : Icon => icon.Set(Icon.StrokeWidthProperty, strokeWidth);

    /// <summary>Sets the width and height of the icon in pixels. The default is 24.</summary>
    public static T Size<T>(this T icon, float size) where T : Icon => icon.Set(Icon.SizeProperty, size);

    #endregion

    #region Variable font axes

    /// <summary>Sets the fill axis of the glyph, from 0 (outlined, the default) to 1 (filled). Values in between animate smoothly.</summary>
    public static T Fill<T>(this T icon, float fill) where T : Icon => icon.Set(Icon.FillProperty, fill);

    /// <summary>Shows the filled version of the glyph, or the outlined one with <c>false</c>.</summary>
    public static T IsFilled<T>(this T icon, bool isFilled = true) where T : Icon
    {
        icon.IsFilled = isFilled;
        return icon;
    }

    /// <summary>Sets the stroke weight axis of the glyph, from 100 (thin) to 700 (bold). The default is 400.</summary>
    public static T Weight<T>(this T icon, float weight) where T : Icon => icon.Set(Icon.WeightProperty, weight);

    /// <summary>Sets the grade axis, which fine-tunes the stroke thickness without changing the size: -25 (lighter) to 200 (heavier). The default is 0.</summary>
    public static T Grade<T>(this T icon, float grade) where T : Icon => icon.Set(Icon.GradeProperty, grade);

    /// <summary>Sets the optical size axis, from 20 to 48, which adjusts detail for the display size. By default it follows the icon size.</summary>
    public static T OpticalSize<T>(this T icon, float opticalSize) where T : Icon => icon.Set(Icon.OpticalSizeProperty, opticalSize);

    #endregion

    #region Creation

    /// <summary>Creates an <see cref="Icon"/> showing this glyph: <c>MaterialIconKind.Home.ToIcon(20)</c>.</summary>
    /// <param name="kind">The glyph.</param>
    /// <param name="size">The width and height in pixels.</param>
    /// <param name="isFilled">Whether to show the filled version.</param>
    /// <param name="foreground">The color, or <c>null</c> to use the inherited or theme color.</param>
    public static Icon ToIcon(this MaterialIconKind kind, float size = 24f, bool isFilled = false, Color? foreground = null) =>
        new(kind, size, isFilled, foreground);

    /// <summary>Creates an <see cref="Icon"/> showing this vector path. The icon doesn't dispose the path.</summary>
    /// <param name="path">The geometry.</param>
    /// <param name="size">The width and height in pixels.</param>
    /// <param name="foreground">The color, or <c>null</c> to use the inherited or theme color.</param>
    public static Icon ToIcon(this SKPath path, float size = 24f, Color? foreground = null) => new(path, size, foreground);

    #endregion

    #region Bindings

    /// <summary>Binds the glyph to a value of <paramref name="source"/>.</summary>
    public static T BindKind<T, TSource>(this T icon, TSource source, Func<TSource, MaterialIconKind> getter, Action<TSource, MaterialIconKind>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TSource : class =>
        icon.BindToSource(Icon.KindProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the glyph to a value of the DataContext.</summary>
    public static T BindKind<T, TDataContext>(this T icon, Func<TDataContext, MaterialIconKind> getter, Action<TDataContext, MaterialIconKind>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TDataContext : class =>
        icon.BindToDataContext(Icon.KindProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the icon name or SVG (see <see cref="Icon.Source"/>) to a value of <paramref name="source"/>.</summary>
    public static T BindSource<T, TSource>(this T icon, TSource source, Func<TSource, string?> getter, Action<TSource, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TSource : class =>
        icon.BindToSource(Icon.SourceProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the SVG path data to a value of <paramref name="source"/>.</summary>
    public static T BindPathData<T, TSource>(this T icon, TSource source, Func<TSource, string?> getter, Action<TSource, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TSource : class =>
        icon.BindToSource(Icon.PathDataProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the SVG path data to a value of the DataContext.</summary>
    public static T BindPathData<T, TDataContext>(this T icon, Func<TDataContext, string?> getter, Action<TDataContext, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TDataContext : class =>
        icon.BindToDataContext(Icon.PathDataProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the stroke width to a value of <paramref name="source"/>.</summary>
    public static T BindStrokeWidth<T, TSource>(this T icon, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TSource : class =>
        icon.BindToSource(Icon.StrokeWidthProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the stroke width to a value of the DataContext.</summary>
    public static T BindStrokeWidth<T, TDataContext>(this T icon, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TDataContext : class =>
        icon.BindToDataContext(Icon.StrokeWidthProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the size to a value of <paramref name="source"/>.</summary>
    public static T BindSize<T, TSource>(this T icon, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TSource : class =>
        icon.BindToSource(Icon.SizeProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the size to a value of the DataContext.</summary>
    public static T BindSize<T, TDataContext>(this T icon, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TDataContext : class =>
        icon.BindToDataContext(Icon.SizeProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the fill axis to a value of <paramref name="source"/>.</summary>
    public static T BindFill<T, TSource>(this T icon, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TSource : class =>
        icon.BindToSource(Icon.FillProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the fill axis to a value of the DataContext.</summary>
    public static T BindFill<T, TDataContext>(this T icon, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TDataContext : class =>
        icon.BindToDataContext(Icon.FillProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the weight axis to a value of <paramref name="source"/>.</summary>
    public static T BindWeight<T, TSource>(this T icon, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TSource : class =>
        icon.BindToSource(Icon.WeightProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the weight axis to a value of the DataContext.</summary>
    public static T BindWeight<T, TDataContext>(this T icon, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TDataContext : class =>
        icon.BindToDataContext(Icon.WeightProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the grade axis to a value of <paramref name="source"/>.</summary>
    public static T BindGrade<T, TSource>(this T icon, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TSource : class =>
        icon.BindToSource(Icon.GradeProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the grade axis to a value of the DataContext.</summary>
    public static T BindGrade<T, TDataContext>(this T icon, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TDataContext : class =>
        icon.BindToDataContext(Icon.GradeProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the optical size axis to a value of <paramref name="source"/>.</summary>
    public static T BindOpticalSize<T, TSource>(this T icon, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TSource : class =>
        icon.BindToSource(Icon.OpticalSizeProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the optical size axis to a value of the DataContext.</summary>
    public static T BindOpticalSize<T, TDataContext>(this T icon, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Icon where TDataContext : class =>
        icon.BindToDataContext(Icon.OpticalSizeProperty, getter, setter, updateSourceTrigger, getterExpression);

    #endregion
}

/// <summary>Fluent methods for <see cref="Image"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ImageMarkup
{
    /// <summary>Sets the image to show. The control doesn't dispose it.</summary>
    public static T Source<T>(this T image, SKImage? source) where T : Image => image.Set(Image.SourceProperty, source);

    /// <summary>
    /// Loads and shows the image at <paramref name="pathOrResource"/>: a file path (absolute, or relative to the working
    /// or application directory) or the file name of an embedded resource. Nothing is shown if it can't be loaded. The
    /// control disposes the loaded image when its source changes.
    /// </summary>
    public static T Source<T>(this T image, string pathOrResource) where T : Image
    {
        image.LoadSource(pathOrResource);
        return image;
    }

    /// <summary>Sets how the image is scaled to its space: <see cref="Core.Primitives.Stretch.Uniform"/> (the default) fits it without distortion.</summary>
    public static T Stretch<T>(this T image, Stretch stretch) where T : Image => image.Set(Image.StretchProperty, stretch);

    /// <summary>Limits stretching to scaling up or down only, e.g. <see cref="Controls.StretchDirection.DownOnly"/> to never enlarge small images.</summary>
    public static T StretchDirection<T>(this T image, StretchDirection direction) where T : Image => image.Set(Image.StretchDirectionProperty, direction);

    /// <summary>Binds the image to a value of <paramref name="source"/>.</summary>
    public static T BindSource<T, TSource>(this T image, TSource source, Func<TSource, SKImage?> getter, Action<TSource, SKImage?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Image where TSource : class =>
        image.BindToSource(Image.SourceProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the image to a value of the DataContext.</summary>
    public static T BindSource<T, TDataContext>(this T image, Func<TDataContext, SKImage?> getter, Action<TDataContext, SKImage?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Image where TDataContext : class =>
        image.BindToDataContext(Image.SourceProperty, getter, setter, updateSourceTrigger, getterExpression);
}
