using System;
using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class TextBoxesViewModel : PageViewModel
{
    // Master interactive enable/disable toggle
    [ObservableProperty]
    private bool _interactiveControlsEnabled = true;

    // Outlined Fields
    [ObservableProperty]
    private string _outlinedUsername = "";

    [ObservableProperty]
    private string _outlinedEmail = "alex.morgan@atelier.design";

    [ObservableProperty]
    private string _outlinedSearch = "";

    [ObservableProperty]
    private string _outlinedRepo = "";

    [ObservableProperty]
    private string _outlinedPhone = "+1 (555) 019-2834";

    [ObservableProperty]
    private string _outlinedSecurityKey = "SuperSecretPass!";

    // Filled Fields
    [ObservableProperty]
    private string _filledOrganization = "";

    [ObservableProperty]
    private string _filledEnvironment = "Production Cluster Alpha";

    [ObservableProperty]
    private string _filledApiToken = "";

    [ObservableProperty]
    private string _filledBranch = "";

    [ObservableProperty]
    private string _filledLocation = "San Francisco, CA";

    [ObservableProperty]
    private string _filledInbox = "contact@atelier.design";

    // Two-Way MVVM Binding Card Fields
    [ObservableProperty]
    private string _username = "alex.morgan";

    [ObservableProperty]
    private string _email = "alex.morgan@atelier.design";

    [ObservableProperty]
    private string _phone = "+1 (555) 019-2834";

    [ObservableProperty]
    private string _bio = "UI framework enthusiast and designer.";

    public TextBoxesViewModel()
    {
        PageIcon = MaterialIconKind.Edit;
        PageTitle = "Text Fields";
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        InteractiveControlsEnabled = true;

        OutlinedUsername = "";
        OutlinedEmail = "alex.morgan@atelier.design";
        OutlinedSearch = "";
        OutlinedRepo = "";
        OutlinedPhone = "+1 (555) 019-2834";
        OutlinedSecurityKey = "SuperSecretPass!";

        FilledOrganization = "";
        FilledEnvironment = "Production Cluster Alpha";
        FilledApiToken = "";
        FilledBranch = "";
        FilledLocation = "San Francisco, CA";
        FilledInbox = "contact@atelier.design";

        Username = "alex.morgan";
        Email = "alex.morgan@atelier.design";
        Phone = "+1 (555) 019-2834";
        Bio = "UI framework enthusiast and designer.";
    }

    [RelayCommand]
    private void ClearAll()
    {
        OutlinedUsername = "";
        OutlinedEmail = "";
        OutlinedSearch = "";
        OutlinedRepo = "";
        OutlinedPhone = "";
        OutlinedSecurityKey = "";

        FilledOrganization = "";
        FilledEnvironment = "";
        FilledApiToken = "";
        FilledBranch = "";
        FilledLocation = "";
        FilledInbox = "";

        Username = "";
        Email = "";
        Phone = "";
        Bio = "";
    }
}
