using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Properties;

namespace Atelier.Markup;

/// <summary>Fluent methods for <see cref="TextBox"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class TextBoxMarkup
{
    #region Properties

    /// <summary>Sets the text. Text set from code is not limited by <c>MaxLength</c>.</summary>
    public static T Text<T>(this T textBox, string? text) where T : TextBox => textBox.Set(TextBox.TextProperty, text ?? string.Empty);

    /// <summary>Sets the hint shown while the field is empty. With a label, it appears once the label has floated up.</summary>
    public static T Placeholder<T>(this T textBox, string? placeholder) where T : TextBox => textBox.Set(TextBox.PlaceholderProperty, placeholder ?? string.Empty);

    /// <summary>Sets the Material field style: outlined (the default) or filled.</summary>
    public static T Variant<T>(this T textBox, TextBoxVariant variant) where T : TextBox => textBox.Set(TextBox.VariantProperty, variant);

    /// <summary>Sets the label, shown inside the empty field and floating above the text while focused or filled.</summary>
    public static T Label<T>(this T textBox, string? label) where T : TextBox => textBox.Set(TextBox.LabelProperty, label ?? string.Empty);

    /// <summary>Sets the icon shown before the text. <see cref="MaterialIconKind.None"/> (the default) shows none.</summary>
    public static T LeadingIconKind<T>(this T textBox, MaterialIconKind iconKind) where T : TextBox => textBox.Set(TextBox.LeadingIconKindProperty, iconKind);

    /// <summary>
    /// Sets the icon button shown after the text, e.g. to clear it or open a picker; handle clicks with
    /// <see cref="OnTrailingIconClick{T}(T, Action)"/>. <see cref="MaterialIconKind.None"/> (the default) shows none.
    /// </summary>
    public static T TrailingIconKind<T>(this T textBox, MaterialIconKind iconKind) where T : TextBox => textBox.Set(TextBox.TrailingIconKindProperty, iconKind);

    /// <summary>Handles <see cref="TextBox.TrailingIconClick"/>, raised when the trailing icon is clicked.</summary>
    public static T OnTrailingIconClick<T>(this T textBox, EventHandler handler) where T : TextBox
    {
        textBox.TrailingIconClick += handler;
        return textBox;
    }

    /// <summary>Runs <paramref name="action"/> when the trailing icon is clicked (<see cref="TextBox.TrailingIconClick"/>).</summary>
    public static T OnTrailingIconClick<T>(this T textBox, Action action) where T : TextBox => textBox.OnTrailingIconClick(MarkupExtensions.ToHandler(action));

    /// <summary>Sets the helper text shown below the field. A validation error replaces it.</summary>
    public static T SupportingText<T>(this T textBox, string? supportingText) where T : TextBox =>
        textBox.Set(TextBox.SupportingTextProperty, supportingText ?? string.Empty);

    /// <summary>Makes the text read-only: it can still be selected and copied, but not edited.</summary>
    public static T IsReadOnly<T>(this T textBox, bool isReadOnly = true) where T : TextBox => textBox.Set(TextBox.IsReadOnlyProperty, isReadOnly);

    /// <summary>Limits how many characters the user can type or paste. 0 (the default) means no limit.</summary>
    public static T MaxLength<T>(this T textBox, int maxLength) where T : TextBox => textBox.Set(TextBox.MaxLengthProperty, maxLength);

    /// <summary>
    /// Turns the field into a password field that shows <paramref name="passwordChar"/> instead of each character.
    /// Copying and cutting are disabled. <c>'\0'</c> shows the text again.
    /// </summary>
    public static T PasswordChar<T>(this T textBox, char passwordChar = '●') where T : TextBox => textBox.Set(TextBox.PasswordCharProperty, passwordChar);

    /// <summary>Sets how the text is aligned inside the field.</summary>
    public static T TextAlignment<T>(this T textBox, TextAlignment alignment) where T : TextBox => textBox.Set(TextBox.TextAlignmentProperty, alignment);

    /// <summary>Sets how many undo steps are kept. 0 disables undo; the default is 100.</summary>
    public static T UndoLimit<T>(this T textBox, int undoLimit) where T : TextBox => textBox.Set(TextBox.UndoLimitProperty, undoLimit);

    /// <summary>
    /// Sets the height of the field container in pixels, excluding the supporting text. By default the theme's sizing
    /// decides. The field still grows to fit its text.
    /// </summary>
    public static T FieldHeight<T>(this T textBox, float fieldHeight) where T : TextBox => textBox.Set(TextBox.FieldHeightProperty, fieldHeight);

    /// <summary>Sets the width of the caret in pixels. The default is 2.</summary>
    public static T CaretWidth<T>(this T textBox, float caretWidth) where T : TextBox => textBox.Set(TextBox.CaretWidthProperty, caretWidth);

    /// <summary>Sets whether Enter inserts a line break, making the field multi-line. The default is <c>false</c>.</summary>
    public static T AcceptsReturn<T>(this T textBox, bool acceptsReturn = true) where T : TextBox => textBox.Set(TextBox.AcceptsReturnProperty, acceptsReturn);

    /// <summary>Sets whether lines wrap at the field's width; wrapping makes the field multi-line. The default is no wrapping.</summary>
    public static T TextWrapping<T>(this T textBox, TextWrapping wrapping = Controls.TextWrapping.Wrap) where T : TextBox =>
        textBox.Set(TextBox.TextWrappingProperty, wrapping);

    /// <summary>Sets the number of lines a multi-line field is at least tall enough for (at least 1, the default).</summary>
    public static T MinLines<T>(this T textBox, int minLines) where T : TextBox => textBox.Set(TextBox.MinLinesProperty, minLines);

    /// <summary>Sets the number of lines a multi-line field grows to before it scrolls; 0 (the default) grows with the text.</summary>
    public static T MaxLines<T>(this T textBox, int maxLines) where T : TextBox => textBox.Set(TextBox.MaxLinesProperty, maxLines);

    /// <summary>
    /// Makes the field a multi-line text area: Enter inserts line breaks, lines wrap, and the field shows
    /// <paramref name="minLines"/> lines and grows to <paramref name="maxLines"/> (0: with the text) before scrolling.
    /// </summary>
    public static T Multiline<T>(this T textBox, int minLines = 3, int maxLines = 0) where T : TextBox =>
        textBox.AcceptsReturn().TextWrapping().MinLines(minLines).MaxLines(maxLines);

    #endregion

    #region Events

    /// <summary>Handles <see cref="TextBox.TextChanged"/>, raised with the new text whenever the text changes.</summary>
    public static T OnTextChanged<T>(this T textBox, EventHandler<string> handler) where T : TextBox
    {
        textBox.TextChanged += handler;
        return textBox;
    }

    /// <summary>Runs <paramref name="action"/> with the new text whenever the text changes (<see cref="TextBox.TextChanged"/>).</summary>
    public static T OnTextChanged<T>(this T textBox, Action<string> action) where T : TextBox => textBox.OnTextChanged(MarkupExtensions.ToHandler(action));

    #endregion

    #region Bindings

    /// <summary>
    /// Binds the text to a value of <paramref name="source"/>. With a <paramref name="setter"/>, typing writes the
    /// text back, on every change or, with <see cref="UpdateSourceTrigger.LostFocus"/>, when the field loses focus.
    /// </summary>
    public static T BindText<T, TSource>(this T textBox, TSource source, Func<TSource, string> getter, Action<TSource, string>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBox where TSource : class =>
        textBox.BindToSource(TextBox.TextProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>
    /// Binds the text to a value of the DataContext. With a <paramref name="setter"/>, typing writes the text back:
    /// <c>.BindText((PersonViewModel p) =&gt; p.Name, (p, value) =&gt; p.Name = value)</c>.
    /// </summary>
    public static T BindText<T, TDataContext>(this T textBox, Func<TDataContext, string> getter, Action<TDataContext, string>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBox where TDataContext : class =>
        textBox.BindToDataContext(TextBox.TextProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the label to a value of <paramref name="source"/>.</summary>
    public static T BindLabel<T, TSource>(this T textBox, TSource source, Func<TSource, string> getter, Action<TSource, string>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBox where TSource : class =>
        textBox.BindToSource(TextBox.LabelProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the label to a value of the DataContext.</summary>
    public static T BindLabel<T, TDataContext>(this T textBox, Func<TDataContext, string> getter, Action<TDataContext, string>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBox where TDataContext : class =>
        textBox.BindToDataContext(TextBox.LabelProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the placeholder to a value of <paramref name="source"/>.</summary>
    public static T BindPlaceholder<T, TSource>(this T textBox, TSource source, Func<TSource, string> getter, Action<TSource, string>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBox where TSource : class =>
        textBox.BindToSource(TextBox.PlaceholderProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the placeholder to a value of the DataContext.</summary>
    public static T BindPlaceholder<T, TDataContext>(this T textBox, Func<TDataContext, string> getter, Action<TDataContext, string>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBox where TDataContext : class =>
        textBox.BindToDataContext(TextBox.PlaceholderProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the supporting text to a value of <paramref name="source"/>, e.g. a hint that depends on the input.</summary>
    public static T BindSupportingText<T, TSource>(this T textBox, TSource source, Func<TSource, string> getter, Action<TSource, string>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBox where TSource : class =>
        textBox.BindToSource(TextBox.SupportingTextProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the supporting text to a value of the DataContext.</summary>
    public static T BindSupportingText<T, TDataContext>(this T textBox, Func<TDataContext, string> getter, Action<TDataContext, string>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBox where TDataContext : class =>
        textBox.BindToDataContext(TextBox.SupportingTextProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the read-only state to a value of <paramref name="source"/>.</summary>
    public static T BindIsReadOnly<T, TSource>(this T textBox, TSource source, Func<TSource, bool> getter, Action<TSource, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBox where TSource : class =>
        textBox.BindToSource(TextBox.IsReadOnlyProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the read-only state to a value of the DataContext.</summary>
    public static T BindIsReadOnly<T, TDataContext>(this T textBox, Func<TDataContext, bool> getter, Action<TDataContext, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBox where TDataContext : class =>
        textBox.BindToDataContext(TextBox.IsReadOnlyProperty, getter, setter, updateSourceTrigger, getterExpression);

    #endregion
}

/// <summary>Fluent methods for <see cref="Slider"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class SliderMarkup
{
    /// <summary>Sets the smallest value, at the left end of the track. The default is 0.</summary>
    public static T Minimum<T>(this T slider, float minimum) where T : Slider => slider.Set(Slider.MinimumProperty, minimum);

    /// <summary>Sets the largest value, at the right end of the track. The default is 100.</summary>
    public static T Maximum<T>(this T slider, float maximum) where T : Slider => slider.Set(Slider.MaximumProperty, maximum);

    /// <summary>Sets the range of values: <see cref="Minimum"/> and <see cref="Maximum"/>.</summary>
    public static T Range<T>(this T slider, float minimum, float maximum) where T : Slider => slider.Minimum(minimum).Maximum(maximum);

    /// <summary>Sets the current value. It is kept within the range.</summary>
    public static T Value<T>(this T slider, float value) where T : Slider => slider.Set(Slider.ValueProperty, value);

    /// <summary>Sets the step of the arrow keys. The default is 1.</summary>
    public static T SmallChange<T>(this T slider, float smallChange) where T : Slider => slider.Set(Slider.SmallChangeProperty, smallChange);

    /// <summary>Sets the step of Page Up and Page Down. The default is 10.</summary>
    public static T LargeChange<T>(this T slider, float largeChange) where T : Slider => slider.Set(Slider.LargeChangeProperty, largeChange);

    /// <summary>Sets the distance between ticks, counted from the minimum, used when snapping to ticks. The default is 1.</summary>
    public static T TickFrequency<T>(this T slider, float tickFrequency) where T : Slider => slider.Set(Slider.TickFrequencyProperty, tickFrequency);

    /// <summary>Makes dragging and the keyboard snap the value to the nearest tick. Values set from code are not snapped.</summary>
    public static T IsSnapToTickEnabled<T>(this T slider, bool isSnapToTickEnabled = true) where T : Slider =>
        slider.Set(Slider.IsSnapToTickEnabledProperty, isSnapToTickEnabled);

    /// <summary>Snaps the value to multiples of <paramref name="step"/> (counted from the minimum) while the user changes it.</summary>
    public static T SnapTo<T>(this T slider, float step) where T : Slider => slider.TickFrequency(step).SmallChange(step).IsSnapToTickEnabled();

    /// <summary>Shows a bubble with the value above the thumb while it is dragged (the default).</summary>
    public static T ShowValueIndicator<T>(this T slider, bool show = true) where T : Slider => slider.Set(Slider.ShowValueIndicatorProperty, show);

    /// <summary>Sets the composite format of the value indicator, with the value as argument 0, e.g. <c>"{0:0.0} dB"</c>. The default is <c>"{0:0}"</c>.</summary>
    public static T ValueFormat<T>(this T slider, string valueFormat) where T : Slider => slider.Set(Slider.ValueFormatProperty, valueFormat);

    /// <summary>Handles <see cref="Slider.ValueChanged"/>, raised with the new value whenever the value changes.</summary>
    public static T OnValueChanged<T>(this T slider, EventHandler<float> handler) where T : Slider
    {
        slider.ValueChanged += handler;
        return slider;
    }

    /// <summary>Runs <paramref name="action"/> with the new value whenever the value changes (<see cref="Slider.ValueChanged"/>).</summary>
    public static T OnValueChanged<T>(this T slider, Action<float> action) where T : Slider => slider.OnValueChanged(MarkupExtensions.ToHandler(action));

    /// <summary>Binds the value to <paramref name="source"/>. With a <paramref name="setter"/>, moving the slider writes the value back.</summary>
    public static T BindValue<T, TSource>(this T slider, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Slider where TSource : class =>
        slider.BindToSource(Slider.ValueProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the value to the DataContext. With a <paramref name="setter"/>, moving the slider writes the value back.</summary>
    public static T BindValue<T, TDataContext>(this T slider, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Slider where TDataContext : class =>
        slider.BindToDataContext(Slider.ValueProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the minimum to a value of <paramref name="source"/>.</summary>
    public static T BindMinimum<T, TSource>(this T slider, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Slider where TSource : class =>
        slider.BindToSource(Slider.MinimumProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the minimum to a value of the DataContext.</summary>
    public static T BindMinimum<T, TDataContext>(this T slider, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Slider where TDataContext : class =>
        slider.BindToDataContext(Slider.MinimumProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the maximum to a value of <paramref name="source"/>.</summary>
    public static T BindMaximum<T, TSource>(this T slider, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Slider where TSource : class =>
        slider.BindToSource(Slider.MaximumProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the maximum to a value of the DataContext.</summary>
    public static T BindMaximum<T, TDataContext>(this T slider, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Slider where TDataContext : class =>
        slider.BindToDataContext(Slider.MaximumProperty, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>Fluent methods for <see cref="ProgressBar"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ProgressBarMarkup
{
    /// <summary>Sets the value of an empty bar. The default is 0.</summary>
    public static T Minimum<T>(this T progressBar, float minimum) where T : ProgressBar => progressBar.Set(ProgressBar.MinimumProperty, minimum);

    /// <summary>Sets the value of a full bar. The default is 100.</summary>
    public static T Maximum<T>(this T progressBar, float maximum) where T : ProgressBar => progressBar.Set(ProgressBar.MaximumProperty, maximum);

    /// <summary>Sets the range of values: <see cref="Minimum"/> and <see cref="Maximum"/>.</summary>
    public static T Range<T>(this T progressBar, float minimum, float maximum) where T : ProgressBar => progressBar.Minimum(minimum).Maximum(maximum);

    /// <summary>Sets the progress, between the minimum and the maximum.</summary>
    public static T Value<T>(this T progressBar, float value) where T : ProgressBar => progressBar.Set(ProgressBar.ValueProperty, value);

    /// <summary>Shows an animated bar for work of unknown length instead of the value.</summary>
    public static T IsIndeterminate<T>(this T progressBar, bool isIndeterminate = true) where T : ProgressBar =>
        progressBar.Set(ProgressBar.IsIndeterminateProperty, isIndeterminate);

    /// <summary>Binds the progress to a value of <paramref name="source"/>.</summary>
    public static T BindValue<T, TSource>(this T progressBar, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ProgressBar where TSource : class =>
        progressBar.BindToSource(ProgressBar.ValueProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the progress to a value of the DataContext.</summary>
    public static T BindValue<T, TDataContext>(this T progressBar, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ProgressBar where TDataContext : class =>
        progressBar.BindToDataContext(ProgressBar.ValueProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the indeterminate state to a value of <paramref name="source"/>, e.g. "is loading but the size is unknown".</summary>
    public static T BindIsIndeterminate<T, TSource>(this T progressBar, TSource source, Func<TSource, bool> getter, Action<TSource, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ProgressBar where TSource : class =>
        progressBar.BindToSource(ProgressBar.IsIndeterminateProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the indeterminate state to a value of the DataContext.</summary>
    public static T BindIsIndeterminate<T, TDataContext>(this T progressBar, Func<TDataContext, bool> getter, Action<TDataContext, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : ProgressBar where TDataContext : class =>
        progressBar.BindToDataContext(ProgressBar.IsIndeterminateProperty, getter, setter, updateSourceTrigger, getterExpression);
}
