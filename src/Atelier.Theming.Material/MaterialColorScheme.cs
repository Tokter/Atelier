using System;
using Atelier.Core.Primitives;

namespace Atelier.Theming.Material;

/// <summary>How colorful a scheme created by <see cref="MaterialColorScheme.FromSeed"/> is.</summary>
public enum MaterialSchemeVariant
{
    /// <summary>The MD3 default: a calm primary accent from the seed's hue, muted secondary and tertiary accents and nearly gray surfaces.</summary>
    TonalSpot,
    /// <summary>The most colorful primary the seed's hue allows, livelier accents and tinted surfaces.</summary>
    Vibrant,
    /// <summary>Keeps the seed's own colorfulness: vivid seeds give vivid schemes, muted seeds muted ones.</summary>
    Fidelity,
    /// <summary>Barely tinted: nearly gray accents and surfaces.</summary>
    Neutral,
    /// <summary>Grays only; the seed's hue is ignored.</summary>
    Monochrome,
}

/// <summary>
/// The Material Design 3 color roles of a theme. <see cref="Light"/> and <see cref="Dark"/> are the MD3 baseline
/// schemes (seed color #6750A4); create an instance with your own values for a custom scheme.
/// </summary>
/// <remarks>
/// "On" roles are the colors of content drawn on the matching role, e.g. <see cref="OnPrimary"/> text on a
/// <see cref="Primary"/> filled button. See https://m3.material.io/styles/color/roles.
/// </remarks>
public class MaterialColorScheme
{
    /// <summary>The main accent: filled buttons, active states, the selected value of controls.</summary>
    public Color Primary { get; init; }
    /// <summary>Content on <see cref="Primary"/>.</summary>
    public Color OnPrimary { get; init; }
    /// <summary>A less prominent accent container (e.g. FABs, selected chips).</summary>
    public Color PrimaryContainer { get; init; }
    /// <summary>Content on <see cref="PrimaryContainer"/>.</summary>
    public Color OnPrimaryContainer { get; init; }

    /// <summary>A secondary accent; also the color of focus rings.</summary>
    public Color Secondary { get; init; }
    /// <summary>Content on <see cref="Secondary"/>.</summary>
    public Color OnSecondary { get; init; }
    /// <summary>Tonal containers: tonal buttons, selected list items, checked toggle buttons.</summary>
    public Color SecondaryContainer { get; init; }
    /// <summary>Content on <see cref="SecondaryContainer"/>.</summary>
    public Color OnSecondaryContainer { get; init; }

    /// <summary>A contrasting accent for balance and emphasis.</summary>
    public Color Tertiary { get; init; }
    /// <summary>Content on <see cref="Tertiary"/>.</summary>
    public Color OnTertiary { get; init; }
    /// <summary>A contrasting accent container.</summary>
    public Color TertiaryContainer { get; init; }
    /// <summary>Content on <see cref="TertiaryContainer"/>.</summary>
    public Color OnTertiaryContainer { get; init; }

    /// <summary>The default background of windows and content.</summary>
    public Color Surface { get; init; }
    /// <summary>The dimmest surface.</summary>
    public Color SurfaceDim { get; init; }
    /// <summary>The brightest surface.</summary>
    public Color SurfaceBright { get; init; }
    /// <summary>The lowest-emphasis container.</summary>
    public Color SurfaceContainerLowest { get; init; }
    /// <summary>Elevated cards and buttons.</summary>
    public Color SurfaceContainerLow { get; init; }
    /// <summary>Menus, navigation areas, app bars.</summary>
    public Color SurfaceContainer { get; init; }
    /// <summary>Dialogs and other high-emphasis containers.</summary>
    public Color SurfaceContainerHigh { get; init; }
    /// <summary>Filled cards and fields, inactive tracks.</summary>
    public Color SurfaceContainerHighest { get; init; }
    /// <summary>Text and icons on surfaces.</summary>
    public Color OnSurface { get; init; }
    /// <summary>Lower-emphasis text and icons on surfaces (supporting text, labels, icons).</summary>
    public Color OnSurfaceVariant { get; init; }

    /// <summary>Important boundaries: outlined buttons and fields, unchecked selection controls.</summary>
    public Color Outline { get; init; }
    /// <summary>Decorative boundaries: dividers, outlined cards.</summary>
    public Color OutlineVariant { get; init; }

    /// <summary>The app background (the same as <see cref="Surface"/> in the baseline schemes).</summary>
    public Color Background { get; init; }
    /// <summary>Content on <see cref="Background"/>.</summary>
    public Color OnBackground { get; init; }

    /// <summary>Errors: invalid fields, destructive actions.</summary>
    public Color Error { get; init; }
    /// <summary>Content on <see cref="Error"/>.</summary>
    public Color OnError { get; init; }

