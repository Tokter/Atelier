using System;
using System.Collections.Generic;
using Atelier.Controls;
using Atelier.Core.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public sealed record IconItemInfo(MaterialIconKind Kind, string Name, string DisplayName, Color Color);

public partial class IconsViewModel : PageViewModel
{
    private static readonly Color[] AccentColors =
    [
        Color.FromHex("#3B82F6"), // Blue
        Color.FromHex("#10B981"), // Emerald
        Color.FromHex("#8B5CF6"), // Purple
        Color.FromHex("#F59E0B"), // Amber
        Color.FromHex("#EC4899"), // Pink
        Color.FromHex("#06B6D4"), // Cyan
        Color.FromHex("#F97316"), // Orange
        Color.FromHex("#6366F1"), // Indigo
        Color.FromHex("#14B8A6"), // Teal
        Color.FromHex("#E11D48"), // Rose
    ];

    private static readonly List<IconItemInfo> _allIcons = [];
    public static IReadOnlyList<IconItemInfo> AllIcons => _allIcons;

    static IconsViewModel()
    {
        var values = Enum.GetValues<MaterialIconKind>();
        int colorIndex = 0;
        foreach (var kind in values)
        {
            if (kind == MaterialIconKind.None)
                continue;

            string name = kind.ToString();
            // If name starts with "Icon" followed by a digit (e.g. Icon360, Icon10k, Icon3dRotation), 
            // friendly display name strips the "Icon" prefix
            string displayName = (name.StartsWith("Icon", StringComparison.Ordinal) && name.Length > 4 && char.IsDigit(name[4]))
                ? name.Substring(4)
                : name;

            Color color = AccentColors[colorIndex % AccentColors.Length];
            colorIndex++;

            _allIcons.Add(new IconItemInfo(kind, name, displayName, color));
        }
    }

    [ObservableProperty]
    private MaterialIconKind _playgroundKind = MaterialIconKind.Favorite;

    [ObservableProperty]
    private Color _playgroundColor = Color.FromHex("#E11D48");

    [ObservableProperty]
    private float _playgroundFill = 0f;

    [ObservableProperty]
    private float _playgroundWeight = 400f;

    [ObservableProperty]
    private float _playgroundGrade = 0f;

    [ObservableProperty]
    private float _playgroundOpticalSize = 48f;

    [ObservableProperty]
    private float _playgroundSize = 72f;

    [ObservableProperty]
    private float _customStrokeWidth = 1.8f;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _displayLimit = 120;

    [ObservableProperty]
    private string _searchStatus = string.Empty;

    [ObservableProperty]
    private int _filterRevision = 0;

    public List<IconItemInfo> FilteredIcons { get; } = [];

    public int TotalMatchingCount { get; private set; }

    public bool HasMoreIcons => TotalMatchingCount > DisplayLimit;

    public string WeightName => PlaygroundWeight switch
    {
        < 150 => "Thin (100)",
        < 250 => "ExtraLight (200)",
        < 350 => "Light (300)",
        < 450 => "Regular (400)",
        < 550 => "Medium (500)",
        < 650 => "SemiBold (600)",
        _ => "Bold (700)"
    };

    public IconsViewModel()
    {
        PageIcon = MaterialIconKind.Mood;
        PageTitle = "Icons";
        ApplyFilter();
    }

    partial void OnPlaygroundWeightChanged(float value)
    {
        OnPropertyChanged(nameof(WeightName));
    }

    partial void OnSearchTextChanged(string value)
    {
        DisplayLimit = 120;
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        FilteredIcons.Clear();
        string query = SearchText?.Trim() ?? string.Empty;

        IEnumerable<IconItemInfo> matches;
        if (string.IsNullOrEmpty(query))
        {
            matches = _allIcons;
            TotalMatchingCount = _allIcons.Count;
            SearchStatus = $"Showing {Math.Min(DisplayLimit, TotalMatchingCount):N0} of {TotalMatchingCount:N0} icons";
        }
        else
        {
            var list = new List<IconItemInfo>();
            for (int i = 0; i < _allIcons.Count; i++)
            {
                var item = _allIcons[i];
                if (item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(item);
                }
            }
            matches = list;
            TotalMatchingCount = list.Count;
            SearchStatus = TotalMatchingCount switch
            {
                0 => $"No icons found matching \"{query}\"",
                1 => $"Found 1 icon matching \"{query}\"",
                _ => $"Found {TotalMatchingCount:N0} icons matching \"{query}\"" +
                     (TotalMatchingCount > DisplayLimit ? $" (showing first {DisplayLimit})" : "")
            };
        }

        int count = 0;
        foreach (var item in matches)
        {
            FilteredIcons.Add(item);
            count++;
            if (count >= DisplayLimit)
                break;
        }

        OnPropertyChanged(nameof(HasMoreIcons));
        FilterRevision++;
    }

    [RelayCommand]
    public void LoadMore()
    {
        DisplayLimit += 120;
        ApplyFilter();
    }

    [RelayCommand]
    public void ShowAll()
    {
        DisplayLimit = _allIcons.Count;
        ApplyFilter();
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = string.Empty;
        DisplayLimit = 120;
        ApplyFilter();
    }

    [RelayCommand]
    private void SelectIcon(MaterialIconKind kind)
    {
        PlaygroundKind = kind;
    }

    public void SelectIconWithColor(MaterialIconKind kind, Color color)
    {
        PlaygroundKind = kind;
        PlaygroundColor = color;
    }

    [RelayCommand]
    private void ToggleFill()
    {
        PlaygroundFill = PlaygroundFill >= 0.5f ? 0f : 1f;
    }

    [RelayCommand]
    private void ResetPlayground()
    {
        PlaygroundKind = MaterialIconKind.Favorite;
        PlaygroundColor = Color.FromHex("#E11D48");
        PlaygroundFill = 0f;
        PlaygroundWeight = 400f;
        PlaygroundGrade = 0f;
        PlaygroundOpticalSize = 48f;
        PlaygroundSize = 72f;
        CustomStrokeWidth = 1.8f;
    }
}
