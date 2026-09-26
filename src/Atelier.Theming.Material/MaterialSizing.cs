using System;

namespace Atelier.Theming.Material;

/// <summary>
/// How large the Material theme makes controls: the MD3 density scale plus the size of the selection controls.
/// </summary>
/// <remarks>
/// <para>
/// MD3's component sizes are made for touch. For dense, pointer-driven UIs the spec defines a density scale from 0 (the
/// default) to -3, where each step removes 4 px of height from buttons, text fields, selects and list items and shrinks
/// the hover/press circle of selection controls (40 px at 0). Horizontal spacing is not affected. See
/// https://m3.material.io/foundations/layout/grids-spacing/density.
/// </para>
/// <para>
/// Density doesn't change the visible size of the switch (52×32 in MD3), which is large for a desktop app;
/// <see cref="DesktopSelectionControls"/> uses a desktop-sized switch (40×24) instead, which goes beyond the spec.
/// </para>
/// <para>
/// <see cref="Desktop"/> (the default of <see cref="MaterialTheme"/>) uses density -2 and desktop selection controls;
/// <see cref="Touch"/> uses the MD3 defaults.
/// </para>
/// </remarks>
public sealed record MaterialSizing
{
    private readonly int _density;

    /// <summary>Desktop sizing: density -2 (32 px buttons, 48 px fields, 40 px list rows) and a 40×24 switch.</summary>
    public static MaterialSizing Desktop { get; } = new() { Density = -2, DesktopSelectionControls = true };

    /// <summary>The MD3 default (touch) sizing: density 0 (40 px buttons, 56 px fields, 48 px list rows), 52×32 switch.</summary>
    public static MaterialSizing Touch { get; } = new() { Density = 0, DesktopSelectionControls = false };

    /// <summary>
    /// Gets the MD3 density: 0 (default) to -3 (most dense). Each step removes 4 px from component heights.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside -3..0.</exception>
    public int Density
    {
        get => _density;
        init
        {
            if (value is < -3 or > 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The Material density scale goes from -3 to 0.");
            }
            _density = value;
        }
    }

    /// <summary>Gets whether selection controls use desktop sizes (a 40×24 switch) instead of the MD3 touch sizes.</summary>
    public bool DesktopSelectionControls { get; init; }

    /// <summary>Applies the density to an MD3 component height: 4 px less per density step.</summary>
    /// <param name="defaultHeight">The height at density 0.</param>
    public float Height(float defaultHeight) => defaultHeight + 4f * Density;

    /// <summary>The diameter of the hover/press circle of check boxes, radio buttons, switches and slider handles.</summary>
    public float StateLayerSize => Height(40f);

    /// <summary>The switch track width: 40 px on the desktop, 52 px (MD3) otherwise.</summary>
    public float SwitchTrackWidth => DesktopSelectionControls ? 40f : 52f;

    /// <summary>The switch track height: 24 px on the desktop, 32 px (MD3) otherwise.</summary>
    public float SwitchTrackHeight => DesktopSelectionControls ? 24f : 32f;
}
