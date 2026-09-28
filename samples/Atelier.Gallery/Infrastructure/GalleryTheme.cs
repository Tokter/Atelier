using System;
using System.Reflection;
using Atelier.Core.Primitives;
using Atelier.Core.Threading;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Gallery.Infrastructure;

/// <summary>
/// The gallery's light and dark color schemes (the MD3 baselines until the Theme Editor changes them) and which one is
/// active. The theme is global, so every window shows the same schemes and follows a switch made in any window.
/// </summary>
public static class GalleryTheme
{
    private static MaterialColorScheme s_light = MaterialColorScheme.Light();
    private static MaterialColorScheme s_dark = MaterialColorScheme.Dark();
    private static bool s_isDark;
    private static bool s_applyPending;

    /// <summary>Occurs when a scheme or the mode changed (the theme itself follows within the frame).</summary>
    public static event Action? Changed;

    /// <summary>Gets the light scheme. Don't change it: use <see cref="Generate"/> or <see cref="SetRole"/>.</summary>
    public static MaterialColorScheme Light => s_light;

    /// <summary>Gets the dark scheme. Don't change it: use <see cref="Generate"/> or <see cref="SetRole"/>.</summary>
    public static MaterialColorScheme Dark => s_dark;

    /// <summary>Gets the light or the dark scheme.</summary>
    public static MaterialColorScheme Scheme(bool dark) => dark ? s_dark : s_light;

    /// <summary>Gets or sets whether the dark scheme is active; setting it switches the theme.</summary>
    public static bool IsDark
    {
        get => s_isDark;
        set
        {
            if (s_isDark == value && ThemeManager.HasTheme) return;
            s_isDark = value;
            Apply();
            Changed?.Invoke();
        }
    }

    /// <summary>Switches between the light and the dark scheme.</summary>
    public static void Toggle() => IsDark = !IsDark;

    /// <summary>The accent color of the MD3 baseline schemes.</summary>
    public static readonly Color BaselineSeed = Color.FromHex("#6750A4");

    /// <summary>Gets the accent color the schemes were last generated from (<see cref="BaselineSeed"/> at first).</summary>
    public static Color Seed { get; private set; } = BaselineSeed;

    /// <summary>Gets the style the schemes were last generated with.</summary>
    public static MaterialSchemeVariant Variant { get; private set; }

    /// <summary>Gets where the schemes come from, e.g. "Generated from #006A6A (TonalSpot)".</summary>
    public static string Description { get; private set; } = "The MD3 baseline schemes";

    /// <summary>Generates both schemes from <paramref name="seed"/> (see <see cref="MaterialColorScheme.FromSeed"/>).</summary>
    public static void Generate(Color seed, MaterialSchemeVariant variant)
    {
        Seed = seed;
        Variant = variant;
        Description = $"Generated from #{seed.R:X2}{seed.G:X2}{seed.B:X2} ({variant})";
        SetSchemes(MaterialColorScheme.FromSeed(seed, isDark: false, variant), MaterialColorScheme.FromSeed(seed, isDark: true, variant));
    }

    /// <summary>Goes back to the MD3 baseline schemes.</summary>
    public static void Reset()
    {
        Seed = BaselineSeed;
        Variant = MaterialSchemeVariant.TonalSpot;
        Description = "The MD3 baseline schemes";
        SetSchemes(MaterialColorScheme.Light(), MaterialColorScheme.Dark());
    }

    private static void SetSchemes(MaterialColorScheme light, MaterialColorScheme dark)
    {
        s_light = light;
        s_dark = dark;
        ScheduleApply();
        Changed?.Invoke();
    }

    /// <summary>Sets one color role (a <see cref="Color"/> property of <see cref="MaterialColorScheme"/>) of the light or dark scheme.</summary>
    public static void SetRole(bool dark, PropertyInfo role, Color color)
    {
        if ((Color)role.GetValue(Scheme(dark))! == color) return;
        var scheme = Scheme(dark).Clone();
        role.SetValue(scheme, color); // init-only setters can be called by reflection on a fresh copy
        if (dark) s_dark = scheme;
        else s_light = scheme;
        Description = "Fine-tuned";
        if (dark == s_isDark) ScheduleApply();
        Changed?.Invoke();
    }

    /// <summary>Makes the active scheme the theme now.</summary>
    public static void Apply()
    {
        s_applyPending = false;
        ThemeManager.Current = new MaterialTheme(s_isDark ? "Gallery Dark" : "Gallery Light", s_isDark, Scheme(s_isDark));
    }

    // Edits while dragging a color come faster than frames: rebuild the theme once, before the next frame.
    private static void ScheduleApply()
    {
        if (s_applyPending) return;
        s_applyPending = true;
        Dispatcher.Post(() =>
        {
            if (s_applyPending) Apply();
        });
    }
}
