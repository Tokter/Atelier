using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Atelier.Gallery.ViewModels;

/// <summary>The view model of a gallery page: what the navigation shows and searches.</summary>
public partial class PageViewModel : ObservableObject
{
    [ObservableProperty]
    private MaterialIconKind _pageIcon = MaterialIconKind.DeviceUnknown;

    [ObservableProperty]
    private string _pageTitle = "Untitled Page";

    /// <summary>Extra words the navigation search matches, such as the names of the controls on the page.</summary>
    public string Keywords { get; protected set; } = string.Empty;

    /// <summary>Returns whether the page matches the navigation search text.</summary>
    public bool Matches(string search) =>
        string.IsNullOrWhiteSpace(search) ||
        PageTitle.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
        Keywords.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
}
