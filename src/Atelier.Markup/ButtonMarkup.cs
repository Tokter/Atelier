using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Atelier.Controls;
using Atelier.Core.Properties;

namespace Atelier.Markup;

/// <summary>
/// Fluent methods for all buttons (<see cref="ButtonBase"/>): click handlers and commands. See
/// <see cref="MarkupExtensions"/> for the conventions.
/// </summary>
public static class ButtonBaseMarkup
{
    /// <summary>Handles <see cref="ButtonBase.Click"/>, raised when the button is clicked with the mouse or with Space or Enter.</summary>
    public static T OnClick<T>(this T button, EventHandler handler) where T : ButtonBase
    {
        button.Click += handler;
        return button;
    }

    /// <summary>Runs <paramref name="action"/> when the button is clicked (<see cref="ButtonBase.Click"/>).</summary>
    public static T OnClick<T>(this T button, Action action) where T : ButtonBase => button.OnClick(MarkupExtensions.ToHandler(action));

    /// <summary>
    /// Sets the <see cref="ICommand"/> executed when the button is clicked. The button is disabled while the command
    /// can't execute.
    /// </summary>
    public static T Command<T>(this T button, ICommand? command) where T : ButtonBase => button.Set(ButtonBase.CommandProperty, command);

    /// <summary>Sets the command executed on click and the parameter passed to it.</summary>
    public static T Command<T>(this T button, ICommand? command, object? parameter) where T : ButtonBase =>
        button.Command(command).CommandParameter(parameter);

    /// <summary>Sets the parameter passed to the <see cref="ButtonBase.Command"/>.</summary>
    public static T CommandParameter<T>(this T button, object? parameter) where T : ButtonBase => button.Set(ButtonBase.CommandParameterProperty, parameter);

    /// <summary>Sets when a click happens: on release (the default), on press, or on hover.</summary>
    public static T ClickMode<T>(this T button, ClickMode clickMode) where T : ButtonBase => button.Set(ButtonBase.ClickModeProperty, clickMode);
}

