using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

/// <summary>An item type for the typed item template and typed selection binding demos.</summary>
public sealed record Destination(string City, string Country, MaterialIconKind Icon)
{
    public override string ToString() => City;
}

public partial class ComboBoxViewModel : PageViewModel
{
    private int _nextTag = 4;

    [ObservableProperty]
    private bool _controlsEnabled = true;

    [ObservableProperty]
    private int _sizeIndex = 1;

    [ObservableProperty]
    private Destination? _destination;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveTagCommand))]
    private string? _selectedTag;

    [ObservableProperty]
    private string? _month;

    [ObservableProperty]
    private string? _number;

    [ObservableProperty]
    private bool _isDropDownOpen;

    [ObservableProperty]
    private string _lastEvent = "Open the combo box or pick an item to see its events";

    public ComboBoxViewModel()
    {
        PageIcon = MaterialIconKind.ArrowDropDownCircle;
        PageTitle = "ComboBox";
        Keywords = "combobox select dropdown picker";
        Destination = Destinations[1];
    }

    public ObservableCollection<Destination> Destinations { get; } =
    [
        new("Lisbon", "Portugal", MaterialIconKind.Sailing),
        new("Kyoto", "Japan", MaterialIconKind.TempleBuddhist),
        new("Zurich", "Switzerland", MaterialIconKind.Landscape),
        new("New York", "United States", MaterialIconKind.LocationCity),
        new("Cape Town", "South Africa", MaterialIconKind.BeachAccess),
    ];

    public ObservableCollection<string> Tags { get; } = ["Tag 1", "Tag 2", "Tag 3"];

    public string[] Months { get; } = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames.Where(m => m.Length > 0).ToArray();

    public string[] Numbers { get; } = Enumerable.Range(1, 60).Select(i => $"Item {i:00}").ToArray();

    [RelayCommand]
    [property: Command("AddTag", "ComboBox", Label = "Add", Icon = MaterialIcons.Add, Description = "Add a tag to the bound collection; the drop-down updates")]
    private void AddTag()
    {
        string tag = $"Tag {_nextTag++}";
        Tags.Add(tag);
        SelectedTag = tag;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveTag))]
    [property: Command("RemoveTag", "ComboBox", Label = "Remove selected", Icon = MaterialIcons.Delete, Description = "Remove the selected tag from the collection")]
    private void RemoveTag()
    {
        if (SelectedTag != null)
        {
            Tags.Remove(SelectedTag);
        }
    }

    private bool CanRemoveTag() => SelectedTag != null;

    [RelayCommand]
    [property: Command("OpenDropDown", "ComboBox", Label = "Open drop-down", Description = "Set IsDropDownOpen from the view model")]
    private void OpenDropDown() => IsDropDownOpen = true;

    [RelayCommand]
    [property: Command("Reset", "ComboBox", Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
    private void Reset()
    {
        ControlsEnabled = true;
        SizeIndex = 1;
        Destination = Destinations[1];
        Tags.Clear();
        Tags.Add("Tag 1");
        Tags.Add("Tag 2");
        Tags.Add("Tag 3");
        _nextTag = 4;
        SelectedTag = null;
        Month = null;
        Number = null;
        IsDropDownOpen = false;
        LastEvent = "Open the combo box or pick an item to see its events";
    }
}
