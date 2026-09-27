using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class ToolTipsView : GalleryPage
{
    private readonly ToolTipsViewModel _vm;

    public ToolTipsView(ToolTipsViewModel viewModel)
        : base(MaterialIconKind.Comment, "Tooltips",
            "A tooltip shows a short description while the pointer rests on an element. Plain tooltips are text; rich " +
            "tooltips can hold any element, including buttons and links, and stay open while the pointer is over them.")
    {
        _vm = viewModel;
        DataContext = _vm;

        Settings(
            new Switch("Show on keyboard focus").ShowThumbIcon()
                .BindIsChecked(_vm, v => v.ShowOnKeyboardFocus, (v, on) => v.ShowOnKeyboardFocus = on)
                .ToolTip("A global setting: focusing an element with Tab shows its tooltip"),
            new Button("Reset").Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        Sections(PlainSection(), RichSection(), PlacementSection());

        // ToolTipService events are static: subscribe only while the page is shown.
        this.OnAttachedToVisualTree(() =>
        {
            ToolTipService.ToolTipOpened += OnToolTipOpened;
            ToolTipService.ToolTipClosed += OnToolTipClosed;
        });
        this.OnDetachedFromVisualTree(() =>
        {
            ToolTipService.ToolTipOpened -= OnToolTipOpened;
            ToolTipService.ToolTipClosed -= OnToolTipClosed;
        });
    }

    private void OnToolTipOpened(object? sender, UIElement owner) => _vm.LastEvent = $"ToolTipOpened → {Describe(owner)}";

    private void OnToolTipClosed(object? sender, UIElement owner) => _vm.LastEvent = $"ToolTipClosed → {Describe(owner)}";

    private static string Describe(UIElement owner) => ToolTipService.GetToolTip(owner) is string text ? $"\"{text}\"" : owner.GetType().Name;

    private UIElement PlainSection() => Ui.Section("Plain tooltips",
        "A string makes a plain tooltip: short text on a small surface that doesn't take any input. It opens after the " +
        "pointer rests for half a second and closes when it leaves; moving on to the next element shows its tooltip at once.",
        Ui.Columns(320,
            Ui.Demo("Icon buttons",
                Ui.Row(
                    FormatButton(MaterialIconKind.FormatBold, "Bold (Ctrl+B)"),
                    FormatButton(MaterialIconKind.FormatItalic, "Italic (Ctrl+I)"),
                    FormatButton(MaterialIconKind.FormatUnderlined, "Underline (Ctrl+U)"),
                    FormatButton(MaterialIconKind.Link, "Insert link (Ctrl+K)")),
                Ui.Note("Move along the row: after the first tooltip, the others open without delay.")),

            Ui.Demo("Disabled elements",
                Ui.Row(
                    new Button("Publish")
                        .BindIsEnabled(_vm, v => v.CanPublish)
                        .ToolTip("Accept the terms to publish")
                        .ToolTipShowOnDisabled(),
                    new CheckBox("I accept the terms").BindIsChecked(_vm, v => v.CanPublish, (v, ok) => v.CanPublish = ok)),
                Ui.Note("ToolTipShowOnDisabled lets a disabled element explain why it's disabled."))),

        Ui.Columns(320,
            Ui.Demo("Any element",
                Ui.Row(
                    new TextBlock("Hover this text").ToolTip("TextBlocks, icons and panels can have tooltips too"),
                    new Icon(MaterialIconKind.HelpOutline, 22).ToolTip("Icons make good tooltip anchors")),
                new TextBox().Label("Username").ToolTip("3–20 letters, digits or underscores").Width(280)
                    .HorizontalAlignment(HorizontalAlignment.Left)),

            Ui.Demo("Long text wraps",
                new Button("Hover for a long tooltip").Variant(ButtonVariant.Outlined)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .ToolTip("Plain tooltips wrap at 320 pixels, so a longer explanation stays readable instead of " +
                             "running across the whole window."))),

        Ui.Code("new Button().Content(new Icon(MaterialIconKind.FormatBold)).ToolTip(\"Bold (Ctrl+B)\")"));

    private static Button FormatButton(MaterialIconKind icon, string toolTip) =>
        new Button().Variant(ButtonVariant.Text).Padding(6).MinWidth(32).Content(new Icon(icon, 20)).ToolTip(toolTip);

    private UIElement RichSection() => Ui.Section("Rich tooltips",
        "Any other content makes a rich tooltip on a larger, elevated surface. It stays open while the pointer moves " +
        "onto it, so its buttons and links can be clicked, and closes shortly after the pointer leaves both.",
        Ui.Columns(320,
            Ui.Demo("RichToolTip",
                Ui.Row(
                    new Icon(MaterialIconKind.Info, 24)
                        .Themed(Control.ForegroundProperty, c => c.Primary)
                        .ToolTip(new RichToolTip()
                            .Title("Sync is paused")
                            .Text("Changes are kept on this device and uploaded as soon as you reconnect.")
                            .Actions(
                                new Button("Retry now").Variant(ButtonVariant.Text).Command(_vm.RunActionCommand, "Retry now"),
                                new Button("Settings").Variant(ButtonVariant.Text).Command(_vm.RunActionCommand, "Settings"))),
                    new TextBlock("Hover the info icon, then click an action").BodyMedium()),
                Ui.Readout(_vm, v => v.LastAction)),

            Ui.Demo("Custom element, bound to the page's view model",
                new Button("Ada Lovelace")
                    .Variant(ButtonVariant.Tonal)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .ToolTip(ProfileCard()),
                Ui.Row(
                    new Button("Change status").Variant(ButtonVariant.Outlined).Command(_vm.NextStatusCommand),
                    Ui.Readout(_vm, v => $"Status = {v.Status}")),
                Ui.Note("A tooltip gets its element's DataContext, so its content can bind to the same view model."))),

        Ui.Demo("Bound plain tooltip",
            Ui.Row(
                new Icon(MaterialIconKind.Circle, 16).IsFilled()
                    .Themed(Control.ForegroundProperty, c => c.Tertiary)
                    .BindToolTip(_vm, v => $"Status: {v.Status}"),
                new TextBlock("Hover the dot; the open tooltip follows status changes").BodyMedium())),

        Ui.Code("icon.ToolTip(new RichToolTip()\n" +
                "    .Title(\"Sync is paused\")\n" +
                "    .Text(\"Changes are kept on this device ...\")\n" +
                "    .Actions(new Button(\"Retry now\").Variant(ButtonVariant.Text).Command(vm.RetryCommand)))"));

    private UIElement ProfileCard() =>
        new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(
            new Border()
                .Size(40, 40)
                .CornerRadius(20)
                .Themed(Border.BackgroundProperty, c => c.PrimaryContainer)
                .Child(new TextBlock("AL").Center().Themed(TextBlock.ForegroundProperty, c => c.OnPrimaryContainer)),
            new StackPanel().Spacing(2).Children(
                new TextBlock("Ada Lovelace").FontWeight(FontWeight.Medium),
                new TextBlock().BindText((ToolTipsViewModel v) => v.Status),
                new Button("Send message").Variant(ButtonVariant.Text).Margin(-12, 4, 0, 0)
                    .Command(_vm.RunActionCommand, "Send message")));

    private UIElement PlacementSection() => Ui.Section("Placement and timing",
        "Tooltips appear below their element by default and flip when there isn't enough room. They can also appear on " +
        "another side or below the pointer. The delay before a tooltip opens can be set per element.",
        Ui.Demo("Placement",
            Ui.Row(
                Placed("Bottom (default)", PlacementMode.Bottom),
                Placed("Top", PlacementMode.Top),
                Placed("Left", PlacementMode.Left),
                Placed("Right", PlacementMode.Right),
                Placed("Pointer", PlacementMode.Pointer))),

        Ui.Columns(320,
            Ui.Demo("Show delay",
                Ui.SliderSetting("Delay (ms)", _vm, v => v.ShowDelayMs, (v, ms) => v.ShowDelayMs = ms, 0, 2000, "0", 100),
                new Button("Hover me").Variant(ButtonVariant.Outlined)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .ToolTip("Opened after the delay set above")
                    .Bind(ToolTipService.ShowDelayProperty, _vm, v => (TimeSpan?)TimeSpan.FromMilliseconds(v.ShowDelayMs))),

            Ui.Demo("Events",
                Ui.Readout(_vm, v => v.LastEvent),
                Ui.Note("ToolTipService.ToolTipOpened and ToolTipClosed report every tooltip. A press, the mouse wheel " +
                        "or Escape closes a tooltip."))),

        Ui.Code("button.ToolTip(\"Delete\").ToolTipPlacement(PlacementMode.Pointer).ToolTipShowDelay(TimeSpan.FromMilliseconds(200))"));

    private static Button Placed(string label, PlacementMode placement) =>
        new Button(label).Variant(ButtonVariant.Outlined).ToolTip($"Placement: {placement}").ToolTipPlacement(placement);
}