/// <summary>Fluent methods for <see cref="Button"/> and <see cref="RepeatButton"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ButtonMarkup
{
    /// <summary>Sets the Material button style: filled, tonal, elevated, outlined or text.</summary>
    public static T Variant<T>(this T button, ButtonVariant variant) where T : Button => button.Set(Button.VariantProperty, variant);

    /// <summary>Sets the resting shadow elevation of the filled and elevated variants; hover and press raise it. The default is 1.</summary>
    public static T Elevation<T>(this T button, float elevation) where T : Button => button.Set(Button.ElevationProperty, elevation);

    /// <summary>Binds the button style to a value of <paramref name="source"/>.</summary>
    public static T BindVariant<T, TSource>(this T button, TSource source, Func<TSource, ButtonVariant> getter, Action<TSource, ButtonVariant>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Button where TSource : class =>
        button.BindToSource(Button.VariantProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the button style to a value of the DataContext.</summary>
    public static T BindVariant<T, TDataContext>(this T button, Func<TDataContext, ButtonVariant> getter, Action<TDataContext, ButtonVariant>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Button where TDataContext : class =>
        button.BindToDataContext(Button.VariantProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Sets how long the <see cref="RepeatButton"/> waits after being pressed before it starts repeating clicks.</summary>
    public static T Delay<T>(this T button, TimeSpan delay) where T : RepeatButton => button.Set(RepeatButton.DelayProperty, delay);

    /// <summary>Sets the time between repeated clicks of the <see cref="RepeatButton"/> while it is held down.</summary>
    public static T Interval<T>(this T button, TimeSpan interval) where T : RepeatButton => button.Set(RepeatButton.IntervalProperty, interval);
}

/// <summary>
/// Fluent methods for <see cref="ToggleButton"/> and the controls based on it: <see cref="CheckBox"/>,
/// <see cref="RadioButton"/> and <see cref="Switch"/>. See <see cref="MarkupExtensions"/> for the conventions.
/// </summary>
public static class ToggleButtonMarkup
{
    /// <summary>Sets the checked state. <c>null</c> is the indeterminate state of a three-state control.</summary>
    public static T IsChecked<T>(this T toggle, bool? isChecked = true) where T : ToggleButton => toggle.Set(ToggleButton.IsCheckedProperty, isChecked);

    /// <summary>Lets clicks cycle through the indeterminate state as well: unchecked, checked, indeterminate.</summary>
    public static T IsThreeState<T>(this T toggle, bool isThreeState = true) where T : ToggleButton => toggle.Set(ToggleButton.IsThreeStateProperty, isThreeState);

    /// <summary>Handles <see cref="ToggleButton.Checked"/>, raised when the control becomes checked.</summary>
    public static T OnChecked<T>(this T toggle, EventHandler handler) where T : ToggleButton
    {
        toggle.Checked += handler;
        return toggle;
    }

    /// <summary>Runs <paramref name="action"/> when the control becomes checked (<see cref="ToggleButton.Checked"/>).</summary>
    public static T OnChecked<T>(this T toggle, Action action) where T : ToggleButton => toggle.OnChecked(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="ToggleButton.Unchecked"/>, raised when the control becomes unchecked.</summary>
    public static T OnUnchecked<T>(this T toggle, EventHandler handler) where T : ToggleButton
    {
        toggle.Unchecked += handler;
        return toggle;
    }

    /// <summary>Runs <paramref name="action"/> when the control becomes unchecked (<see cref="ToggleButton.Unchecked"/>).</summary>
    public static T OnUnchecked<T>(this T toggle, Action action) where T : ToggleButton => toggle.OnUnchecked(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="ToggleButton.Indeterminate"/>, raised when the control becomes indeterminate.</summary>
    public static T OnIndeterminate<T>(this T toggle, EventHandler handler) where T : ToggleButton
    {
        toggle.Indeterminate += handler;
        return toggle;
    }

    /// <summary>Runs <paramref name="action"/> when the control becomes indeterminate (<see cref="ToggleButton.Indeterminate"/>).</summary>
    public static T OnIndeterminate<T>(this T toggle, Action action) where T : ToggleButton => toggle.OnIndeterminate(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="ToggleButton.CheckedChanged"/>, raised with the new state whenever the checked state changes.</summary>
    public static T OnCheckedChanged<T>(this T toggle, EventHandler<bool?> handler) where T : ToggleButton
    {
        toggle.CheckedChanged += handler;
        return toggle;
    }

    /// <summary>Runs <paramref name="action"/> with the new state whenever the checked state changes (<see cref="ToggleButton.CheckedChanged"/>).</summary>
    public static T OnCheckedChanged<T>(this T toggle, Action<bool?> action) where T : ToggleButton => toggle.OnCheckedChanged(MarkupExtensions.ToHandler(action));

    /// <summary>
    /// Binds the checked state to a <see cref="bool"/> of <paramref name="source"/>. With a <paramref name="setter"/>,
    /// toggling writes the new state back; the indeterminate state is written as <c>false</c>.
    /// </summary>
    public static T BindIsChecked<T, TSource>(this T toggle, TSource source, Func<TSource, bool> getter, Action<TSource, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ToggleButton where TSource : class =>
        toggle.BindToSource(ToggleButton.IsCheckedProperty, source, s => (bool?)getter(s), ToNullableSetter(setter), updateSourceTrigger, getterExpression);

    /// <summary>
    /// Binds the checked state to a <see cref="bool"/> of the DataContext:
    /// <c>.BindIsChecked((SettingsViewModel s) =&gt; s.AutoSave, (s, value) =&gt; s.AutoSave = value)</c>.
    /// </summary>
    public static T BindIsChecked<T, TDataContext>(this T toggle, Func<TDataContext, bool> getter, Action<TDataContext, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ToggleButton where TDataContext : class =>
        toggle.BindToDataContext(ToggleButton.IsCheckedProperty, (TDataContext s) => (bool?)getter(s), ToNullableSetter(setter), updateSourceTrigger, getterExpression);

    /// <summary>Binds the three-state checked state to a <see cref="Nullable{Boolean}"/> of <paramref name="source"/>; <c>null</c> is indeterminate.</summary>
    public static T BindIsChecked<T, TSource>(this T toggle, TSource source, Func<TSource, bool?> getter, Action<TSource, bool?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ToggleButton where TSource : class =>
        toggle.BindToSource(ToggleButton.IsCheckedProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the three-state checked state to a <see cref="Nullable{Boolean}"/> of the DataContext; <c>null</c> is indeterminate.</summary>
    public static T BindIsChecked<T, TDataContext>(this T toggle, Func<TDataContext, bool?> getter, Action<TDataContext, bool?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ToggleButton where TDataContext : class =>
        toggle.BindToDataContext(ToggleButton.IsCheckedProperty, getter, setter, updateSourceTrigger, getterExpression);

    // Adapts a bool setter to the bool? checked state; the indeterminate state is written as false.
    private static Action<TSource, bool?>? ToNullableSetter<TSource>(Action<TSource, bool>? setter) =>
        setter == null ? null : (s, value) => setter(s, value == true);
}

/// <summary>Fluent methods for <see cref="RadioButton"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class RadioButtonMarkup
{
    /// <summary>
    /// Sets the group name. Checking a radio button unchecks the others with the same group name in its window; without a name, the
    /// group is the radio buttons that share the same parent.
    /// </summary>
    public static T GroupName<T>(this T radioButton, string? groupName) where T : RadioButton => radioButton.Set(RadioButton.GroupNameProperty, groupName);

    /// <summary>
    /// Checks the radio button while a value of <paramref name="source"/> equals <paramref name="valueToMatch"/>. When
    /// the user checks it, <paramref name="setter"/> writes <paramref name="valueToMatch"/> back. One option of an enum
    /// then looks like <c>.BindIsChecked(vm, v =&gt; v.Size, (v, s) =&gt; v.Size = s, Size.Large)</c>.
    /// </summary>
    public static T BindIsChecked<T, TSource, TValue>(this T radioButton, TSource source, Func<TSource, TValue> getter, Action<TSource, TValue> setter,
        TValue valueToMatch,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : RadioButton where TSource : class
    {
        ArgumentNullException.ThrowIfNull(setter);
        return radioButton.BindToSource(
            ToggleButton.IsCheckedProperty,
            source,
            s => (bool?)EqualityComparer<TValue>.Default.Equals(getter(s), valueToMatch),
            (s, isChecked) =>
            {
                if (isChecked == true) setter(s, valueToMatch);
            },
            default,
            getterExpression);
    }

    /// <summary>
    /// Checks the radio button while a value of the DataContext equals <paramref name="valueToMatch"/>. When the user
    /// checks it, <paramref name="setter"/> writes <paramref name="valueToMatch"/> back:
    /// <c>.BindIsChecked((OrderViewModel o) =&gt; o.Size, (o, s) =&gt; o.Size = s, Size.Large)</c>.
    /// </summary>
    public static T BindIsChecked<T, TDataContext, TValue>(this T radioButton, Func<TDataContext, TValue> getter, Action<TDataContext, TValue> setter,
        TValue valueToMatch,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : RadioButton where TDataContext : class
    {
        ArgumentNullException.ThrowIfNull(setter);
        return radioButton.BindToDataContext(
            ToggleButton.IsCheckedProperty,
            (TDataContext s) => (bool?)EqualityComparer<TValue>.Default.Equals(getter(s), valueToMatch),
            (s, isChecked) =>
            {
                if (isChecked == true) setter(s, valueToMatch);
            },
            default,
            getterExpression);
    }
}

/// <summary>Fluent methods for <see cref="Switch"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class SwitchMarkup
{
    /// <summary>Shows a check mark in the thumb while the switch is on and a dash while it is off.</summary>
    public static T ShowThumbIcon<T>(this T @switch, bool show = true) where T : Switch => @switch.Set(Switch.ShowThumbIconProperty, show);

    /// <summary>Sets the width of the track in pixels. By default the theme's sizing decides.</summary>
    public static T TrackWidth<T>(this T @switch, float width) where T : Switch => @switch.Set(Switch.TrackWidthProperty, width);

    /// <summary>Sets the height of the track in pixels; the thumb scales with it. By default the theme's sizing decides.</summary>
    public static T TrackHeight<T>(this T @switch, float height) where T : Switch => @switch.Set(Switch.TrackHeightProperty, height);

    /// <summary>Sets the track width and height together.</summary>
    public static T TrackSize<T>(this T @switch, float width, float height) where T : Switch => @switch.TrackWidth(width).TrackHeight(height);
}
