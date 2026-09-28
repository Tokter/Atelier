using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Markup;

/// <summary>Fluent methods for <see cref="ColorPicker"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ColorPickerMarkup
{
    /// <summary>Sets the picked color. The default is white.</summary>
    public static T Color<T>(this T picker, Color color) where T : ColorPicker => picker.Set(ColorPicker.ColorProperty, color);

    /// <summary>Sets the color model of the channel sliders: RGB (the default), HSL or HSB.</summary>
    public static T Mode<T>(this T picker, ColorPickerMode mode) where T : ColorPicker => picker.Set(ColorPicker.ModeProperty, mode);

    /// <summary>Shows or hides the alpha slider (shown by default).</summary>
    public static T IsAlphaEnabled<T>(this T picker, bool isEnabled = true) where T : ColorPicker => picker.Set(ColorPicker.IsAlphaEnabledProperty, isEnabled);

    /// <summary>Handles <see cref="ColorPicker.ColorChanged"/>, raised with the new color whenever the color changes.</summary>
    public static T OnColorChanged<T>(this T picker, EventHandler<Color> handler) where T : ColorPicker
    {
        picker.ColorChanged += handler;
        return picker;
    }

    /// <summary>Runs <paramref name="action"/> with the new color whenever the color changes (<see cref="ColorPicker.ColorChanged"/>).</summary>
    public static T OnColorChanged<T>(this T picker, Action<Color> action) where T : ColorPicker => picker.OnColorChanged(MarkupExtensions.ToHandler(action));

    /// <summary>Binds the color to <paramref name="source"/>. With a <paramref name="setter"/>, picking a color writes it back.</summary>
    public static T BindColor<T, TSource>(this T picker, TSource source, Func<TSource, Color> getter, Action<TSource, Color>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ColorPicker where TSource : class =>
        picker.BindToSource(ColorPicker.ColorProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the color to the DataContext. With a <paramref name="setter"/>, picking a color writes it back.</summary>
    public static T BindColor<T, TDataContext>(this T picker, Func<TDataContext, Color> getter, Action<TDataContext, Color>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ColorPicker where TDataContext : class =>
        picker.BindToDataContext(ColorPicker.ColorProperty, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>Fluent methods for <see cref="ColorWheel"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ColorWheelMarkup
{
    /// <summary>Sets the hue in degrees (0 red, 120 green, 240 blue).</summary>
    public static T Hue<T>(this T wheel, float hue) where T : ColorWheel => wheel.Set(ColorWheel.HueProperty, hue);

    /// <summary>Sets the saturation, from 0 (the center) to 1 (the edge).</summary>
    public static T Saturation<T>(this T wheel, float saturation) where T : ColorWheel => wheel.Set(ColorWheel.SaturationProperty, saturation);

    /// <summary>Sets the brightness the wheel is shown at, from 0 (black) to 1 (the default).</summary>
    public static T Brightness<T>(this T wheel, float brightness) where T : ColorWheel => wheel.Set(ColorWheel.BrightnessProperty, brightness);

    /// <summary>Handles <see cref="ColorWheel.ColorChanged"/>, raised when the hue, saturation or brightness changes.</summary>
    public static T OnColorChanged<T>(this T wheel, EventHandler handler) where T : ColorWheel
    {
        wheel.ColorChanged += handler;
        return wheel;
    }

    /// <summary>Runs <paramref name="action"/> with the wheel's color whenever it changes (<see cref="ColorWheel.ColorChanged"/>).</summary>
    public static T OnColorChanged<T>(this T wheel, Action<Color> action) where T : ColorWheel =>
        wheel.OnColorChanged((s, _) => action(((ColorWheel)s!).Color));
}

/// <summary>Fluent methods for <see cref="ColorSlider"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ColorSliderMarkup
{
    /// <summary>Sets the colors of the track's gradient, spaced evenly along it.</summary>
    public static T TrackColors<T>(this T slider, params Color[] colors) where T : ColorSlider
    {
        slider.TrackColors = colors;
        return slider;
    }

    /// <summary>Sets the color filling the thumb.</summary>
    public static T ThumbColor<T>(this T slider, Color color) where T : ColorSlider
    {
        slider.ThumbColor = color;
        return slider;
    }

    /// <summary>Shows a checkerboard through the track and thumb, for gradients with transparent colors.</summary>
    public static T ShowsTransparency<T>(this T slider, bool shows = true) where T : ColorSlider
    {
        slider.ShowsTransparency = shows;
        return slider;
    }
}

/// <summary>Fluent methods for <see cref="ColorSwatch"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ColorSwatchMarkup
{
    /// <summary>Sets the color shown.</summary>
    public static T Color<T>(this T swatch, Color color) where T : ColorSwatch => swatch.Set(ColorSwatch.ColorProperty, color);

    /// <summary>Binds the color shown to <paramref name="source"/>.</summary>
    public static T BindColor<T, TSource>(this T swatch, TSource source, Func<TSource, Color> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ColorSwatch where TSource : class =>
        swatch.BindToSource(ColorSwatch.ColorProperty, source, getter, null, UpdateSourceTrigger.PropertyChanged, getterExpression);
}
