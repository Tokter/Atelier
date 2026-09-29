using System;
using System.Collections.Generic;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

/// <summary>A catalog entry: the glyph and the name shown for it.</summary>
public sealed record IconItemInfo(MaterialIconKind Kind, string Name);

public partial class IconsViewModel : PageViewModel
{
    /// <summary>The keybinding group of this page's commands.</summary>
    public const string Group = "Icons";

    private const int PageSize = 64;

    private static readonly IconItemInfo[] _allIcons = CreateCatalog();

    // Playground
    [ObservableProperty]
    private MaterialIconKind _kind = MaterialIconKind.Favorite;

    [ObservableProperty]
    private ColorRole _colorRole = ColorRole.Primary;

    [ObservableProperty]
    private float _fill;

    [ObservableProperty]
    private float _weight = 400f;

    [ObservableProperty]
    private float _grade;

    [ObservableProperty]
    private bool _autoOpticalSize = true;

    [ObservableProperty]
    private float _opticalSize = 48f;

    [ObservableProperty]
    private float _size = 96f;

    [ObservableProperty]
    private float _strokeWidth = 2f;

    // Catalog
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _displayLimit = PageSize;

    [ObservableProperty]
    private string _searchStatus = string.Empty;

    [ObservableProperty]
    private bool _hasMore;

    /// <summary>The catalog entries to show; replaced (with a <see cref="CatalogChanged"/>) when the search changes.</summary>
    public List<IconItemInfo> VisibleIcons { get; } = [];

    /// <summary>Raised after <see cref="VisibleIcons"/> changed.</summary>
    public event Action? CatalogChanged;

    public static int TotalIconCount => _allIcons.Length;

    public IconsViewModel()
    {
        PageIcon = MaterialIconKind.Mood;
        PageTitle = "Icons & Images";
        CommandGroup = Group;
        Keywords = "icon material symbols glyph svg path image picture stretch bitmap";
        ApplyFilter();
    }

    public string WeightName => Weight switch
    {
        < 150 => "Thin",
        < 250 => "Extra light",
        < 350 => "Light",
        < 450 => "Regular",
        < 550 => "Medium",
        < 650 => "Semibold",
        _ => "Bold",
    };

    partial void OnWeightChanged(float value) => OnPropertyChanged(nameof(WeightName));

    partial void OnSearchTextChanged(string value)
    {
        DisplayLimit = PageSize;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        VisibleIcons.Clear();
        string query = SearchText.Trim();
        int matches = 0;
        foreach (var item in _allIcons)
        {
            if (query.Length > 0 && !item.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches++;
            if (VisibleIcons.Count < DisplayLimit)
            {
                VisibleIcons.Add(item);
            }
        }

        HasMore = matches > VisibleIcons.Count;
        SearchStatus = query.Length == 0
            ? $"Showing {VisibleIcons.Count:N0} of {matches:N0} icons"
            : matches == 0
                ? $"No icons match \"{query}\""
                : $"Showing {VisibleIcons.Count:N0} of {matches:N0} icons matching \"{query}\"";
        CatalogChanged?.Invoke();
    }

    [RelayCommand]
    [property: Command("ShowMore", Group, Label = "Show more", Icon = MaterialIcons.ExpandMore, Description = "Show the next icons of the catalog")]
    private void ShowMore()
    {
        DisplayLimit += PageSize;
        ApplyFilter();
    }

    [RelayCommand]
    [property: Command("ClearSearch", Group, Label = "Clear", Description = "Clear the search and show all icons")]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    [property: Command("SelectIcon", Group, Label = "Select icon", Description = "Load the icon into the playground")]
    private void SelectIcon(MaterialIconKind kind) => Kind = kind;

    [RelayCommand]
    [property: Command("ToggleFilled", Group, Label = "Toggle filled", Icon = MaterialIcons.FormatColorFill, Description = "Switch the playground icon between outlined and filled")]
    private void ToggleFilled() => Fill = Fill >= 0.5f ? 0f : 1f;

    [RelayCommand]
    [property: Command("Reset", Group, Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
    private void Reset()
    {
        Kind = MaterialIconKind.Favorite;
        ColorRole = ColorRole.Primary;
        Fill = 0f;
        Weight = 400f;
        Grade = 0f;
        AutoOpticalSize = true;
        OpticalSize = 48f;
        Size = 96f;
        StrokeWidth = 2f;
    }

    private static IconItemInfo[] CreateCatalog()
    {
        var items = new List<IconItemInfo>();
        foreach (var kind in Enum.GetValues<MaterialIconKind>())
        {
            if (kind == MaterialIconKind.None)
            {
                continue;
            }

            // Names like Icon360 or Icon3dRotation exist only because identifiers can't start with a digit.
            string name = kind.ToString();
            if (name.StartsWith("Icon", StringComparison.Ordinal) && name.Length > 4 && char.IsDigit(name[4]))
            {
                name = name[4..];
            }

            items.Add(new IconItemInfo(kind, name));
        }
        return items.ToArray();
    }
}
