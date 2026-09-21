using System;
using Atelier.Core.Primitives;

namespace Atelier.Theming.Material;

public class MaterialColorScheme
{
    public Color Primary { get; init; }
    public Color OnPrimary { get; init; }
    public Color PrimaryContainer { get; init; }
    public Color OnPrimaryContainer { get; init; }

    public Color Secondary { get; init; }
    public Color OnSecondary { get; init; }
    public Color SecondaryContainer { get; init; }
    public Color OnSecondaryContainer { get; init; }

    public Color Tertiary { get; init; }
    public Color OnTertiary { get; init; }
    public Color TertiaryContainer { get; init; }
    public Color OnTertiaryContainer { get; init; }

    public Color Surface { get; init; }
    public Color SurfaceDim { get; init; }
    public Color SurfaceBright { get; init; }
    public Color SurfaceContainerLowest { get; init; }
    public Color SurfaceContainerLow { get; init; }
    public Color SurfaceContainer { get; init; }
    public Color SurfaceContainerHigh { get; init; }
    public Color SurfaceContainerHighest { get; init; }
    public Color OnSurface { get; init; }
    public Color OnSurfaceVariant { get; init; }

    public Color Outline { get; init; }
    public Color OutlineVariant { get; init; }

    public Color Background { get; init; }
    public Color OnBackground { get; init; }

    public Color Error { get; init; }
    public Color OnError { get; init; }

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
        OnError = Color.FromHex("#FFFFFF")
    };

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
        OnError = Color.FromHex("#601410")
    };
}
