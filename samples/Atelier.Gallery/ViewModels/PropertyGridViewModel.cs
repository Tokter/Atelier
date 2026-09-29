using System;
using System.Collections.ObjectModel;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Gallery.Models;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public enum InspectedObject
{
    Shape,
    Service,
    Emitter
}

public partial class PropertyGridViewModel : PageViewModel
{
    /// <summary>The keybinding group of this page's commands.</summary>
    public const string Group = "PropertyGrid";

    private static readonly Color[] Palette =
    [
        Color.FromHex("#6750A4"), Color.FromHex("#006A6A"), Color.FromHex("#B3261E"),
        Color.FromHex("#7D5260"), Color.FromHex("#386A20"), Color.FromHex("#00639B"),
    ];

    private readonly Random _random = new(7);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedObject))]
    private InspectedObject _inspected = InspectedObject.Shape;

    [ObservableProperty]
    private PropertySortMode _sortMode = PropertySortMode.Categorized;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _isToolbarVisible = true;

    [ObservableProperty]
    private bool _isDescriptionVisible = true;

    [ObservableProperty]
    private float _toolbarElevation = 2;

    [ObservableProperty]
    private float _labelWidth = 160;

    public ShapeModel Shape { get; } = new();

    public ServiceConfigModel Service { get; } = new();

    public EmitterModel Emitter { get; } = new();

    public PreferencesModel Preferences { get; } = new();

    public object SelectedObject => Inspected switch
    {
        InspectedObject.Service => Service,
        InspectedObject.Emitter => Emitter,
        _ => Shape,
    };

    /// <summary>The grid's events, newest first.</summary>
    public ObservableCollection<string> Events { get; } = [];

    public PropertyGridViewModel()
    {
        PageTitle = "Property Grid";
        CommandGroup = Group;
        PageIcon = MaterialIconKind.Tune;
        Keywords = "propertygrid property grid inspector editor properties";
    }

    public void Log(string message)
    {
        Events.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        while (Events.Count > 8)
        {
            Events.RemoveAt(Events.Count - 1);
        }
    }

    [RelayCommand]
    [property: Command("RandomizeShape", Group, Label = "Change shape in code", Icon = MaterialIcons.Shuffle, Description = "Change the shape in the view model; the grid follows")]
    private void RandomizeShape()
    {
        Inspected = InspectedObject.Shape;
        Shape.Width = _random.Next(80, 360);
        Shape.Height = _random.Next(60, 200);
        Shape.Kind = (ShapeKind)_random.Next(3);
        Shape.Fill = Palette[_random.Next(Palette.Length)];
        Shape.Opacity = MathF.Round(0.5f + (float)_random.NextDouble() * 0.5f, 2);
        Log("Shape changed in code; the grid follows through INotifyPropertyChanged");
    }

    [RelayCommand]
    [property: Command("Reset", Group, Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
    private void Reset()
    {
        Shape.Reset();
        SortMode = PropertySortMode.Categorized;
        FilterText = string.Empty;
        IsToolbarVisible = true;
        IsDescriptionVisible = true;
        ToolbarElevation = 2;
        LabelWidth = 160;
        Events.Clear();
    }
}
