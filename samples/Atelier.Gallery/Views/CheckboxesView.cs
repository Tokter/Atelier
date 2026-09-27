using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class CheckboxesView : GalleryPage
{
    private readonly CheckboxesViewModel _vm;

    public CheckboxesView(CheckboxesViewModel viewModel)
        : base(MaterialIconKind.CheckBox, "Selection Controls",
            "Check boxes, radio buttons and switches let users choose options and turn settings on or off. They support " +
            "rich content, three states, groups and two-way binding.")
    {
        _vm = viewModel;

        Settings(
            new Switch("Controls enabled").ShowThumbIcon().BindIsChecked(_vm, v => v.ControlsEnabled, (v, on) => v.ControlsEnabled = on),
            new Button("Reset").Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        // IsEnabled is inherited: disabling the sections panel disables every demo on the page.
        SectionsPanel.BindIsEnabled(_vm, v => v.ControlsEnabled);

        Sections(CheckBoxSection(), RadioButtonSection(), SwitchSection());
    }

    private UIElement CheckBoxSection() => Ui.Section("Check boxes",
        "Select one or more items from a list, or turn an item on or off. A check box can also show a mixed state.",
        Ui.Demo("States",
            Ui.Row(
                new CheckBox("Unchecked"),
                new CheckBox("Checked").IsChecked(),
                new CheckBox("Indeterminate").IsThreeState().IsChecked(null)),
            Ui.Row(
                new CheckBox("Disabled").IsEnabled(false),
                new CheckBox("Disabled checked").IsChecked().IsEnabled(false),
                new CheckBox("Disabled mixed").IsThreeState().IsChecked(null).IsEnabled(false))),

        Ui.Columns(320,
            Ui.Demo("Select all (mixed state)",
                new CheckBox("All toppings").BindIsChecked(_vm, v => v.AllToppings, (v, all) => v.AllToppings = all),
                Ui.Stack(
                    new CheckBox("Cheese").BindIsChecked(_vm, v => v.Cheese, (v, on) => v.Cheese = on),
                    new CheckBox("Mushrooms").BindIsChecked(_vm, v => v.Mushrooms, (v, on) => v.Mushrooms = on),
                    new CheckBox("Olives").BindIsChecked(_vm, v => v.Olives, (v, on) => v.Olives = on))
                    .Margin(28, 0, 0, 0),
                Ui.Note("The parent shows a mixed state while only some toppings are selected; clicking it selects or clears all.")),

            Ui.Demo("Three states by click",
                new CheckBox("Cycles unchecked → checked → mixed")
                    .IsThreeState()
                    .BindIsChecked(_vm, v => v.TriState, (v, state) => v.TriState = state),
                Ui.Readout(_vm, v => $"TriState = {(v.TriState is { } b ? b.ToString() : "null")}"))),

        Ui.Columns(320,
            Ui.Demo("Rich content",
                new CheckBox().Content(Ui.Row(
                    new Icon(MaterialIconKind.Cloud, 20).Themed(Control.ForegroundProperty, c => c.Primary),
                    new TextBlock("Sync with cloud storage"))),
                new CheckBox().BindIsChecked(_vm, v => v.Subscribe, (v, on) => v.Subscribe = on).Content(
                    new StackPanel().Spacing(2).Children(
                        new TextBlock("Product newsletter"),
                        new TextBlock("One email per month with release notes").BodySmall().Muted()))),

            Ui.Demo("Binding and commands",
                new CheckBox("Push notifications").BindIsChecked(_vm, v => v.EnableNotifications, (v, on) => v.EnableNotifications = on),
                Ui.Row(
                    Ui.Readout(_vm, v => $"EnableNotifications = {v.EnableNotifications}"),
                    new Button("Toggle from the view model").Variant(ButtonVariant.Outlined).Command(_vm.ToggleNotificationsCommand)))),

        Ui.Code("new CheckBox(\"Push notifications\")\n    .BindIsChecked(vm, v => v.EnableNotifications, (v, on) => v.EnableNotifications = on)"));

    private UIElement RadioButtonSection() => Ui.Section("Radio buttons",
        "Select exactly one option from a set. Radio buttons with the same group name form a group; without a name, the " +
        "radio buttons that share a parent do.",
        Ui.Columns(320,
            Ui.Demo("Named group, bound to an enum",
                new RadioButton("720p HD").GroupName("quality")
                    .BindIsChecked(_vm, v => v.StreamingQuality, (v, q) => v.StreamingQuality = q, QualitySetting.Standard720p),
                new RadioButton("1080p Full HD").GroupName("quality")
                    .BindIsChecked(_vm, v => v.StreamingQuality, (v, q) => v.StreamingQuality = q, QualitySetting.High1080p),
                new RadioButton("4K Ultra HD").GroupName("quality")
                    .BindIsChecked(_vm, v => v.StreamingQuality, (v, q) => v.StreamingQuality = q, QualitySetting.Ultra4K),
                Ui.Readout(_vm, v => $"StreamingQuality = {v.StreamingQuality}")),

            Ui.Demo("Unnamed group (shared parent)",
                new RadioButton("Small"),
                new RadioButton("Medium").IsChecked(),
                new RadioButton("Large"),
                new RadioButton("Extra large (disabled)").IsEnabled(false))),

        Ui.Demo("Rich content",
            Ui.Columns(280,
                ShippingOption("Standard", MaterialIconKind.LocalShipping, "Standard shipping", "3–5 business days · Free"),
                ShippingOption("Express", MaterialIconKind.Bolt, "Express delivery", "Next business day · $9.99"),
                ShippingOption("Pickup", MaterialIconKind.Storefront, "Store pickup", "Ready in 2 hours · Free"))),

        Ui.Code("new RadioButton(\"4K Ultra HD\").GroupName(\"quality\")\n" +
                "    .BindIsChecked(vm, v => v.StreamingQuality, (v, q) => v.StreamingQuality = q, QualitySetting.Ultra4K)"));

    private RadioButton ShippingOption(string value, MaterialIconKind icon, string title, string detail) =>
        new RadioButton()
            .GroupName("shipping")
            .BindIsChecked(_vm, v => v.ShippingMethod, (v, m) => v.ShippingMethod = m, value)
            .Content(new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(
                new Icon(icon, 24).VerticalAlignment(VerticalAlignment.Center).Themed(Control.ForegroundProperty, c => c.Primary),
                new StackPanel().Spacing(2).Children(
                    new TextBlock(title).TitleSmall(),
                    new TextBlock(detail).BodySmall().Muted())));

    private UIElement SwitchSection() => Ui.Section("Switches",
        "Turn a single setting on or off immediately. Thumb icons make the state clearer; the track size is set by the " +
        "theme and can be changed per switch.",
        Ui.Demo("States",
            Ui.Row(
                new Switch("Off"),
                new Switch("On").IsChecked(),
                new Switch("Thumb icon").ShowThumbIcon(),
                new Switch("Thumb icon").ShowThumbIcon().IsChecked()),
            Ui.Row(
                new Switch("Disabled").IsEnabled(false),
                new Switch("Disabled on").IsChecked().IsEnabled(false),
                new Switch("Touch size (52×32)").TrackSize(52, 32).ShowThumbIcon().IsChecked())),

        Ui.Columns(320,
            Ui.Demo("Rich content",
                new Switch().ShowThumbIcon().BindIsChecked(_vm, v => v.AirplaneMode, (v, on) => v.AirplaneMode = on).Content(
                    new StackPanel().Spacing(2).Children(
                        new TextBlock("Airplane mode"),
                        new TextBlock("Turns off Wi-Fi, Bluetooth and cellular radios").BodySmall().Muted()))),

            Ui.Demo("Binding, commands and events",
                new Switch("Wi-Fi").ShowThumbIcon()
                    .BindIsChecked(_vm, v => v.WifiEnabled, (v, on) => v.WifiEnabled = on)
                    .OnCheckedChanged(state => _vm.LastEvent = $"Wi-Fi CheckedChanged → {state}"),
                new Switch("Bluetooth").ShowThumbIcon()
                    .BindIsChecked(_vm, v => v.BluetoothEnabled, (v, on) => v.BluetoothEnabled = on)
                    .OnCheckedChanged(state => _vm.LastEvent = $"Bluetooth CheckedChanged → {state}"),
                Ui.Row(
                    new Button("Toggle both").Variant(ButtonVariant.Outlined).Command(_vm.ToggleWirelessCommand),
                    Ui.Readout(_vm, v => v.LastEvent)))));
}
