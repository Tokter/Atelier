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

public enum ServerLogLevel
{
    Debug,
    Information,
    Warning,
    Error
}

[Inspectable]
public partial class ServerConfigModel
{
    [InspectableProperty("Server Host", "Network")]
    public string Host { get; set; } = "api.atelier.design";

    [InspectableProperty("Port Number", "Network")]
    public int Port { get; set; } = 443;

    [InspectableProperty("Use TLS / SSL", "Security")]
    public bool EnableSsl { get; set; } = true;

    [InspectableProperty("Timeout (ms)", "Performance")]
    public int TimeoutMs { get; set; } = 5000;

    [InspectableProperty("CPU Threshold (%)", "Performance")]
    public double CpuThreshold { get; set; } = 85.5;

    [InspectableProperty("Log Level", "Diagnostics")]
    public ServerLogLevel LogLevel { get; set; } = ServerLogLevel.Information;

    [InspectableProperty("Status Color", "Appearance")]
    public Color StatusColor { get; set; } = Color.FromHex("#1E88E5");

    [InspectableProperty("Client Version", "Diagnostics", IsReadOnly = true)]
    public string Version { get; } = "v2.5.0-AOT";
}

[Inspectable]
public partial class DisplaySettingsModel
{
    [InspectableProperty("Window Title", "Window")]
    public string WindowTitle { get; set; } = "Atelier Desktop Client";

    [InspectableProperty("Dark Mode", "Appearance")]
    public bool DarkMode { get; set; } = false;

    [InspectableProperty("UI Scale", "Appearance")]
    public float UiScale { get; set; } = 1.0f;

    [InspectableProperty("Accent Hue", "Appearance")]
    public Color AccentHue { get; set; } = Color.FromHex("#6750A4");

    [InspectableProperty("Render Device", "System", IsReadOnly = true)]
    public string GpuDevice { get; } = "Direct3D 12 (Silk.NET / SkiaSharp)";
}

public partial class CityStatsDetailViewModel : ObservableObject
{
    [ObservableProperty]
    private string _cityName;

    [ObservableProperty]
    private string _country;

    [ObservableProperty]
    private string _population;

    [ObservableProperty]
    private string _landmark;

    public CityStatsDetailViewModel(string cityName, string country, string population, string landmark)
    {
        _cityName = cityName;
        _country = country;
        _population = population;
        _landmark = landmark;
    }
}

public partial class CityWeatherDetailViewModel : ObservableObject
{
    [ObservableProperty]
    private string _cityName;

    [ObservableProperty]
    private string _temperature;

    [ObservableProperty]
    private string _condition;

    [ObservableProperty]
    private string _humidity;

    public CityWeatherDetailViewModel(string cityName, string temperature, string condition, string humidity)
    {
        _cityName = cityName;
        _temperature = temperature;
        _condition = condition;
        _humidity = humidity;
    }
}

public class CityStatsDetailView : Border
{
    public CityStatsDetailView()
    {
        Padding = new Thickness(16);
        CornerRadius = new CornerRadius(8);
        Background = Color.FromHex("#1E88E5").WithAlpha(0.12f);

        var title = new TextBlock().Bold().FontSize(14);
        title.BindText<CityStatsDetailViewModel>(vm => $"🏛️ {vm.CityName} Statistics & Landmarks");

        var country = new TextBlock().FontSize(12);
        country.BindText<CityStatsDetailViewModel>(vm => $"Country: {vm.Country}");

        var pop = new TextBlock().FontSize(12);
        pop.BindText<CityStatsDetailViewModel>(vm => $"Population: {vm.Population}");

        var landmark = new TextBlock().FontSize(12);
        landmark.BindText<CityStatsDetailViewModel>(vm => $"Famous Landmark: {vm.Landmark}");

        Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 }
            .Children(title, country, pop, landmark);
    }
}

public class CityWeatherDetailView : Border
{
    public CityWeatherDetailView()
    {
        Padding = new Thickness(16);
        CornerRadius = new CornerRadius(8);
        Background = Color.FromHex("#FB8C00").WithAlpha(0.12f);

        var title = new TextBlock().Bold().FontSize(14);
        title.BindText<CityWeatherDetailViewModel>(vm => $"☀️ {vm.CityName} Live Weather Forecast");

        var temp = new TextBlock().FontSize(12);
        temp.BindText<CityWeatherDetailViewModel>(vm => $"Temperature: {vm.Temperature}");

        var cond = new TextBlock().FontSize(12);
        cond.BindText<CityWeatherDetailViewModel>(vm => $"Condition: {vm.Condition}");

        var humidity = new TextBlock().FontSize(12);
        humidity.BindText<CityWeatherDetailViewModel>(vm => $"Humidity: {vm.Humidity}");

        Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 }
            .Children(title, temp, cond, humidity);
    }
}

public partial class GalleryViewModel : ObservableObject
{
    [ObservableProperty]
    private int _clickCount = 0;

    [ObservableProperty]
    private float _sliderValue = 45f;

    [ObservableProperty]
    private bool _notificationsEnabled = true;

    [ObservableProperty]
    private string _currentThemeMode = "Switch to Dark Mode";

    [ObservableProperty]
    private string _userInputText = "Hello Atelier!";

    [ObservableProperty]
    private string? _selectedItem = "Paris";

    public ObservableCollection<string> Cities { get; } =
    [
        "Tokyo",
        "Paris",
        "New York",
        "London",
        "Sydney",
        "San Francisco",
        "Berlin",
        "Barcelona",
        "Moscow",
        "Dubai",
    ];

    [RelayCommand]
    [property: Keybinding("Increment", "Global", "F11")]
    private void Increment() => ClickCount++;

    [RelayCommand]
    private void ToggleTheme()
    {
        if (ThemeManager.Current.IsDark)
        {
            ThemeManager.Current = MaterialTheme.CreateLight();
            CurrentThemeMode = "Switch to Dark Mode";
        }
        else
        {
            ThemeManager.Current = MaterialTheme.CreateDark();
            CurrentThemeMode = "Switch to Light Mode";
        }
    }

    [ObservableProperty]
    private object? _selectedDetail = new CityStatsDetailViewModel("Paris", "France", "2.16 Million", "Eiffel Tower");

    [RelayCommand]
    private void ShowCityStats()
    {
        string city = SelectedItem ?? "Paris";
        SelectedDetail = new CityStatsDetailViewModel(city, "Capital Region", "2.16M residents", "Historical Landmark Core");
    }

