using Atelier.Controls;
using Atelier.Core.Keybinding;
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
    [ObservableProperty]
    private bool _controlsEnabled = true;

    // Check boxes
    [ObservableProperty]
    private bool _acceptTerms;

    [ObservableProperty]
    private bool _subscribe = true;

    [ObservableProperty]
    private bool _enableNotifications = true;

    [ObservableProperty]
    private bool? _triState;

    // "Select all" parent of three check boxes: checked, unchecked or indeterminate (mixed).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllToppings))]
    private bool _cheese = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllToppings))]
    private bool _mushrooms;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllToppings))]
    private bool _olives;

    public bool? AllToppings
    {
        get => Cheese && Mushrooms && Olives ? true : !Cheese && !Mushrooms && !Olives ? false : null;
        set
        {
            bool all = value == true;
            Cheese = Mushrooms = Olives = all;
        }
    }

    // Radio buttons
    [ObservableProperty]
    private string _shippingMethod = "Standard";

    [ObservableProperty]
    private QualitySetting _streamingQuality = QualitySetting.High1080p;

    // Switches
    [ObservableProperty]
    private bool _wifiEnabled = true;

    [ObservableProperty]
    private bool _bluetoothEnabled;

    [ObservableProperty]
    private bool _airplaneMode;

    [ObservableProperty]
    private string _lastEvent = "Toggle a switch to see its events";

    public CheckboxesViewModel()
    {
        PageIcon = MaterialIconKind.CheckBox;
        PageTitle = "Selection Controls";
        Keywords = "checkbox radio button switch toggle tri-state";
    }

    [RelayCommand]
    [property: Command("ToggleNotifications", "SelectionControls", Label = "Toggle from the view model", Description = "Change EnableNotifications in the view model; the check box follows its binding")]
    private void ToggleNotifications() => EnableNotifications = !EnableNotifications;

    [RelayCommand]
    [property: Command("ToggleWireless", "SelectionControls", Label = "Toggle both", Description = "Switch Wi-Fi and Bluetooth in the view model")]
    private void ToggleWireless()
    {
        bool target = !(WifiEnabled && BluetoothEnabled);
        WifiEnabled = target;
        BluetoothEnabled = target;
    }

    [RelayCommand]
    [property: Command("Reset", "SelectionControls", Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
    private void Reset()
    {
        ControlsEnabled = true;
        AcceptTerms = false;
        Subscribe = true;
        EnableNotifications = true;
        TriState = null;
        Cheese = true;
        Mushrooms = false;
        Olives = false;
        ShippingMethod = "Standard";
        StreamingQuality = QualitySetting.High1080p;
        WifiEnabled = true;
        BluetoothEnabled = false;
        AirplaneMode = false;
    }
}
