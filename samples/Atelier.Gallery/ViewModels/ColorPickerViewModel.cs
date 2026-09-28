using Atelier.Controls;
using Atelier.Core.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class ColorPickerViewModel : PageViewModel
{
    private static readonly Color DefaultColor = Color.FromHex("#6750A4");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HexText), nameof(RgbText), nameof(HslText), nameof(HsbText))]
    private Color _pickedColor = DefaultColor;

    [ObservableProperty]
    private bool _isAlphaEnabled = true;

    [ObservableProperty]
    private Color _accentColor = Color.FromHex("#FFB4AB");

    [ObservableProperty]
    private string _wheelText = "Drag on the wheel";

    public string HexText => $"Hex  {ColorPicker.ToHex(PickedColor)}";

    public string RgbText => $"RGBA {PickedColor.R}, {PickedColor.G}, {PickedColor.B}, {PickedColor.Af:0.00}";

    public string HslText
    {
        get
        {
            PickedColor.ToHsl(out float h, out float s, out float l);
            return $"HSL  {h:0}°, {s * 100:0}%, {l * 100:0}%";
        }
    }

    public string HsbText
    {
        get
        {
            PickedColor.ToHsv(out float h, out float s, out float v);
            return $"HSB  {h:0}°, {s * 100:0}%, {v * 100:0}%";
        }
    }

    public ColorPickerViewModel()
    {
        PageIcon = MaterialIconKind.Palette;
        PageTitle = "Color Picker";
        Keywords = "color colour picker wheel hex rgb rgba hsl hsb hsv alpha swatch slider gradient";
    }

    [RelayCommand]
    private void Reset()
    {
        PickedColor = DefaultColor;
        IsAlphaEnabled = true;
        AccentColor = Color.FromHex("#FFB4AB");
    }
}
