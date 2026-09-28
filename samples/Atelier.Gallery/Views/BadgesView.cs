using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class BadgesView : GalleryPage
{
    private readonly BadgesViewModel _vm;

    public BadgesView(BadgesViewModel viewModel)
        : base(MaterialIconKind.NotificationsActive, "Badges",
            "A badge wraps an element, such as an icon or a button, and shows a small dot or a short count at its " +
            "top-right corner, for example the number of unread messages.")
    {
        _vm = viewModel;

        Settings(
            new Switch("Badges visible").ShowThumbIcon().BindIsChecked(_vm, v => v.BadgesVisible, (v, on) => v.BadgesVisible = on),
            new Button("Reset").Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        Sections(IconSection(), ButtonSection(), LiveSection());
    }

    private static Button IconButton(MaterialIconKind icon, string toolTip) =>
        new Button().Variant(ButtonVariant.Text).Padding(8).MinWidth(40).Content(new Icon(icon, 24)).ToolTip(toolTip);

    private UIElement IconSection() => Ui.Section("Small and large badges",
        "Without a count the badge is a small dot, meaning \"something new\". With a count or a short text it is the large " +
        "badge; counts above MaxCount (999) show a \"+\".",
        Ui.Row(
            Labeled("Dot", new Badge(new Icon(MaterialIconKind.Notifications, 24)).BindIsBadgeVisible(_vm, v => v.BadgesVisible)),
            Labeled("3", new Badge(new Icon(MaterialIconKind.Mail, 24)).Count(3).BindIsBadgeVisible(_vm, v => v.BadgesVisible)),
            Labeled("42", new Badge(new Icon(MaterialIconKind.Chat, 24)).Count(42).BindIsBadgeVisible(_vm, v => v.BadgesVisible)),
            Labeled("1234", new Badge(new Icon(MaterialIconKind.Inbox, 24)).Count(1234).BindIsBadgeVisible(_vm, v => v.BadgesVisible)),
            Labeled("\"New\"", new Badge(new Icon(MaterialIconKind.Campaign, 24)).Text("New").BindIsBadgeVisible(_vm, v => v.BadgesVisible))),
        Ui.Code("new Badge(new Icon(MaterialIconKind.Mail)).Count(3)"));

    private static StackPanel Labeled(string label, UIElement element) =>
        new StackPanel().Spacing(12).Width(72).Children(
            element.HorizontalAlignment(HorizontalAlignment.Center),
            new TextBlock(label).BodySmall().Muted().HorizontalAlignment(HorizontalAlignment.Center));

    private UIElement ButtonSection() => Ui.Section("On buttons",
        "Any element can wear a badge: icon buttons, common buttons or a whole card. The badge is drawn over the corner " +
        "and doesn't change the layout; BadgeOffset moves it, and BadgeBackground and BadgeForeground change its colors.",
        Ui.Row(
            new Badge(IconButton(MaterialIconKind.ShoppingCart, "Cart")).Count(2).BindIsBadgeVisible(_vm, v => v.BadgesVisible),
            new Badge(IconButton(MaterialIconKind.Settings, "Settings")).BindIsBadgeVisible(_vm, v => v.BadgesVisible).BadgeOffset(-6, 6),
            new Badge(new Button("Inbox").Variant(ButtonVariant.Tonal)).Count(12).BindIsBadgeVisible(_vm, v => v.BadgesVisible),
            new Badge(new Button("Updates").Variant(ButtonVariant.Outlined)).Text("New").BindIsBadgeVisible(_vm, v => v.BadgesVisible),
            new Badge(new Button("Sync").Variant(ButtonVariant.Filled)).Count(5).BindIsBadgeVisible(_vm, v => v.BadgesVisible)
                .Themed(Badge.BadgeBackgroundProperty, c => c.Tertiary)
                .Themed(Badge.BadgeForegroundProperty, c => c.OnTertiary)));

    private UIElement LiveSection() => Ui.Section("Bound count",
        "Bind the count to your data. A count of 0 hides the badge unless ShowZero is set.",
        Ui.Row(
            new Badge(IconButton(MaterialIconKind.Mail, "Messages"))
                .BindCount(_vm, v => (int?)v.Unread)
                .Bind(Badge.ShowZeroProperty, _vm, v => v.ShowZero)
                .BindIsBadgeVisible(_vm, v => v.BadgesVisible),
            new Button("−").Variant(ButtonVariant.Outlined).Command(_vm.DecrementCommand),
            new Button("+").Variant(ButtonVariant.Outlined).Command(_vm.IncrementCommand),
            new Switch("Show zero").BindIsChecked(_vm, v => v.ShowZero, (v, on) => v.ShowZero = on),
            Ui.Readout(_vm, v => v.CountText)),
        Ui.Code("new Badge(mailButton).BindCount(vm, v => v.Unread)"));
}
