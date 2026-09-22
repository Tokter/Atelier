using System;
using System.Collections.ObjectModel;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using Atelier.Controls;
using Atelier.Core.Inspection;
using Atelier.Core.Primitives;
using Atelier.Core.Styling;
using Atelier.Core.Tree;
using Atelier.Core.ViewResolution;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Platform.Silk;
using Atelier.Theming;
using Atelier.Theming.Material;
using Atelier.Core.Keybinding;
using Atelier.Gallery.Views;
using Atelier.Gallery.ViewModels;

namespace Atelier.Gallery;

internal static class Program
{
    private static void Main()
    {
        Console.WriteLine("Launching Atelier UI Framework Gallery...");

        // Register compile-time discovered keybindings
        Atelier.Generated.GeneratedKeybindings.RegisterKeybindings();

        // Register ViewModels with ViewLocator
        ViewLocator.Current
            .Register<CheckboxesViewModel>(vm => new CheckboxesView(vm))
            .Register<TextBoxesViewModel>(vm => new TextBoxesView(vm))
            .Register<CardsViewModel>(vm => new CardsView(vm))
            .Register<IconsViewModel>(vm => new IconsView(vm))
            .Register<TypographyViewModel>(vm => new TypographyView(vm))
            .Register<TreeViewViewModel>(vm => new TreeViewView(vm))
            .Register<TransformationViewModel>(vm => new TransformationView(vm))
            .Register<DialogHostViewModel>(vm => new DialogHostView(vm))
            .Register<PropertyGridViewModel>(vm => new PropertyGridView(vm))
            .Register<LayoutViewModel>(vm => new LayoutView(vm))
            .Register<KeybindingViewModel>(vm => new KeybindingView(vm))
            .Register<TransitionsViewModel>(vm => new TransitionsView(vm));

        // 1. Initialize ViewModel (state is preserved across Hot Reload passes)
        var vm = new MainViewModel();

        // 2. Set default initial theme
        ThemeManager.Current = MaterialTheme.CreateLight();

        // 3. Create window and bind content factory for Hot Reload
        var window = new SilkWindow(
            title: "Atelier UI - Material Design 3 Showcase",
            width: 1100,
            height: 800,
            isTitleLess: true,
            isTransparent: true,
            windowOpacity: 1.0f,
            iconPath: "Assets/Icons/Atelier.png");

        // Setting content via factory lambda enables instant Hot Reload!
        window.SetContent(() => new MainView(vm));
        window.Run();
    }
}
