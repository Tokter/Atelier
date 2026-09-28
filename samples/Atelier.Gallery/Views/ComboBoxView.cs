using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class ComboBoxView : GalleryPage
{
    private readonly ComboBoxViewModel _vm;

    public ComboBoxView(ComboBoxViewModel viewModel)
        : base(MaterialIconKind.ArrowDropDownCircle, "ComboBox",
            "A combo box shows the selected option and opens a drop-down list to pick another one. Items come from a list " +
            "or a bound collection and can be shown with a template.")
    {
        _vm = viewModel;

        Settings(
            new Switch("Controls enabled").ShowThumbIcon().BindIsChecked(_vm, v => v.ControlsEnabled, (v, on) => v.ControlsEnabled = on),
            new Button().Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        SectionsPanel.BindIsEnabled(_vm, v => v.ControlsEnabled);

        Sections(BasicsSection(), BindingSection(), LongListSection(), EventsSection());
    }

    private UIElement BasicsSection() => Ui.Section("Items and selection",
        "Add options with Items, and bind the selection by index or by item. The placeholder shows while nothing is " +
        "selected.",
        Ui.Columns(240,
            Ui.Demo("Selected index",
                Combo().Items("Small", "Medium", "Large", "Extra large")
                    .BindSelectedIndex(_vm, v => v.SizeIndex, (v, i) => v.SizeIndex = i),
                Ui.Readout(_vm, v => $"SelectedIndex = {v.SizeIndex}")),
            Ui.Demo("Placeholder",
                Combo().Items("Email", "Phone", "Letter").Placeholder("How should we contact you?")),
            Ui.Demo("Disabled",
                Combo().Items("Read-only choice", "Other").SelectedIndex(0).IsEnabled(false))),
        Ui.Code("new ComboBox()\n    .Items(\"Small\", \"Medium\", \"Large\")\n" +
                "    .BindSelectedIndex(vm, v => v.SizeIndex, (v, i) => v.SizeIndex = i)"));

    private UIElement BindingSection() => Ui.Section("Bound items and templates",
        "ItemsSource takes any collection; observable collections update the drop-down when they change. A typed item " +
        "template builds each option, and the typed selection binding needs no casts.",
        Ui.Columns(300,
            Ui.Demo("Item template",
                Combo()
                    .ItemsSource(_vm.Destinations)
                    .WithItemTemplate((Destination d) => new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(
                        new Icon(d.Icon, 20).VerticalAlignment(VerticalAlignment.Center).Themed(Control.ForegroundProperty, c => c.Primary),
                        new TextBlock(d.City).VerticalAlignment(VerticalAlignment.Center),
                        new TextBlock(d.Country).BodySmall().Muted().VerticalAlignment(VerticalAlignment.Center)))
                    .BindSelectedItem(_vm, v => v.Destination, (v, d) => v.Destination = d),
                Ui.Readout(_vm, v => $"Destination = {v.Destination?.City ?? "null"}, {v.Destination?.Country}")),
            Ui.Demo("Observable collection",
                Combo()
                    .Placeholder("Pick a tag")
                    .BindItemsSource(_vm, v => v.Tags)
                    .BindSelectedItem(_vm, v => v.SelectedTag, (v, tag) => v.SelectedTag = tag),
                Ui.Row(
                    new Button().Variant(ButtonVariant.Tonal).Command(_vm.AddTagCommand),
                    new Button().Variant(ButtonVariant.Outlined).Command(_vm.RemoveTagCommand)),
                Ui.Readout(_vm, v => $"{v.Tags.Count} tags · SelectedTag = {v.SelectedTag ?? "null"}"))),
        Ui.Code("new ComboBox().ItemsSource(vm.Destinations)\n" +
                "    .WithItemTemplate((Destination d) => new TextBlock(d.City))\n" +
                "    .BindSelectedItem(vm, v => v.Destination, (v, d) => v.Destination = d)"));

    private UIElement LongListSection() => Ui.Section("Long lists and keyboard",
        "MaxDropDownHeight limits the drop-down; longer lists scroll. With the box focused, arrow keys change the " +
        "selection and typing jumps to the first option that starts with the typed text.",
        Ui.Columns(240,
            Ui.Demo("Max drop-down height",
                Combo().Items(_vm.Numbers).MaxDropDownHeight(160).Placeholder("60 items")
                    .BindSelectedItem(_vm, v => v.Number, (v, n) => v.Number = n)),
            Ui.Demo("Text search",
                Combo().Items(_vm.Months).Placeholder("Type \"ma\" for March")
                    .BindSelectedItem(_vm, v => v.Month, (v, m) => v.Month = m)),
            Ui.Demo("Text search off",
                Combo().Items(_vm.Months).Placeholder("Typing does nothing").IsTextSearchEnabled(false))),
        Ui.Note("Space, Enter, F4 or Alt+Down open the drop-down; Enter commits the highlighted option and Escape closes it."));

    private UIElement EventsSection() => Ui.Section("Drop-down state and events",
        "IsDropDownOpen opens and closes the drop-down and can be bound. SelectionChanged, DropDownOpened and " +
        "DropDownClosed report what happens.",
        Ui.Columns(300,
            Ui.Demo("Open from code",
                Combo().Items(_vm.Months).Placeholder("Pick a month")
                    .BindIsDropDownOpen(_vm, v => v.IsDropDownOpen, (v, open) => v.IsDropDownOpen = open)
                    .OnSelectionChanged(item => _vm.LastEvent = $"SelectionChanged → {item ?? "null"}")
                    .OnDropDownOpened(() => _vm.LastEvent = "DropDownOpened")
                    .OnDropDownClosed(() => _vm.LastEvent = "DropDownClosed"),
                Ui.Row(new Button().Variant(ButtonVariant.Tonal).Command(_vm.OpenDropDownCommand))),
            Ui.Demo("Events",
                Ui.Readout(_vm, v => v.LastEvent),
                Ui.Readout(_vm, v => $"IsDropDownOpen = {v.IsDropDownOpen}"))));

    // Combo boxes are top-aligned so a taller neighbor in the same column row doesn't stretch them.
    private static ComboBox Combo() => new ComboBox().VerticalAlignment(VerticalAlignment.Top);
}