    [RelayCommand]
    private void ShowCityWeather()
    {
        string city = SelectedItem ?? "Paris";
        SelectedDetail = new CityWeatherDetailViewModel(city, "23°C", "Sunny with light clouds", "58%");
    }

    [ObservableProperty]
    private string _dialogStatus = "No dialog action performed yet.";

    [ObservableProperty]
    private string _treeSelectionStatus = "Selected node: None (click or use arrow keys to navigate)";

    [ObservableProperty]
    private float _windowScale = 1.0f;

    [ObservableProperty]
    private float _cardRotation = 0f;

    [ObservableProperty]
    private float _cardScale = 1.0f;

    [ObservableProperty]
    private string _transformStatus = "No interaction in transformed box yet.";

    [ObservableProperty]
    private int _transformClickCount = 0;
}

public class ProjectNode
{
    public string Name { get; set; }
    public MaterialIconKind IconKind { get; set; }
    public Color? IconColor { get; set; }
    public string? Tag { get; set; }
    public ObservableCollection<ProjectNode> Children { get; set; } = [];

    public ProjectNode(string name, MaterialIconKind iconKind, Color? iconColor = null, string? tag = null, params ProjectNode[] children)
    {
        Name = name;
        IconKind = iconKind;
        IconColor = iconColor;
        Tag = tag;
        foreach (var c in children) Children.Add(c);
    }
}

internal static class Program
{
    private static void Main()
    {
        Generated.GeneratedKeybindings.RegisterKeybindings();

        Console.WriteLine("Launching Atelier UI Framework Gallery...");

        // Register ViewModels with ViewLocator
        ViewLocator.Current
            .Register<CityStatsDetailViewModel, CityStatsDetailView>()
            .Register<CityWeatherDetailViewModel, CityWeatherDetailView>()
            .Register<CheckboxesViewModel>(vm => new CheckboxesView(vm));

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

    public static UIElement BuildUI(GalleryViewModel vm)
    {
        // 1. Custom TitleBar with embedded search box and theme toggle
        var searchBox = new TextBox
        {
            Placeholder = "Search controls, themes, settings...",
            Width = 320,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14, 6),
            VerticalAlignment = VerticalAlignment.Center
        };

        var themeIcon = new Icon
        {
            Kind = ThemeManager.Current.IsDark ? MaterialIconKind.LightMode : MaterialIconKind.DarkMode,
            Size = 18,
            VerticalAlignment = VerticalAlignment.Center
        };
        var themeButtonText = new TextBlock
        {
            FontSize = 12f,
            VerticalAlignment = VerticalAlignment.Center
        }.BindText(vm, x => x.CurrentThemeMode);

        var themeButton = new Button
        {
            Variant = ButtonVariant.Tonal,
            Height = 32,
            Padding = new Thickness(14, 4),
            CornerRadius = new CornerRadius(16),
            VerticalAlignment = VerticalAlignment.Center,
            Command = vm.ToggleThemeCommand,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            }.Children(themeIcon, themeButtonText)
        };
        themeButton.Click += (s, e) =>
        {
            themeIcon.Kind = ThemeManager.Current.IsDark ? MaterialIconKind.LightMode : MaterialIconKind.DarkMode;
        };

        var titleBarCenter = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titleBarCenter.Add(searchBox);
        titleBarCenter.Add(themeButton);

        var titleBar = new TitleBar
        {
            Title = "Atelier Studio",
            Icon = new Image("Assets/Icons/Atelier.png") { Width = 22, Height = 22, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center },
            Content = titleBarCenter
        }.Row(0);

        // Content Area (Sidebar + Main Content)
        var contentGrid = new Grid()
            .Columns(GridLength.Pixels(340), GridLength.Star);

        // Left Sidebar: Interactive Controls Showcase
        var sidebar = new Border
        {
            Padding = new Thickness(20),
            Margin = new Thickness(8)
        }.Column(0);

