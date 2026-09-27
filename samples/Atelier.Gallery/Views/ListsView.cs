using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class ListsView : GalleryPage
{
    private readonly ListsViewModel _vm;

    public ListsView(ListsViewModel viewModel)
        : base(MaterialIconKind.ViewList, "Lists",
            "ListBox shows a scrolling list of items with single selection, keyboard navigation and type-to-search. " +
            "ItemsControl shows items without selection. Both generate a row per item, from a template or as text.")
    {
        _vm = viewModel;

        Settings(
            new Switch("Controls enabled").ShowThumbIcon().BindIsChecked(_vm, v => v.ControlsEnabled, (v, on) => v.ControlsEnabled = on),
            new Button("Reset").Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        SectionsPanel.BindIsEnabled(_vm, v => v.ControlsEnabled);

        Sections(ListBoxSection(), SimpleListSection(), ItemsControlSection());
    }

    private UIElement ListBoxSection() => Ui.Section("ListBox with templates and a live collection",
        "The list is bound to an ObservableCollection: adding, removing, moving and clearing items updates only the " +
        "affected rows. Each row comes from a typed item template; the selection is bound two-way.",
        Ui.Columns(360,
            new ListBox()
                .Height(336)
                .BindItemsSource(_vm, v => v.Contacts)
                .WithItemTemplate((Contact contact) => ContactRow(contact))
                .BindSelectedItem(_vm, v => v.SelectedContact, (v, contact) => v.SelectedContact = contact)
                .BindSelectedIndex(_vm, v => v.SelectedIndex, (v, index) => v.SelectedIndex = index)
                .OnSelectionChanged(item => _vm.LastSelectionChanged = $"SelectionChanged → {item?.ToString() ?? "null"}"),

            Ui.Stack(
                Ui.Demo("Selection",
                    Ui.Readout(_vm, v => $"SelectedIndex = {v.SelectedIndex}"),
                    Ui.Readout(_vm, v => $"SelectedItem = {v.SelectedContact?.Name ?? "null"}"),
                    Ui.Readout(_vm, v => v.LastSelectionChanged)),
                Ui.Demo("Change the collection",
                    Ui.Row(
                        Ui.IconButton(MaterialIconKind.Add, "Add", ButtonVariant.Tonal).Command(_vm.AddContactCommand),
                        Ui.IconButton(MaterialIconKind.Delete, "Remove", ButtonVariant.Outlined).Command(_vm.RemoveSelectedCommand),
                        Ui.IconButton(MaterialIconKind.ArrowUpward, "Up", ButtonVariant.Outlined).Command(_vm.MoveUpCommand),
                        Ui.IconButton(MaterialIconKind.ArrowDownward, "Down", ButtonVariant.Outlined).Command(_vm.MoveDownCommand),
                        new Button("Clear").Variant(ButtonVariant.Text).Command(_vm.ClearContactsCommand),
                        new Button("Restore").Variant(ButtonVariant.Text).Command(_vm.ResetContactsCommand))),
                Ui.Note("Keyboard: Up/Down, Home/End and Page Up/Down move the selection. Typing jumps to the next " +
                        "contact whose name starts with the typed letters."))),

        Ui.Code("new ListBox()\n" +
                "    .BindItemsSource(vm, v => v.Contacts)\n" +
                "    .WithItemTemplate((Contact contact) => ContactRow(contact))\n" +
                "    .BindSelectedItem(vm, v => v.SelectedContact, (v, contact) => v.SelectedContact = contact)"));

    private static Grid ContactRow(Contact contact) =>
        new Grid()
            .Columns(GridLength.Auto, GridLength.Star, GridLength.Auto)
            .ColumnSpacing(12)
            .Children(
                new Border()
                    .Size(36, 36)
                    .CornerRadius(18)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Themed(Border.BackgroundProperty, c => c.PrimaryContainer)
                    .Child(new TextBlock(contact.Initials)
                        .LabelLarge()
                        .Center()
                        .Themed(TextBlock.ForegroundProperty, c => c.OnPrimaryContainer)),
                new StackPanel().Column(1).Spacing(2).VerticalAlignment(VerticalAlignment.Center).Children(
                    new TextBlock(contact.Name).BodyLarge(),
                    new TextBlock(contact.Email).BodySmall().Muted()),
                new StackPanel().Column(2).Orientation(Orientation.Horizontal).Spacing(6).VerticalAlignment(VerticalAlignment.Center).Children(
                    new Border()
                        .Size(8, 8)
                        .CornerRadius(4)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Themed(Border.BackgroundProperty, c => contact.IsOnline ? c.Primary : c.OutlineVariant),
                    new TextBlock(contact.Role).LabelMedium().Muted().VerticalAlignment(VerticalAlignment.Center)));

    private UIElement SimpleListSection() => Ui.Section("Items, text search and states",
        "Without an ItemsSource, items are added to Items directly; strings are shown as text. ListBoxItem can also be " +
        "used on its own as a selectable row.",
        Ui.Columns(280,
            Ui.Demo("Type to search",
                new ListBox()
                    .Height(240)
                    .Items("Apple", "Apricot", "Banana", "Blackberry", "Blueberry", "Cherry", "Grape", "Kiwi", "Lemon", "Mango", "Orange", "Peach", "Pear", "Plum")
                    .Bind(ListBox.IsTextSearchEnabledProperty, _vm, v => v.IsTextSearchEnabled)
                    .OnSelectionChanged(item => _vm.SelectedFruit = item?.ToString() ?? "(none)"),
                new Switch("IsTextSearchEnabled").BindIsChecked(_vm, v => v.IsTextSearchEnabled, (v, on) => v.IsTextSearchEnabled = on),
                Ui.Readout(_vm, v => $"Selected: {v.SelectedFruit}"),
                Ui.Note("Click the list, then type \"bl\" to jump to Blackberry.")),

            Ui.Demo("Disabled",
                new ListBox().Items("First", "Second", "Third").SelectedIndex(1).IsEnabled(false),
                Ui.Note("A disabled list keeps its selection but ignores input.")),

            Ui.Demo("ListBoxItem on its own",
                Ui.Stack(
                    StandaloneItem("Inbox", MaterialIconKind.Inbox),
                    StandaloneItem("Starred", MaterialIconKind.Star).IsSelected(),
                    StandaloneItem("Sent", MaterialIconKind.Mail)).Spacing(0),
                Ui.Readout(_vm, v => v.LastItemClicked))));

    private ListBoxItem StandaloneItem(string text, MaterialIconKind icon) =>
        new ListBoxItem()
            .Content(new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(
                new Icon(icon, 20).VerticalAlignment(VerticalAlignment.Center),
                new TextBlock(text).VerticalAlignment(VerticalAlignment.Center)))
            .Configure(item => item.OnClicked(() =>
            {
                item.IsSelected = !item.IsSelected;
                _vm.LastItemClicked = $"Clicked {text} → IsSelected = {item.IsSelected}";
            }));

    private UIElement ItemsControlSection() => Ui.Section("ItemsControl",
        "ItemsControl generates the same rows without selection. Derived controls can arrange them differently: this " +
        "one lays its item panel out horizontally.",
        Ui.Demo("Tags from an ObservableCollection",
            new ChipList()
                .BindItemsSource(_vm, v => v.Tags)
                .WithItemTemplate((string tag) => new Border()
                    .Padding(12, 6)
                    .CornerRadius(8)
                    .Themed(Border.BackgroundProperty, c => c.SecondaryContainer)
                    .Child(new StackPanel().Orientation(Orientation.Horizontal).Spacing(6).Children(
                        new Icon(MaterialIconKind.Label, 16).VerticalAlignment(VerticalAlignment.Center)
                            .Themed(Control.ForegroundProperty, c => c.OnSecondaryContainer),
                        new TextBlock(tag).LabelLarge().VerticalAlignment(VerticalAlignment.Center)
                            .Themed(TextBlock.ForegroundProperty, c => c.OnSecondaryContainer)))),
            Ui.Row(
                new TextBox().Placeholder("New tag").Width(200).BindText(_vm, v => v.NewTag, (v, text) => v.NewTag = text),
                Ui.IconButton(MaterialIconKind.Add, "Add tag", ButtonVariant.Tonal).Command(_vm.AddTagCommand),
                new Button("Remove last").Variant(ButtonVariant.Outlined).Command(_vm.RemoveLastTagCommand))),

        Ui.Code("class ChipList : ItemsControl\n" +
                "{\n" +
                "    public ChipList() => ItemPanel.Orientation(Orientation.Horizontal).Spacing(8);\n" +
                "}"));

    /// <summary>An ItemsControl whose items flow horizontally (the item panel is a protected StackPanel).</summary>
    private sealed class ChipList : ItemsControl
    {
        public ChipList()
        {
            ItemPanel.Orientation(Orientation.Horizontal).Spacing(8);
            ScrollViewer.HorizontalScrollBarVisibility(ScrollBarVisibility.Auto).VerticalScrollBarVisibility(ScrollBarVisibility.Disabled);
        }
    }
}
