using System;
using System.Collections.Generic;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class DialogHostViewModel : PageViewModel
{
    private readonly List<string> _dialogEvents = [];

    // Dialogs
    [ObservableProperty]
    private string _lastResult = "No dialog closed yet";

    [ObservableProperty]
    private string _dialogEventTrail = "Open a dialog to see Opened → Closing → Closed";

    [ObservableProperty]
    private bool _closeOnEscape = true;

    [ObservableProperty]
    private float _dialogElevation = 6;

    [ObservableProperty]
    private string _projectName = string.Empty;

    // Local dialog host
    [ObservableProperty]
    private bool _isLocalHostOpen;

    [ObservableProperty]
    private bool _localCloseOnClickAway = true;

    [ObservableProperty]
    private float _localScrimOpacity = 0.32f;

    [ObservableProperty]
    private string _lastHostEvent = "No host events yet";

    // Popups
    [ObservableProperty]
    private bool _isPlacementPopupOpen;

    [ObservableProperty]
    private PlacementMode _placement = PlacementMode.Bottom;

    [ObservableProperty]
    private float _horizontalOffset;

    [ObservableProperty]
    private float _verticalOffset = 4;

    [ObservableProperty]
    private bool _staysOpen;

    [ObservableProperty]
    private bool _matchTargetWidth;

    [ObservableProperty]
    private bool _showPopupBorder;

    [ObservableProperty]
    private float _popupElevation = 3;

    [ObservableProperty]
    private string _lastPopupEvent = "No popup events yet";

    public DialogHostViewModel()
    {
        PageTitle = "Popups & Dialogs";
        PageIcon = MaterialIconKind.WebAsset;
        Keywords = "dialog dialoghost popup modal overlay flyout menu placement confirm alert";
    }

    public Color LocalScrimColor => Color.Black.WithAlpha(LocalScrimOpacity);

    partial void OnLocalScrimOpacityChanged(float value) => OnPropertyChanged(nameof(LocalScrimColor));

    /// <summary>Records a dialog lifecycle event; the trail shows the events of the current dialog in order.</summary>
    public void DialogEvent(string name, bool first = false)
    {
        if (first)
        {
            _dialogEvents.Clear();
        }
        _dialogEvents.Add(name);
        DialogEventTrail = string.Join(" → ", _dialogEvents);
    }

    [RelayCommand]
    [property: Command("ToggleLocalHost", "Dialogs", Label = "Close all", Description = "Close the dialogs of the local host (enabled while it is open)")]
    private void ToggleLocalHost() => IsLocalHostOpen = !IsLocalHostOpen;

    [RelayCommand]
    [property: Command("TogglePlacementPopup", "Dialogs", Label = "Open popup", Description = "Open or close the popup placed at this button")]
    private void TogglePlacementPopup() => IsPlacementPopupOpen = !IsPlacementPopupOpen;

    [RelayCommand]
    [property: Command("Reset", "Dialogs", Label = "Reset options", Icon = MaterialIcons.RestartAlt, Description = "Put the dialog options back as they were")]
    private void Reset()
    {
        CloseOnEscape = true;
        DialogElevation = 6;
        LocalCloseOnClickAway = true;
        LocalScrimOpacity = 0.32f;
        Placement = PlacementMode.Bottom;
        HorizontalOffset = 0;
        VerticalOffset = 4;
        StaysOpen = false;
        MatchTargetWidth = false;
        ShowPopupBorder = false;
        PopupElevation = 3;
    }
}
