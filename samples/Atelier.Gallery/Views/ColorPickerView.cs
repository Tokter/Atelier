using System.Linq;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class ColorPickerView : GalleryPage
{
    private readonly ColorPickerViewModel _vm;

    public ColorPickerView(ColorPickerViewModel viewModel)
        : base(MaterialIconKind.Palette, "Color Picker",
            "The color picker combines a color wheel, channel sliders in RGB, HSL or HSB, an alpha slider and a hex box. " +
            "Each slider shows the gradient of colors it can reach from the current color.")
    {
        _vm = viewModel;

        Settings(
            new Switch("Alpha slider").ShowThumbIcon().BindIsChecked(_vm, v => v.IsAlphaEnabled, (v, on) => v.IsAlphaEnabled = on),
            new Button("Reset").Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        Sections(PickerSection(), ToolTipSection(), PartsSection());
    }

    private UIElement PickerSection() => Ui.Section("Color picker",
        "Drag on the wheel to pick hue and saturation, switch the sliders between RGB, HSL and HSB, or type a hex " +
        "value (#RGB, #RRGGBB or #AARRGGBB) or a channel value and press Enter. The picker keeps the hue while it " +
        "doesn't show, so dragging the brightness to black and back returns to the same color.",
        Ui.Columns(300,
            Ui.Demo("Bound to the view model",
                new ColorPicker()
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .BindColor(_vm, v => v.PickedColor, (v, color) => v.PickedColor = color)
                    .Bind(ColorPicker.IsAlphaEnabledProperty, _vm, v => v.IsAlphaEnabled)),

            Ui.Demo("The picked color",
                new ColorSwatch().Size(160, 96).CornerRadius(12).BindColor(_vm, v => v.PickedColor),
                Ui.Readout(_vm, v => v.HexText),
                Ui.Readout(_vm, v => v.RgbText),
                Ui.Readout(_vm, v => v.HslText),
                Ui.Readout(_vm, v => v.HsbText),
                Ui.Code("new ColorPicker()\n    .Mode(ColorPickerMode.Hsl)\n    .BindColor(vm, v => v.Color, (v, c) => v.Color = c)"))));

    private UIElement ToolTipSection() => Ui.Section("In a rich tooltip",
        "A picker can be the rich tooltip of a compact color field: hover the field and pick without leaving it. The " +
        "tooltip stays open while the pointer is over it or while one of its boxes has the keyboard focus. The Property " +
        "Grid's color editor works this way.",
        Ui.Demo("Hover the accent color",
            new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).HorizontalAlignment(HorizontalAlignment.Left)
                .Children(
                    new ColorSwatch().Size(32, 32).CornerRadius(16).BindColor(_vm, v => v.AccentColor),
                    new TextBlock().VerticalAlignment(VerticalAlignment.Center)
                        .Bind(TextBlock.TextProperty, _vm, v => $"Accent  {ColorPicker.ToHex(v.AccentColor)}"))
                .ToolTip(new ColorPicker().Mode(ColorPickerMode.Hsb).BindColor(_vm, v => v.AccentColor, (v, color) => v.AccentColor = color)),
            Ui.Code("field.ToolTip(new ColorPicker().BindColor(vm, v => v.Accent, (v, c) => v.Accent = c))")));

    private UIElement PartsSection() => Ui.Section("Building blocks",
        "The parts of the picker are controls of their own: a color wheel, sliders with a gradient track and swatches " +
        "that show transparency over a checkerboard.",
        Ui.Columns(260,
            Ui.Demo("ColorWheel",
                new ColorWheel().Size(140, 140).Saturation(0.6f).Hue(200)
                    .OnColorChanged(color => _vm.WheelText = $"Wheel → {ColorPicker.ToHex(color)}"),
                Ui.Readout(_vm, v => v.WheelText)),

            Ui.Demo("ColorSlider",
                new ColorSlider().Range(0, 360).Value(200).MinWidth(200)
                    .TrackColors(Enumerable.Range(0, 13).Select(i => Color.FromHsv(i * 30, 1, 1)).ToArray())
                    .ThumbColor(Color.FromHsv(200, 1, 1))
                    .OnValueChanged((s, value) => ((ColorSlider)s!).ThumbColor = Color.FromHsv(value, 1, 1)),
                new ColorSlider().Value(60).MinWidth(200)
                    .TrackColors(Color.FromHex("#0061A4").WithAlpha((byte)0), Color.FromHex("#0061A4"))
                    .ShowsTransparency()
                    .ThumbColor(Color.FromHex("#990061A4")),
                Ui.Note("A hue slider, and an opacity slider over a checkerboard.")),

            Ui.Demo("ColorSwatch",
                Ui.Row(
                    new ColorSwatch(Color.FromHex("#6750A4")),
                    new ColorSwatch(Color.FromHex("#B3261E")),
                    new ColorSwatch(Color.FromHex("#80386A1F")),
                    new ColorSwatch(Color.FromHex("#330061A4")).Size(48, 24).CornerRadius(12)),
                Ui.Note("Translucent colors show a checkerboard behind them."))));
}
