using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Layout;

namespace Atelier.Markup;

/// <summary>Fluent methods for <see cref="DatePicker"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class DatePickerMarkup
{
    /// <summary>Sets the date; <c>null</c> for none.</summary>
    public static T SelectedDate<T>(this T picker, DateOnly? date) where T : DatePicker => picker.Set(DatePicker.SelectedDateProperty, date);

    /// <summary>Sets how the calendar button opens the picker: docked (the default), modal, or modal input.</summary>
    public static T Mode<T>(this T picker, DatePickerMode mode) where T : DatePicker => picker.Set(DatePicker.ModeProperty, mode);

    /// <summary>Sets the field's label. The default is "Date".</summary>
    public static T Label<T>(this T picker, string label) where T : DatePicker => picker.Set(DatePicker.LabelProperty, label);

    /// <summary>Sets the pattern the date is shown and typed in, e.g. <c>dd.MM.yyyy</c>; <c>null</c> uses the culture's.</summary>
    public static T DateFormat<T>(this T picker, string? pattern) where T : DatePicker => picker.Set(DatePicker.DateFormatProperty, pattern);

    /// <summary>Sets the earliest date that can be picked or typed.</summary>
    public static T MinDate<T>(this T picker, DateOnly date) where T : DatePicker => picker.Set(DatePicker.MinDateProperty, date);

    /// <summary>Sets the latest date that can be picked or typed.</summary>
    public static T MaxDate<T>(this T picker, DateOnly date) where T : DatePicker => picker.Set(DatePicker.MaxDateProperty, date);

    /// <summary>Sets the range of dates that can be picked or typed.</summary>
    public static T DateRange<T>(this T picker, DateOnly min, DateOnly max) where T : DatePicker => picker.MinDate(min).MaxDate(max);

    /// <summary>Sets the first day of the calendar's weeks. The default comes from the culture.</summary>
    public static T FirstDayOfWeek<T>(this T picker, DayOfWeek day) where T : DatePicker => picker.Set(DatePicker.FirstDayOfWeekProperty, day);

    /// <summary>Handles <see cref="DatePicker.SelectedDateChanged"/>, raised with the new date.</summary>
    public static T OnSelectedDateChanged<T>(this T picker, EventHandler<DateOnly?> handler) where T : DatePicker
    {
        picker.SelectedDateChanged += handler;
        return picker;
    }

    /// <summary>Runs <paramref name="action"/> with the new date whenever it changes (<see cref="DatePicker.SelectedDateChanged"/>).</summary>
    public static T OnSelectedDateChanged<T>(this T picker, Action<DateOnly?> action) where T : DatePicker => picker.OnSelectedDateChanged(MarkupExtensions.ToHandler(action));

    /// <summary>Binds the date to <paramref name="source"/>. With a <paramref name="setter"/>, picking a date writes it back.</summary>
    public static T BindSelectedDate<T, TSource>(this T picker, TSource source, Func<TSource, DateOnly?> getter, Action<TSource, DateOnly?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : DatePicker where TSource : class =>
        picker.BindToSource(DatePicker.SelectedDateProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the date to the DataContext. With a <paramref name="setter"/>, picking a date writes it back.</summary>
    public static T BindSelectedDate<T, TDataContext>(this T picker, Func<TDataContext, DateOnly?> getter, Action<TDataContext, DateOnly?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : DatePicker where TDataContext : class =>
        picker.BindToDataContext(DatePicker.SelectedDateProperty, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>Fluent methods for <see cref="Calendar"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class CalendarMarkup
{
    /// <summary>Sets the selected date; <c>null</c> for none.</summary>
    public static T SelectedDate<T>(this T calendar, DateOnly? date) where T : Calendar => calendar.Set(Calendar.SelectedDateProperty, date);

    /// <summary>Sets a date in the month to show.</summary>
    public static T DisplayDate<T>(this T calendar, DateOnly date) where T : Calendar => calendar.Set(Calendar.DisplayDateProperty, date);

    /// <summary>Sets the earliest date that can be picked.</summary>
    public static T MinDate<T>(this T calendar, DateOnly date) where T : Calendar => calendar.Set(Calendar.MinDateProperty, date);

    /// <summary>Sets the latest date that can be picked.</summary>
    public static T MaxDate<T>(this T calendar, DateOnly date) where T : Calendar => calendar.Set(Calendar.MaxDateProperty, date);

    /// <summary>Sets the first day of the weeks. The default comes from the culture.</summary>
    public static T FirstDayOfWeek<T>(this T calendar, DayOfWeek day) where T : Calendar => calendar.Set(Calendar.FirstDayOfWeekProperty, day);

    /// <summary>Sets the header layout: modal (the default) or docked.</summary>
    public static T Layout<T>(this T calendar, CalendarLayout layout) where T : Calendar => calendar.Set(Calendar.LayoutProperty, layout);

    /// <summary>Sets what the calendar shows: days (the default), the month menu or the years.</summary>
    public static T View<T>(this T calendar, CalendarView view) where T : Calendar => calendar.Set(Calendar.ViewProperty, view);

    /// <summary>Handles <see cref="Calendar.SelectedDateChanged"/>, raised with the new date.</summary>
    public static T OnSelectedDateChanged<T>(this T calendar, EventHandler<DateOnly?> handler) where T : Calendar
    {
        calendar.SelectedDateChanged += handler;
        return calendar;
    }

    /// <summary>Runs <paramref name="action"/> with the new date whenever it changes (<see cref="Calendar.SelectedDateChanged"/>).</summary>
    public static T OnSelectedDateChanged<T>(this T calendar, Action<DateOnly?> action) where T : Calendar => calendar.OnSelectedDateChanged(MarkupExtensions.ToHandler(action));

    /// <summary>Binds the selected date to <paramref name="source"/>. With a <paramref name="setter"/>, picking a date writes it back.</summary>
    public static T BindSelectedDate<T, TSource>(this T calendar, TSource source, Func<TSource, DateOnly?> getter, Action<TSource, DateOnly?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Calendar where TSource : class =>
        calendar.BindToSource(Calendar.SelectedDateProperty, source, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>Fluent methods for <see cref="DatePickerDialog"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class DatePickerDialogMarkup
{
    /// <summary>Sets the date selected when the dialog opens.</summary>
    public static T SelectedDate<T>(this T dialog, DateOnly? date) where T : DatePickerDialog => dialog.Set(DatePickerDialog.SelectedDateProperty, date);

    /// <summary>Sets whether the dialog shows the calendar (the default) or the text field.</summary>
    public static T InputMode<T>(this T dialog, DatePickerInputMode mode) where T : DatePickerDialog => dialog.Set(DatePickerDialog.InputModeProperty, mode);

    /// <summary>Sets the small title above the headline. The default is "Select date".</summary>
    public static T HeaderText<T>(this T dialog, string text) where T : DatePickerDialog => dialog.Set(DatePickerDialog.HeaderTextProperty, text);

    /// <summary>Sets the pattern of typed dates, e.g. <c>dd.MM.yyyy</c>; <c>null</c> uses the culture's.</summary>
    public static T DateFormat<T>(this T dialog, string? pattern) where T : DatePickerDialog => dialog.Set(DatePickerDialog.DateFormatProperty, pattern);
}

/// <summary>Fluent methods for <see cref="TimePicker"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class TimePickerMarkup
{
    /// <summary>Sets the time; <c>null</c> for none.</summary>
    public static T SelectedTime<T>(this T picker, TimeOnly? time) where T : TimePicker => picker.Set(TimePicker.SelectedTimeProperty, time);

    /// <summary>Sets whether the picker opens with the dial (the default) or the text fields.</summary>
    public static T InputMode<T>(this T picker, TimePickerInputMode mode) where T : TimePicker => picker.Set(TimePicker.InputModeProperty, mode);

    /// <summary>Sets the clock: 12-hour with AM/PM, 24-hour, or the culture's (the default).</summary>
    public static T ClockFormat<T>(this T picker, ClockFormat format) where T : TimePicker => picker.Set(TimePicker.ClockFormatProperty, format);

    /// <summary>Sets the field's label. The default is "Time".</summary>
    public static T Label<T>(this T picker, string label) where T : TimePicker => picker.Set(TimePicker.LabelProperty, label);

    /// <summary>Handles <see cref="TimePicker.SelectedTimeChanged"/>, raised with the new time.</summary>
    public static T OnSelectedTimeChanged<T>(this T picker, EventHandler<TimeOnly?> handler) where T : TimePicker
    {
        picker.SelectedTimeChanged += handler;
        return picker;
    }

    /// <summary>Runs <paramref name="action"/> with the new time whenever it changes (<see cref="TimePicker.SelectedTimeChanged"/>).</summary>
    public static T OnSelectedTimeChanged<T>(this T picker, Action<TimeOnly?> action) where T : TimePicker => picker.OnSelectedTimeChanged(MarkupExtensions.ToHandler(action));

    /// <summary>Binds the time to <paramref name="source"/>. With a <paramref name="setter"/>, picking a time writes it back.</summary>
    public static T BindSelectedTime<T, TSource>(this T picker, TSource source, Func<TSource, TimeOnly?> getter, Action<TSource, TimeOnly?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TimePicker where TSource : class =>
        picker.BindToSource(TimePicker.SelectedTimeProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the time to the DataContext. With a <paramref name="setter"/>, picking a time writes it back.</summary>
    public static T BindSelectedTime<T, TDataContext>(this T picker, Func<TDataContext, TimeOnly?> getter, Action<TDataContext, TimeOnly?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TimePicker where TDataContext : class =>
        picker.BindToDataContext(TimePicker.SelectedTimeProperty, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>Fluent methods for <see cref="TimePickerDialog"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class TimePickerDialogMarkup
{
    /// <summary>Sets the time shown when the dialog opens.</summary>
    public static T SelectedTime<T>(this T dialog, TimeOnly? time) where T : TimePickerDialog => dialog.Set(TimePickerDialog.SelectedTimeProperty, time);

    /// <summary>Sets whether the dialog shows the dial (the default) or the text fields.</summary>
    public static T InputMode<T>(this T dialog, TimePickerInputMode mode) where T : TimePickerDialog => dialog.Set(TimePickerDialog.InputModeProperty, mode);

    /// <summary>Sets the clock: 12-hour with AM/PM, 24-hour, or the culture's (the default).</summary>
    public static T ClockFormat<T>(this T dialog, ClockFormat format) where T : TimePickerDialog => dialog.Set(TimePickerDialog.ClockFormatProperty, format);
}

/// <summary>Fluent methods for <see cref="ClockDial"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ClockDialMarkup
{
    /// <summary>Sets the hour (0–23) or minute (0–59) the handle points at.</summary>
    public static T Value<T>(this T dial, int value) where T : ClockDial => dial.Set(ClockDial.ValueProperty, value);

    /// <summary>Sets whether the dial shows hours (the default) or minutes.</summary>
    public static T Mode<T>(this T dial, ClockDialMode mode) where T : ClockDial => dial.Set(ClockDial.ModeProperty, mode);

    /// <summary>Makes the hour dial show 24 hours (13–00 on an inner ring).</summary>
    public static T Is24Hour<T>(this T dial, bool is24Hour = true) where T : ClockDial => dial.Set(ClockDial.Is24HourProperty, is24Hour);

    /// <summary>Runs <paramref name="action"/> with the new value whenever it changes (<see cref="ClockDial.ValueChanged"/>).</summary>
    public static T OnValueChanged<T>(this T dial, Action<int> action) where T : ClockDial
    {
        dial.ValueChanged += MarkupExtensions.ToHandler(action);
        return dial;
    }
}

/// <summary>Fluent methods for <see cref="TimePickerSegment"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class TimePickerSegmentMarkup
{
    /// <summary>Sets the digits shown.</summary>
    public static T Text<T>(this T segment, string text) where T : TimePickerSegment => segment.Set(TimePickerSegment.TextProperty, text);

    /// <summary>Sets whether the dial shows this part.</summary>
    public static T IsSelected<T>(this T segment, bool isSelected = true) where T : TimePickerSegment => segment.Set(TimePickerSegment.IsSelectedProperty, isSelected);

    /// <summary>Makes the box an editable input field.</summary>
    public static T IsEditable<T>(this T segment, bool isEditable = true) where T : TimePickerSegment => segment.Set(TimePickerSegment.IsEditableProperty, isEditable);
}

/// <summary>Fluent methods for <see cref="TimePeriodSelector"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class TimePeriodSelectorMarkup
{
    /// <summary>Selects PM (or AM with <c>false</c>).</summary>
    public static T IsPm<T>(this T selector, bool isPm = true) where T : TimePeriodSelector => selector.Set(TimePeriodSelector.IsPmProperty, isPm);

    /// <summary>Stacks the segments (the default) or places them side by side.</summary>
    public static T Orientation<T>(this T selector, Orientation orientation) where T : TimePeriodSelector => selector.Set(TimePeriodSelector.OrientationProperty, orientation);
}
