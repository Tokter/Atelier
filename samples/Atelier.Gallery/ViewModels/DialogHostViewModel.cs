using System;
using System.Threading.Tasks;
using Atelier.Controls;
using Atelier.Core.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class DialogHostViewModel : PageViewModel
{
    [ObservableProperty]
    private bool _isGlobalTarget = true;

    [ObservableProperty]
    private bool _closeOnClickAway = true;

    [ObservableProperty]
    private float _overlayOpacity = 0.5f;

    [ObservableProperty]
    private int _localCardClickCount = 0;

    [ObservableProperty]
    private string _interactionLog = "Ready. Select a dialog type and host scope to test modal hosting.";

    [ObservableProperty]
    private string _lastSubmittedFormResult = "No form submitted yet.";

    [ObservableProperty]
    private bool _isLocalAutoSyncEnabled = true;

    public string TargetHostName => IsGlobalTarget ? "Global App Window (RootHost)" : "Local Workspace Card (LocalGalleryHost)";

    public string TargetHostIdentifier => IsGlobalTarget ? "RootHost" : "LocalGalleryHost";

    public Color CurrentOverlayColor => Color.FromArgb((byte)(OverlayOpacity * 255f), 0, 0, 0);

    public DialogHostViewModel()
    {
        PageTitle = "Dialog Host";
        PageIcon = MaterialIconKind.WebAsset;
    }

    public void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        InteractionLog = $"[{timestamp}] {message}";
    }

    [RelayCommand]
    private void IncrementLocalCardClicks()
    {
        LocalCardClickCount++;
        Log($"Local card button clicked! Total clicks: {LocalCardClickCount}.");
    }

    [RelayCommand]
    private void SetOpacity25()
    {
        OverlayOpacity = 0.25f;
        Log("Overlay scrim darkness set to 25% (Light).");
    }

    [RelayCommand]
    private void SetOpacity50()
    {
        OverlayOpacity = 0.50f;
        Log("Overlay scrim darkness set to 50% (Standard MD3).");
    }

    [RelayCommand]
    private void SetOpacity75()
    {
        OverlayOpacity = 0.75f;
        Log("Overlay scrim darkness set to 75% (Deep Focus).");
    }

    [RelayCommand]
    private void ClearLog()
    {
        InteractionLog = "Log cleared.";
    }
}