    /// <summary>Surfaces that contrast with the rest of the UI, such as snackbars and tooltips.</summary>
    public Color InverseSurface { get; init; }
    /// <summary>Content on <see cref="InverseSurface"/>.</summary>
    public Color InverseOnSurface { get; init; }
    /// <summary>Accent on <see cref="InverseSurface"/>.</summary>
    public Color InversePrimary { get; init; }

    /// <summary>The color of elevation shadows.</summary>
    public Color Shadow { get; init; } = Color.Black;
    /// <summary>The color of the scrim behind modal content (drawn at 32% opacity).</summary>
    public Color Scrim { get; init; } = Color.Black;

    /// <summary>
    /// Creates a scheme from one accent color: tonal palettes for the primary, secondary and tertiary accents, the
    /// neutrals and errors (see <see cref="MaterialTonalPalette"/>), with each role at its MD3 tone, e.g. primary at
    /// tone 40 in light schemes and 80 in dark ones. "On" roles contrast with their role by at least 4.5:1.
    /// </summary>
    /// <param name="seed">The accent color; its hue colors the whole scheme, even the surfaces slightly.</param>
    /// <param name="isDark">Whether to create the dark scheme.</param>
    /// <param name="variant">How colorful the scheme is.</param>
    public static MaterialColorScheme FromSeed(Color seed, bool isDark, MaterialSchemeVariant variant = MaterialSchemeVariant.TonalSpot)
    {
        MaterialTonalPalette.ToLch(seed, out _, out double seedChroma, out double hue);
        (double primary, double secondary, double tertiary, double neutral, double neutralVariant) = variant switch
        {
            MaterialSchemeVariant.Vibrant => (200.0, 24.0, 32.0, 9.0, 12.0),
            MaterialSchemeVariant.Fidelity => (seedChroma, Math.Max(seedChroma - 32, seedChroma * 0.5), seedChroma * 0.6, seedChroma / 10, seedChroma / 10 + 3),
            MaterialSchemeVariant.Neutral => (14.0, 7.0, 12.0, 2.0, 3.0),
            MaterialSchemeVariant.Monochrome => (0.0, 0.0, 0.0, 0.0, 0.0),
            _ => (48.0, 14.0, 21.0, 4.5, 6.5),
        };
        var p = new MaterialTonalPalette(hue, primary);
        var s = new MaterialTonalPalette(hue, secondary);
        var t = new MaterialTonalPalette(hue + 55, tertiary);
        var n = new MaterialTonalPalette(hue, neutral);
        var nv = new MaterialTonalPalette(hue, neutralVariant);
        var e = MaterialTonalPalette.FromColor(Color.FromHex("#B3261E"));

        return isDark
            ? new MaterialColorScheme
            {
                Primary = p.Tone(80), OnPrimary = p.Tone(20), PrimaryContainer = p.Tone(30), OnPrimaryContainer = p.Tone(90),
                Secondary = s.Tone(80), OnSecondary = s.Tone(20), SecondaryContainer = s.Tone(30), OnSecondaryContainer = s.Tone(90),
                Tertiary = t.Tone(80), OnTertiary = t.Tone(20), TertiaryContainer = t.Tone(30), OnTertiaryContainer = t.Tone(90),
                Surface = n.Tone(6), SurfaceDim = n.Tone(6), SurfaceBright = n.Tone(24),
                SurfaceContainerLowest = n.Tone(4), SurfaceContainerLow = n.Tone(10), SurfaceContainer = n.Tone(12),
                SurfaceContainerHigh = n.Tone(17), SurfaceContainerHighest = n.Tone(22),
                OnSurface = n.Tone(90), OnSurfaceVariant = nv.Tone(80),
                Outline = nv.Tone(60), OutlineVariant = nv.Tone(30),
                Background = n.Tone(6), OnBackground = n.Tone(90),
                Error = e.Tone(80), OnError = e.Tone(20),
                InverseSurface = n.Tone(90), InverseOnSurface = n.Tone(20), InversePrimary = p.Tone(40),
            }
            : new MaterialColorScheme
            {
                Primary = p.Tone(40), OnPrimary = p.Tone(100), PrimaryContainer = p.Tone(90), OnPrimaryContainer = p.Tone(10),
                Secondary = s.Tone(40), OnSecondary = s.Tone(100), SecondaryContainer = s.Tone(90), OnSecondaryContainer = s.Tone(10),
                Tertiary = t.Tone(40), OnTertiary = t.Tone(100), TertiaryContainer = t.Tone(90), OnTertiaryContainer = t.Tone(10),
                Surface = n.Tone(98), SurfaceDim = n.Tone(87), SurfaceBright = n.Tone(98),
                SurfaceContainerLowest = n.Tone(100), SurfaceContainerLow = n.Tone(96), SurfaceContainer = n.Tone(94),
                SurfaceContainerHigh = n.Tone(92), SurfaceContainerHighest = n.Tone(90),
                OnSurface = n.Tone(10), OnSurfaceVariant = nv.Tone(30),
                Outline = nv.Tone(50), OutlineVariant = nv.Tone(80),
                Background = n.Tone(98), OnBackground = n.Tone(10),
                Error = e.Tone(40), OnError = e.Tone(100),
                InverseSurface = n.Tone(20), InverseOnSurface = n.Tone(95), InversePrimary = p.Tone(80),
            };
    }

