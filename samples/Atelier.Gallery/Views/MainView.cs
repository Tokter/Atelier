using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atelier.Gallery.Views
{
    public class MainView : KeybindingHandler
    {
        private readonly MainViewModel _viewModel;

        public MainView(MainViewModel viewModel) : base("Global")
        {
            _viewModel = viewModel;
            DataContext = _viewModel;

            var windowLayout = new Grid()
                .Rows(GridLength.Pixels(44), GridLength.Star)
                .Children(
                    CreateTitlebar().Row(0),
                    Pages().Row(1));

            this.Content = new DialogHost
            {
                Identifier = "RootHost",
                Content = windowLayout
            };
        }

        public override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;

            // When focus is outside the active page (e.g. sidebar navigation or window chrome),
            // allow the active page ViewModel to handle matching global keybindings
            if (_viewModel.CurrentPage != null)
            {
                if (KeybindingManager.TryExecuteGesture(Group, e.Key, e.Modifiers, _viewModel.CurrentPage))
                {
                    e.Handled = true;
                }
            }
        }

        private TitleBar CreateTitlebar()
        {
            var titleBar = new TitleBar
            {
                Title = "Atelier Studio",
                Icon = new Image("Assets/Icons/Atelier.png") { Width = 22, Height = 22, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center },
                Content =
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }.Children(
                        new TextBox
                        {
                            Placeholder = "Search controls, themes, settings...",
                            Width = 320,
                            Height = 32,
                            //CornerRadius = new CornerRadius(16),
                            Padding = new Thickness(14, 6),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new Button
                        {
                            Variant = ButtonVariant.Text,
                            Height = 32,
                            Padding = new Thickness(14, 4),
                            CornerRadius = new CornerRadius(16),
                            VerticalAlignment = VerticalAlignment.Center,
                            Command = _viewModel.NewWindowCommand,
                            Content =
                                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                                .Children(
                                    new Icon { Kind = MaterialIconKind.OpenInNew, Size = 18, VerticalAlignment = VerticalAlignment.Center },
                                    new TextBlock("New window").LabelMedium().VerticalAlign(VerticalAlignment.Center)
                                )
                        },
                        new Button
                        {
                            Variant = ButtonVariant.Tonal,
                            Height = 32,
                            Padding = new Thickness(14, 4),
                            CornerRadius = new CornerRadius(16),
                            VerticalAlignment = VerticalAlignment.Center,
                            Command = _viewModel.ToggleThemeCommand,
                            Content =
                                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                                .Children(
                                    new Icon
                                    {
                                        Kind = ThemeManager.Current.IsDark ? MaterialIconKind.LightMode : MaterialIconKind.DarkMode,
                                        Size = 18,
                                        VerticalAlignment = VerticalAlignment.Center
                                    }.BindKind(_viewModel, x => x.CurrentThemeIcon),
                                    new TextBlock()
                                        .LabelMedium()
                                        .VerticalAlign(VerticalAlignment.Center)
                                        .BindText(_viewModel, x => x.CurrentThemeMode)
                                )
                        }
                    )
            };
            return titleBar;
        }

        private UIElement Pages()
        {
            var dockPanel = new DockPanel { LastChildFill = true }
            .Children(
                new ListBox { Width = 250 }
                .BindSelectedItem(_viewModel, x => x.CurrentPage, (vm, p) => vm.CurrentPage = p)
                .BindItemsSource(_viewModel, x => x.Pages)
                .Margin(10)
                .Dock(Dock.Left)
                .ItemTemplate<PageViewModel>(item =>
                {
                    return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
                    .Children(
                        new Icon().BindKind(item, x => x.PageIcon),
                        new TextBlock()
                            .LabelLarge()
                            .VerticalAlign(VerticalAlignment.Center)
                            .BindText(item, x => x.PageTitle)
                    );
                }),

                new ContentControl()
                    .BindContent(_viewModel, x => x.CurrentPage)
                    .Margin(20.0f)
            );
            return dockPanel;
        }

    }
}