        var sidebarStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 16 };

        // Card 1: Buttons
        var buttonCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12)
        };
        var buttonStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(
                new TextBlock("Material 3 Buttons (With Ink Ripples)").Bold().FontSize(14),

                new Button("Filled Button (Increment Count)")
                    .Variant(ButtonVariant.Filled)
                    .Command(vm.IncrementCommand),

                new Button("Elevated Button")
                    .Variant(ButtonVariant.Elevated)
                    .Command(vm.IncrementCommand),

                new Button("Tonal Button")
                    .Variant(ButtonVariant.Tonal),

                new Button("Outlined Button")
                    .Variant(ButtonVariant.Outlined),

                new Button("Text Button")
                    .Variant(ButtonVariant.Text),

                new TextBlock()
                    .FontSize(13)
                    .Bold()
                    .BindText(vm, x => $"Button Click Count: {x.ClickCount}")
            );
        buttonCard.Child = buttonStack;

        // Card 2: Selections & Checkboxes
        var selectCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12)
        };
        var selectStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(
                new TextBlock("Selection Controls").Bold().FontSize(14),
                new CheckBox("Enable High FPS Mode") { IsChecked = true },
                new CheckBox("Enable Hardware Acceleration") { IsChecked = true },
                new CheckBox("Notifications Enabled") { IsChecked = false },
                new RadioButton { Content = new TextBlock("60 Hz VSync Mode"), GroupName = "fps", IsChecked = false },
                new RadioButton { Content = new TextBlock("120 Hz High Smoothness"), GroupName = "fps", IsChecked = true }
            );
        selectCard.Child = selectStack;

        sidebarStack.Children(buttonCard, selectCard);
        sidebar.Child = sidebarStack;

        // Main Content Area
        var mainContent = new Border
        {
            Padding = new Thickness(20),
            Margin = new Thickness(8)
        }.Column(1);

        var mainStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 20 };

        // Card 3: Inputs & Sliders
        var inputsCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12)
        };
        var slider = new Slider { Minimum = 0, Maximum = 200, Value = 45 };
        var progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 45 };
        var indProgressBar = new ProgressBar { IsIndeterminate = true };

        slider.ValueChanged += (s, val) =>
        {
            progressBar.Value = val;
            vm.SliderValue = val;
        };

        var opacitySlider = new Slider
        {
            Minimum = 20,
            Maximum = 100,
            Value = 100,
            ShowValueIndicator = true,
            ValueFormat = "Opacity: {0:0}%"
        };
        opacitySlider.ValueChanged += (s, val) =>
        {
            if (SilkWindow.Current != null)
            {
                SilkWindow.Current.WindowOpacity = val / 100f;
            }
        };

        var textBox = new TextBox(vm.UserInputText)
        {
            Placeholder = "Type something here..."
        }.Bind(TextBox.TextProperty, vm, x => x.UserInputText, (m, v) => m.UserInputText = v);

        var inputsStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }
            .Children(
                new TextBlock("Text Input & Real-time Editing").Bold().FontSize(14),
                textBox,
                new TextBox().Bind(TextBox.TextProperty, vm, x => x.UserInputText, (m,v) => m.UserInputText = v),
                new TextBlock("Slider & Determinate Progress").Bold().FontSize(14),
                slider,
                progressBar,
                new TextBlock("Window Transparency / Opacity").Bold().FontSize(14),
                opacitySlider,
                new TextBlock("Indeterminate Smooth Loading").Bold().FontSize(14),
                indProgressBar
            );
        inputsCard.Child = inputsStack;

        // Card 9: Material Symbols & Variable Font Morphing Showcase
        var iconsCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12)
        };

        var heroIcon = new Icon
        {
            Kind = MaterialIconKind.Favorite,
            Size = 72,
            Fill = 0f,
            Weight = 400f,
            Grade = 0f,
            OpticalSize = 48f,
            Foreground = Color.FromRgb(225, 29, 72),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var heroContainer = new Border
        {
            Width = 140,
            Height = 140,
            CornerRadius = new CornerRadius(16),
            Background = Color.FromRgb(128, 128, 128).WithAlpha(0.10f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = heroIcon
        };

        var fillLabel = new TextBlock("FILL: 0.00 (Outlined)").FontSize(11).Bold();
        var fillSlider = new Slider { Minimum = 0, Maximum = 100, Value = 0, Width = 170 };
        fillSlider.ValueChanged += (s, v) =>
        {
            float val = v / 100f;
            heroIcon.Fill = val;
            fillLabel.Text = $"FILL: {val:F2} ({(val >= 0.5f ? "Filled" : "Outlined")})";
        };

        var weightLabel = new TextBlock("wght: 400 (Regular)").FontSize(11).Bold();
        var weightSlider = new Slider { Minimum = 100, Maximum = 700, Value = 400, Width = 170 };
        weightSlider.ValueChanged += (s, v) =>
        {
            heroIcon.Weight = v;
            string name = v switch
            {
                < 200 => "Thin",
                < 300 => "ExtraLight",
                < 400 => "Light",
                < 500 => "Regular",
                < 600 => "Medium",
                _ => "Bold"
            };
            weightLabel.Text = $"wght: {v:F0} ({name})";
        };

        var gradeLabel = new TextBlock("GRAD: 0").FontSize(11).Bold();
        var gradeSlider = new Slider { Minimum = -25, Maximum = 200, Value = 0, Width = 170 };
        gradeSlider.ValueChanged += (s, v) =>
        {
            heroIcon.Grade = v;
            gradeLabel.Text = $"GRAD: {v:F0}";
        };

        var opszLabel = new TextBlock("opsz: 48").FontSize(11).Bold();
        var opszSlider = new Slider { Minimum = 20, Maximum = 48, Value = 48, Width = 170 };
        opszSlider.ValueChanged += (s, v) =>
        {
            heroIcon.OpticalSize = v;
            opszLabel.Text = $"opsz: {v:F0}";
        };

        var sizeLabel = new TextBlock("Size: 72dp").FontSize(11).Bold();
        var sizeSlider = new Slider { Minimum = 24, Maximum = 96, Value = 72, Width = 170 };
        sizeSlider.ValueChanged += (s, v) =>
        {
            heroIcon.Size = v;
            sizeLabel.Text = $"Size: {v:F0}dp";
        };

        var sliderControls = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 }
            .Children(
                fillLabel, fillSlider,
                weightLabel, weightSlider,
                gradeLabel, gradeSlider,
                opszLabel, opszSlider,
                sizeLabel, sizeSlider
            );

        // Quick icon switcher buttons
        var iconButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(
                new Button { Content = MaterialIconKind.Favorite.ToIcon(20, foreground: Color.FromRgb(225, 29, 72)), Height = 34, Width = 42 }
                    .OnClick(() => { heroIcon.Kind = MaterialIconKind.Favorite; heroIcon.Foreground = Color.FromRgb(225, 29, 72); }),
                new Button { Content = MaterialIconKind.Star.ToIcon(20, foreground: Color.FromRgb(234, 179, 8)), Height = 34, Width = 42 }
                    .OnClick(() => { heroIcon.Kind = MaterialIconKind.Star; heroIcon.Foreground = Color.FromRgb(234, 179, 8); }),
                new Button { Content = MaterialIconKind.Settings.ToIcon(20, foreground: Color.FromRgb(59, 130, 246)), Height = 34, Width = 42 }
                    .OnClick(() => { heroIcon.Kind = MaterialIconKind.Settings; heroIcon.Foreground = Color.FromRgb(59, 130, 246); }),
                new Button { Content = MaterialIconKind.Palette.ToIcon(20, foreground: Color.FromRgb(168, 85, 247)), Height = 34, Width = 42 }
                    .OnClick(() => { heroIcon.Kind = MaterialIconKind.Palette; heroIcon.Foreground = Color.FromRgb(168, 85, 247); }),
                new Button { Content = MaterialIconKind.RocketLaunch.ToIcon(20, foreground: Color.FromRgb(249, 115, 22)), Height = 34, Width = 42 }
                    .OnClick(() => { heroIcon.Kind = MaterialIconKind.RocketLaunch; heroIcon.Foreground = Color.FromRgb(249, 115, 22); }),
                new Button { Content = MaterialIconKind.Bolt.ToIcon(20, foreground: Color.FromRgb(16, 185, 129)), Height = 34, Width = 42 }
                    .OnClick(() => { heroIcon.Kind = MaterialIconKind.Bolt; heroIcon.Foreground = Color.FromRgb(16, 185, 129); })
            );

        var playgroundPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 }
            .Children(
                new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }
                    .Children(heroContainer, iconButtons),
                sliderControls
            );

        static Border CreateTile(MaterialIconKind kind, string name, bool isFilled = false, Color? color = null)
        {
            var ic = new Icon(kind, 28, isFilled, color) { HorizontalAlignment = HorizontalAlignment.Center };
            var lb = new TextBlock(name) { FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
            var sp = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center }
                .Children(ic, lb);
            return new Border
            {
                Padding = new Thickness(8, 6),
                CornerRadius = new CornerRadius(8),
                Background = Color.FromRgb(128, 128, 128).WithAlpha(0.08f),
                Width = 72,
                Child = sp
            };
        }

        var row1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(
                CreateTile(MaterialIconKind.Home, "Home"),
                CreateTile(MaterialIconKind.Search, "Search"),
                CreateTile(MaterialIconKind.Settings, "Settings"),
                CreateTile(MaterialIconKind.Favorite, "Heart", color: Color.FromRgb(225, 29, 72)),
                CreateTile(MaterialIconKind.Star, "Star", color: Color.FromRgb(234, 179, 8)),
                CreateTile(MaterialIconKind.Share, "Share"),
                CreateTile(MaterialIconKind.Notifications, "Alerts")
            );

        var row2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(
                CreateTile(MaterialIconKind.CheckCircle, "Check", color: Color.FromRgb(16, 185, 129)),
                CreateTile(MaterialIconKind.Cancel, "Cancel", color: Color.FromRgb(239, 68, 68)),
                CreateTile(MaterialIconKind.AddCircle, "Add"),
                CreateTile(MaterialIconKind.PlayCircle, "Play", color: Color.FromRgb(59, 130, 246)),
                CreateTile(MaterialIconKind.Download, "Download"),
                CreateTile(MaterialIconKind.Cloud, "Cloud"),
                CreateTile(MaterialIconKind.Palette, "Theme", color: Color.FromRgb(168, 85, 247))
            );

        var row3 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(
                CreateTile(MaterialIconKind.Favorite, "Filled", isFilled: true, color: Color.FromRgb(225, 29, 72)),
                CreateTile(MaterialIconKind.Star, "Filled", isFilled: true, color: Color.FromRgb(234, 179, 8)),
                CreateTile(MaterialIconKind.DarkMode, "Dark", color: Color.FromRgb(139, 92, 246)),
                CreateTile(MaterialIconKind.LightMode, "Light", color: Color.FromRgb(245, 158, 11)),
                CreateTile(MaterialIconKind.Bolt, "Energy", color: Color.FromRgb(234, 179, 8)),
                CreateTile(MaterialIconKind.RocketLaunch, "Launch", color: Color.FromRgb(249, 115, 22)),
                CreateTile(MaterialIconKind.Verified, "Verified", color: Color.FromRgb(59, 130, 246))
            );

        var catalogStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 }
            .Children(row1, row2, row3);

        // Custom vector icons demo
        using var shieldBuilder = new SKPathBuilder();
        shieldBuilder.MoveTo(12, 2);
        shieldBuilder.LineTo(22, 6);
        shieldBuilder.LineTo(22, 12);
        shieldBuilder.CubicTo(22, 18, 17, 22, 12, 24);
        shieldBuilder.CubicTo(7, 22, 2, 18, 2, 12);
        shieldBuilder.LineTo(2, 6);
        shieldBuilder.Close();
        var customShieldPath = shieldBuilder.Detach();

        string svgHeart = "M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z";
        string svgSparkle = "M12 2L9.19 8.63L2 12l7.19 3.37L12 22l2.81-6.63L22 12l-7.19-3.37L12 2z";
        string svgBookmark = "M17 3H7c-1.1 0-1.99.9-1.99 2L5 21l7-3 7 3V5c0-1.1-.9-2-2-2z";

        static Border CreateCustomTile(Icon icon, string name)
        {
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            var lb = new TextBlock(name) { FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
            var sp = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center }
                .Children(icon, lb);
            return new Border
            {
                Padding = new Thickness(8, 6),
                CornerRadius = new CornerRadius(8),
                Background = Color.FromRgb(128, 128, 128).WithAlpha(0.08f),
                Width = 84,
                Child = sp
            };
        }

        var customRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(
                CreateCustomTile(new Icon(customShieldPath, 28, Color.FromRgb(59, 130, 246)), "SKPath Fill"),
                CreateCustomTile(new Icon(customShieldPath, 28, Color.FromRgb(59, 130, 246)) { StrokeWidth = 1.8f }, "SKPath Stroke"),
                CreateCustomTile(new Icon(svgHeart, 28, Color.FromRgb(225, 29, 72)), "SVG Path Fill"),
                CreateCustomTile(new Icon(svgSparkle, 28, Color.FromRgb(234, 179, 8)), "SVG Sparkle"),
                CreateCustomTile(new Icon(svgBookmark, 28, Color.FromRgb(16, 185, 129)) { StrokeWidth = 1.8f }, "SVG Stroke"),
                CreateCustomTile(svgSparkle.ToIcon(28, foreground: Color.FromRgb(168, 85, 247)), "Fluent Ext")
            );

        var iconsStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 }
            .Children(
                new TextBlock("Material Symbols & Variable Font Morphing").Bold().FontSize(14),
                new TextBlock("Real-time SkiaSharp variable font morphing across all 4 axes (FILL, wght, GRAD, opsz) with 2,100+ icons:").FontSize(12),
                playgroundPanel,
                new TextBlock("Icon Catalog Samples (Outlined & Filled):").Bold().FontSize(13),
                catalogStack,
                new TextBlock("Custom Vector Icons (SKPath & SVG Path Data):").Bold().FontSize(13),
                new TextBlock("Render custom geometries via SKPath or SVG path strings, with support for uniform scaling, centering, fill, and stroke outline:").FontSize(12),
                customRow
            );
        iconsCard.Child = iconsStack;

        // Card 4: ItemsControl & ListBox with DataBinding
        var listCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12)
        };

        var listBox = new ListBox
        {
            ItemsSource = vm.Cities,
            SelectedItem = vm.SelectedItem
        };
        listBox.MaxHeight = 200;

        var selectedCityText = new TextBlock($"Selected City: {vm.SelectedItem}").Bold();
        listBox.SelectionChanged += (s, item) =>
        {
            selectedCityText.Text = $"Selected City: {item ?? "None"}";
        };

        var listStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 20 }
            .Children(
                new TextBlock("ListBox & ObservableCollection Binding").Bold().FontSize(14),
                selectedCityText,
                listBox
            );
        listCard.Child = listStack;

        // Card 5: CSS-like Styling System (Headings & Custom Styles)
        var stylesCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12)
        };

        // Scoped Styles for Headings
        stylesCard.Styles(
            new Style("Heading1", typeof(TextBlock))
                .Set(TextBlock.FontSizeProperty, 26f)
                .Set(TextBlock.BoldProperty, true),

            new Style("Heading2", typeof(TextBlock))
                .Set(TextBlock.FontSizeProperty, 20f)
                .Set(TextBlock.BoldProperty, true),

            new Style("Heading3", typeof(TextBlock))
                .Set(TextBlock.FontSizeProperty, 16f)
                .Set(TextBlock.BoldProperty, true),

            new Style("Subtext", typeof(TextBlock))
                .Set(TextBlock.FontSizeProperty, 13f)
                .Set(TextBlock.ForegroundProperty, Color.FromRgb(128, 128, 128))
        );

        var stylesStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 }
            .Children(
                new TextBlock("CSS-like Styling System (Scoped Styles & StyleKeys)").Bold().FontSize(14),
                new TextBlock("Heading 1 Style (26px, Bold)").StyleKey("Heading1"),
                new TextBlock("Heading 2 Style (20px, Bold)").StyleKey("Heading2"),
                new TextBlock("Heading 3 Style (16px, Bold)").StyleKey("Heading3"),
                new TextBlock("Subtext Style (13px, Muted Gray)").StyleKey("Subtext")
            );
        stylesCard.Child = stylesStack;

        // Card 6: Popups & ComboBox with ItemTemplate & Smart Placement
        var comboCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12)
        };

        var standardCombo = new ComboBox
        {
            ItemsSource = vm.Cities,
            Placeholder = "Choose a destination city...",
            Width = 280
        }.BindSelectedItem(vm, x => x.SelectedItem, (m, v) => m.SelectedItem = (string?)v);

        var templateCombo = new ComboBox
        {
            ItemsSource = vm.Cities,
            Placeholder = "Choose with custom template...",
            Width = 280,
            ItemTemplate = item =>
            {
                var dot = new Border
                {
                    Width = 8,
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    Background = Color.FromHex("#2196F3"),
                    Margin = new Thickness(0, 4, 8, 0)
                };
                var text = new TextBlock(item?.ToString() ?? string.Empty).Bold();
                return new StackPanel { Orientation = Orientation.Horizontal }
                    .Children(dot, text);
            }
        }.BindSelectedItem(vm, x => x.SelectedItem, (m, v) => m.SelectedItem = (string?)v);

        var comboStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }
            .Children(
                new TextBlock("In-Window Popups & ComboBox with Smart Placement").Bold().FontSize(14),
                new TextBlock("Standard Dropdown (Two-Way Bound to ListBox selection):").FontSize(12),
                standardCombo,
                new TextBlock("Custom ItemTemplate Dropdown (Styled with Badges/Indicators):").FontSize(12),
                templateCombo
            );
        comboCard.Child = comboStack;

        // Card 7: Master-Detail with ContentControl & ViewLocator
        var masterDetailCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12)
        };

        var statsBtn = new Button("View City Stats")
            .Command(vm.ShowCityStatsCommand);

        var weatherBtn = new Button("View Weather Forecast")
            .Command(vm.ShowCityWeatherCommand);

        var buttonBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 }
            .Children(statsBtn, weatherBtn);

        var detailContent = new ContentControl()
            .BindContent(vm, x => x.SelectedDetail);

        var masterDetailStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }
            .Children(
                new TextBlock("Master-Detail ViewModel Resolution via ViewLocator & ContentControl").Bold().FontSize(14),
                new TextBlock("The detail ViewModel is bound to ContentControl.Content. When switched, ViewLocator resolves and mounts the matching view:").FontSize(12),
                buttonBar,
                detailContent
            );
        masterDetailCard.Child = masterDetailStack;

        // Card 8: Modal Dialogs & DialogHost
        var dialogsCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12)
        };

        var statusText = new TextBlock().FontSize(12);
        statusText.BindText<GalleryViewModel>(v => $"Current Status: {v.DialogStatus}");

        var alertBtn = new Button("Show Alert Dialog")
        {
            Variant = ButtonVariant.Filled
        };
        alertBtn.Click += async (s, e) =>
        {
            var dlg = new Dialog("Important Notice", "This is an alert dialog displayed via DialogHost. The background content is darkened and blocked from input.", DialogButtons.Ok);
            var res = await dlg.ShowAsync(alertBtn);
            vm.DialogStatus = $"Alert closed with result: {res.Result} (Button: '{res.ButtonText}')";
        };

        var confirmBtn = new Button("Show Confirm Dialog")
        {
            Variant = ButtonVariant.Tonal
        };
        confirmBtn.Click += async (s, e) =>
        {
            var dlg = new Dialog("Confirm Changes", "Would you like to save all pending modifications before proceeding?", DialogButtons.YesNoCancel);
            var res = await dlg.ShowAsync(confirmBtn);
            vm.DialogStatus = $"Confirm dialog returned: {res.Result} (Button: '{res.ButtonText}')";
        };

        var customBtn = new Button("Show Custom Form Dialog")
        {
            Variant = ButtonVariant.Outlined
        };
        customBtn.Click += async (s, e) =>
        {
            var inputField = new TextBox { Placeholder = "Enter custom destination note...", Width = 280 };
            var customForm = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 }
                .Children(
                    new TextBlock("Enter custom note or label below:").FontSize(12),
                    inputField
                );

            var dlg = new Dialog
            {
                Title = "Custom Form Input",
                Content = customForm,
                ButtonsPreset = DialogButtons.OkCancel
            };

            var res = await dlg.ShowAsync(customBtn);
            vm.DialogStatus = res.Result == DialogResult.Ok
                ? $"Saved custom note: '{inputField.Text}'"
                : $"Custom dialog dismissed: {res.Result}";
        };

        var dialogButtonsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 }
            .Children(alertBtn, confirmBtn, customBtn);

        // In-Card Scoped DialogHost demonstration
        var scopedBox = new Border
        {
            Background = Color.FromHex("#7B1FA2").WithAlpha(0.08f),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14)
        };

        var scopedInnerBtn = new Button("Show Scoped Dialog (In Card Only)")
        {
            Variant = ButtonVariant.Filled
        };

        var scopedHost = new DialogHost
        {
            Identifier = "CardScopedHost",
            CloseOnClickAway = true,
            Content = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 }
                .Children(
                    new TextBlock("Scoped DialogHost Container (In-Card)").Bold().FontSize(12),
                    new TextBlock("Dialogs triggered here only darken and lock this card, leaving the rest of the window interactive:").FontSize(11),
                    scopedInnerBtn
                )
        };
        scopedBox.Child = scopedHost;

        scopedInnerBtn.Click += async (s, e) =>
        {
            var dlg = new Dialog("Scoped Card Dialog", "Notice that only this card is darkened and blocked! The DialogHost automatically expanded to fit this dialog without clipping.", DialogButtons.Ok);
            var res = await dlg.ShowAsync(scopedInnerBtn);
            vm.DialogStatus = $"Scoped dialog finished: {res.Result}";
        };

        var dialogsStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }
            .Children(
                new TextBlock("In-Tree DialogHost & Material 3 Dialogs").Bold().FontSize(14),
                new TextBlock("DialogHost darkens the default content and blocks input. Dialog.ShowAsync walks up the visual tree to find the nearest host:").FontSize(12),
                dialogButtonsRow,
                scopedBox,
                statusText
            );
        dialogsCard.Child = dialogsStack;

        // Card 8: Hierarchical TreeView with Material Symbols Expanders
        var treeCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12)
        };

        var srcNode = new ProjectNode("src", MaterialIconKind.Folder, Color.FromHex("#FFA000"), "3 projects",
            new ProjectNode("Atelier.Core", MaterialIconKind.Folder, Color.FromHex("#1E88E5"), null,
                new ProjectNode("Animation", MaterialIconKind.Folder, Color.FromHex("#1E88E5"), null,
                    new ProjectNode("AnimationClock.cs", MaterialIconKind.Code, Color.FromHex("#00ACC1")),
                    new ProjectNode("FloatAnimation.cs", MaterialIconKind.Code, Color.FromHex("#00ACC1"))
                ),
                new ProjectNode("Primitives", MaterialIconKind.Folder, Color.FromHex("#1E88E5"), null,
                    new ProjectNode("Color.cs", MaterialIconKind.Code, Color.FromHex("#00ACC1")),
                    new ProjectNode("Rect.cs", MaterialIconKind.Code, Color.FromHex("#00ACC1"))
                )
            ),
            new ProjectNode("Atelier.Controls", MaterialIconKind.Folder, Color.FromHex("#1E88E5"), null,
                new ProjectNode("TreeView.cs", MaterialIconKind.Code, Color.FromHex("#00E676"), "NEW"),
                new ProjectNode("TreeViewItem.cs", MaterialIconKind.Code, Color.FromHex("#00E676"), "NEW"),
                new ProjectNode("ScrollViewer.cs", MaterialIconKind.Code, Color.FromHex("#00ACC1")),
                new ProjectNode("Icon.cs", MaterialIconKind.Code, Color.FromHex("#00ACC1"))
            ),
            new ProjectNode("Atelier.Theming.Material", MaterialIconKind.Folder, Color.FromHex("#1E88E5"), null,
                new ProjectNode("MaterialTheme.cs", MaterialIconKind.Code, Color.FromHex("#00ACC1")),
                new ProjectNode("MaterialRenderers.cs", MaterialIconKind.Code, Color.FromHex("#00ACC1"))
            )
        );

        var rootProject = new ProjectNode("Atelier.Solution", MaterialIconKind.FolderSpecial, Color.FromHex("#3949AB"), "Solution",
            srcNode,
            new ProjectNode("samples", MaterialIconKind.Folder, Color.FromHex("#FFA000"), "1 app",
                new ProjectNode("Atelier.Gallery", MaterialIconKind.Folder, Color.FromHex("#1E88E5"), null,
                    new ProjectNode("Program.cs", MaterialIconKind.Code, Color.FromHex("#00ACC1"))
                )
            ),
            new ProjectNode("Assets", MaterialIconKind.Folder, Color.FromHex("#FFA000"), null,
                new ProjectNode("Fonts", MaterialIconKind.Folder, Color.FromHex("#1E88E5"), null,
                    new ProjectNode("MaterialSymbolsRounded.ttf", MaterialIconKind.TextSnippet, Color.FromHex("#E91E63"))
                )
            ),
            new ProjectNode("README.md", MaterialIconKind.Description, Color.FromHex("#FB8C00")),
            new ProjectNode("Directory.Build.props", MaterialIconKind.Settings, Color.FromHex("#8E24AA"))
        );

        var treeView = new TreeView
        {
            Height = 280,
            ItemsSource = new[] { rootProject },
            ChildrenSelector = item => ((ProjectNode)item).Children,
            IndentSize = 18f
        };

        treeView.ItemTemplate = item =>
        {
            var node = (ProjectNode)item;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6f, VerticalAlignment = VerticalAlignment.Center };
            row.Add(new Icon(node.IconKind, 18f, foreground: node.IconColor ?? Color.FromHex("#78909C")) { VerticalAlignment = VerticalAlignment.Center });
            row.Add(new TextBlock(node.Name) { VerticalAlignment = VerticalAlignment.Center });
            if (!string.IsNullOrEmpty(node.Tag))
            {
                var badge = new Border
                {
                    Background = node.Tag == "NEW" ? Color.FromHex("#00E676").WithAlpha(0.2f) : Color.FromHex("#9E9E9E").WithAlpha(0.15f),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(4, 1),
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock(node.Tag)
                    {
                        FontSize = 10f,
                        Bold = true,
                        Foreground = node.Tag == "NEW" ? Color.FromHex("#00C853") : Color.FromHex("#757575"),
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                row.Add(badge);
            }
            return row;
        };

        treeView.SelectionChanged += (s, sel) =>
        {
            if (sel is ProjectNode pn)
            {
                vm.TreeSelectionStatus = $"Selected: '{pn.Name}' ({(pn.Children.Count > 0 ? $"{pn.Children.Count} items" : "File")})";
            }
        };

        var expandAllBtn = new Button("Expand All") { Variant = ButtonVariant.Outlined };
        expandAllBtn.Click += (s, e) => treeView.ExpandAll();

        var collapseAllBtn = new Button("Collapse All") { Variant = ButtonVariant.Outlined };
        collapseAllBtn.Click += (s, e) => treeView.CollapseAll();

        int extraCounter = 1;
        var addNodeBtn = new Button("Add Dynamic File") { Variant = ButtonVariant.Tonal };
        addNodeBtn.Click += (s, e) =>
        {
            srcNode.Children.Add(new ProjectNode($"Generated_{extraCounter++}.cs", MaterialIconKind.Code, Color.FromHex("#FF7043"), "DYNAMIC"));
        };

        var treeButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(expandAllBtn, collapseAllBtn, addNodeBtn);

        var treeStatusText = new TextBlock()
            .FontSize(12)
            .Foreground(Color.FromHex("#757575"))
            .BindText(vm, x => x.TreeSelectionStatus);

        var treeContainer = new Border
        {
            Background = Color.FromHex("#000000").WithAlpha(0.03f),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.08f),
            Padding = new Thickness(4),
            Child = treeView
        };

        var treeStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(
                new TextBlock("Hierarchical TreeView with Material Symbols Chevrons").Bold().FontSize(14),
                new TextBlock("Supports arbitrary nested data structures via ChildrenSelector, custom ItemTemplate, arrow-key navigation, and live collection observation:").FontSize(12),
                treeButtons,
                treeContainer,
                treeStatusText
            );
        treeCard.Child = treeStack;

        // 9. Transformation Matrix & Accessibility Zoom Card
        var transformCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12)
        };

        // Window Zoom controls
        var zoom100Btn = new Button("100% (Default)") { Variant = ButtonVariant.Outlined };
        zoom100Btn.Click += (s, e) => vm.WindowScale = 1.0f;

        var zoom125Btn = new Button("125% (Accessibility)") { Variant = ButtonVariant.Outlined };
        zoom125Btn.Click += (s, e) => vm.WindowScale = 1.25f;

        var zoom150Btn = new Button("150% (Large)") { Variant = ButtonVariant.Outlined };
        zoom150Btn.Click += (s, e) => vm.WindowScale = 1.5f;

        var zoomButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(zoom100Btn, zoom125Btn, zoom150Btn);

        // Interactive transformed target container
        var previewBox = new Border
        {
            Width = 320,
            Height = 170,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Color.FromHex("#1E88E5").WithAlpha(0.12f),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(2),
            BorderBrush = Color.FromHex("#1E88E5"),
            Padding = new Thickness(14)
        }.TransformOrigin(0.5f, 0.5f);

        void UpdatePreviewTransform()
        {
            float rad = vm.CardRotation * (MathF.PI / 180f);
            previewBox.Transform = Matrix3x2.CreateScale(vm.CardScale) * Matrix3x2.CreateRotation(rad);
        }

        var rotLabel = new TextBlock("0°") { VerticalAlignment = VerticalAlignment.Center, Width = 36 }.FontSize(12);
        var rotSlider = new Slider { Minimum = 0, Maximum = 360, Value = 0, Width = 140, VerticalAlignment = VerticalAlignment.Center };
        rotSlider.ValueChanged += (s, v) =>
        {
            vm.CardRotation = v;
            rotLabel.Text = $"{v:F0}°";
            UpdatePreviewTransform();
        };

        void SetRotation(float deg)
        {
            vm.CardRotation = deg;
            rotSlider.Value = deg;
            rotLabel.Text = $"{deg:F0}°";
            UpdatePreviewTransform();
        }

        var rot0Btn = new Button("0°") { Variant = ButtonVariant.Tonal };
        rot0Btn.Click += (s, e) => SetRotation(0f);

        var rot15Btn = new Button("15°") { Variant = ButtonVariant.Tonal };
        rot15Btn.Click += (s, e) => SetRotation(15f);

        var rot45Btn = new Button("45°") { Variant = ButtonVariant.Tonal };
        rot45Btn.Click += (s, e) => SetRotation(45f);

        var rot90Btn = new Button("90°") { Variant = ButtonVariant.Tonal };
        rot90Btn.Click += (s, e) => SetRotation(90f);

        var scale08Btn = new Button("0.8x") { Variant = ButtonVariant.Tonal };
        scale08Btn.Click += (s, e) => { vm.CardScale = 0.8f; UpdatePreviewTransform(); };

        var scale10Btn = new Button("1.0x") { Variant = ButtonVariant.Tonal };
        scale10Btn.Click += (s, e) => { vm.CardScale = 1.0f; UpdatePreviewTransform(); };

        var scale12Btn = new Button("1.25x") { Variant = ButtonVariant.Tonal };
        scale12Btn.Click += (s, e) => { vm.CardScale = 1.25f; UpdatePreviewTransform(); };

        var presetRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(
                new TextBlock("Rotate:") { VerticalAlignment = VerticalAlignment.Center }.FontSize(12),
                rotSlider, rotLabel,
                rot0Btn, rot15Btn, rot45Btn, rot90Btn,
                new TextBlock("Scale:") { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) }.FontSize(12),
                scale08Btn, scale10Btn, scale12Btn
            );

        // Content inside the transformed preview box
        var previewClickBtn = new Button("Click Me While Rotated!") { Variant = ButtonVariant.Filled };
        previewClickBtn.Click += (s, e) =>
        {
            vm.TransformClickCount++;
            vm.TransformStatus = $"Button clicked {vm.TransformClickCount} times! (Rot={vm.CardRotation:F0}°, Scale={vm.CardScale:F2}x)";
        };

        var previewSlider = new Slider { Minimum = 0, Maximum = 100, Value = 50, Height = 28 };
        previewSlider.ValueChanged += (s, val) =>
        {
            vm.TransformStatus = $"Slider dragged to {val:F0}% while rotated!";
        };

        var previewStatusText = new TextBlock()
            .FontSize(11)
            .Bold()
            .Foreground(Color.FromHex("#1565C0"))
            .BindText(vm, x => x.TransformStatus);

        var previewStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 }
            .Children(
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(MaterialIconKind.CropRotate, 20, foreground: Color.FromHex("#1E88E5")),
                        new TextBlock("Live Transformed Container").Bold().FontSize(13)
                    ),
                previewClickBtn,
                previewSlider,
                previewStatusText
            );
        previewBox.Child = previewStack;

        var previewWrapper = new Border
        {
            MinHeight = 260,
            Background = Color.FromHex("#000000").WithAlpha(0.02f),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.08f),
            Padding = new Thickness(16),
            Child = previewBox
        };

        var transformStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(
                new TextBlock("VisualNode 2D Transformation Matrix & Window Zoom").Bold().FontSize(14),
                new TextBlock("Rotate and scale any control and its children with real-time transformed hit-testing and pointer routing:").FontSize(12),
                new TextBlock("Accessibility Window Zoom (scales root node):").FontSize(12).Bold(),
                zoomButtons,
                new TextBlock("Control Subtree Transformation (try dragging the slider or clicking the button):").FontSize(12).Bold(),
                presetRow,
                previewWrapper
            );
        transformCard.Child = transformStack;

        // 10. Native AOT PropertyGrid Control
        var inspectorCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12)
        };

        var serverConfig = new ServerConfigModel();
        var displaySettings = new DisplaySettingsModel();

        var propertyGrid = new PropertyGrid()
            .Inspect(serverConfig)
            .WithSortMode(PropertySortMode.Categorized)
            .WithLabelWidth(140f)
            .Height(360f);

        // Live preview elements reflecting model state
        var previewHostText = new TextBlock($"Host: {serverConfig.Host}:{serverConfig.Port}").Bold().FontSize(13);
        var previewSslBadge = new TextBlock(serverConfig.EnableSsl ? "🔒 TLS / SSL Enabled" : "⚠️ Insecure")
        {
            FontSize = 11,
            Foreground = serverConfig.EnableSsl ? Color.FromHex("#388E3C") : Color.FromHex("#D32F2F")
        };
        var previewLogLevelBadge = new TextBlock($"Log Level: {serverConfig.LogLevel}").FontSize(11).Foreground(Color.FromHex("#1E88E5"));
        var previewCpuBadge = new TextBlock($"CPU Alert: {serverConfig.CpuThreshold:F1}%").FontSize(11);
        var previewColorSwatch = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(4),
            Background = serverConfig.StatusColor,
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#40000000"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var previewColorHex = new TextBlock(serverConfig.StatusColor.ToString()) { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        var previewColorRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
            .Children(new TextBlock("Status Tint:") { FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, previewColorSwatch, previewColorHex);

        var lastEditedText = new TextBlock("Edit any property in the grid to see instant live reflection here.")
            .FontSize(11).Foreground(Color.FromHex("#757575"));

        void UpdatePreview()
        {
            if (propertyGrid.SelectedObject is ServerConfigModel sc)
            {
                previewHostText.Text = $"Host: {sc.Host}:{sc.Port}";
                previewSslBadge.Text = sc.EnableSsl ? "🔒 TLS / SSL Enabled" : "⚠️ Insecure";
                previewSslBadge.Foreground = sc.EnableSsl ? Color.FromHex("#388E3C") : Color.FromHex("#D32F2F");
                previewLogLevelBadge.Text = $"Log Level: {sc.LogLevel}";
                previewCpuBadge.Text = $"CPU Alert: {sc.CpuThreshold:F1}%";
                previewColorSwatch.Background = sc.StatusColor;
                previewColorHex.Text = sc.StatusColor.ToString();
            }
            else if (propertyGrid.SelectedObject is DisplaySettingsModel ds)
            {
                previewHostText.Text = $"Title: {ds.WindowTitle}";
                previewSslBadge.Text = ds.DarkMode ? "🌙 Dark Mode" : "☀️ Light Mode";
                previewSslBadge.Foreground = Color.FromHex("#1E88E5");
                previewLogLevelBadge.Text = $"Device: {ds.GpuDevice}";
                previewCpuBadge.Text = $"Scale: {ds.UiScale:F2}x";
                previewColorSwatch.Background = ds.AccentHue;
                previewColorHex.Text = ds.AccentHue.ToString();
            }
        }

        propertyGrid.PropertyValueChanged += (s, e) =>
        {
            UpdatePreview();
            lastEditedText.Text = $"Last edited: '{e.Property.DisplayName}' → {e.NewValue} ({DateTime.Now:HH:mm:ss})";
        };

        var previewPanel = new Border
        {
            Background = Color.FromHex("#1E88E5").WithAlpha(0.06f),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#1E88E5").WithAlpha(0.20f),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 }
                .Children(
                    new TextBlock("Live Target Object Preview").Bold().FontSize(12).Foreground(Color.FromHex("#1E88E5")),
                    previewHostText,
                    previewSslBadge,
                    previewLogLevelBadge,
                    previewCpuBadge,
                    previewColorRow,
                    lastEditedText
                )
        };

        var inspectServerBtn = new Button("Inspect Server Config") { Variant = ButtonVariant.Tonal };
        inspectServerBtn.Click += (s, e) =>
        {
            propertyGrid.SelectedObject = serverConfig;
            UpdatePreview();
        };

        var inspectDisplayBtn = new Button("Inspect Display Settings") { Variant = ButtonVariant.Outlined };
        inspectDisplayBtn.Click += (s, e) =>
        {
            propertyGrid.SelectedObject = displaySettings;
            UpdatePreview();
        };

        var toggleToolbarBtn = new Button("Toggle Toolbar") { Variant = ButtonVariant.Text };
        toggleToolbarBtn.Click += (s, e) =>
        {
            propertyGrid.IsToolbarVisible = !propertyGrid.IsToolbarVisible;
        };

        var toggleElevationBtn = new Button($"Elevation: {propertyGrid.ToolbarElevation:0}dp") { Variant = ButtonVariant.Text };
        toggleElevationBtn.Click += (s, e) =>
        {
            float nextElev = propertyGrid.ToolbarElevation switch
            {
                0f => 2f,
                2f => 4f,
                _ => 0f
            };
            propertyGrid.ToolbarElevation = nextElev;
            toggleElevationBtn.Content = new TextBlock($"Elevation: {nextElev:0}dp");
        };

        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(inspectServerBtn, inspectDisplayBtn, toggleToolbarBtn, toggleElevationBtn);

        var gridAndPreview = new Grid()
            .Columns(GridLength.Stars(1.6f), GridLength.Stars(1.0f));

        propertyGrid.Column(0);
        previewPanel.Column(1).Margin(8, 0, 0, 0);

        gridAndPreview.Children(propertyGrid, previewPanel);

        var inspectorStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }
            .Children(
                new TextBlock("Native AOT PropertyGrid Control").Bold().FontSize(14),
                new TextBlock("Reflection-free property grid with elevated Material Design toolbar, categorized/alphabetical sorting, real-time filtering, built-in editors, and custom editors:").FontSize(12),
                actionsRow,
                gridAndPreview
            );
        inspectorCard.Child = inspectorStack;

        mainStack.Children(inputsCard, iconsCard, listCard, stylesCard, comboCard, masterDetailCard, dialogsCard, treeCard, transformCard, inspectorCard);
        mainContent.Child = mainStack;

        contentGrid.Children(sidebar, mainContent);

        var contentScrollViewer = new ScrollViewer
        {
            Content = contentGrid,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        }.Row(1);

        var windowLayout = new Grid()
            .Rows(GridLength.Pixels(44), GridLength.Star);

        windowLayout.Children(titleBar, contentScrollViewer);

        var rootDialogHost = new DialogHost
        {
            Identifier = "RootHost",
            Content = windowLayout
        };

        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(GalleryViewModel.WindowScale))
            {
                rootDialogHost.Transform = Matrix3x2.CreateScale(vm.WindowScale);
            }
        };
       
        return new KeybindingHandler("Global", rootDialogHost) { DataContext = vm };
    }
}
