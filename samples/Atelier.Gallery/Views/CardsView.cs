using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class CardsView : GalleryPage
{
    private readonly CardsViewModel _vm;

    public CardsView(CardsViewModel viewModel)
        : base(MaterialIconKind.Dashboard, "Cards",
            "Cards group related content and actions on a surface. The variant sets the look; elevation, corner radius, " +
            "padding and colors can be changed per card. A Card is a Border, so everything shown here works on borders too.")
    {
        _vm = viewModel;

        Settings(new Button().Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        Sections(VariantsSection(), CustomizationSection(), MediaSection(), InteractiveSection(), PlaygroundSection(), BorderSection());
    }

    private UIElement VariantsSection() => Ui.Section("Variants",
        "Material 3 defines three card styles. Outlined cards separate content with a border, elevated cards with a shadow " +
        "and filled cards with a tinted surface.",
        Ui.Columns(220,
            VariantCard(CardVariant.Outlined, MaterialIconKind.CropSquare, "Outlined", "A surface with an outline and no shadow."),
            VariantCard(CardVariant.Elevated, MaterialIconKind.Layers, "Elevated", "A low surface container with a shadow."),
            VariantCard(CardVariant.Filled, MaterialIconKind.FormatColorFill, "Filled", "The highest surface container, no outline.")),
        Ui.Code("new Card(CardVariant.Elevated).Child(content)   // 16 px padding and 12 px corners by default"));

    private static Card VariantCard(CardVariant variant, MaterialIconKind icon, string title, string text) =>
        new Card(variant).Child(new StackPanel().Spacing(8).Children(
            new Icon(icon, 24).HorizontalAlignment(HorizontalAlignment.Left).Themed(Control.ForegroundProperty, c => c.Primary),
            new TextBlock(title).TitleMedium(),
            new TextBlock(text).BodyMedium().Muted().TextWrapping()));

    private UIElement CustomizationSection() => Ui.Section("Elevation, shape and color",
        "Elevation adds a shadow to any variant. Corner radius, background, border brush and border thickness override the " +
        "variant's values.",
        Ui.Demo("Elevation",
            Ui.Row(
                SampleCard(CardVariant.Outlined, "Elevation 0"),
                SampleCard(CardVariant.Outlined, "Elevation 1").Elevation(1),
                SampleCard(CardVariant.Outlined, "Elevation 3").Elevation(3),
                SampleCard(CardVariant.Outlined, "Elevation 6").Elevation(6))),
        Ui.Demo("Corner radius",
            Ui.Row(
                SampleCard(CardVariant.Filled, "0 px").CornerRadius(0),
                SampleCard(CardVariant.Filled, "12 px (default)"),
                SampleCard(CardVariant.Filled, "28 px").CornerRadius(28),
                SampleCard(CardVariant.Filled, "Per corner").CornerRadius(24, 4, 24, 4))),
        Ui.Demo("Colors and borders",
            Ui.Row(
                SampleCard(CardVariant.Filled, "Primary container")
                    .Themed(Border.BackgroundProperty, c => c.PrimaryContainer),
                SampleCard(CardVariant.Filled, "Tertiary container")
                    .Themed(Border.BackgroundProperty, c => c.TertiaryContainer),
                SampleCard(CardVariant.Outlined, "Primary outline, 2 px")
                    .BorderThickness(2)
                    .Themed(Border.BorderBrushProperty, c => c.Primary),
                SampleCard(CardVariant.Outlined, "Left accent bar")
                    .BorderThickness(new Thickness(6, 1, 1, 1))
                    .Themed(Border.BorderBrushProperty, c => c.Tertiary))));

    private static Card SampleCard(CardVariant variant, string text) =>
        new Card(variant).Width(170).Height(88).Child(new TextBlock(text).BodyMedium().TextWrapping());

    private UIElement MediaSection() => Ui.Section("Media and clipping",
        "Cards clip their content to their rounded shape (ClipToBounds is on by default), so with no padding, images and " +
        "headers can reach the edges.",
        Ui.Columns(260,
            MediaCard("Clipped (default)", "The image follows the rounded corners.", clip: true),
            MediaCard("ClipToBounds(false)", "Without clipping, the image corners stick out.", clip: false),
            new Card(CardVariant.Outlined).Padding(0).Child(new StackPanel().Children(
                new Border()
                    .Height(72)
                    .Padding(16)
                    .Themed(Border.BackgroundProperty, c => c.SecondaryContainer)
                    .Child(new TextBlock("Header band").TitleMedium().VerticalAlignment(VerticalAlignment.Center)
                        .Themed(TextBlock.ForegroundProperty, c => c.OnSecondaryContainer)),
                new TextBlock("A colored header that follows the card's top corners.")
                    .BodyMedium().Muted().TextWrapping().Margin(16)))));

    private static Card MediaCard(string title, string text, bool clip) =>
        new Card(CardVariant.Elevated).Padding(0).CornerRadius(24).ClipToBounds(clip).Child(new StackPanel().Children(
            new Image().Source(SampleImages.Logo).Stretch(Stretch.UniformToFill).Height(120),
            new StackPanel().Spacing(4).Margin(16).Children(
                new TextBlock(title).TitleMedium(),
                new TextBlock(text).BodyMedium().Muted().TextWrapping())));

    private UIElement InteractiveSection() => Ui.Section("Interactive cards",
        "Cards are regular elements: handle pointer events to make the whole card clickable, or place buttons inside for " +
        "explicit actions.",
        Ui.Columns(300,
            Ui.Demo("Whole card clickable (pointer events)",
                new Card(CardVariant.Filled)
                    .OnPointerPressed((_, _) => _vm.CardClickCommand.Execute(null))
                    .OnPointerEntered((_, _) => _vm.HoverState = "Pointer over the card")
                    .OnPointerExited((_, _) => _vm.HoverState = "Pointer outside")
                    .Child(new StackPanel().Spacing(8).Children(
                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(
                            new Icon(MaterialIconKind.TouchApp, 28).Themed(Control.ForegroundProperty, c => c.Primary),
                            new TextBlock("Click anywhere on this card").TitleMedium().VerticalAlignment(VerticalAlignment.Center)),
                        Ui.Row(
                            Ui.Readout(_vm, v => $"Clicks = {v.CardClicks}"),
                            Ui.Readout(_vm, v => v.HoverState))))),

            Ui.Demo("Card with actions",
                new Card(CardVariant.Outlined).Child(new StackPanel().Spacing(12).Children(
                    new TextBlock("Mountain cabin").TitleMedium(),
                    new TextBlock("Two nights, breakfast included. Free cancellation until Friday.").BodyMedium().Muted().TextWrapping(),
                    Ui.Row(
                        new Button().Command(_vm.CardClickCommand),
                        new Button()
                            .Variant(ButtonVariant.Text)
                            .Command(_vm.ToggleFavoriteCommand)
                            .Content(new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
                                new Icon(MaterialIconKind.Favorite, 18)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .BindFill(_vm, v => v.IsFavorite ? 1f : 0f),
                                new TextBlock().VerticalAlignment(VerticalAlignment.Center)
                                    .BindText(_vm, v => v.IsFavorite ? "Saved" : "Save")))))))));

    private UIElement PlaygroundSection() => Ui.Section("Playground",
        "Change the card's properties; the preview is bound to the same view model values.",
        Ui.Columns(300,
            Ui.Stack(
                Ui.Labeled("Variant", Ui.Row(
                    VariantOption("Outlined", CardVariant.Outlined),
                    VariantOption("Elevated", CardVariant.Elevated),
                    VariantOption("Filled", CardVariant.Filled))),
                Ui.SliderSetting("Elevation", _vm, v => v.Elevation, (v, x) => v.Elevation = x, 0, 12),
                Ui.SliderSetting("Corner radius", _vm, v => v.CornerRadius, (v, x) => v.CornerRadius = x, 0, 40),
                Ui.SliderSetting("Padding", _vm, v => v.Padding, (v, x) => v.Padding = x, 0, 48)),
            new Border().Padding(24).Child(
                new Card()
                    .BindVariant(_vm, v => v.Variant)
                    .BindElevation(_vm, v => v.Elevation)
                    .BindCornerRadius(_vm, v => v.CornerRadius)
                    .BindPadding(_vm, v => v.Padding)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Child(new StackPanel().Spacing(8).Children(
                        new TextBlock("Preview").TitleLarge(),
                        new TextBlock("This card follows the settings on the left.").BodyMedium().Muted().TextWrapping(),
                        Ui.Readout(_vm, v => $"{v.Variant}, elevation {v.Elevation:0}, radius {v.CornerRadius:0}, padding {v.Padding:0}"))))),
        Ui.Code("new Card()\n" +
                "    .BindVariant(vm, v => v.Variant)\n" +
                "    .BindElevation(vm, v => v.Elevation)\n" +
                "    .BindCornerRadius(vm, v => v.CornerRadius)   // float: the same radius on all corners\n" +
                "    .BindPadding(vm, v => v.Padding)"));

    private RadioButton VariantOption(string text, CardVariant variant) =>
        new RadioButton(text).GroupName("card-variant").BindIsChecked(_vm, v => v.Variant, (v, x) => v.Variant = x, variant);

    private static UIElement BorderSection() => Ui.Section("Border",
        "The plain Border draws exactly what you set: background, border brush, a thickness per side, a radius per corner, " +
        "padding and a shadow through Elevation. It has no theme style of its own.",
        Ui.Row(
            DemoBorder("Background + radius")
                .CornerRadius(12)
                .Themed(Border.BackgroundProperty, c => c.SurfaceContainerHighest),
            DemoBorder("Bottom border only")
                .BorderThickness(new Thickness(0, 0, 0, 3))
                .Themed(Border.BorderBrushProperty, c => c.Primary),
            DemoBorder("Stroke + per-corner radius")
                .Stroke(ThemeColors.Current.Outline, 1.5f)
                .CornerRadius(0, 20, 0, 20)
                .Themed(Border.BorderBrushProperty, c => c.Outline),
            DemoBorder("Elevation 4")
                .CornerRadius(8)
                .Elevation(4)
                .Themed(Border.BackgroundProperty, c => c.SurfaceContainerLow)),
        Ui.Code("new Border()\n    .Padding(16).CornerRadius(0, 20, 0, 20)\n    .Stroke(color, 1.5f)          // BorderBrush + BorderThickness\n    .Child(content)"));

    private static Border DemoBorder(string text) =>
        new Border().Padding(16).Width(170).Height(80).Child(new TextBlock(text).BodyMedium().TextWrapping());
}
