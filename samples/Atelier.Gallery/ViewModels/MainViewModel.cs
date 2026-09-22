using Atelier.Controls;
using Atelier.Theming;
using Atelier.Theming.Material;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atelier.Gallery.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _currentThemeMode = "Switch to Dark Mode";

        [ObservableProperty]
        private MaterialIconKind _currentThemeIcon = MaterialIconKind.LightMode;

        [ObservableProperty]
        private List<PageViewModel> _pages = new List<PageViewModel>();

        [ObservableProperty]
        private PageViewModel? _currentPage;

        public MainViewModel()
        {
            var initialPage = new CheckboxesViewModel();
            _pages.Add(initialPage);
            _pages.Add(new TextBoxesViewModel());
            _pages.Add(new CardsViewModel());
            _pages.Add(new IconsViewModel());
            _pages.Add(new TypographyViewModel());
            _pages.Add(new TreeViewViewModel());
            _pages.Add(new TransformationViewModel());
            _pages.Add(new DialogHostViewModel());
            _pages.Add(new PropertyGridViewModel());
            _pages.Add(new LayoutViewModel());
            _pages.Add(new KeybindingViewModel());
            _pages.Add(new TransitionsViewModel());
            _currentPage = initialPage;
        }


        [RelayCommand]
        private void ToggleTheme()
        {
            if (ThemeManager.Current.IsDark)
            {
                ThemeManager.Current = MaterialTheme.CreateLight();
                CurrentThemeMode = "Switch to Dark Mode";
                CurrentThemeIcon = MaterialIconKind.LightMode;
            }
            else
            {
                ThemeManager.Current = MaterialTheme.CreateDark();
                CurrentThemeMode = "Switch to Light Mode";
                CurrentThemeIcon = MaterialIconKind.DarkMode;
            }
        }
    }
}
