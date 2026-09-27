using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class DialogHostView : GalleryPage
{
    private readonly DialogHostViewModel _vm;

    public DialogHostView(DialogHostViewModel viewModel)
        : base(MaterialIconKind.WebAsset, "Popups & Dialogs",
            "Dialogs ask for a decision or input on top of the page; a DialogHost darkens the content below them. " +
            "Popups show transient content, such as menus, next to an element.")
    {
        _vm = viewModel;

        Settings(new Button("Reset options").Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        Sections(DialogSection(), HostSection(), PopupSection());
    }

    #region Dialogs

    private UIElement DialogSection() => Ui.Section("Dialogs",
        "A Dialog has a title, a message or custom content, and buttons. ShowAsync shows it in the nearest DialogHost " +
        "and returns the button the user chose. Enter presses the default button; Escape the cancel button.",
        Ui.Demo("Button presets",
            Ui.Row(
                Launcher("OK", () => new Dialog("Update installed", "Atelier 2.4 is ready to use.", DialogButtons.Ok)),
                Launcher("OK / Cancel", () => new Dialog("Discard draft?", "The draft of this message will be deleted.", DialogButtons.OkCancel)),
                Launcher("Yes / No", () => new Dialog("Enable sync?", "Settings will be kept in sync across your devices.", DialogButtons.YesNo)),
                Launcher("Yes / No / Cancel", () => new Dialog("Save changes?", "Save the changes to \"Report.docx\" before closing?", DialogButtons.YesNoCancel)),
                Launcher("No buttons", () => new Dialog("Press Escape", "This dialog has no buttons. Press Escape to close it."))),
            Ui.Readout(_vm, v => $"Result: {v.LastResult}")),

        Ui.Columns(340,
            Ui.Demo("Custom buttons",
                Ui.Note("AddButton sets each button's text, result, variant and whether Enter (default) or Escape (cancel) presses it."),
                Launcher("Close document", () => new Dialog("Close document?", "You have unsaved changes in \"Quarterly plan\".")
                    .AddButton("Cancel", DialogResult.Cancel, isCancel: true)
                    .AddButton("Don't save", DialogResult.No)
                    .AddButton("Save", DialogResult.Yes, isDefault: true, variant: ButtonVariant.Filled))),

            Ui.Demo("Custom content and validation",
                Ui.Note("Content can be any element. Closing is cancelled while the name is empty."),
                Launcher("New project", CreateProjectDialog)),

            Ui.Demo("Options",
                new Switch("Close on Escape").BindIsChecked(_vm, v => v.CloseOnEscape, (v, on) => v.CloseOnEscape = on),
                Ui.SliderSetting("Elevation", _vm, v => v.DialogElevation, (v, e) => v.DialogElevation = e, 0, 12, step: 1))),

        Ui.Demo("Events",
            Ui.Readout(_vm, v => v.DialogEventTrail)),

        Ui.Code("var response = await new Dialog(\"Save changes?\", \"Save before closing?\", DialogButtons.YesNoCancel)\n" +
                "    .ShowAsync(this);\n" +
                "if (response.Result == DialogResult.Yes) Save();"));

    private Dialog CreateProjectDialog()
    {
        var name = new TextBox()
            .Label("Project name")
            .SupportingText("Required")
            .BindText(_vm, v => v.ProjectName, (v, text) => v.ProjectName = text);

        var dialog = new Dialog("New project")
            .Content(new StackPanel().Spacing(16).MinWidth(320).Children(
                new TextBlock("Create a project to group related documents.").TextWrapping().Muted(),
                name,
                new CheckBox("Open after creating").IsChecked()))
            .AddButton("Cancel", DialogResult.Cancel, isCancel: true)
            .AddButton("Create", DialogResult.Ok, isDefault: true, variant: ButtonVariant.Filled);

        dialog.OnClosing((_, e) =>
        {
            if (e.Response.Result == DialogResult.Ok && string.IsNullOrWhiteSpace(_vm.ProjectName))
            {
                e.Cancel = true;
                name.SupportingText("Enter a name to create the project");
                _vm.DialogEvent("Closing (cancelled: name is empty)");
            }
        });
        return dialog;
    }

    /// <summary>A button that shows the dialog made by <paramref name="create"/> in the nearest host and reports the result.</summary>
    private Button Launcher(string text, Func<Dialog> create)
    {
        var button = new Button(text).Variant(ButtonVariant.Outlined).HorizontalAlignment(HorizontalAlignment.Left);
        button.OnClick(() => ShowDialog(create(), button));
        return button;
    }

    private async void ShowDialog(Dialog dialog, UIElement visualContext)
    {
        dialog.CloseOnEscape(_vm.CloseOnEscape)
            .Elevation(_vm.DialogElevation)
            .OnOpened(() => _vm.DialogEvent("Opened", first: true))
            .OnClosing((_, e) =>
            {
                if (!e.Cancel)
                {
                    _vm.DialogEvent($"Closing ({e.Response.Result})");
                }
            })
            .OnClosed((_, e) => _vm.DialogEvent("Closed"));

        var response = await dialog.ShowAsync(visualContext);
        _vm.LastResult = response.Button != null ? $"{response.Result} (\"{response.ButtonText}\")" : response.Result.ToString();
    }

    #endregion

    #region Dialog hosts

    private UIElement HostSection()
    {
        var localHost = new DialogHost()
            .Identifier("LocalHost")
            .Bind(DialogHost.OverlayColorProperty, _vm, v => v.LocalScrimColor)
            .BindIsOpen(_vm, v => v.IsLocalHostOpen, (v, open) => v.IsLocalHostOpen = open)
            .Bind(DialogHost.CloseOnClickAwayProperty, _vm, v => v.LocalCloseOnClickAway)
            .OnDialogOpened((_, e) => _vm.LastHostEvent = $"DialogOpened: {Describe(e.Dialog)}")
            .OnDialogClosed((_, e) => _vm.LastHostEvent = $"DialogClosed: {Describe(e.Dialog)} → {e.Response?.Result.ToString() ?? "removed"}");

        var showDialog = new Button("Show a dialog here").Variant(ButtonVariant.Filled);
        showDialog.OnClick(() => ShowDialog(
            new Dialog("Scoped dialog", "Only this area is covered. The rest of the page stays usable.", DialogButtons.Ok),
            showDialog));

        var showCard = new Button("Show a card").Variant(ButtonVariant.Outlined);
        showCard.OnClick(() => localHost.Dialog = new Card(CardVariant.Elevated)
            .Padding(20)
            .Center()
            .Child(Ui.Stack(
                new TextBlock("Any element").TitleMedium(),
                new TextBlock("Setting DialogHost.Dialog shows any element. Click outside to close it.").TextWrapping().MaxWidth(260),
                new Button("Close").Variant(ButtonVariant.Text).HorizontalAlignment(HorizontalAlignment.Right)
                    .OnClick(() => localHost.CloseAllDialogs()))));

        localHost.Content(new StackPanel()
            .Spacing(12)
            .Margin(24)
            .Center()
            .Children(
                new Icon(MaterialIconKind.Layers, 32).HorizontalAlignment(HorizontalAlignment.Center)
                    .Themed(Control.ForegroundProperty, c => c.Primary),
                new TextBlock("DialogHost \"LocalHost\"").TitleMedium().HorizontalAlignment(HorizontalAlignment.Center),
                Ui.Row(showDialog, showCard).HorizontalAlignment(HorizontalAlignment.Center)));

        return Ui.Section("Dialog hosts",
            "A DialogHost shows dialogs over its content. This window's root host covers the whole window; a host placed " +
            "inside the page covers only its own area. ShowAsync(element) uses the host nearest to the element, " +
            "DialogHost.ShowAsync(dialog, \"Identifier\") a specific one.",
            Ui.Columns(340,
                new Border()
                    .Height(280)
                    .CornerRadius(12)
                    .BorderThickness(1)
                    .ClipToBounds()
                    .Themed(Border.BorderBrushProperty, c => c.OutlineVariant)
                    .Themed(Border.BackgroundProperty, c => c.SurfaceContainerLow)
                    .Child(localHost),
                Ui.Stack(
                    Ui.SliderSetting("Scrim opacity", _vm, v => v.LocalScrimOpacity, (v, o) => v.LocalScrimOpacity = o, 0, 0.8f, "0.00", 0.04f),
                    new Switch("Close on click away").BindIsChecked(_vm, v => v.LocalCloseOnClickAway, (v, on) => v.LocalCloseOnClickAway = on),
                    Ui.Row(
                        new Button("Show in root host").Variant(ButtonVariant.Tonal).OnClick(ShowInRootHost),
                        new Button("Close all").Variant(ButtonVariant.Text).Command(_vm.ToggleLocalHostCommand)
                            .BindIsEnabled(_vm, v => v.IsLocalHostOpen)),
                    Ui.Readout(_vm, v => $"IsOpen = {v.IsLocalHostOpen}"),
                    Ui.Readout(_vm, v => v.LastHostEvent))),
            Ui.Code("new DialogHost()\n" +
                    "    .Identifier(\"LocalHost\")\n" +
                    "    .OverlayColor(Color.Black.WithAlpha(0.32f))\n" +
                    "    .CloseOnClickAway()\n" +
                    "    .Content(page)"));
    }

    private async void ShowInRootHost()
    {
        var dialog = new Dialog("Root host", "Shown with DialogHost.ShowAsync(dialog, \"RootHost\"): the whole window is covered.", DialogButtons.Ok)
            .Elevation(_vm.DialogElevation);
        var response = await DialogHost.ShowAsync(dialog, "RootHost");
        _vm.LastResult = response.Result.ToString();
    }

    private static string Describe(UIElement element) => element is Dialog { Title: { Length: > 0 } title } ? $"\"{title}\"" : element.GetType().Name;

    #endregion

    #region Popups

    private UIElement PopupSection() => Ui.Section("Popups",
        "A Popup shows its child above everything else, next to its PlacementTarget, and stays inside the window: it " +
        "flips to the other side when there isn't enough room. Clicking outside or pressing Escape closes it, unless " +
        "StaysOpen is set.",
        Ui.Columns(340, MenuDemo(), PlacementDemo()),
        Ui.Readout(_vm, v => v.LastPopupEvent),
        Ui.Code("new Popup()\n" +
                "    .PlacementTarget(button)\n" +
                "    .Placement(PlacementMode.BottomLeft)\n" +
                "    .Offset(0, 4)\n" +
                "    .Child(menu)"));

    private UIElement MenuDemo()
    {
        var target = Ui.IconButton(MaterialIconKind.MoreVert, "Actions", ButtonVariant.Outlined);
        var menu = new Popup()
            .PlacementTarget(target)
            .Placement(PlacementMode.BottomLeft)
            .VerticalOffset(4)
            .OnOpened(() => _vm.LastPopupEvent = "Menu opened")
            .OnClosed(() => _vm.LastPopupEvent = "Menu closed");

        menu.Child(new ListBox()
            .MinWidth(200)
            .Items("Rename", "Duplicate", "Move to…", "Delete")
            .OnSelectionChanged(item =>
            {
                if (item != null)
                {
                    _vm.LastPopupEvent = $"Chose \"{item}\"";
                    menu.IsOpen = false;
                }
            }));

        target.OnClick(() =>
        {
            ((ListBox)menu.Child!).SelectedIndex = -1;
            menu.IsOpen = !menu.IsOpen;
        });

        return Ui.Demo("Menu",
            Ui.Note("A list in a popup, placed below the button's left edge; choosing an item closes it."),
            target.HorizontalAlignment(HorizontalAlignment.Left),
            menu);
    }

    private UIElement PlacementDemo()
    {
        var target = new Button().Variant(ButtonVariant.Tonal).HorizontalAlignment(HorizontalAlignment.Left)
            .Command(_vm.TogglePlacementPopupCommand)
            .Bind(ContentControl.ContentProperty, _vm, v => (object?)(v.IsPlacementPopupOpen ? "Close popup" : "Open popup"));

        var popup = new Popup()
            .PlacementTarget(target)
            .BindIsOpen(_vm, v => v.IsPlacementPopupOpen, (v, open) => v.IsPlacementPopupOpen = open)
            .Bind(Popup.PlacementProperty, _vm, v => v.Placement)
            .Bind(Popup.HorizontalOffsetProperty, _vm, v => v.HorizontalOffset)
            .Bind(Popup.VerticalOffsetProperty, _vm, v => v.VerticalOffset)
            .Bind(Popup.StaysOpenProperty, _vm, v => v.StaysOpen)
            .Bind(Popup.MatchTargetWidthProperty, _vm, v => v.MatchTargetWidth)
            .Bind(Popup.ElevationProperty, _vm, v => v.PopupElevation)
            .Bind(Popup.BorderThicknessProperty, _vm, v => new Thickness(v.ShowPopupBorder ? 1 : 0))
            .Themed(Popup.BorderBrushProperty, c => c.Outline)
            .OnOpened(() => _vm.LastPopupEvent = $"Popup opened ({_vm.Placement})")
            .OnClosed(() => _vm.LastPopupEvent = "Popup closed")
            // Padding on a Border rather than a margin on the child: a popup's child is rendered without its margin.
            .Child(new Border().Padding(16, 12).Child(new StackPanel().Spacing(2).Children(
                new TextBlock("Popup").TitleSmall(),
                new TextBlock().BodySmall().Muted().BindText(_vm, v => $"Placement: {v.Placement}"))));

        var placements = new WrapPanel().Spacing(12, 8);
        foreach (var mode in Enum.GetValues<PlacementMode>())
        {
            placements.Add(new RadioButton(mode.ToString())
                .GroupName("popup-placement")
                .BindIsChecked(_vm, v => v.Placement, (v, p) => v.Placement = p, mode));
        }

        return Ui.Demo("Placement and options",
            target,
            popup,
            Ui.Labeled("Placement", placements),
            Ui.Columns(200,
                Ui.SliderSetting("Horizontal offset", _vm, v => v.HorizontalOffset, (v, o) => v.HorizontalOffset = o, -40, 40, step: 1),
                Ui.SliderSetting("Vertical offset", _vm, v => v.VerticalOffset, (v, o) => v.VerticalOffset = o, -40, 40, step: 1),
                Ui.SliderSetting("Elevation", _vm, v => v.PopupElevation, (v, e) => v.PopupElevation = e, 0, 12, step: 1)),
            Ui.Row(
                new Switch("Stays open").BindIsChecked(_vm, v => v.StaysOpen, (v, on) => v.StaysOpen = on),
                new Switch("Match target width").BindIsChecked(_vm, v => v.MatchTargetWidth, (v, on) => v.MatchTargetWidth = on),
                new Switch("Border").BindIsChecked(_vm, v => v.ShowPopupBorder, (v, on) => v.ShowPopupBorder = on)));
    }

    #endregion
}
