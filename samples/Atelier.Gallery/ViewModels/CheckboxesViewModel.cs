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
    private bool _basicUnchecked = false;

    [ObservableProperty]
    private bool _basicChecked = true;

    [ObservableProperty]
    private bool _syncCloudStorage = false;

    [ObservableProperty]
    private bool _autoUpdate = true;

    [ObservableProperty]
    private bool _enableNotifications = true;

    [ObservableProperty]
    private bool _sendAnalytics = false;

    // Radio Button selections
    [ObservableProperty]
    private string _basicOption = "Option A";

    [ObservableProperty]
    private string _shippingMethod = "Standard";

    [ObservableProperty]
    private QualitySetting _streamingQuality = QualitySetting.High1080p;

    // Switch observable properties
    [ObservableProperty]
    private bool _standardSwitchOff = false;

    [ObservableProperty]
    private bool _standardSwitchOn = true;

    [ObservableProperty]
    private bool _iconSwitchOff = false;

    [ObservableProperty]
    private bool _iconSwitchOn = true;

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
        BasicUnchecked = false;
        BasicChecked = true;
        SyncCloudStorage = false;
        AutoUpdate = true;
        EnableNotifications = true;
        SendAnalytics = false;
        BasicOption = "Option A";
        ShippingMethod = "Standard";
        StreamingQuality = QualitySetting.High1080p;
        StandardSwitchOff = false;
        StandardSwitchOn = true;
        IconSwitchOff = false;
        IconSwitchOn = true;
        WifiEnabled = true;
        BluetoothEnabled = false;
        AirplaneMode = false;
        HighFpsMode = true;
    }

    [RelayCommand]
    private void ClearAll()
    {
        BasicUnchecked = false;
        BasicChecked = false;
        SyncCloudStorage = false;
        AutoUpdate = false;
        EnableNotifications = false;
        SendAnalytics = false;
        BasicOption = "";
        ShippingMethod = "";
        StandardSwitchOff = false;
        StandardSwitchOn = false;
        IconSwitchOff = false;
        IconSwitchOn = false;
        WifiEnabled = false;
        BluetoothEnabled = false;
        AirplaneMode = false;
        HighFpsMode = false;
    }
}
