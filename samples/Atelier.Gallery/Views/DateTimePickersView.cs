using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class DateTimePickersView : GalleryPage
{
    private readonly DateTimePickersViewModel _vm;

    public DateTimePickersView(DateTimePickersViewModel viewModel)
        : base(MaterialIconKind.Event, "Date & Time Pickers",
            "Material Design 3 date and time pickers. A date field opens a docked calendar, a modal calendar or a modal " +
            "date input; a time field opens the time picker with a clock dial or text fields, on a 12-hour or 24-hour clock.")
    {
        _vm = viewModel;

        Settings(
            new Switch("Pickers enabled").ShowThumbIcon().BindIsChecked(_vm, v => v.PickersEnabled, (v, on) => v.PickersEnabled = on),
            new Button("Reset").Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        SectionsPanel.BindIsEnabled(_vm, v => v.PickersEnabled);

        Sections(DateSection(), TimeSection(), DialogSection(), CalendarSection());
    }

    private UIElement DateSection() => Ui.Section("Date pickers",
        "Type a date in the field, or click the calendar icon (or press Alt+Down). The docked picker opens below the " +
        "field; the modal picker and the modal date input open as dialogs, and their header button switches between the " +
        "calendar and the text field. In the calendar, the arrow keys move between days and Page Up/Down between months.",
        Ui.Columns(260,
            Ui.Demo("Docked",
                new DatePicker().Mode(DatePickerMode.Docked).MinWidth(240)
                    .BindSelectedDate(_vm, v => v.DockedDate, (v, d) => v.DockedDate = d)),
            Ui.Demo("Modal",
                new DatePicker().Mode(DatePickerMode.Modal).Label("Departure date").MinWidth(240)
                    .BindSelectedDate(_vm, v => v.ModalDate, (v, d) => v.ModalDate = d)),
            Ui.Demo("Modal input",
                new DatePicker().Mode(DatePickerMode.ModalInput).Label("Birthday").MinWidth(240)
                    .DateRange(new DateOnly(1900, 1, 1), DateOnly.FromDateTime(DateTime.Today))
                    .BindSelectedDate(_vm, v => v.InputDate, (v, d) => v.InputDate = d),
                Ui.Note("Limited to past dates; later dates show an error."))),
        Ui.Readout(_vm, v => v.DatesText));

    private UIElement TimeSection() => Ui.Section("Time pickers",
        "Type a time such as 7:30 pm, 7p or 1930, or click the clock icon. On the dial, pick the hour and the picker moves " +
        "on to the minutes; drag for any minute. The keyboard button switches to the text fields.",
        Ui.Columns(260,
            Ui.Demo("Dial, AM/PM",
                new TimePicker().Label("Alarm").ClockFormat(ClockFormat.TwelveHour).MinWidth(240)
                    .BindSelectedTime(_vm, v => v.Alarm, (v, t) => v.Alarm = t)),
            Ui.Demo("Dial, 24-hour",
                new TimePicker().Label("Departure").ClockFormat(ClockFormat.TwentyFourHour).MinWidth(240)
                    .BindSelectedTime(_vm, v => v.Departure, (v, t) => v.Departure = t)),
            Ui.Demo("Time input",
                new TimePicker().Label("Reminder").InputMode(TimePickerInputMode.Input).MinWidth(240)
                    .BindSelectedTime(_vm, v => v.Reminder, (v, t) => v.Reminder = t),
                Ui.Note("Uses the culture's clock."))),
        Ui.Readout(_vm, v => v.TimesText));

    private UIElement DialogSection() => Ui.Section("Pickers from code",
        "DatePickerDialog.PickAsync and TimePickerDialog.PickAsync show a picker from any button and return the picked " +
        "value, or null when it was canceled.",
        Ui.Row(
            new Button("Pick a date").Variant(ButtonVariant.Tonal).OnClick(async (s, _) =>
            {
                var date = await DatePickerDialog.PickAsync((UIElement)s!, DateOnly.FromDateTime(DateTime.Today));
                _vm.LastPicked = date is { } d ? $"Picked {d:D}" : "Canceled";
            }),
            new Button("Pick a time").Variant(ButtonVariant.Tonal).OnClick(async (s, _) =>
            {
                var time = await TimePickerDialog.PickAsync((UIElement)s!, new TimeOnly(9, 0));
                _vm.LastPicked = time is { } t ? $"Picked {t:t}" : "Canceled";
            }),
            Ui.Readout(_vm, v => v.LastPicked)),
        Ui.Code("DateOnly? date = await DatePickerDialog.PickAsync(button, vm.Date);\n" +
                "TimeOnly? time = await TimePickerDialog.PickAsync(button, vm.Time, TimePickerInputMode.Input);"));

    private UIElement CalendarSection() => Ui.Section("Calendar",
        "The calendar of the pickers is a control of its own, in the modal or the docked layout.",
        Ui.Columns(360,
            Ui.Demo("Modal layout",
                new Calendar().Width(336).HorizontalAlignment(HorizontalAlignment.Left)),
            Ui.Demo("Docked layout",
                new Calendar().Layout(CalendarLayout.Docked).Width(336).HorizontalAlignment(HorizontalAlignment.Left))));
}
