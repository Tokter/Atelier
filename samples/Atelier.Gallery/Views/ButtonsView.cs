using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class ButtonsView : GalleryPage
{
    private static readonly ButtonVariant[] Variants =
        [ButtonVariant.Filled, ButtonVariant.Tonal, ButtonVariant.Elevated, ButtonVariant.Outlined, ButtonVariant.Text];

    private readonly ButtonsViewModel _vm;

    public ButtonsView(ButtonsViewModel viewModel)
        : base(MaterialIconKind.SmartButton, "Buttons",
            "Buttons start actions. Pick the variant by emphasis: filled for the main action, text for the least important " +
            "one. Buttons run commands, repeat while held and toggle on and off.")
    {
        _vm = viewModel;

        Settings(
            new Switch("Controls enabled").ShowThumbIcon().BindIsChecked(_vm, v => v.ControlsEnabled, (v, on) => v.ControlsEnabled = on),
            new Button().Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        SectionsPanel.BindIsEnabled(_vm, v => v.ControlsEnabled);

        Sections(VariantsSection(), CommandsSection(), RepeatAndClickModeSection(), ToggleSection(), ToolbarSection());
    }

    private UIElement VariantsSection() => Ui.Section("Variants",
        "Five variants with decreasing emphasis. Labels are centered and sized by the theme; icons go before the label.",
        Ui.Demo("Text", Ui.Row(Array.ConvertAll(Variants, v => (UIElement)Log(new Button(v.ToString()).Variant(v))))),
        Ui.Demo("With icons", Ui.Row(
            Log(Ui.IconButton(MaterialIconKind.Send, "Send", ButtonVariant.Filled)),
            Log(Ui.IconButton(MaterialIconKind.Edit, "Edit", ButtonVariant.Tonal)),
            Log(Ui.IconButton(MaterialIconKind.Download, "Download", ButtonVariant.Elevated)),
            Log(Ui.IconButton(MaterialIconKind.Share, "Share", ButtonVariant.Outlined)),
            Log(Ui.IconButton(MaterialIconKind.Add, "Add item", ButtonVariant.Text)))),
        Ui.Demo("Icon only", Ui.Row(
            Log(IconOnly(MaterialIconKind.Favorite, ButtonVariant.Filled).ToolTip("Add to favorites")),
            Log(IconOnly(MaterialIconKind.Bookmark, ButtonVariant.Tonal).ToolTip("Bookmark")),
            Log(IconOnly(MaterialIconKind.ContentCopy, ButtonVariant.Outlined).ToolTip("Copy (Ctrl+C)")),
            Log(IconOnly(MaterialIconKind.MoreVert, ButtonVariant.Text).ToolTip("More options")))),
        Ui.Demo("Disabled", Ui.Row(Array.ConvertAll(Variants, v => (UIElement)new Button(v.ToString()).Variant(v).IsEnabled(false)))),
        Ui.Demo("Click event", Ui.Readout(_vm, v => v.LastClick)),
        Ui.Columns(320,
            Ui.Demo("Elevation",
                Ui.SliderSetting("Resting elevation", _vm, v => v.Elevation, (v, e) => v.Elevation = e, 0, 5, "0", 1),
                Ui.Row(
                    new Button("Filled").Bind(Button.ElevationProperty, _vm, v => v.Elevation),
                    new Button("Elevated").Variant(ButtonVariant.Elevated).Bind(Button.ElevationProperty, _vm, v => v.Elevation)),
                Ui.Note("Filled and elevated buttons cast a shadow; hovering and pressing raise it.")),
            Ui.Code("new Button(\"Save\")\n    .Variant(ButtonVariant.Tonal)\n    .OnClick(() => Save())\n\n" +
                    "Ui.IconButton(MaterialIconKind.Send, \"Send\")")));

    private UIElement CommandsSection() => Ui.Section("Commands",
        "A button runs its Command when clicked and is disabled while the command can't execute. The CommandParameter is " +
        "passed to the command. Without content of its own, a button shows the icon and label of its command's [Command] " +
        "declaration, and its tooltip shows the description (hover the plus and minus buttons).",
        Ui.Columns(320,
            Ui.Demo("CanExecute",
                Ui.Row(
                    CommandIcon(ButtonVariant.Outlined).Command(_vm.DecrementCommand).ToolTipShowOnDisabled(),
                    new TextBlock().TitleLarge().MinWidth(40).BindText(_vm, v => v.Count.ToString()),
                    CommandIcon(ButtonVariant.Filled).Command(_vm.IncrementCommand)),
                Ui.Note("The minus button disables itself at 0: the command's CanExecute returns false.")),
            Ui.Demo("CommandParameter",
                Ui.Row(
                    new Button("Red").Variant(ButtonVariant.Tonal).Command(_vm.ChooseCommand, "Red"),
                    new Button("Green").Variant(ButtonVariant.Tonal).Command(_vm.ChooseCommand, "Green"),
                    new Button("Blue").Variant(ButtonVariant.Tonal).Command(_vm.ChooseCommand, "Blue")),
                Ui.Readout(_vm, v => v.LastParameter))),
        Ui.Code("[RelayCommand(CanExecute = nameof(CanDecrement))]\n" +
                "[property: Command(\"Decrement\", \"Buttons\", Label = \"Decrease\", Icon = MaterialIcons.Remove,\n" +
                "    Description = \"Decrease the count (disabled at 0)\")]\n" +
                "private void Decrement() => Count--;\n\n" +
                "new Button().CommandDisplay(CommandDisplay.Icon).Command(vm.DecrementCommand)\n" +
                "new Button(\"Blue\").Command(vm.ChooseCommand, \"Blue\")"));

    private UIElement RepeatAndClickModeSection() => Ui.Section("Repeat buttons and click modes",
        "A RepeatButton clicks repeatedly while held, after a delay. ClickMode sets when a click happens: on release (the " +
        "default), on press, or when the pointer enters the button.",
        Ui.Columns(320,
            Ui.Demo("Hold to repeat",
                Ui.Row(
                    new RepeatButton().Variant(ButtonVariant.Outlined).Padding(6).MinWidth(32).CommandDisplay(CommandDisplay.Icon)
                        .Command(_vm.RepeatDownCommand),
                    new ProgressBar().Width(160).BindValue(_vm, v => v.RepeatValue),
                    new RepeatButton().Padding(6).MinWidth(32).CommandDisplay(CommandDisplay.Icon)
                        .Delay(TimeSpan.FromMilliseconds(300))
                        .Interval(TimeSpan.FromMilliseconds(20))
                        .Command(_vm.RepeatUpCommand),
                    Ui.Readout(_vm, v => $"{v.RepeatValue:0}")),
                Ui.Note("Minus: default 500 ms delay, 33 ms interval. Plus: 300 ms delay, 20 ms interval.")),
            Ui.Demo("Click modes",
                Ui.Row(
                    new Button("On press").Variant(ButtonVariant.Tonal).ClickMode(ClickMode.Press).OnClick(() => _vm.PressClicks++),
                    new Button("On hover").Variant(ButtonVariant.Outlined).ClickMode(ClickMode.Hover).OnClick(() => _vm.HoverClicks++)),
                Ui.Readout(_vm, v => $"Press: {v.PressClicks}  ·  Hover: {v.HoverClicks}"))));

    private UIElement ToggleSection() => Ui.Section("Toggle buttons",
        "A ToggleButton stays checked after a click, like a segmented button. With IsThreeState it also cycles through a " +
        "mixed (null) state.",
        Ui.Columns(320,
            Ui.Demo("Segmented group",
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(4).Children(
                    Segment(MaterialIconKind.FormatAlignLeft, SegmentAlignment.Left),
                    Segment(MaterialIconKind.FormatAlignCenter, SegmentAlignment.Center),
                    Segment(MaterialIconKind.FormatAlignRight, SegmentAlignment.Right)),
                Ui.Readout(_vm, v => $"Alignment = {v.Alignment}")),
            Ui.Demo("Two and three states",
                Ui.Row(
                    new ToggleButton().BindIsChecked(_vm, v => v.IsBold, (v, on) => v.IsBold = on).Content(Label(MaterialIconKind.FormatBold, "Bold")),
                    new ToggleButton().IsThreeState().BindIsChecked(_vm, v => v.IsItalic, (v, state) => v.IsItalic = state)
                        .Content(Label(MaterialIconKind.FormatItalic, "Italic")),
                    new ToggleButton("Disabled").IsChecked().IsEnabled(false)),
                Ui.Readout(_vm, v => $"IsBold = {v.IsBold}  ·  IsItalic = {(v.IsItalic is { } i ? i.ToString() : "null")}"))));

    private UIElement ToolbarSection() => Ui.Section("Toolbars",
        "A Toolbar is a bar for actions: raised with an elevation, or flat with a divider line.",
        Ui.Demo("Raised (elevation 2)", new Toolbar(ToolbarContent())),
        Ui.Demo("Flat with a bottom divider",
            new Toolbar(ToolbarContent())
                .Elevation(0)
                .BorderThickness(new Thickness(0, 0, 0, 1))
                .Themed(Toolbar.BorderBrushProperty, c => c.OutlineVariant)));

    private UIElement ToolbarContent() =>
        new DockPanel().Children(
            new StackPanel().Orientation(Orientation.Horizontal).Spacing(4).Dock(Dock.Left).Children(
                IconOnly(MaterialIconKind.Undo, ButtonVariant.Text).ToolTip("Undo (Ctrl+Z)"),
                IconOnly(MaterialIconKind.Redo, ButtonVariant.Text).ToolTip("Redo (Ctrl+Y)"),
                IconOnly(MaterialIconKind.ContentCopy, ButtonVariant.Text).ToolTip("Copy (Ctrl+C)")),
            new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Dock(Dock.Right).Children(
                new Button("Discard").Variant(ButtonVariant.Text),
                Ui.IconButton(MaterialIconKind.Save, "Save")),
            new TextBlock("Document.txt").TitleSmall().Center());

    private ToggleButton Segment(MaterialIconKind icon, SegmentAlignment alignment) =>
        new ToggleButton()
            .Padding(12, 6)
            .Content(new Icon(icon, 20))
            .ToolTip($"Align {alignment.ToString().ToLowerInvariant()}")
            .BindIsChecked(_vm, v => v.Alignment == alignment)
            .Command(_vm.SelectAlignmentCommand, alignment);

    // An icon-only button for a command: the icon and the tooltip come from its [Command] declaration.
    private static Button CommandIcon(ButtonVariant variant) =>
        new Button().Variant(variant).Padding(6).MinWidth(32).CommandDisplay(CommandDisplay.Icon);

    // Icon-only buttons are square-ish: less padding than labeled buttons.
    private static Button IconOnly(MaterialIconKind icon, ButtonVariant variant) =>
        new Button().Variant(variant).Padding(6).MinWidth(32).Content(new Icon(icon, 20));

    private static StackPanel Label(MaterialIconKind icon, string text) =>
        new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
            new Icon(icon, 18).VerticalAlignment(VerticalAlignment.Center),
            new TextBlock(text).VerticalAlignment(VerticalAlignment.Center));

    private Button Log(Button button) =>
        button.OnClick(() => _vm.LastClick = $"Click: {(button.Content is string text ? text : button.Variant + " button")} at {DateTime.Now:HH:mm:ss}");
}
