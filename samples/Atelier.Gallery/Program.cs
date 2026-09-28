using System;
using Atelier.Core.ViewResolution;
using Atelier.Gallery.ViewModels;
using Atelier.Gallery.Views;
using Atelier.Platform.Silk;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Gallery;

internal static class Program
{
    private static void Main()
    {
        // Keybindings declared with [Keybinding] are discovered at compile time by the source generator.
        Atelier.Generated.GeneratedKeybindings.RegisterKeybindings();
        RegisterViews();

        // ATELIER_GALLERY_SNAPSHOT=<directory> renders the pages to PNG files instead of opening a window.
        if (Infrastructure.Snapshot.TryRun())
        {
            return;
        }

        // ATELIER_GALLERY_THEME=dark starts in the dark theme.
        ThemeManager.Current = string.Equals(Environment.GetEnvironmentVariable("ATELIER_GALLERY_THEME"), "dark", StringComparison.OrdinalIgnoreCase)
            ? MaterialTheme.CreateDark()
            : MaterialTheme.CreateLight();

        // ATELIER_GALLERY_WINDOWS=N opens N windows at startup (for multi-window testing).
        var mainWindow = OpenGalleryWindow();
        int extraWindows = int.TryParse(Environment.GetEnvironmentVariable("ATELIER_GALLERY_WINDOWS"), out int count) ? count - 1 : 0;
        for (int i = 0; i < extraWindows; i++)
        {
            OpenGalleryWindow();
        }

        // Runs until the last window closes.
        mainWindow.Run();
    }

    /// <summary>Maps each page view model to its view, so a ContentControl showing a page view model shows the page.</summary>
    private static void RegisterViews() =>
        ViewLocator.Current
            .Register<ButtonsViewModel>(vm => new ButtonsView(vm))
            .Register<CheckboxesViewModel>(vm => new CheckboxesView(vm))
            .Register<TextBoxesViewModel>(vm => new TextBoxesView(vm))
            .Register<ComboBoxViewModel>(vm => new ComboBoxView(vm))
            .Register<RangeControlsViewModel>(vm => new RangeControlsView(vm))
            .Register<ColorPickerViewModel>(vm => new ColorPickerView(vm))
            .Register<DateTimePickersViewModel>(vm => new DateTimePickersView(vm))
            .Register<ListsViewModel>(vm => new ListsView(vm))
            .Register<TreeViewViewModel>(vm => new TreeViewView(vm))
            .Register<CardsViewModel>(vm => new CardsView(vm))
            .Register<IconsViewModel>(vm => new IconsView(vm))
            .Register<BadgesViewModel>(vm => new BadgesView(vm))
            .Register<TabsViewModel>(vm => new TabsView(vm))
            .Register<MenusViewModel>(vm => new MenusView(vm))
            .Register<TypographyViewModel>(vm => new TypographyView(vm))
            .Register<LayoutViewModel>(vm => new LayoutView(vm))
            .Register<DialogHostViewModel>(vm => new DialogHostView(vm))
            .Register<ToolTipsViewModel>(vm => new ToolTipsView(vm))
            .Register<TransformationViewModel>(vm => new TransformationView(vm))
            .Register<TransitionsViewModel>(vm => new TransitionsView(vm))
            .Register<PropertyGridViewModel>(vm => new PropertyGridView(vm))
            .Register<KeybindingViewModel>(vm => new KeybindingView(vm));

    /// <summary>
    /// Opens a gallery window with its own view model. Works before and while the application runs.
    /// </summary>
    internal static SilkWindow OpenGalleryWindow()
    {
        // Each window keeps its own state, which is preserved across Hot Reload passes.
        var vm = new MainViewModel();

        var window = new SilkWindow(
            title: "Atelier Gallery",
            width: 1280,
            height: 860,
            isTitleLess: true,
            isTransparent: true,
            windowOpacity: 1.0f,
            iconPath: "Assets/Icons/Atelier.png");

        vm.ToggleFpsOverlayAction = () => window.ShowFpsOverlay = !window.ShowFpsOverlay;

        // Setting content via a factory lambda enables Hot Reload: the view is rebuilt from the same view model.
        window.SetContent(() => new MainView(vm));
        window.Show();
        return window;
    }
}
