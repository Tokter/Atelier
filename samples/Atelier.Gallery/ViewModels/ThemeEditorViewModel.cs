using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Atelier.Controls;
using Atelier.Core.Platform;
using Atelier.Core.Primitives;
using Atelier.Gallery.Infrastructure;
using Atelier.Theming.Material;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

/// <summary>An accent color the Theme Editor offers as a starting point.</summary>
public sealed record AccentPreset(string Name, Color Color);

/// <summary>
/// One color role of the theme in the Theme Editor, with its light and dark colors. "On" roles also know the role
/// they're drawn on, for the contrast readout.
/// </summary>
public partial class ThemeRoleRow : ObservableObject
{
    internal ThemeRoleRow(string group, PropertyInfo property, PropertyInfo? background)
    {
        Group = group;
        Property = property;
        Background = background;
        Label = SplitWords(property.Name);
    }

    public string Group { get; }

    /// <summary>The <see cref="MaterialColorScheme"/> property.</summary>
    public PropertyInfo Property { get; }

    /// <summary>The role this one's content is drawn on, or <c>null</c> if it isn't a content color.</summary>
    public PropertyInfo? Background { get; }

    public string Label { get; }

    public bool IsContentColor => Background != null;

    public Color LightColor => Get(false);

    public Color DarkColor => Get(true);

    public Color LightBackground => Background != null ? (Color)Background.GetValue(GalleryTheme.Light)! : LightColor;

    public Color DarkBackground => Background != null ? (Color)Background.GetValue(GalleryTheme.Dark)! : DarkColor;

    public string LightText => ColorText(false);

    public string DarkText => ColorText(true);

    [ObservableProperty]
    private bool _isLightSelected;

    [ObservableProperty]
    private bool _isDarkSelected;

    public Color Get(bool dark) => (Color)Property.GetValue(GalleryTheme.Scheme(dark))!;

    public Color BackgroundOf(bool dark) => dark ? DarkBackground : LightBackground;

    // The hex value, and for content colors the contrast with their background (4.5:1 is the WCAG minimum for text).
    private string ColorText(bool dark)
    {
        string hex = ColorPicker.ToHex(Get(dark));
        return Background == null ? hex : $"{hex}  {ThemeEditorViewModel.Contrast(Get(dark), BackgroundOf(dark)):0.0}:1";
    }

    internal void Refresh() => OnPropertyChanged(string.Empty);

    private static string SplitWords(string name)
    {
        var text = new StringBuilder();
        foreach (char c in name)
        {
            if (char.IsUpper(c) && text.Length > 0) text.Append(' ').Append(char.ToLowerInvariant(c));
            else text.Append(c);
        }
        return text.ToString();
    }
}

public partial class ThemeEditorViewModel : PageViewModel
{
    /// <summary>The keybinding group of this page's commands.</summary>
    public const string Group = "ThemeEditor";

    private bool _isSyncing;

    [ObservableProperty]
    private Color _seedColor = GalleryTheme.Seed;

    [ObservableProperty]
    private int _variantIndex = (int)GalleryTheme.Variant;

