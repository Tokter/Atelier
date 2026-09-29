using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class MenusView : GalleryPage
{
    private readonly MenusViewModel _vm;

    public MenusView(MenusViewModel viewModel)
        : base(MaterialIconKind.Menu, "Menus",
            "Menu bars, submenus and context menus. Shortcuts come from the keybindings of the items' commands, items are " +
            "disabled (or hidden) while their command can't run, and menus can be built from data.")
    {
        _vm = viewModel;
        DataContext = _vm;

        Settings(
            new Switch("Has selection").ShowThumbIcon().BindIsChecked(_vm, v => v.HasSelection, (v, on) => v.HasSelection = on),
            new Switch("Clipboard filled").ShowThumbIcon().BindIsChecked(_vm, v => v.HasClipboard, (v, on) => v.HasClipboard = on),
            new Button().Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        Sections(MenuBarSection(), DataSection(), ContextMenuSection());
    }

    private UIElement MenuBarSection() => Ui.Section("Menu bar",
        "Click a header, then hover the others. The items only name their command: headers, access keys, icons and " +
        "shortcuts come from [property: Keybinding] on the view model's commands (group \"Menus\"). The shortcuts work " +
        "while the focus is on this page; Cut, Copy and Paste follow CanExecute and " +
        "Delete hides itself. Underscores in the headers are access keys: with the focus in the demo, press a letter in an " +
        "open menu. The gallery's own menu in the title bar opens with Alt or F10 and Alt+F/V/H.",
        new KeybindingHandler("Menus", new StackPanel().Spacing(12).Children(
            new Border()
                .CornerRadius(8)
                .Padding(4, 2)
                .HorizontalAlignment(HorizontalAlignment.Left)
                .Themed(Border.BackgroundProperty, c => c.SurfaceContainer)
                .Child(DemoMenu()),
            Ui.Readout(_vm, v => v.LastAction))),
        Ui.Code("[RelayCommand(CanExecute = nameof(HasSelection))]\n" +
                "[property: Keybinding(\"MenusCopy\", \"Menus\", \"Ctrl+C\", Label = \"_Copy\", Icon = MaterialIcons.ContentCopy)]\n" +
                "private void Copy() { ... }\n\n" +
                "new MenuItem().Command(vm.CopyCommand)   // header, access key, icon and Ctrl+C from the command\n" +
                "new MenuItem(\"Word _wrap\").IsCheckable().StaysOpenOnClick().BindIsChecked(vm, v => v.WordWrap, ...)"));

    private Menu DemoMenu() => new Menu()
        .IsMainMenu(false)
        .KeybindingGroup("Menus")
        .Items(
            new MenuItem("_File").Items(
                new MenuItem().Command(_vm.NewCommand),
                new MenuItem().Command(_vm.OpenCommand),
                new MenuItem("Open _recent").Icon(MaterialIconKind.History)
                    .ItemsSource(_vm.RecentFiles),
                new Separator(),
                new MenuItem().Command(_vm.SaveCommand)),
            new MenuItem("_Edit").Items(
                new MenuItem().Command(_vm.UndoCommand),
                new MenuItem().Command(_vm.RedoCommand),
                new Separator(),
                new MenuItem().Command(_vm.CutCommand),
                new MenuItem().Command(_vm.CopyCommand),
                new MenuItem().Command(_vm.PasteCommand),
                new MenuItem().Command(_vm.DeleteCommand).HideWhenDisabled(),
                new Separator(),
                new MenuItem().Command(_vm.SelectAllCommand),
                new MenuItem().Command(_vm.CommentLineCommand),
                new MenuItem("_Find in files").Icon(MaterialIconKind.FindInPage).InputGestureText("Ctrl+Shift+F")
                    .OnClick(() => _vm.LastAction = "Find in files (its shortcut text is set by hand)")),
            new MenuItem("_View").Items(
                new MenuItem("Word _wrap").Icon(MaterialIconKind.WrapText).IsCheckable().StaysOpenOnClick()
                    .BindIsChecked(_vm, v => v.WordWrap, (v, on) => v.WordWrap = on),
                new MenuItem("_Minimap").Icon(MaterialIconKind.Map).IsCheckable().StaysOpenOnClick()
                    .BindIsChecked(_vm, v => v.ShowMinimap, (v, on) => v.ShowMinimap = on),
                new Separator(),
                new MenuItem("_Align").Icon(MaterialIconKind.FormatAlignLeft).Items(
                    AlignItem("_Left"), AlignItem("_Center"), AlignItem("_Right"))),
            new MenuItem("_Help").Items(
                new MenuItem("_About").Icon(MaterialIconKind.Info).OnClick(() => _vm.LastAction = "About"))
            )
        .WithItemSetup((MenuItem item, string file) => item
            .Header(file)
            .Command(_vm.OpenRecentCommand)
            .CommandParameter(file));

    // Radio items: checking one unchecks the others of the group.
    private MenuItem AlignItem(string header)
    {
        string value = header.Replace("_", "");
        return new MenuItem(header)
            .IsCheckable()
            .GroupName("align")
            .StaysOpenOnClick()
            .BindIsChecked(_vm, v => v.Alignment == value, (v, on) => { if (on) v.Alignment = value; });
    }

    private UIElement DataSection() => Ui.Section("Built from data",
        "A menu bound to a tree of nodes: ChildrenSelector gives each node its submenu, ItemSetup turns a node into a " +
        "menu item (header, icon, action), and MenuSeparator.Instance in the data makes a separator. An item template " +
        "can change how items look: the highlight colors show a swatch.",
        Ui.Row(
            new Border()
                .CornerRadius(8)
                .Padding(4, 2)
                .Themed(Border.BackgroundProperty, c => c.SurfaceContainer)
                .Child(new Menu()
                    .IsMainMenu(false)
                    .BindItemsSource(_vm, v => v.Catalog)
                    .WithChildrenSelector((MenuNode node) => node.Children)
                    .WithItemSetup((MenuItem item, MenuNode node) =>
                    {
                        item.Header(node.Title);
                        if (node.Icon != MaterialIconKind.None && node.Children == null) item.Icon(node.Icon);
                        if (node.Children == null) item.OnClick(() => _vm.LastAction = $"Catalog → {node.Title}");
                    })),
            new Button("Highlight").Variant(ButtonVariant.Outlined)
                .ContextMenu(new ContextMenu()
                    .ItemsSource(_vm.HighlightColors)
                    .WithItemTemplate((HighlightColor color) => new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(
                        new ColorSwatch(color.Color).Size(20, 20).CornerRadius(10),
                        new TextBlock(color.Name).VerticalAlignment(VerticalAlignment.Center)))
                    .WithItemSetup((MenuItem item, HighlightColor color) => item.OnClick(() => _vm.LastAction = $"Highlight → {color.Name}")))
                .ToolTip("Right-click for the color menu"),
            Ui.Readout(_vm, v => v.LastAction)),
        Ui.Code("new Menu()\n    .BindItemsSource(vm, v => v.Catalog)\n    .WithChildrenSelector((MenuNode n) => n.Children)\n" +
                "    .WithItemSetup((MenuItem item, MenuNode n) => item.Header(n.Title).Icon(n.Icon))"));

    private UIElement ContextMenuSection() => Ui.Section("Context menu",
        "Right-click the card, or focus it and press the menu key or Shift+F10. The menu takes the card's DataContext, " +
        "so its items bind to the same view model; Opening can change the items first.",
        new KeybindingHandler("Menus", new Border()
            .Height(140)
            .CornerRadius(12)
            .IsFocusable(true)
            .Themed(Border.BackgroundProperty, c => c.SurfaceContainerHigh)
            .Child(new TextBlock("Right-click here").VerticalAlignment(VerticalAlignment.Center).HorizontalAlignment(HorizontalAlignment.Center))
            .ContextMenu(new ContextMenu()
                .KeybindingGroup("Menus")
                .Items(
                    new MenuItem().Command(_vm.CutCommand),
                    new MenuItem().Command(_vm.CopyCommand),
                    new MenuItem().Command(_vm.PasteCommand),
                    new Separator(),
                    new MenuItem("_Sort by").Icon(MaterialIconKind.Sort).Items(
                        new MenuItem("_Name").IsCheckable().GroupName("sort").IsChecked().OnClick(() => _vm.LastAction = "Sort by name"),
                        new MenuItem("_Date").IsCheckable().GroupName("sort").OnClick(() => _vm.LastAction = "Sort by date"),
                        new MenuItem("_Size").IsCheckable().GroupName("sort").OnClick(() => _vm.LastAction = "Sort by size")),
                    new MenuItem("Word _wrap").IsCheckable().BindIsChecked(_vm, v => v.WordWrap, (v, on) => v.WordWrap = on),
                    new Separator(),
                    new MenuItem("_Rename").Icon(MaterialIconKind.DriveFileRenameOutline).InputGestureText("F2")
                        .OnClick(() => _vm.LastAction = "Rename"))
                .OnOpening((s, e) => _vm.LastAction = e.ByKeyboard ? "Context menu opened with the keyboard" : "Context menu opened"))),
        Ui.Readout(_vm, v => v.LastAction),
        Ui.Code("element.ContextMenu(new ContextMenu().Items(\n    new MenuItem().Command(vm.CopyCommand),\n    new Separator(),\n    ...))"));
}
