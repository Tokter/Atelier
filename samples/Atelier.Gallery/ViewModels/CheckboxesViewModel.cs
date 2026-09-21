using System;
using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public enum QualitySetting
{
    Standard720p,
    High1080p,
    Ultra4K
}

public partial class CheckboxesViewModel : PageViewModel
{
    // Master interactive enable/disable toggle
    [ObservableProperty]
    private bool _interactiveControlsEnabled = true;

    // Checkbox observable properties
    [ObservableProperty]
    private bool _enableNotifications = true;

    [ObservableProperty]
    private bool _autoUpdate = true;

    [ObservableProperty]
    private bool _sendAnalytics = false;

    // Radio Button enum selection
    [ObservableProperty]
    private QualitySetting _streamingQuality = QualitySetting.High1080p;

    // Switch observable properties
    [ObservableProperty]
    private bool _wifiEnabled = true;

    [ObservableProperty]
    private bool _bluetoothEnabled = false;

    [ObservableProperty]
    private bool _airplaneMode = false;

    [ObservableProperty]
    private bool _highFpsMode = true;

    public CheckboxesViewModel()
    {
        PageIcon = MaterialIconKind.CheckBox;
        PageTitle = "Selection Controls";
    }

    [RelayCommand]
    private void ToggleNotifications()
    {
        EnableNotifications = !EnableNotifications;
    }

    [RelayCommand]
    private void ToggleAllSwitches()
    {
        bool target = !(WifiEnabled && BluetoothEnabled && HighFpsMode);
        WifiEnabled = target;
        BluetoothEnabled = target;
        HighFpsMode = target;
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        InteractiveControlsEnabled = true;
        EnableNotifications = true;
        AutoUpdate = true;
        SendAnalytics = false;
        StreamingQuality = QualitySetting.High1080p;
        WifiEnabled = true;
        BluetoothEnabled = false;
        AirplaneMode = false;
        HighFpsMode = true;
    }
}
