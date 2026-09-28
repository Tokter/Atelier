using System;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class DateTimePickersViewModel : PageViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DatesText))]
    private DateOnly? _dockedDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DatesText))]
    private DateOnly? _modalDate = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DatesText))]
    private DateOnly? _inputDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimesText))]
    private TimeOnly? _alarm = new(7, 30);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimesText))]
    private TimeOnly? _departure = new(18, 45);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimesText))]
    private TimeOnly? _reminder;

    [ObservableProperty]
    private string _lastPicked = "Nothing picked yet";

    [ObservableProperty]
    private bool _pickersEnabled = true;

    public string DatesText => $"Docked = {Format(DockedDate)}   Modal = {Format(ModalDate)}   Input = {Format(InputDate)}";

    public string TimesText => $"Alarm = {Format(Alarm)}   Departure = {Format(Departure)}   Reminder = {Format(Reminder)}";

    private static string Format(DateOnly? date) => date?.ToString("yyyy-MM-dd") ?? "null";

    private static string Format(TimeOnly? time) => time?.ToString("HH:mm") ?? "null";

    public DateTimePickersViewModel()
    {
        PageIcon = MaterialIconKind.Event;
        PageTitle = "Date & Time Pickers";
        Keywords = "date time picker calendar clock dial docked modal input 24h am pm hour minute";
    }

    [RelayCommand]
    [property: Command("Reset", "DateTimePickers", Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
    private void Reset()
    {
        DockedDate = null;
        ModalDate = DateOnly.FromDateTime(DateTime.Today);
        InputDate = null;
        Alarm = new TimeOnly(7, 30);
        Departure = new TimeOnly(18, 45);
        Reminder = null;
        PickersEnabled = true;
        LastPicked = "Nothing picked yet";
    }
}
