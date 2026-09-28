using System;
using System.ComponentModel;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming.Material;

namespace Atelier.Gallery.Views;

public class TypographyView : GalleryPage
{
    private const string Pangram = "The quick brown fox jumps over the lazy dog.";
    private const string LongText = "Typography is the art of arranging type to make written language legible, readable and appealing when displayed.";

    private readonly TypographyViewModel _vm;

    public TypographyView(TypographyViewModel viewModel)
        : base(MaterialIconKind.TextFields, "Typography",
            "TextBlock shows text with the Material 3 type scale or your own font settings: size, family, weight, style, " +
            "color, alignment, wrapping, trimming and line height.")
    {
        _vm = viewModel;

        Settings(
            new Button().Variant(ButtonVariant.Tonal).Command(_vm.ToggleBoldCommand),
            new Button().Variant(ButtonVariant.Tonal).Command(_vm.ToggleItalicCommand),
            new Button().Variant(ButtonVariant.Outlined).Command(_vm.ResetCommand));

        Sections(PlaygroundSection(), TypeScaleSection(), FontSection(), StyleAndColorSection(), LayoutSection());
    }

    #region Playground

    private UIElement PlaygroundSection()
    {
        var preview = new TextBlock()
            .BindText(_vm, v => v.SampleText)
            .BindStyleKey(_vm, v => v.EffectiveStyleKey)
            .BindFontFamily(_vm, v => v.EffectiveFontFamily)
            .BindBold(_vm, v => v.IsBold)
            .BindItalic(_vm, v => v.IsItalic)
            .BindMuted(_vm, v => v.IsMuted)
            .BindTextAlignment(_vm, v => v.TextAlignment)
            .BindTextWrapping(_vm, v => v.IsWrapping ? TextWrapping.Wrap : TextWrapping.NoWrap)
            .Bind(TextBlock.TextTrimmingProperty, _vm, v => v.Trimming)
            .Bind(TextBlock.MaxLinesProperty, _vm, v => (int)MathF.Round(v.MaxLines))
            .Bind(TextBlock.LineHeightProperty, _vm, v => MathF.Round(v.LineHeight))
            // "Theme default" clears the local color, so the text keeps the theme's text color.
            .BindColorRole(TextBlock.ForegroundProperty, _vm, nameof(TypographyViewModel.ColorRole), v => v.ColorRole);
        FollowSizeOverride(preview);

        return Ui.Section("Playground",
            "Pick a type scale style, then override its values locally: local values win over the style, and switching the " +
            "override off lets the style decide again.",
            Ui.Columns(340,
                Ui.Stack(
                    new TextBox().Label("Sample text").BindText(_vm, v => v.SampleText, (v, text) => v.SampleText = text),
                    Ui.Columns(160,
                        Ui.Labeled("Style", new ComboBox()
                            .ItemsSource(TypographyViewModel.StyleKeys)
                            .BindSelectedItem(_vm, v => v.StyleKey, (v, key) => v.StyleKey = key ?? TypographyViewModel.StyleKeys[0])),
                        Ui.Labeled("Font family", new ComboBox()
                            .ItemsSource(TypographyViewModel.Fonts)
                            .BindSelectedItem(_vm, v => v.FontFamily, (v, font) => v.FontFamily = font ?? TypographyViewModel.DefaultFontLabel))),
                    new Switch("Override size and weight").BindIsChecked(_vm, v => v.OverrideSizeAndWeight, (v, on) => v.OverrideSizeAndWeight = on),
                    Ui.Columns(160,
                        Ui.SliderSetting("Font size", _vm, v => v.FontSize, (v, x) => v.FontSize = x, 10, 72),
                        Ui.SliderSetting("Font weight", _vm, v => v.FontWeight, (v, x) => v.FontWeight = x, 100, 900, step: 100))
                        .BindIsEnabled(_vm, v => v.OverrideSizeAndWeight),
                    Ui.Row(
                        new CheckBox("Bold").BindIsChecked(_vm, v => v.IsBold, (v, on) => v.IsBold = on),
                        new CheckBox("Italic").BindIsChecked(_vm, v => v.IsItalic, (v, on) => v.IsItalic = on),
                        new CheckBox("Muted").BindIsChecked(_vm, v => v.IsMuted, (v, on) => v.IsMuted = on),
                        new CheckBox("Wrap").BindIsChecked(_vm, v => v.IsWrapping, (v, on) => v.IsWrapping = on)),
                    Ui.Labeled("Color", Ui.Row(Array.ConvertAll(Enum.GetValues<ColorRole>(), role =>
                        (UIElement)new RadioButton(ColorRoleBinding.DisplayName(role)).GroupName("type-color")
                            .BindIsChecked(_vm, v => v.ColorRole, (v, r) => v.ColorRole = r, role)))),
                    Ui.Labeled("Alignment", Ui.Row(Array.ConvertAll(Enum.GetValues<TextAlignment>(), alignment =>
                        (UIElement)new RadioButton(alignment.ToString()).GroupName("type-align")
                            .BindIsChecked(_vm, v => v.TextAlignment, (v, a) => v.TextAlignment = a, alignment)))),
                    Ui.Labeled("Trimming", Ui.Row(Array.ConvertAll(Enum.GetValues<TextTrimming>(), trimming =>
                        (UIElement)new RadioButton(trimming.ToString()).GroupName("type-trim")
                            .BindIsChecked(_vm, v => v.Trimming, (v, t) => v.Trimming = t, trimming)))),
                    Ui.Columns(160,
                        Ui.SliderSetting("Max lines (0 = no limit)", _vm, v => v.MaxLines, (v, x) => v.MaxLines = x, 0, 5, step: 1),
                        Ui.SliderSetting("Line height (0 = font)", _vm, v => v.LineHeight, (v, x) => v.LineHeight = x, 0, 80))),
                Ui.Stack(
                    new Border()
                        .Padding(24)
                        .MinHeight(240)
                        .CornerRadius(16)
                        .ClipToBounds()
                        .Themed(Border.BackgroundProperty, c => c.SurfaceContainerLow)
                        .Child(preview),
                    Ui.Readout(_vm, v => $"StyleKey = {v.EffectiveStyleKey ?? "none"}, " +
                                         (v.OverrideSizeAndWeight ? $"FontSize = {v.FontSize:0}, FontWeight = {v.EffectiveFontWeight}" : "size and weight from the style")))));
    }

