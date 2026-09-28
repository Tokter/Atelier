using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using Atelier.Theming;
using Atelier.Theming.Material;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly List<PageViewModel> _allPages;

    [ObservableProperty]
    private string _themeToggleText = "Dark theme";

    [ObservableProperty]
    private MaterialIconKind _themeToggleIcon = MaterialIconKind.DarkMode;

    [ObservableProperty]
    private PageViewModel? _currentPage;

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>The pages shown in the navigation: all pages, or those matching <see cref="SearchText"/>.</summary>
    public ObservableCollection<PageViewModel> Pages { get; } = [];

    /// <summary>Set by the window: shows or hides the frame-rate overlay.</summary>
    public Action? ToggleFpsOverlayAction { get; set; }

    public MainViewModel()
    {
        _allPages =
        [
            new ButtonsViewModel(),
            new CheckboxesViewModel(),
            new TextBoxesViewModel(),
            new ComboBoxViewModel(),
            new RangeControlsViewModel(),
            new ColorPickerViewModel(),
            new DateTimePickersViewModel(),
            new ListsViewModel(),
            new TreeViewViewModel(),
            new CardsViewModel(),
            new IconsViewModel(),
            new BadgesViewModel(),
            new TabsViewModel(),
            new MenusViewModel(),
            new TypographyViewModel(),
            new LayoutViewModel(),
            new DialogHostViewModel(),
            new ToolTipsViewModel(),
            new TransformationViewModel(),
            new TransitionsViewModel(),
            new PropertyGridViewModel(),
            new KeybindingViewModel(),
        ];

        foreach (var page in _allPages)
        {
            Pages.Add(page);
        }

        _currentPage = _allPages[0];

        // Every window shows the theme toggle, so all of them follow a switch made in any window.
        ThemeManager.ThemeChanged += _ => UpdateThemeToggle();
        UpdateThemeToggle();

        // Start page by index, e.g. for screenshots of a specific page (see also ATELIER_GALLERY_WINDOWS).
        if (int.TryParse(Environment.GetEnvironmentVariable("ATELIER_GALLERY_PAGE"), out int startPage) && startPage >= 0 && startPage < _allPages.Count)
        {
            _currentPage = _allPages[startPage];
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        var current = CurrentPage;
        Pages.Clear();
        foreach (var page in _allPages)
        {
            if (page.Matches(value))
            {
                Pages.Add(page);
            }
        }

        // Keep showing the current page while it matches; otherwise show the first match.
        if (current == null || !Pages.Contains(current))
        {
            CurrentPage = Pages.Count > 0 ? Pages[0] : current;
        }
    }

    [RelayCommand]
    private void NewWindow() => Program.OpenGalleryWindow();

    [RelayCommand]
    [property: Keybinding("ToggleTheme", "Global", "Ctrl+T")]
    private void ToggleTheme()
    {
        ThemeManager.Current = ThemeManager.Current.IsDark ? MaterialTheme.CreateLight() : MaterialTheme.CreateDark();
    }

    [RelayCommand]
    [property: Keybinding("ToggleFpsOverlay", "Global", "Ctrl+Shift+F")]
    private void ToggleFpsOverlay() => ToggleFpsOverlayAction?.Invoke();

    private void UpdateThemeToggle()
    {
        bool dark = ThemeManager.HasTheme && ThemeManager.Current.IsDark;
        ThemeToggleText = dark ? "Light theme" : "Dark theme";
        ThemeToggleIcon = dark ? MaterialIconKind.LightMode : MaterialIconKind.DarkMode;
    }
}