    [ObservableProperty]
    private bool _isDark = GalleryTheme.IsDark;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditedColor), nameof(EditedTitle), nameof(EditedNote))]
    private ThemeRoleRow? _selectedRole;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditedColor), nameof(EditedTitle), nameof(EditedNote))]
    private bool _isEditingDark;

    [ObservableProperty]
    private string _status = GalleryTheme.Description;

    public ThemeEditorViewModel()
    {
        PageIcon = MaterialIconKind.FormatPaint;
        PageTitle = "Theme Editor";
        CommandGroup = Group;
        Keywords = "theme editor color scheme accent seed palette tonal light dark mode primary secondary tertiary surface role customize";

        Roles = CreateRoles();
        _selectedRole = Roles[0];
        _isEditingDark = GalleryTheme.IsDark;
        UpdateSelection();

        // The schemes are shared by all windows: show edits made in another one too.
        GalleryTheme.Changed += OnGalleryThemeChanged;
    }

    /// <summary>Accent colors to start from.</summary>
    public IReadOnlyList<AccentPreset> Presets { get; } =
    [
        new("Baseline", GalleryTheme.BaselineSeed),
        new("Blue", Color.FromHex("#0061A4")),
        new("Teal", Color.FromHex("#006A6A")),
        new("Green", Color.FromHex("#386A20")),
        new("Olive", Color.FromHex("#606200")),
        new("Amber", Color.FromHex("#FFB300")),
        new("Orange", Color.FromHex("#E65100")),
        new("Red", Color.FromHex("#B3261E")),
        new("Pink", Color.FromHex("#E91E63")),
        new("Indigo", Color.FromHex("#3F51B5")),
    ];

    /// <summary>The names of the <see cref="MaterialSchemeVariant"/> values, in order.</summary>
    public IReadOnlyList<string> Variants { get; } = ["Tonal spot", "Vibrant", "Fidelity", "Neutral", "Monochrome"];

    public MaterialSchemeVariant Variant => (MaterialSchemeVariant)Math.Clamp(VariantIndex, 0, Variants.Count - 1);

    /// <summary>Every color role, in the order of <see cref="MaterialColorScheme"/>.</summary>
    public IReadOnlyList<ThemeRoleRow> Roles { get; }

    /// <summary>The color of the selected role in the edited mode; setting it changes the theme.</summary>
    public Color EditedColor
    {
        get => SelectedRole?.Get(IsEditingDark) ?? Color.Transparent;
        set
        {
            if (SelectedRole == null) return;
            GalleryTheme.SetRole(IsEditingDark, SelectedRole.Property, value);
        }
    }

    public string EditedTitle => SelectedRole == null ? "Pick a role" : $"{SelectedRole.Label} · {(IsEditingDark ? "dark" : "light")}";

    public string EditedNote
    {
        get
        {
            if (SelectedRole == null) return string.Empty;
            string mode = IsEditingDark == GalleryTheme.IsDark
                ? "The theme follows as you pick."
                : $"The {(IsEditingDark ? "dark" : "light")} mode isn't active: switch to it to see the change.";
            return SelectedRole.IsContentColor
                ? $"Drawn on {SelectedRole.Background!.Name}; keep the contrast at {(SelectedRole.Group == "Outlines" ? "3:1 or more for boundaries" : "4.5:1 or more for text")}. {mode}"
                : mode;
        }
    }

    /// <summary>How to create the generated schemes in code.</summary>
    public string SeedCode =>
        $"var light = MaterialColorScheme.FromSeed(Color.FromHex(\"{ColorPicker.ToHex(SeedColor)}\"), isDark: false, MaterialSchemeVariant.{Variant});\n" +
        $"var dark = MaterialColorScheme.FromSeed(Color.FromHex(\"{ColorPicker.ToHex(SeedColor)}\"), isDark: true, MaterialSchemeVariant.{Variant});\n" +
        "ThemeManager.Current = new MaterialTheme(\"My Light\", false, light);";

    /// <summary>Selects <paramref name="role"/> in the light or dark mode for the picker.</summary>
    public void Select(ThemeRoleRow role, bool dark)
    {
        SelectedRole = role;
        IsEditingDark = dark;
        UpdateSelection();
    }

    partial void OnSeedColorChanged(Color value) => Generate();

    partial void OnVariantIndexChanged(int value) => Generate();

    partial void OnIsDarkChanged(bool value) => GalleryTheme.IsDark = value;

    [RelayCommand]
    [property: Command("UsePreset", Group, Label = "Use preset", Description = "Generate the light and dark schemes from this accent color")]
    private void UsePreset(AccentPreset preset) => SeedColor = preset.Color;

    [RelayCommand]
    [property: Command("Reset", Group, Label = "Reset to baseline", Icon = MaterialIcons.RestartAlt, Description = "Go back to the Material 3 baseline schemes, in light and dark")]
    private void Reset() => GalleryTheme.Reset();

    [RelayCommand]
    [property: Command("CopyLight", Group, Label = "Copy the light scheme", Icon = MaterialIcons.ContentCopy, Description = "Copy the light scheme with every role as C#")]
    private void CopyLight() => CopyScheme(false);

    [RelayCommand]
    [property: Command("CopyDark", Group, Label = "Copy the dark scheme", Icon = MaterialIcons.ContentCopy, Description = "Copy the dark scheme with every role as C#")]
    private void CopyDark() => CopyScheme(true);

    [RelayCommand]
    [property: Command("CopySeedCode", Group, Label = "Copy this code", Icon = MaterialIcons.ContentCopy, Description = "Copy the FromSeed code to the clipboard")]
    private void CopySeedCode()
    {
        Clipboard.SetText(SeedCode);
        Status = "Copied the FromSeed code";
    }

    /// <summary>The C# code that creates the light or dark scheme with every role as it is now.</summary>
    public string SchemeCode(bool dark)
    {
        var code = new StringBuilder("new MaterialColorScheme\n{\n");
        foreach (var role in Roles)
        {
            code.Append($"    {role.Property.Name} = Color.FromHex(\"{ColorPicker.ToHex(role.Get(dark))}\"),\n");
        }
        return code.Append('}').ToString();
    }

    /// <summary>The WCAG contrast ratio of two opaque colors, from 1 to 21.</summary>
    public static double Contrast(Color a, Color b)
    {
        static double Luminance(Color c)
        {
            static double Linear(byte v) => v / 255.0 <= 0.04045 ? v / 255.0 / 12.92 : Math.Pow((v / 255.0 + 0.055) / 1.055, 2.4);
            return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        }
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private void CopyScheme(bool dark)
    {
        Clipboard.SetText(SchemeCode(dark));
        Status = $"Copied the {(dark ? "dark" : "light")} scheme as C#";
    }

    private void Generate()
    {
        OnPropertyChanged(nameof(SeedCode));
        if (!_isSyncing) GalleryTheme.Generate(SeedColor, Variant);
    }

    private void OnGalleryThemeChanged()
    {
        _isSyncing = true;
        SeedColor = GalleryTheme.Seed;
        VariantIndex = (int)GalleryTheme.Variant;
        _isSyncing = false;
        Status = GalleryTheme.Description;
        IsDark = GalleryTheme.IsDark;
        foreach (var role in Roles) role.Refresh();
        OnPropertyChanged(nameof(EditedColor));
        OnPropertyChanged(nameof(EditedNote));
    }

    private void UpdateSelection()
    {
        foreach (var role in Roles)
        {
            role.IsLightSelected = role == SelectedRole && !IsEditingDark;
            role.IsDarkSelected = role == SelectedRole && IsEditingDark;
        }
    }

    private static List<ThemeRoleRow> CreateRoles()
    {
        static PropertyInfo P(string name) => typeof(MaterialColorScheme).GetProperty(name)!;
        var roles = new List<ThemeRoleRow>();
        void Add(string group, string name, string? background = null) =>
            roles.Add(new ThemeRoleRow(group, P(name), background == null ? null : P(background)));

        foreach (var accent in new[] { "Primary", "Secondary", "Tertiary" })
        {
            Add(accent, accent);
            Add(accent, "On" + accent, accent);
            Add(accent, accent + "Container");
            Add(accent, "On" + accent + "Container", accent + "Container");
        }
        Add("Error", "Error");
        Add("Error", "OnError", "Error");
        foreach (var surface in new[] { "Surface", "SurfaceDim", "SurfaceBright", "SurfaceContainerLowest", "SurfaceContainerLow", "SurfaceContainer", "SurfaceContainerHigh", "SurfaceContainerHighest" })
        {
            Add("Surfaces", surface);
        }
        Add("Surfaces", "OnSurface", "Surface");
        Add("Surfaces", "OnSurfaceVariant", "SurfaceContainerHighest");
        Add("Surfaces", "Background");
        Add("Surfaces", "OnBackground", "Background");
        Add("Outlines", "Outline", "Surface");
        Add("Outlines", "OutlineVariant", "Surface");
        Add("Inverse", "InverseSurface");
        Add("Inverse", "InverseOnSurface", "InverseSurface");
        Add("Inverse", "InversePrimary", "InverseSurface");
        Add("Effects", "Shadow");
        Add("Effects", "Scrim");

        // Every Color property must be editable; a role added to the scheme shows up in "Other" until grouped.
        foreach (var property in typeof(MaterialColorScheme).GetProperties().Where(p => p.PropertyType == typeof(Color)))
        {
            if (!roles.Any(r => r.Property == property)) roles.Add(new ThemeRoleRow("Other", property, null));
        }
        return roles;
    }
}
