using System;
using Atelier.Controls;
using Atelier.Theming.Material;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class TypographyViewModel : PageViewModel
{
    public const string DefaultSampleText =
        "The quick brown fox jumps over the lazy dog. Good typography makes text easy to read, at every size.";

    public const string DefaultFontLabel = "Default";

    public static readonly string[] Fonts = [DefaultFontLabel, "Segoe UI", "Arial", "Georgia", "Times New Roman", "Consolas", "Verdana"];

    /// <summary>The style keys the playground offers; the first entry means "no style".</summary>
    public static readonly string[] StyleKeys =
    [
        "None",
        MaterialTypography.DisplaySmallKey,
        MaterialTypography.HeadlineMediumKey,
        MaterialTypography.TitleLargeKey,
        MaterialTypography.TitleMediumKey,
        MaterialTypography.BodyLargeKey,
        MaterialTypography.BodyMediumKey,
        MaterialTypography.LabelLargeKey,
        MaterialTypography.Heading1Key,
        MaterialTypography.SubtextKey,
    ];

    [ObservableProperty]
    private string _sampleText = DefaultSampleText;

    [ObservableProperty]
    private string _styleKey = MaterialTypography.HeadlineMediumKey;

    // Local values override the style's values; while off, the style decides.
    [ObservableProperty]
    private bool _overrideSizeAndWeight;

    [ObservableProperty]
    private float _fontSize = 24f;

    [ObservableProperty]
    private float _fontWeight = 400f;

    [ObservableProperty]
    private string _fontFamily = DefaultFontLabel;

    [ObservableProperty]
    private bool _isBold;

    [ObservableProperty]
    private bool _isItalic;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private ColorRole _colorRole = ColorRole.Default;

    [ObservableProperty]
    private TextAlignment _textAlignment = TextAlignment.Left;

    [ObservableProperty]
    private bool _isWrapping = true;

    [ObservableProperty]
    private TextTrimming _trimming = TextTrimming.CharacterEllipsis;

    [ObservableProperty]
    private float _maxLines;

    [ObservableProperty]
    private float _lineHeight;

    public TypographyViewModel()
    {
        PageIcon = MaterialIconKind.TextFields;
        PageTitle = "Typography";
        Keywords = "textblock text font type scale typography weight bold italic wrap trimming ellipsis line height";
    }

    /// <summary>The style key to apply, or <c>null</c> for none.</summary>
    public string? EffectiveStyleKey => StyleKey == StyleKeys[0] ? null : StyleKey;

    /// <summary>The font family to apply, or <c>null</c> for the default font.</summary>
    public string? EffectiveFontFamily => FontFamily == DefaultFontLabel ? null : FontFamily;

    public Atelier.Core.Primitives.FontWeight EffectiveFontWeight => new((int)Math.Round(Math.Clamp(FontWeight, 100f, 900f) / 100f) * 100);

    partial void OnStyleKeyChanged(string value) => OnPropertyChanged(nameof(EffectiveStyleKey));

    partial void OnFontFamilyChanged(string value) => OnPropertyChanged(nameof(EffectiveFontFamily));

    partial void OnFontWeightChanged(float value) => OnPropertyChanged(nameof(EffectiveFontWeight));

    [RelayCommand]
    private void ToggleBold() => IsBold = !IsBold;

    [RelayCommand]
    private void ToggleItalic() => IsItalic = !IsItalic;

    [RelayCommand]
    private void Reset()
    {
        SampleText = DefaultSampleText;
        StyleKey = MaterialTypography.HeadlineMediumKey;
        OverrideSizeAndWeight = false;
        FontSize = 24f;
        FontWeight = 400f;
        FontFamily = DefaultFontLabel;
        IsBold = false;
        IsItalic = false;
        IsMuted = false;
        ColorRole = ColorRole.Default;
        TextAlignment = TextAlignment.Left;
        IsWrapping = true;
        Trimming = TextTrimming.CharacterEllipsis;
        MaxLines = 0;
        LineHeight = 0;
    }
}