    /// <summary>Creates a copy of the scheme.</summary>
    public MaterialColorScheme Clone() => (MaterialColorScheme)MemberwiseClone();

    /// <summary>The MD3 baseline light scheme.</summary>
    public static MaterialColorScheme Light() => new()
    {
        Primary = Color.FromHex("#6750A4"),
        OnPrimary = Color.FromHex("#FFFFFF"),
        PrimaryContainer = Color.FromHex("#EADDFF"),
        OnPrimaryContainer = Color.FromHex("#21005D"),

        Secondary = Color.FromHex("#625B71"),
        OnSecondary = Color.FromHex("#FFFFFF"),
        SecondaryContainer = Color.FromHex("#E8DEF8"),
        OnSecondaryContainer = Color.FromHex("#1D192B"),

        Tertiary = Color.FromHex("#7D5260"),
        OnTertiary = Color.FromHex("#FFFFFF"),
        TertiaryContainer = Color.FromHex("#FFD8E4"),
        OnTertiaryContainer = Color.FromHex("#31111D"),

        Surface = Color.FromHex("#FEF7FF"),
        SurfaceDim = Color.FromHex("#DED8E1"),
        SurfaceBright = Color.FromHex("#FEF7FF"),
        SurfaceContainerLowest = Color.FromHex("#FFFFFF"),
        SurfaceContainerLow = Color.FromHex("#F7F2FA"),
        SurfaceContainer = Color.FromHex("#F3EDF7"),
        SurfaceContainerHigh = Color.FromHex("#ECE6F0"),
        SurfaceContainerHighest = Color.FromHex("#E6E0E9"),
        OnSurface = Color.FromHex("#1D1B20"),
        OnSurfaceVariant = Color.FromHex("#49454F"),

        Outline = Color.FromHex("#79747E"),
        OutlineVariant = Color.FromHex("#CAC4D0"),

        Background = Color.FromHex("#FEF7FF"),
        OnBackground = Color.FromHex("#1D1B20"),

        Error = Color.FromHex("#B3261E"),
        OnError = Color.FromHex("#FFFFFF"),

        InverseSurface = Color.FromHex("#322F35"),
        InverseOnSurface = Color.FromHex("#F5EFF7"),
        InversePrimary = Color.FromHex("#D0BCFF"),
    };

    /// <summary>The MD3 baseline dark scheme.</summary>
    public static MaterialColorScheme Dark() => new()
    {
        Primary = Color.FromHex("#D0BCFF"),
        OnPrimary = Color.FromHex("#381E72"),
        PrimaryContainer = Color.FromHex("#4F378B"),
        OnPrimaryContainer = Color.FromHex("#EADDFF"),

        Secondary = Color.FromHex("#CCC2DC"),
        OnSecondary = Color.FromHex("#332D41"),
        SecondaryContainer = Color.FromHex("#4A4458"),
        OnSecondaryContainer = Color.FromHex("#E8DEF8"),

        Tertiary = Color.FromHex("#EFB8C8"),
        OnTertiary = Color.FromHex("#492532"),
        TertiaryContainer = Color.FromHex("#633B48"),
        OnTertiaryContainer = Color.FromHex("#FFD8E4"),

        Surface = Color.FromHex("#141218"),
        SurfaceDim = Color.FromHex("#141218"),
        SurfaceBright = Color.FromHex("#3B383E"),
        SurfaceContainerLowest = Color.FromHex("#0F0D13"),
        SurfaceContainerLow = Color.FromHex("#1D1B20"),
        SurfaceContainer = Color.FromHex("#211F26"),
        SurfaceContainerHigh = Color.FromHex("#2B2930"),
        SurfaceContainerHighest = Color.FromHex("#36343B"),
        OnSurface = Color.FromHex("#E6E0E9"),
        OnSurfaceVariant = Color.FromHex("#CAC4D0"),

        Outline = Color.FromHex("#938F99"),
        OutlineVariant = Color.FromHex("#49454F"),

        Background = Color.FromHex("#141218"),
        OnBackground = Color.FromHex("#E6E0E9"),

        Error = Color.FromHex("#F2B8B5"),
        OnError = Color.FromHex("#601410"),

        InverseSurface = Color.FromHex("#E6E0E9"),
        InverseOnSurface = Color.FromHex("#322F35"),
        InversePrimary = Color.FromHex("#6750A4"),
    };
}
