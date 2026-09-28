using Atelier.Controls;
using Atelier.Layout;
using Atelier.Theming.Material.Renderers;

namespace Atelier.Theming.Material;

/// <summary>
/// The Material Design 3 theme: renderers for every control, the default control styles
/// (<see cref="MaterialStyles"/>) and the type scale styles (<see cref="MaterialTypography"/>), for a color scheme.
/// </summary>
/// <remarks>
/// Creating a theme has no global effects; activate it with <c>ThemeManager.Current = MaterialTheme.CreateLight()</c>.
/// Switching between the light and dark theme restyles and redraws open windows. Controls are sized for the desktop by
/// default (<see cref="MaterialSizing.Desktop"/>); pass <see cref="MaterialSizing.Touch"/> for the MD3 touch sizes.
/// </remarks>
public class MaterialTheme : Theme
{
    /// <inheritdoc/>
    public override string Name { get; }

    /// <inheritdoc/>
    public override bool IsDark { get; }

    /// <summary>Gets the theme's color scheme.</summary>
    public MaterialColorScheme Colors { get; }

    /// <summary>Gets how large the theme makes controls.</summary>
    public MaterialSizing Sizing { get; }

    /// <summary>Initializes a Material theme for <paramref name="colors"/>.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="isDark">Whether the scheme is dark.</param>
    /// <param name="colors">The color scheme.</param>
    /// <param name="sizing">How large controls are; <c>null</c> for <see cref="MaterialSizing.Desktop"/>.</param>
    public MaterialTheme(string name, bool isDark, MaterialColorScheme colors, MaterialSizing? sizing = null)
    {
        Name = name;
        IsDark = isDark;
        Colors = colors;
        Sizing = sizing ?? MaterialSizing.Desktop;

        // Content colors (e.g. a filled button's label) are resolved through this registry.
        var renderers = Renderers;
        renderers.Register(new MaterialButtonRenderer(colors));
        renderers.Register(new MaterialToggleButtonRenderer(colors));
        renderers.Register(new MaterialCheckBoxRenderer(colors, Sizing));
        renderers.Register(new MaterialRadioButtonRenderer(colors, Sizing));
        renderers.Register(new MaterialSwitchRenderer(colors, Sizing));
        renderers.Register(new MaterialTextBoxRenderer(colors));
        renderers.Register(new MaterialSliderRenderer(colors, Sizing));
        renderers.Register(new MaterialColorSliderRenderer(colors));
        renderers.Register(new MaterialColorWheelRenderer(colors));
        renderers.Register(new MaterialColorSwatchRenderer(colors));
        renderers.Register(new MaterialCalendarDayRenderer(colors));
        renderers.Register(new MaterialCalendarYearRenderer(colors));
        renderers.Register(new MaterialCalendarMenuItemRenderer(colors));
        renderers.Register(new MaterialClockDialRenderer(colors));
        renderers.Register(new MaterialTimePickerSegmentRenderer(colors));
        renderers.Register(new MaterialTimePeriodSelectorRenderer(colors));
        renderers.Register(new MaterialBadgeRenderer(colors));
        renderers.Register(new MaterialTabControlRenderer(colors));
        renderers.Register(new MaterialTabItemRenderer(colors));
        renderers.Register(new MaterialProgressBarRenderer(colors));
        renderers.Register(new MaterialTextBlockRenderer(colors, renderers));
        renderers.Register(new MaterialBorderRenderer(colors));
        renderers.Register(new MaterialPanelRenderer());
        renderers.Register(new MaterialCardRenderer(colors));
        renderers.Register(new MaterialListBoxItemRenderer(colors));
        renderers.Register(new MaterialScrollViewerRenderer(colors));
        renderers.Register(new MaterialTitleBarRenderer(colors));
        renderers.Register(new MaterialComboBoxRenderer(colors));
        renderers.Register(new MaterialPopupRenderer(colors));
        renderers.Register(new MaterialToolTipRenderer(colors));
        renderers.Register(new MaterialGridSplitterRenderer(colors));
        renderers.Register(new MaterialDialogRenderer(colors));
        renderers.Register(new MaterialIconRenderer(colors, renderers));
        renderers.Register(new MaterialTreeViewItemRenderer(colors));
        renderers.Register(new MaterialImageRenderer());
        renderers.Register(new MaterialToolbarRenderer(colors));

        Styles.AddRange(MaterialStyles.CreateStyles(colors, Sizing));
        Styles.AddRange(MaterialTypography.CreateStyles());
    }

    /// <summary>Creates the MD3 baseline light theme (desktop sizing unless <paramref name="sizing"/> says otherwise).</summary>
    public static MaterialTheme CreateLight(MaterialSizing? sizing = null) => new("Material 3 Light", false, MaterialColorScheme.Light(), sizing);

    /// <summary>Creates the MD3 baseline dark theme (desktop sizing unless <paramref name="sizing"/> says otherwise).</summary>
    public static MaterialTheme CreateDark(MaterialSizing? sizing = null) => new("Material 3 Dark", true, MaterialColorScheme.Dark(), sizing);
}
