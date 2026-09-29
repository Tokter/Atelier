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

    // The commands of the current page (its group, on its view model), wherever the focus is: single keys, chords and
    // the command palette find them.
    private readonly KeybindingScope _pageScope = new();

    public MainView(MainViewModel viewModel) : base("Global")
    {
        _vm = viewModel;
        DataContext = _vm;
        _vm.ShowCommandEditorAction = ShowCommandEditor;
        _vm.ShowCommandPaletteAction = ShowCommandPalette;
        AdditionalScopes.Add(_pageScope);
        UpdatePageScope();

        _search = new TextBox()
            .Placeholder("Search pages (Ctrl+F)")
            .LeadingIconKind(MaterialIconKind.Search)
            .MinWidth(140)
            .FieldHeight(36)
            .BindText(_vm, v => v.SearchText, (v, text) => v.SearchText = text);

        // The navigation can be resized with the splitter next to it (between 180 and 400 px).
        var body = new Grid()
            .Columns(GridLength.Pixels(248), GridLength.Auto, GridLength.Star)
            .Children(
                Navigation(),
                new GridSplitter().Column(1).Margin(0, 4, 0, 12).ToolTip("Drag to resize the navigation; double-click to reset"),
                new ContentControl().Column(2).BindContent(_vm, v => v.CurrentPage));
        body.ColumnDefinitions[0].MinWidth = 180;
        body.ColumnDefinitions[0].MaxWidth = 400;

        Content = new DialogHost()
            .Identifier("RootHost")
            .Content(new Grid()
                .Rows(GridLength.Pixels(48), GridLength.Star)
                .Children(TitleBar(), body.Row(1)));
    }

    // The command editor in a dialog; changes apply at once and are saved (see CommandSettings).
    private void ShowCommandEditor() =>
        _ = new Dialog("Customize commands")
            .Content(new KeybindingEditor().Width(1080).Height(580))
            .AddButton("Close", DialogResult.Ok, isDefault: true, isCancel: true)
            .MaxWidth(1160)
            .ShowAsync(this);

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
    }

    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        UpdatePageScope();
    }

    protected override void OnDetachedFromVisualTree()
    {
        _vm.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnDetachedFromVisualTree();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage)) UpdatePageScope();
    }

    private void UpdatePageScope()
    {
        _pageScope.Group = _vm.CurrentPage?.CommandGroup ?? string.Empty;
        _pageScope.Target = _vm.CurrentPage;
    }

    // The command palette, for the commands that work where the focus is (but not the one that opens it).
    private void ShowCommandPalette() =>
        CommandPalette.Show(this, c => !(c.Descriptor.Group == "Global" && c.Descriptor.Name == "CommandPalette"));

    // The main menu in the title bar. Icons and shortcuts come from the [Command] declarations (Ctrl+T, F1, ...); Alt or F10
    // reach the menu from the keyboard.
    private Menu MainMenu() => new Menu()
        .WithItemSetup((MenuItem item, PageViewModel page) => item
            .Header(page.PageTitle)
            .Icon(page.PageIcon)
            .OnClick(() => _vm.CurrentPage = page))
        .Items(
            new MenuItem("_File").Items(
                new MenuItem().Command(_vm.NewWindowCommand),
                new Separator(),
                new MenuItem("E_xit").Icon(MaterialIconKind.Logout).InputGestureText("Alt+F4").OnClick(() => Host?.Close())),
            new MenuItem("_View").Items(ViewMenuItems()),
            new MenuItem("_Help").Items(
                new MenuItem().Command(new ShowShortcutsHelpCommand()),
                new MenuItem("_Search pages").Icon(MaterialIconKind.Search).InputGestureText("Ctrl+F").OnClick(() => _search.Focus())));

    private object[] ViewMenuItems() =>
    [
        new MenuItem().Command(_vm.ToggleThemeCommand),
        new MenuItem().Command(_vm.ToggleFpsOverlayCommand),
        new MenuItem().Command(_vm.ShowCommandPaletteCommand),
        new MenuItem().Command(_vm.CustomizeCommandsCommand),
#if DEBUG
        // The developer tools' command: its label, icon and F12 come from its registration, like any other command.
        new MenuItem().Command(Atelier.DevTools.DevToolsManager.ToggleCommand),
#endif
        new Separator(),
        new MenuItem("_Go to page").Icon(MaterialIconKind.Pageview).ItemsSource(_vm.Pages),
    ];

    private TitleBar TitleBar() => new TitleBar()
        .Title("Atelier Gallery")
        .Icon(new Image("Assets/Icons/Atelier.png").Size(22, 22))
        .Menu(MainMenu())
        // The search box takes the space the buttons leave (up to 320 px), so the title bar fits narrow windows too.
        .Content(new Grid()
            .Columns(GridLength.Star, GridLength.Auto, GridLength.Auto)
            .ColumnSpacing(8)
            .MaxWidth(620)
            .Margin(16, 0)
            .VerticalAlignment(VerticalAlignment.Center)
            .Children(
                _search,
                new Button().Variant(ButtonVariant.Text).Column(1).Command(_vm.NewWindowCommand),
                new Button()
                    .Column(2)
                    .Variant(ButtonVariant.Tonal)
                    .Command(_vm.ToggleThemeCommand) // the tooltip (description and Ctrl+T) comes from the command
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
