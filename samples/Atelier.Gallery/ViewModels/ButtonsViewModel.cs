using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public enum SegmentAlignment
{
    Left,
    Center,
    Right
}

public partial class ButtonsViewModel : PageViewModel
{
    [ObservableProperty]
    private bool _controlsEnabled = true;

    [ObservableProperty]
    private float _elevation = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecrementCommand))]
    private int _count;

    [ObservableProperty]
    private float _repeatValue = 50;

    [ObservableProperty]
    private string _lastClick = "Click a button to see its Click event";

    [ObservableProperty]
    private string _lastParameter = "No parameter received yet";

    [ObservableProperty]
    private int _hoverClicks;

    [ObservableProperty]
    private int _pressClicks;

    [ObservableProperty]
    private SegmentAlignment _alignment = SegmentAlignment.Left;

    [ObservableProperty]
    private bool _isBold = true;

    [ObservableProperty]
    private bool? _isItalic;

    public ButtonsViewModel()
    {
        PageIcon = MaterialIconKind.SmartButton;
        PageTitle = "Buttons";
        Keywords = "button repeatbutton togglebutton command toolbar icon button clickmode";
    }

    [RelayCommand]
    private void Increment() => Count++;

    // The command's CanExecute disables every button bound to it while the count is 0.
    [RelayCommand(CanExecute = nameof(CanDecrement))]
    private void Decrement() => Count--;

    private bool CanDecrement() => Count > 0;

    [RelayCommand]
    private void Choose(string? parameter) => LastParameter = $"Command received parameter \"{parameter}\"";

    // Segmented toggle group: clicking the selected segment toggles it off locally, so the change notification is
    // raised even when the value stays the same, which checks it again.
    [RelayCommand]
    private void SelectAlignment(SegmentAlignment alignment)
    {
        Alignment = alignment;
        OnPropertyChanged(nameof(Alignment));
    }

    [RelayCommand]
    private void RepeatUp() => RepeatValue = Math.Min(100, RepeatValue + 1);

    [RelayCommand]
    private void RepeatDown() => RepeatValue = Math.Max(0, RepeatValue - 1);

    [RelayCommand]
    private void Reset()
    {
        ControlsEnabled = true;
        Elevation = 1;
        Count = 0;
        RepeatValue = 50;
        LastClick = "Click a button to see its Click event";
        LastParameter = "No parameter received yet";
        HoverClicks = 0;
        PressClicks = 0;
        Alignment = SegmentAlignment.Left;
        IsBold = true;
        IsItalic = null;
    }
}
