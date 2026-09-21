using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Theming.Material;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class TypographyViewModel : PageViewModel
{
    public static readonly string[] AvailableFonts =
    [
        "Segoe UI",
        "Arial",
        "Georgia",
        "Consolas",
        "Courier New",
        "Trebuchet MS",
        "Verdana"
    ];

    [ObservableProperty]
    private string _sampleText = "The quick brown fox jumps over the lazy dog. Design is not just what it looks like and feels like — design is how it works.";

    [ObservableProperty]
    private float _fontSize = 24f;

    [ObservableProperty]
    private string _fontFamily = "Segoe UI";

    [ObservableProperty]
    private bool _isBold = false;

    [ObservableProperty]
    private bool _isItalic = false;

    [ObservableProperty]
    private bool _isMuted = false;

    [ObservableProperty]
    private Color _selectedColor = Color.Transparent;

    [ObservableProperty]
    private TextAlignment _textAlignment = TextAlignment.Left;

    [ObservableProperty]
    private bool _isWrapping = true;

    [ObservableProperty]
    private string _activePresetName = "Custom";

    [ObservableProperty]
    private string _selectedStyleKey = MaterialTypography.Heading1Key;

    public TextWrapping TextWrapping => IsWrapping ? TextWrapping.Wrap : TextWrapping.NoWrap;

    public TypographyViewModel()
    {
        PageIcon = MaterialIconKind.TextFields;
        PageTitle = "Typography";
        ApplyPreset(MaterialTypography.Heading1Key);
    }

    partial void OnIsWrappingChanged(bool value)
    {
        OnPropertyChanged(nameof(TextWrapping));
    }

    [RelayCommand]
    public void ToggleBold() => IsBold = !IsBold;

    [RelayCommand]
    public void ToggleItalic() => IsItalic = !IsItalic;

    [RelayCommand]
    public void ToggleMuted() => IsMuted = !IsMuted;

    [RelayCommand]
    public void ToggleWrapping() => IsWrapping = !IsWrapping;

    [RelayCommand]
    public void ApplyPreset(string preset)
    {
        SelectedStyleKey = preset;
        ActivePresetName = preset;
        IsItalic = false;

        // Dynamically resolve properties from the registered Style in MaterialTypography / StyleManager.GlobalStyles
        var style = MaterialTypography.GetRegisteredStyle(preset);
        if (style != null)
        {
            foreach (var setter in style.Setters)
            {
                if (setter.Property == TextBlock.FontSizeProperty && setter.Value is float fs)
                    FontSize = fs;
                else if (setter.Property == TextBlock.BoldProperty && setter.Value is bool b)
                    IsBold = b;
                else if (setter.Property == TextBlock.MutedProperty && setter.Value is bool m)
                    IsMuted = m;
            }
        }
    }

    [RelayCommand]
    public void ResetPlayground()
    {
        SampleText = "The quick brown fox jumps over the lazy dog. Design is not just what it looks like and feels like — design is how it works.";
        FontFamily = "Segoe UI";
        IsItalic = false;
        TextAlignment = TextAlignment.Left;
        IsWrapping = true;
        SelectedColor = Color.Transparent;
        ApplyPreset(MaterialTypography.Heading1Key);
    }
}
