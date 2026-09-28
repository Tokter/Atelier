using System.Linq;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class ThemeEditorView : GalleryPage
{
    private const float CellWidth = 176;
    private readonly ThemeEditorViewModel _vm;

    public ThemeEditorView(ThemeEditorViewModel viewModel)
        : base(MaterialIconKind.FormatPaint, "Theme Editor",
            "Pick an accent color and the light and dark color schemes are generated from it, or fine-tune any of the " +
            "theme's color roles in either mode. The whole gallery, in every window, follows as you edit; copy the " +
            "result as C# when you like it.")
    {
        _vm = viewModel;

        Settings(
            new Switch("Dark mode").ShowThumbIcon().BindIsChecked(_vm, v => v.IsDark, (v, on) => v.IsDark = on),
            new Button().Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand),
            Ui.Readout(_vm, v => v.Status));

        Sections(AccentSection(), RolesSection(), CodeSection(), PreviewSection());
    }

    private UIElement AccentSection() => Ui.Section("Accent color",
        "The seed's hue colors the whole scheme: tonal palettes for the three accents, the neutrals and the surfaces, " +
        "with each role at its Material tone, so text keeps its contrast whatever the seed. The style decides how " +
        "colorful the result is. A new seed or style replaces fine-tuned roles.",
        Ui.Columns(300,
            Ui.Demo("Start from a preset",
                new WrapPanel().Spacing(8, 8).Children(_vm.Presets.Select(PresetButton).ToArray()),
                Ui.Labeled("Style", new ComboBox()
                    .Items(_vm.Variants.Cast<object>().ToArray())
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .MinWidth(180)
                    .BindSelectedIndex(_vm, v => v.VariantIndex, (v, i) => v.VariantIndex = i)),
                Ui.Note("Tonal spot is the Material default. Vibrant pushes the accents, Fidelity keeps the seed's own " +
                    "colorfulness, Neutral and Monochrome are nearly or fully gray.")),

            Ui.Demo("Or pick any seed",
                new ColorPicker()
                    .Mode(ColorPickerMode.Hsb)
                    .IsAlphaEnabled(false)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .BindColor(_vm, v => v.SeedColor, (v, color) => v.SeedColor = color)),

            Ui.Demo("Both schemes",
                SchemePreview(dark: false),
                SchemePreview(dark: true),
                Ui.Note("Only one mode is on screen at a time; these show the other one too."))));

    private Button PresetButton(AccentPreset preset) =>
        new Button()
            .Variant(ButtonVariant.Outlined)
            .Command(_vm.UsePresetCommand, preset)
            .Content(new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
                new ColorSwatch(preset.Color).Size(16, 16).CornerRadius(8).VerticalAlignment(VerticalAlignment.Center),
                new TextBlock(preset.Name).VerticalAlignment(VerticalAlignment.Center)));

    // A miniature of a scheme: its surface with a title, a filled button, a container and the accent swatches.
    private UIElement SchemePreview(bool dark)
    {
        ThemeRoleRow Role(string name) => _vm.Roles.First(r => r.Property.Name == name);
        Border Chip(string name) => new Border().Size(28, 28).CornerRadius(14)
            .Bind(Border.BackgroundProperty, Role(name), r => r.Get(dark));

        return new Border()
            .Padding(14)
            .CornerRadius(12)
            .BorderThickness(1)
            .Themed(Border.BorderBrushProperty, c => c.OutlineVariant)
            .Bind(Border.BackgroundProperty, Role("Surface"), r => r.Get(dark))
            .Child(new StackPanel().Spacing(10).Children(
                new TextBlock(dark ? "Dark" : "Light").TitleSmall()
                    .Bind(TextBlock.ForegroundProperty, Role("OnSurface"), r => r.Get(dark)),
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
                    new Border().Padding(16, 6).CornerRadius(16)
                        .Bind(Border.BackgroundProperty, Role("Primary"), r => r.Get(dark))
                        .Child(new TextBlock("Button").LabelLarge()
                            .Bind(TextBlock.ForegroundProperty, Role("OnPrimary"), r => r.Get(dark))),
                    new Border().Padding(16, 6).CornerRadius(16)
                        .Bind(Border.BackgroundProperty, Role("SecondaryContainer"), r => r.Get(dark))
                        .Child(new TextBlock("Tonal").LabelLarge()
                            .Bind(TextBlock.ForegroundProperty, Role("OnSecondaryContainer"), r => r.Get(dark)))),
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(6).Children(
                    Chip("Primary"), Chip("Secondary"), Chip("Tertiary"), Chip("PrimaryContainer"),
                    Chip("TertiaryContainer"), Chip("SurfaceContainerHighest"), Chip("Error"))));
    }

    private UIElement RolesSection() => Ui.Section("Fine-tune every role",
        "Click a light or dark color to edit it with the picker. Content colors (\"on\" roles, outlines) show a " +
        "sample on the color they're drawn on and the contrast ratio: 4.5:1 is the minimum for text, 3:1 for " +
        "boundaries.",
        new Grid()
            .Columns(GridLength.Star, GridLength.Auto)
            .ColumnSpacing(24)
            .Children(
                new StackPanel().Spacing(4).Children(
                    RoleHeader(),
                    new ScrollViewer().Height(520).Content(RoleList())),
                new StackPanel().Spacing(12).Column(1).Children(
                    new TextBlock().TitleMedium().Bind(TextBlock.TextProperty, _vm, v => v.EditedTitle),
                    new ColorPicker()
                        .Mode(ColorPickerMode.Hsb)
                        .IsAlphaEnabled(false)
                        .BindColor(_vm, v => v.EditedColor, (v, color) => v.EditedColor = color),
                    new TextBlock().BodySmall().Muted().TextWrapping().MaxWidth(300)
                        .Bind(TextBlock.TextProperty, _vm, v => v.EditedNote))));

    private static Grid RoleHeader() =>
        new Grid()
            .Columns(GridLength.Star, GridLength.Pixels(CellWidth), GridLength.Pixels(CellWidth))
            .Margin(0, 0, 16, 0)
            .Children(
                new TextBlock("Role").LabelLarge().Muted(),
                new TextBlock("Light").LabelLarge().Muted().Column(1),
                new TextBlock("Dark").LabelLarge().Muted().Column(2));

    private StackPanel RoleList()
    {
        var list = new StackPanel().Spacing(2).Margin(0, 0, 16, 0);
        foreach (var group in _vm.Roles.GroupBy(r => r.Group))
        {
            list.Children(new TextBlock(group.Key).TitleSmall().Margin(0, list.Children.Count == 0 ? 4 : 14, 0, 4)
                .Themed(TextBlock.ForegroundProperty, c => c.Primary));
            foreach (var role in group)
            {
                list.Children(new Grid()
                    .Columns(GridLength.Star, GridLength.Pixels(CellWidth), GridLength.Pixels(CellWidth))
                    .Children(
                        new TextBlock(role.Label).BodyMedium().VerticalAlignment(VerticalAlignment.Center),
                        RoleCell(role, dark: false).Column(1),
                        RoleCell(role, dark: true).Column(2)));
            }
        }
        return list;
    }

    // A role's color in one mode: a swatch (or "Aa" on its background for content colors) and its hex value.
    private Button RoleCell(ThemeRoleRow role, bool dark)
    {
        UIElement sample = role.IsContentColor
            ? new Border().Size(40, 26).CornerRadius(6).BorderThickness(1)
                .Themed(Border.BorderBrushProperty, c => c.OutlineVariant)
                .Bind(Border.BackgroundProperty, role, r => r.BackgroundOf(dark))
                .Child(new TextBlock("Aa").LabelLarge().Center()
                    .Bind(TextBlock.ForegroundProperty, role, r => r.Get(dark)))
            : new ColorSwatch().Size(40, 26).CornerRadius(6).BindColor(role, r => r.Get(dark));

        return new Button()
            .Padding(6, 4)
            .MinWidth(0)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Bind(Button.VariantProperty, role, r => (dark ? r.IsDarkSelected : r.IsLightSelected) ? ButtonVariant.Tonal : ButtonVariant.Text)
            .OnClick(() => _vm.Select(role, dark))
            .Content(new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
                sample.VerticalAlignment(VerticalAlignment.Center),
                new TextBlock().FontFamily(Ui.MonospaceFont).FontSize(12).VerticalAlignment(VerticalAlignment.Center)
                    .Bind(TextBlock.TextProperty, role, r => dark ? r.DarkText : r.LightText)));
    }

    private UIElement CodeSection() => Ui.Section("Use it in code",
        "A generated scheme is one call away. After fine-tuning, copy the full scheme with every role instead.",
        Ui.Demo("Generated from the seed",
            new Border()
                .Padding(14, 10)
                .CornerRadius(8)
                .Themed(Border.BackgroundProperty, c => c.SurfaceContainerHigh)
                .Child(new TextBlock()
                    .FontFamily(Ui.MonospaceFont)
                    .FontSize(12.5f)
                    .LineHeight(18)
                    .TextWrapping()
                    .Themed(TextBlock.ForegroundProperty, c => c.OnSurfaceVariant)
                    .Bind(TextBlock.TextProperty, _vm, v => v.SeedCode)),
            Ui.Row(
                new Button().Variant(ButtonVariant.Tonal).Command(_vm.CopySeedCodeCommand),
                new Button().Variant(ButtonVariant.Outlined).Command(_vm.CopyLightCommand),
                new Button().Variant(ButtonVariant.Outlined).Command(_vm.CopyDarkCommand))));

    private UIElement PreviewSection() => Ui.Section("Preview",
        "A few controls in the current theme; every other page of the gallery shows it too.",
        Ui.Columns(260,
            Ui.Demo("Buttons",
                Ui.Row(
                    new Button("Filled"),
                    new Button("Tonal").Variant(ButtonVariant.Tonal),
                    new Button("Outlined").Variant(ButtonVariant.Outlined),
                    new Button("Elevated").Variant(ButtonVariant.Elevated),
                    new Button("Text").Variant(ButtonVariant.Text))),
            Ui.Demo("Selection",
                Ui.Row(new Switch().IsChecked(), new Switch(), new CheckBox("Checked").IsChecked(), new CheckBox("Unchecked")),
                Ui.Row(new RadioButton("One").GroupName("theme-preview").IsChecked(), new RadioButton("Two").GroupName("theme-preview"))),
            Ui.Demo("Input and progress",
                new TextBox().Label("Name").Placeholder("Type here").MinWidth(200),
                new Slider().Value(40).MinWidth(200),
                new ProgressBar().Value(65).Width(200))));
}
