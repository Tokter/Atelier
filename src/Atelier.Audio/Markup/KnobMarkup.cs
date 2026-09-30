using System;
using System.Runtime.CompilerServices;
using Atelier.Core.Properties;
using Atelier.Markup;

namespace Atelier.Audio;

/// <summary>Fluent methods for <see cref="Knob"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class KnobMarkup
{
    /// <summary>Sets the smallest value, at the bottom left of the arc. The default is 0.</summary>
    public static T Minimum<T>(this T knob, float minimum) where T : Knob => knob.Set(Knob.MinimumProperty, minimum);

    /// <summary>Sets the largest value, at the bottom right of the arc. The default is 100.</summary>
    public static T Maximum<T>(this T knob, float maximum) where T : Knob => knob.Set(Knob.MaximumProperty, maximum);

    /// <summary>Sets the range of values: <see cref="Minimum"/> and <see cref="Maximum"/>.</summary>
    public static T Range<T>(this T knob, float minimum, float maximum) where T : Knob => knob.Minimum(minimum).Maximum(maximum);

    /// <summary>Sets the current value. It is kept within the range.</summary>
    public static T Value<T>(this T knob, float value) where T : Knob => knob.Set(Knob.ValueProperty, value);

    /// <summary>Sets the step of the arrow keys and the wheel. The default is 1.</summary>
    public static T SmallChange<T>(this T knob, float smallChange) where T : Knob => knob.Set(Knob.SmallChangeProperty, smallChange);

    /// <summary>Sets the step of Page Up and Page Down. The default is 10.</summary>
    public static T LargeChange<T>(this T knob, float largeChange) where T : Knob => knob.Set(Knob.LargeChangeProperty, largeChange);

    /// <summary>Sets the value a double click resets the knob to.</summary>
    public static T DefaultValue<T>(this T knob, float defaultValue) where T : Knob => knob.Set(Knob.DefaultValueProperty, defaultValue);

    /// <summary>Sets how many pixels of dragging cover the whole range (ten times as many with Shift held). The default is 200.</summary>
    public static T DragDistance<T>(this T knob, float dragDistance) where T : Knob => knob.Set(Knob.DragDistanceProperty, dragDistance);

    /// <summary>Sets the composite format of the value text, with the value as argument 0, e.g. <c>"{0:0.0} dB"</c>. The default is <c>"{0:0}"</c>.</summary>
    public static T ValueFormat<T>(this T knob, string valueFormat) where T : Knob => knob.Set(Knob.ValueFormatProperty, valueFormat);

    /// <summary>Handles <see cref="Knob.ValueChanged"/>, raised with the new value whenever the value changes.</summary>
    public static T OnValueChanged<T>(this T knob, EventHandler<float> handler) where T : Knob
    {
        knob.ValueChanged += handler;
        return knob;
    }

    /// <summary>Runs <paramref name="action"/> with the new value whenever the value changes (<see cref="Knob.ValueChanged"/>).</summary>
    public static T OnValueChanged<T>(this T knob, Action<float> action) where T : Knob => knob.OnValueChanged(MarkupExtensions.ToHandler(action));

    /// <summary>Binds the value to <paramref name="source"/>. With a <paramref name="setter"/>, turning the knob writes the value back.</summary>
    public static T BindValue<T, TSource>(this T knob, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Knob where TSource : class =>
        knob.BindToSource(Knob.ValueProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the value to the DataContext. With a <paramref name="setter"/>, turning the knob writes the value back.</summary>
    public static T BindValue<T, TDataContext>(this T knob, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Knob where TDataContext : class =>
        knob.BindToDataContext(Knob.ValueProperty, getter, setter, updateSourceTrigger, getterExpression);
}
