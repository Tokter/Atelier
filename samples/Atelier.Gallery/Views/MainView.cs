using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

/// <summary>
/// The gallery window: a title bar with page search and theme switch, the page navigation and the current page.
/// </summary>
public class MainView : KeybindingHandler
{
    private readonly MainViewModel _vm;
    private readonly TextBox _search;

    public MainView(MainViewModel viewModel) : base("Global")
    {
        _vm = viewModel;
        DataContext = _vm;

        _search = new TextBox()
            .Placeholder("Search pages (Ctrl+F)")
            .LeadingIconKind(MaterialIconKind.Search)
            .MinWidth(140)
            .FieldHeight(36)
            .BindText(_vm, v => v.SearchText, (v, text) => v.SearchText = text);

        var body = new Grid()
            .Columns(GridLength.Pixels(248), GridLength.Star)
            .Children(Navigation(), new ContentControl().Column(1).BindContent(_vm, v => v.CurrentPage));

        Content = new DialogHost()
            .Identifier("RootHost")
            .Content(new Grid()
                .Rows(GridLength.Pixels(48), GridLength.Star)
                .Children(TitleBar(), body.Row(1)));
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        if (e.Key == Key.F && e.Modifiers == ModifierKeys.Control)
        {
            _search.Focus();
            e.Handled = true;
        }
        else if (_vm.CurrentPage != null && KeybindingManager.TryExecuteGesture(Group, e.Key, e.Modifiers, _vm.CurrentPage))
        {
            // Global keybindings declared on the current page's view model work wherever the focus is.
            e.Handled = true;
        }
    }

    private TitleBar TitleBar() => new TitleBar()
        .Title("Atelier Gallery")
        .Icon(new Image("Assets/Icons/Atelier.png").Size(22, 22))
        // The search box takes the space the buttons leave (up to 320 px), so the title bar fits narrow windows too.
        .Content(new Grid()
            .Columns(GridLength.Star, GridLength.Auto, GridLength.Auto)
            .ColumnSpacing(8)
            .MaxWidth(620)
            .Margin(16, 0)
            .VerticalAlignment(VerticalAlignment.Center)
            .Children(
                _search,
                Ui.IconButton(MaterialIconKind.OpenInNew, "New window", ButtonVariant.Text).Column(1).Command(_vm.NewWindowCommand).ToolTip("Open another gallery window with its own state"),
                new Button()
                    .Column(2)
                    .Variant(ButtonVariant.Tonal)
                    .Command(_vm.ToggleThemeCommand).ToolTip("Switch the theme in all windows (Ctrl+T)")
                    .Content(new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
                        new Icon().Size(18).VerticalAlignment(VerticalAlignment.Center).BindKind(_vm, v => v.ThemeToggleIcon),
                        new TextBlock().VerticalAlignment(VerticalAlignment.Center).BindText(_vm, v => v.ThemeToggleText)))));

    private UIElement Navigation() =>
        new Border()
            .Margin(12, 4, 0, 12)
            .Padding(8)
            .CornerRadius(16)
            .Themed(Border.BackgroundProperty, c => c.SurfaceContainerLow)
            .Child(new ListBox()
                .BindItemsSource(_vm, v => v.Pages)
                .BindSelectedItem(_vm, v => v.CurrentPage, (v, page) => v.CurrentPage = page)
                .WithItemTemplate((PageViewModel page) => new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(12)
                    .Children(
                        new Icon().Size(20).VerticalAlignment(VerticalAlignment.Center).BindKind(page, p => p.PageIcon),
                        new TextBlock().LabelLarge().VerticalAlignment(VerticalAlignment.Center).BindText(page, p => p.PageTitle))));
}
