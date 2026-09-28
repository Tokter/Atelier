using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class TabsView : GalleryPage
{
    private readonly TabsViewModel _vm;

    public TabsView(TabsViewModel viewModel)
        : base(MaterialIconKind.Tab, "Tabs",
            "A tab control shows one of several pages under a strip of tabs: Material Design 3 primary or secondary tabs, " +
            "or browser-style tabs that can be closed, added and dragged into a new order.")
    {
        _vm = viewModel;

        Settings(
            new Switch("Closeable").ShowThumbIcon().BindIsChecked(_vm, v => v.Closeable, (v, on) => v.Closeable = on),
            new Switch("Add button").ShowThumbIcon().BindIsChecked(_vm, v => v.ShowAddButton, (v, on) => v.ShowAddButton = on),
            new Switch("Reorder").ShowThumbIcon().BindIsChecked(_vm, v => v.CanReorder, (v, on) => v.CanReorder = on),
            new Button("Reset").Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        Sections(MaterialSection(), BrowserSection());
    }

    private static UIElement Page(string title, string text) =>
        new StackPanel().Spacing(8).Margin(16).Children(
            new TextBlock(title).TitleMedium(),
            new TextBlock(text).BodyMedium().Muted().TextWrapping());

    private UIElement MaterialSection() => Ui.Section("Material Design 3 tabs",
        "Primary tabs put the selected label in the primary color over an indicator as wide as the label; with icons, the " +
        "icon sits above the label. Secondary tabs, for a level below, use a thinner indicator under the whole tab. The " +
        "indicator slides to the selected tab. Arrow keys, Home and End move between tabs; tabs can be dragged too.",
        Ui.Demo("Primary, with icons",
            new TabControl().Height(200).CanReorderTabs(false).Tabs(
                new TabItem("Flights", Page("Flights", "Search one-way and round-trip flights.")).Icon(MaterialIconKind.Flight),
                new TabItem("Trips", Page("Trips", "Your upcoming and past trips.")).Icon(MaterialIconKind.Luggage),
                new TabItem("Explore", Page("Explore", "Ideas for your next journey.")).Icon(MaterialIconKind.Explore))),
        Ui.Demo("Secondary",
            new TabControl().TabStyle(TabStyle.Secondary).Height(160).Tabs(
                new TabItem("Overview", Page("Overview", "The secondary tabs of a page.")),
                new TabItem("Specifications", Page("Specifications", "Weight, size and materials.")),
                new TabItem("Reviews", Page("Reviews", "What other people think.")),
                new TabItem("Support", Page("Support", "Manuals and help.")))));

    private UIElement BrowserSection() => Ui.Section("Browser tabs",
        "Bound to a collection of documents. A header template draws each tab with an icon, the title and a dot for unsaved " +
        "changes. Close tabs with their button, a middle click or Delete (the modified Program.cs asks first by refusing); " +
        "the + button adds a document; drag tabs to reorder the collection.",
        new Border().Height(240).CornerRadius(12).ClipToBounds(true).Child(
            new TabControl()
                .TabStyle(TabStyle.Browser)
                .BindItemsSource(_vm, v => v.Documents)
                .BindSelectedIndex(_vm, v => v.SelectedDocument, (v, i) => v.SelectedDocument = i)
                .Bind(TabControl.AreTabsCloseableProperty, _vm, v => v.Closeable)
                .Bind(TabControl.ShowAddButtonProperty, _vm, v => v.ShowAddButton)
                .Bind(TabControl.CanReorderTabsProperty, _vm, v => v.CanReorder)
                .WithHeaderTemplate((DocumentTab doc) => new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
                    new Icon(doc.Icon, 18).VerticalAlignment(VerticalAlignment.Center),
                    new Badge(new TextBlock(doc.Title).VerticalAlignment(VerticalAlignment.Center))
                        .BadgeOffset(10, 6)
                        .BindIsBadgeVisible(doc, d => d.IsModified)))
                .WithContentTemplate((DocumentTab doc) => Page(doc.Title, doc.Body))
                .OnTabClosing((s, e) =>
                {
                    if (e.Item is DocumentTab { Title: "Program.cs", IsModified: true } doc)
                    {
                        e.Cancel = true;
                        doc.IsModified = false;
                        _vm.LastEvent = "TabClosing canceled: Program.cs had unsaved changes (now saved; close again)";
                    }
                })
                .OnTabClosed(item => _vm.LastEvent = $"TabClosed → {((DocumentTab)item).Title}")
                .OnAddTabRequested((s, e) =>
                {
                    e.NewItem = _vm.CreateDocument();
                    _vm.LastEvent = "AddTabRequested → a new document";
                })
                .OnTabMoved((s, e) => _vm.LastEvent = $"TabMoved → {((DocumentTab)e.Item).Title} from {e.OldIndex} to {e.NewIndex}")),
        Ui.Readout(_vm, v => v.LastEvent),
        Ui.Readout(_vm, v => v.Order),
        Ui.Code("new TabControl()\n    .TabStyle(TabStyle.Browser).AreTabsCloseable().ShowAddButton()\n    .BindItemsSource(vm, v => v.Documents)\n" +
                "    .WithHeaderTemplate((Doc d) => new StackPanel().Children(new Icon(d.Icon), new TextBlock(d.Title)))\n" +
                "    .OnAddTabRequested((s, e) => e.NewItem = new Doc())"));
}
