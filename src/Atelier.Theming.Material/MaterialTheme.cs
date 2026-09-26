using Atelier.Controls;
using Atelier.Layout;
using Atelier.Theming.Material.Renderers;

namespace Atelier.Theming.Material;

public class MaterialTheme : Theme
{
    public override string Name { get; }
    public override bool IsDark { get; }
    public MaterialColorScheme Colors { get; }

    public MaterialTheme(string name, bool isDark, MaterialColorScheme colors)
    {
        Name = name;
        IsDark = isDark;
        Colors = colors;

        // Register all control renderers for this theme
        Renderers.Register(new MaterialButtonRenderer(colors));
        Renderers.Register(new MaterialCheckBoxRenderer(colors));
        Renderers.Register(new MaterialRadioButtonRenderer(colors));
        Renderers.Register(new MaterialSwitchRenderer(colors));
        Renderers.Register(new MaterialTextBoxRenderer(colors));
        Renderers.Register(new MaterialSliderRenderer(colors));
        Renderers.Register(new MaterialProgressBarRenderer(colors));
        Renderers.Register(new MaterialTextBlockRenderer(colors));
        Renderers.Register(new MaterialBorderRenderer());
        Renderers.Register(new MaterialPanelRenderer());
        Renderers.Register(new MaterialCardRenderer(colors));
        Renderers.Register(new MaterialListBoxItemRenderer(colors));
        Renderers.Register(new MaterialScrollViewerRenderer(colors));
        Renderers.Register(new MaterialTitleBarRenderer(colors));
        Renderers.Register(new MaterialComboBoxRenderer(colors));
        Renderers.Register(new MaterialPopupRenderer(colors));
        Renderers.Register(new MaterialDialogRenderer(colors));
        Renderers.Register(new MaterialIconRenderer(colors));
        Renderers.Register(new MaterialTreeViewItemRenderer(colors));
        Renderers.Register(new MaterialImageRenderer());
        Renderers.Register(new MaterialToolbarRenderer(colors));

        // Register Material Design 3 Typography Styles globally
        MaterialTypography.RegisterStyles(Atelier.Core.Styling.StyleManager.GlobalStyles, colors);
    }

    public static MaterialTheme CreateLight() => new("Material 3 Light", false, MaterialColorScheme.Light());
    public static MaterialTheme CreateDark() => new("Material 3 Dark", true, MaterialColorScheme.Dark());
}