    // Size and weight are local values only while overriding; otherwise they're cleared so the style applies.
    private void FollowSizeOverride(TextBlock text)
    {
        void Apply()
        {
            if (_vm.OverrideSizeAndWeight)
            {
                text.FontSize = _vm.FontSize;
                text.FontWeight = _vm.EffectiveFontWeight;
            }
            else
            {
                text.ClearValue(TextBlock.FontSizeProperty);
                text.ClearValue(TextBlock.FontWeightProperty);
            }
        }

        void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(TypographyViewModel.OverrideSizeAndWeight) or nameof(TypographyViewModel.FontSize)
                or nameof(TypographyViewModel.FontWeight))
            {
                Apply();
            }
        }

        Apply();
        text.OnAttachedToVisualTree(() => { _vm.PropertyChanged += OnChanged; Apply(); });
        text.OnDetachedFromVisualTree(() => _vm.PropertyChanged -= OnChanged);
    }

    #endregion

    #region Type scale

    private static UIElement TypeScaleSection() => Ui.Section("Type scale",
        "The Material 3 type scale as keyed styles. Apply one with its method (e.g. .TitleLarge()) or with " +
        "StyleKey(MaterialTypography.TitleLargeKey). Sizes are font size / line height in pixels.",
        ScaleGroup("Display",
            ScaleRow(new TextBlock("Display Large").DisplayLarge(), "DisplayLarge", MaterialTypescale.DisplayLarge),
            ScaleRow(new TextBlock("Display Medium").DisplayMedium(), "DisplayMedium", MaterialTypescale.DisplayMedium),
            ScaleRow(new TextBlock("Display Small").DisplaySmall(), "DisplaySmall", MaterialTypescale.DisplaySmall)),
        ScaleGroup("Headline",
            ScaleRow(new TextBlock("Headline Large").HeadlineLarge(), "HeadlineLarge", MaterialTypescale.HeadlineLarge),
            ScaleRow(new TextBlock("Headline Medium").HeadlineMedium(), "HeadlineMedium", MaterialTypescale.HeadlineMedium),
            ScaleRow(new TextBlock("Headline Small").HeadlineSmall(), "HeadlineSmall", MaterialTypescale.HeadlineSmall)),
        ScaleGroup("Title",
            ScaleRow(new TextBlock("Title Large").TitleLarge(), "TitleLarge", MaterialTypescale.TitleLarge),
            ScaleRow(new TextBlock("Title Medium").TitleMedium(), "TitleMedium", MaterialTypescale.TitleMedium),
            ScaleRow(new TextBlock("Title Small").TitleSmall(), "TitleSmall", MaterialTypescale.TitleSmall)),
        ScaleGroup("Body",
            ScaleRow(new TextBlock("Body Large: " + Pangram).BodyLarge().TextWrapping(), "BodyLarge", MaterialTypescale.BodyLarge),
            ScaleRow(new TextBlock("Body Medium: " + Pangram).BodyMedium().TextWrapping(), "BodyMedium", MaterialTypescale.BodyMedium),
            ScaleRow(new TextBlock("Body Small: " + Pangram).BodySmall().TextWrapping(), "BodySmall", MaterialTypescale.BodySmall)),
        ScaleGroup("Label",
            ScaleRow(new TextBlock("Label Large").LabelLarge(), "LabelLarge", MaterialTypescale.LabelLarge),
            ScaleRow(new TextBlock("Label Medium").LabelMedium(), "LabelMedium", MaterialTypescale.LabelMedium),
            ScaleRow(new TextBlock("Label Small").LabelSmall(), "LabelSmall", MaterialTypescale.LabelSmall)),
        ScaleGroup("Aliases",
            AliasRow(new TextBlock("Heading 1").Heading1(), "Heading1", "Headline Large, bold"),
            AliasRow(new TextBlock("Heading 2").Heading2(), "Heading2", "Headline Medium, bold"),
            AliasRow(new TextBlock("Heading 3").Heading3(), "Heading3", "Headline Small, bold"),
            AliasRow(new TextBlock("Normal text").NormalText(), "NormalText", "Body Medium"),
            AliasRow(new TextBlock("Subtext").Subtext(), "Subtext", "Body Small, muted"),
            AliasRow(new TextBlock("Caption").Caption(), "Caption", "11 px, muted")));

    private static UIElement ScaleGroup(string title, params UIElement[] rows) => Ui.Demo(title, Ui.Stack(rows));

    private static UIElement ScaleRow(TextBlock sample, string key, MaterialTextStyle style) =>
        Row(sample, key, $"{style.Size:0}/{style.LineHeight:0} · weight {style.Weight}");

    private static UIElement AliasRow(TextBlock sample, string key, string description) => Row(sample, key, description);

    // The sample on the left, its method name and metrics on the right; stacks when narrow.
    private static UIElement Row(TextBlock sample, string key, string details) =>
        Ui.Columns(300,
            sample.VerticalAlignment(VerticalAlignment.Center),
            new StackPanel().Spacing(2).VerticalAlignment(VerticalAlignment.Center).Children(
                new TextBlock($".{key}()").FontFamily(Ui.MonospaceFont).FontSize(13).Themed(TextBlock.ForegroundProperty, c => c.Primary),
                new TextBlock(details).BodySmall().Muted()));

    #endregion

    #region Fonts, styles and colors

    private static UIElement FontSection() => Ui.Section("Font weight and family",
        "FontWeight takes any OpenType weight from Thin (100) to Black (900); fonts without that weight use the closest " +
        "one. FontFamily selects an installed font by name.",
        Ui.Columns(300,
            Ui.Demo("Weights",
                WeightRow(FontWeight.Thin, "Thin"),
                WeightRow(FontWeight.ExtraLight, "Extra light"),
                WeightRow(FontWeight.Light, "Light"),
                WeightRow(FontWeight.Normal, "Normal"),
                WeightRow(FontWeight.Medium, "Medium"),
                WeightRow(FontWeight.SemiBold, "Semibold"),
                WeightRow(FontWeight.Bold, "Bold"),
                WeightRow(FontWeight.ExtraBold, "Extra bold"),
                WeightRow(FontWeight.Black, "Black")),
            Ui.Demo("Families",
                FamilyRow("Segoe UI"),
                FamilyRow("Arial"),
                FamilyRow("Georgia"),
                FamilyRow("Times New Roman"),
                FamilyRow("Consolas"),
                FamilyRow("Verdana"),
                FamilyRow("Trebuchet MS"))));

    private static TextBlock WeightRow(FontWeight weight, string name) =>
        new TextBlock($"{name} ({weight.Value})").FontSize(20).FontWeight(weight);

    private static TextBlock FamilyRow(string family) =>
        new TextBlock($"{family}: {Pangram}").FontFamily(family).FontSize(16).TextTrimming();

    private static UIElement StyleAndColorSection() => Ui.Section("Style and color",
        "Bold, Italic and Muted are shortcuts. Text without a Foreground of its own inherits the color of its container " +
        "(a filled button's label, for example) or uses the theme's text color.",
        Ui.Demo("Style",
            Ui.Row(
                new TextBlock("Regular").FontSize(18),
                new TextBlock("Bold").FontSize(18).Bold(),
                new TextBlock("Italic").FontSize(18).Italic(),
                new TextBlock("Bold italic").FontSize(18).Bold().Italic(),
                new TextBlock("Muted").FontSize(18).Muted())),
        Ui.Demo("Theme colors",
            Ui.Row(
                new TextBlock("On surface").FontSize(18),
                new TextBlock("Primary").FontSize(18).Themed(TextBlock.ForegroundProperty, c => c.Primary),
                new TextBlock("Secondary").FontSize(18).Themed(TextBlock.ForegroundProperty, c => c.Secondary),
                new TextBlock("Tertiary").FontSize(18).Themed(TextBlock.ForegroundProperty, c => c.Tertiary),
                new TextBlock("Error").FontSize(18).Themed(TextBlock.ForegroundProperty, c => c.Error),
                new Border().Padding(12, 6).CornerRadius(8)
                    .Themed(Border.BackgroundProperty, c => c.InverseSurface)
                    .Child(new TextBlock("Inverse").FontSize(18).Themed(TextBlock.ForegroundProperty, c => c.InverseOnSurface)))),
        Ui.Demo("Inherited from the container",
            Ui.Row(
                new Button().Content(new TextBlock("Label in a filled button")),
                new Button().Variant(ButtonVariant.Tonal).Content(new TextBlock("Tonal")),
                new Button().Variant(ButtonVariant.Text).Content(new TextBlock("Text")))));

    #endregion

    #region Layout

    private static UIElement LayoutSection() => Ui.Section("Alignment, wrapping and trimming",
        "TextAlignment aligns lines within the block's width. TextWrapping breaks long lines at word boundaries; " +
        "TextTrimming ends text that doesn't fit with an ellipsis, and MaxLines limits wrapped text.",
        Ui.Columns(220,
            Box("TextAlignment.Left", new TextBlock(LongText).TextWrapping().TextAlignment(TextAlignment.Left)),
            Box("TextAlignment.Center", new TextBlock(LongText).TextWrapping().TextAlignment(TextAlignment.Center)),
            Box("TextAlignment.Right", new TextBlock(LongText).TextWrapping().TextAlignment(TextAlignment.Right))),
        Ui.Columns(220,
            Box("NoWrap (default), no trimming", new TextBlock(LongText)),
            Box("TextTrimming.CharacterEllipsis", new TextBlock(LongText).TextTrimming(TextTrimming.CharacterEllipsis)),
            Box("TextTrimming.WordEllipsis", new TextBlock(LongText).TextTrimming(TextTrimming.WordEllipsis))),
        Ui.Columns(220,
            Box("Wrap + MaxLines(2) + trimming", new TextBlock(LongText).TextWrapping().MaxLines(2).TextTrimming()),
            Box("LineHeight(16)", new TextBlock(LongText).TextWrapping().LineHeight(16)),
            Box("LineHeight(28)", new TextBlock(LongText).TextWrapping().LineHeight(28))),
        Ui.Code("new TextBlock(text).TextWrapping().MaxLines(2).TextTrimming()   // wraps, then ends line 2 with \"…\""));

    // A labeled, outlined box that makes the text block's width visible.
    private static UIElement Box(string label, TextBlock text) =>
        new StackPanel().Spacing(6).Children(
            new TextBlock(label).LabelMedium().Muted(),
            new Border()
                .Padding(12)
                .CornerRadius(8)
                .BorderThickness(1)
                .ClipToBounds()
                .Themed(Border.BorderBrushProperty, c => c.OutlineVariant)
                .Child(text.BodyMedium()));

    #endregion
}
