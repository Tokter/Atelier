using System;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming;
using Atelier.Gallery.ViewModels;
using SkiaSharp;

namespace Atelier.Gallery.Views;

public class CheckboxesView : Grid
{
    private readonly CheckboxesViewModel _viewModel;
    private readonly ScrollViewer _scrollViewer;

    public CheckboxesView() : this(new CheckboxesViewModel())
    {
    }

    public CheckboxesView(CheckboxesViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        this.Rows(GridLength.Auto, GridLength.Star);
        this.RowSpacing(16);

        // 1. Master Controls & Interactive Toggle Banner (Fixed, Non-Scrolling Header)
        this.Add(CreateMasterBanner().Row(0));

        // 2. Scrollable Showcase Cards Container
        var cardsStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 16
        }.Children(
            CreateCheckboxesCard(),
            CreateRadioButtonsCard(),
            CreateSwitchesCard()
        );

        cardsStack.Margin = new Thickness(0, 0, 10, 20);

        _scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = cardsStack
        }.Row(1);

        this.Add(_scrollViewer);
    }

    public override void OnPointerWheel(PointerWheelEventArgs e)
    {
        base.OnPointerWheel(e);
        if (!e.Handled && _scrollViewer != null)
        {
            _scrollViewer.OnPointerWheel(e);
        }
    }

    private UIElement CreateMasterBanner()
    {
        var card = new Card(CardVariant.Filled)
        {
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(14),
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // Header text
        stack.Add(new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
            .Children(
                new TextBlock("Selection Controls (Material Design 3)").Bold().FontSize(18),
                new TextBlock("Demonstrating Checkboxes, Radio Buttons, and Switches with rich content, two-way data-binding, and disabled states.")
                    .FontSize(12)
                    .Muted()
            )
        );

        // Interactive master toggle row
        var toggleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Switch("Interactive Controls Enabled")
                    .ShowThumbIcon()
                    .BindIsChecked(_viewModel, x => x.InteractiveControlsEnabled, (vm, v) => vm.InteractiveControlsEnabled = v),

                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(MaterialIconKind.CheckCircle, 18)
                            .VerticalAlign(VerticalAlignment.Center)
                            .BindKind(_viewModel, x => x.InteractiveControlsEnabled ? MaterialIconKind.CheckCircle : MaterialIconKind.Cancel)
                            .BindForeground(_viewModel, x => x.InteractiveControlsEnabled ? Color.FromHex("#4CAF50") : Color.FromHex("#E53935")),

                        new TextBlock()
                            .Bold()
                            .FontSize(12)
                            .VerticalAlign(VerticalAlignment.Center)
                            .BindText(_viewModel, x => x.InteractiveControlsEnabled
                                ? "Controls are ENABLED (interactive)"
                                : "Controls are DISABLED (test state)")
                    ),

                new Button("Reset All to Defaults")
                    .Variant(ButtonVariant.Tonal)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ResetDefaultsCommand),

                new Button("Clear / Uncheck All")
                    .Variant(ButtonVariant.Outlined)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ClearAllCommand)
            );

        stack.Add(toggleRow);
        card.Child = stack;
        return card;
    }

    private UIElement CreateCheckboxesCard()
    {
        var children = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // Sub-section 1: Basic & Disabled States
        children.Add(new TextBlock("Standard & Disabled States").Bold().FontSize(13));

        var statesGrid = new Grid()
            .Columns(GridLength.Star, GridLength.Star)
            .Rows(GridLength.Auto, GridLength.Auto)
            .RowSpacing(12)
            .ColumnSpacing(20)
            .Children(
                new CheckBox("Standard Unchecked")
                    .BindIsChecked(_viewModel, x => x.BasicUnchecked, (vm, v) => vm.BasicUnchecked = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(0).Column(0),

                new CheckBox("Standard Checked")
                    .BindIsChecked(_viewModel, x => x.BasicChecked, (vm, v) => vm.BasicChecked = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(0).Column(1),

                new CheckBox("Always Disabled (Unchecked)")
                    .IsEnabled(false)
                    .Row(1).Column(0),

                new CheckBox("Always Disabled (Checked)")
                    { IsChecked = true }
                    .IsEnabled(false)
                    .Row(1).Column(1)
            );
        children.Add(statesGrid);

        // Sub-section 2: Rich Content (Icon, Subtitle)
        children.Add(new TextBlock("Rich Content (Icons & Multi-line Descriptions)").Bold().FontSize(13));

        var iconCheckBox = new CheckBox
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.CloudQueue, 20) { Foreground = Color.FromHex("#1E88E5"), VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock("Sync Workspace with Cloud Storage").VerticalAlign(VerticalAlignment.Center)
                )
        }.BindIsChecked(_viewModel, x => x.SyncCloudStorage, (vm, v) => vm.SyncCloudStorage = v)
         .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled);
        children.Add(iconCheckBox);

        var detailedCheckBox = new CheckBox
        {
            Content = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
                .Children(
                    new TextBlock("Automatic Software Updates").Bold().FontSize(13),
                    new TextBlock("Download and install critical framework hot reload patches in background").FontSize(11).Muted()
                )
        }.BindIsChecked(_viewModel, x => x.AutoUpdate, (vm, v) => vm.AutoUpdate = v)
         .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled);
        children.Add(detailedCheckBox);

        // Sub-section 3: Two-Way Data Binding
        children.Add(new TextBlock("Two-Way MVVM Data Binding").Bold().FontSize(13));

        var bindingRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new CheckBox("Push Notifications")
                    .BindIsChecked(_viewModel, x => x.EnableNotifications, (vm, v) => vm.EnableNotifications = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled),

                new Card(CardVariant.Filled)
                {
                    Padding = new Thickness(10, 4),
                    CornerRadius = new CornerRadius(6),
                    VerticalAlignment = VerticalAlignment.Center
                }.Child(
                    new TextBlock()
                        .Bold()
                        .FontSize(11)
                        .BindText(_viewModel, x => $"ViewModel.EnableNotifications: {(x.EnableNotifications ? "TRUE (Enabled)" : "FALSE (Muted)")}")
                ),

                new Button("Toggle from Code / Command")
                    .Variant(ButtonVariant.Outlined)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ToggleNotificationsCommand)
            );
        children.Add(bindingRow);

        return CreateCard("Checkboxes", "Checkboxes allow users to select one or multiple options, or toggle independent states.", children);
    }

    private UIElement CreateRadioButtonsCard()
    {
        var children = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // Sub-section 1: Mutually Exclusive Selection & Disabled States
        children.Add(new TextBlock("Standard Mutually Exclusive Group & Disabled States").Bold().FontSize(13));

        var groupGrid = new Grid()
            .Columns(GridLength.Star, GridLength.Star)
            .Rows(GridLength.Auto, GridLength.Auto)
            .RowSpacing(12)
            .ColumnSpacing(20)
            .Children(
                new RadioButton("Option A (Standard)")
                    .GroupName("DemoBasic")
                    .BindIsChecked(_viewModel, x => x.BasicOption, (vm, v) => vm.BasicOption = v, "Option A")
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(0).Column(0),

                new RadioButton("Option B (Standard)")
                    .GroupName("DemoBasic")
                    .BindIsChecked(_viewModel, x => x.BasicOption, (vm, v) => vm.BasicOption = v, "Option B")
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(0).Column(1),

                new RadioButton("Always Disabled (Unselected)")
                    .GroupName("DemoDisabled")
                    .IsEnabled(false)
                    .Row(1).Column(0),

                new RadioButton("Always Disabled (Selected)")
                    .GroupName("DemoDisabled")
                    .IsChecked(true)
                    .IsEnabled(false)
                    .Row(1).Column(1)
            );
        children.Add(groupGrid);

        // Sub-section 2: Rich Content (Icon, Header, Badge/Subtitle)
        children.Add(new TextBlock("Rich Content (Icons, Badges & Multi-line Layout)").Bold().FontSize(13));

        var shippingOption1 = new RadioButton
        {
            GroupName = "ShippingMethod",
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.LocalShipping, 20) { Foreground = Color.FromHex("#2E7D32"), VerticalAlignment = VerticalAlignment.Center },
                    new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
                        .Children(
                            new TextBlock("Standard Shipping").Bold().FontSize(13),
                            new TextBlock("Estimated delivery in 3-5 business days (Free)").FontSize(11).Muted()
                        )
                )
        }.BindIsChecked(_viewModel, x => x.ShippingMethod, (vm, v) => vm.ShippingMethod = v, "Standard")
         .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled);
        children.Add(shippingOption1);

        var shippingOption2 = new RadioButton
        {
            GroupName = "ShippingMethod",
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.ElectricBolt, 20) { Foreground = Color.FromHex("#D84315"), VerticalAlignment = VerticalAlignment.Center },
                    new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
                        .Children(
                            new TextBlock("Express Delivery (Next-Day)").Bold().FontSize(13),
                            new TextBlock("Guaranteed morning arrival with real-time GPS tracking ($9.99)").FontSize(11).Muted()
                        )
                )
        }.BindIsChecked(_viewModel, x => x.ShippingMethod, (vm, v) => vm.ShippingMethod = v, "Express")
         .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled);
        children.Add(shippingOption2);

        // Sub-section 3: Enum Data Binding
        children.Add(new TextBlock("Enum / Value Two-Way Data Binding").Bold().FontSize(13));

        var enumStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new RadioButton("720p HD")
                    .GroupName("Quality")
                    .BindIsChecked(_viewModel, x => x.StreamingQuality, (vm, v) => vm.StreamingQuality = v, QualitySetting.Standard720p)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled),

                new RadioButton("1080p FHD")
                    .GroupName("Quality")
                    .BindIsChecked(_viewModel, x => x.StreamingQuality, (vm, v) => vm.StreamingQuality = v, QualitySetting.High1080p)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled),

                new RadioButton("4K Ultra")
                    .GroupName("Quality")
                    .BindIsChecked(_viewModel, x => x.StreamingQuality, (vm, v) => vm.StreamingQuality = v, QualitySetting.Ultra4K)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled),

                new Card(CardVariant.Filled)
                {
                    Padding = new Thickness(10, 4),
                    CornerRadius = new CornerRadius(6),
                    VerticalAlignment = VerticalAlignment.Center
                }.Child(
                    new TextBlock()
                        .Bold()
                        .FontSize(11)
                        .BindText(_viewModel, x => $"Selected Enum: {x.StreamingQuality}")
                )
            );
        children.Add(enumStack);

        return CreateCard("Radio Buttons", "Radio buttons allow users to select exactly one option from a mutually exclusive set.", children);
    }

    private UIElement CreateSwitchesCard()
    {
        var children = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // Sub-section 1: Standard & Disabled MD3 Switches
        children.Add(new TextBlock("Material Design 3 Switches & Thumb Icons").Bold().FontSize(13));

        var switchGrid = new Grid()
            .Columns(GridLength.Star, GridLength.Star)
            .Rows(GridLength.Auto, GridLength.Auto, GridLength.Auto)
            .RowSpacing(12)
            .ColumnSpacing(20)
            .Children(
                new Switch("Standard Switch (Off)")
                    .BindIsChecked(_viewModel, x => x.StandardSwitchOff, (vm, v) => vm.StandardSwitchOff = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(0).Column(0),

                new Switch("Standard Switch (On)")
                    .BindIsChecked(_viewModel, x => x.StandardSwitchOn, (vm, v) => vm.StandardSwitchOn = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(0).Column(1),

                new Switch("MD3 Icon Switch (Off)")
                    .ShowThumbIcon()
                    .BindIsChecked(_viewModel, x => x.IconSwitchOff, (vm, v) => vm.IconSwitchOff = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(1).Column(0),

                new Switch("MD3 Icon Switch (On)")
                    .ShowThumbIcon()
                    .BindIsChecked(_viewModel, x => x.IconSwitchOn, (vm, v) => vm.IconSwitchOn = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(1).Column(1),

                new Switch("Always Disabled (Off)")
                    .IsEnabled(false)
                    .Row(2).Column(0),

                new Switch("Always Disabled (On)")
                    { IsChecked = true }
                    .IsEnabled(false)
                    .Row(2).Column(1)
            );
        children.Add(switchGrid);

        // Sub-section 2: Rich Content Switches
        children.Add(new TextBlock("Rich Content (Icons, Titles & Descriptions)").Bold().FontSize(13));

        var airplaneSwitch = new Switch
        {
            ShowThumbIcon = true,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.AirplanemodeActive, 20) { Foreground = Color.FromHex("#E65100"), VerticalAlignment = VerticalAlignment.Center },
                    new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
                        .Children(
                            new TextBlock("Airplane Mode").Bold().FontSize(13),
                            new TextBlock("Disables Wi-Fi, Bluetooth, and cellular radios simultaneously").FontSize(11).Muted()
                        )
                )
        }.BindIsChecked(_viewModel, x => x.AirplaneMode, (vm, v) => vm.AirplaneMode = v)
         .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled);
        children.Add(airplaneSwitch);

        var fpsSwitch = new Switch
        {
            ShowThumbIcon = true,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.Speed, 20) { Foreground = Color.FromHex("#2E7D32"), VerticalAlignment = VerticalAlignment.Center },
                    new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
                        .Children(
                            new TextBlock("High Smoothness (120 Hz VSync)").Bold().FontSize(13),
                            new TextBlock("Enables sub-pixel spring animations and high-rate frame pacing").FontSize(11).Muted()
                        )
                )
        }.BindIsChecked(_viewModel, x => x.HighFpsMode, (vm, v) => vm.HighFpsMode = v)
         .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled);
        children.Add(fpsSwitch);

        // Sub-section 3: Two-Way Data Binding & Batch Actions
        children.Add(new TextBlock("Two-Way Data Binding & Reactive Batch Actions").Bold().FontSize(13));

        var wirelessRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Switch("Wi-Fi")
                    .ShowThumbIcon()
                    .BindIsChecked(_viewModel, x => x.WifiEnabled, (vm, v) => vm.WifiEnabled = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled),

                new Switch("Bluetooth")
                    .ShowThumbIcon()
                    .BindIsChecked(_viewModel, x => x.BluetoothEnabled, (vm, v) => vm.BluetoothEnabled = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled),

                new Card(CardVariant.Filled)
                {
                    Padding = new Thickness(10, 4),
                    CornerRadius = new CornerRadius(6),
                    VerticalAlignment = VerticalAlignment.Center
                }.Child(
                    new TextBlock()
                        .Bold()
                        .FontSize(11)
                        .BindText(_viewModel, x => $"Wireless State: Wi-Fi: {(x.WifiEnabled ? "ON" : "OFF")} | BT: {(x.BluetoothEnabled ? "ON" : "OFF")}")
                ),

                new Button("Batch Toggle Both")
                    .Variant(ButtonVariant.Tonal)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ToggleAllSwitchesCommand)
            );
        children.Add(wirelessRow);

        return CreateCard("Switches (Material Design 3)", "Switches toggle the state of a single item on or off with an animated capsule track (40×22) and expanding thumb.", children);
    }

    private static UIElement CreateCard(string title, string description, UIElement content)
    {
        var card = new Card(CardVariant.Outlined)
        {
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(12)
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        stack.Add(new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
            .Children(
                new TextBlock(title).Bold().FontSize(15),
                new TextBlock(description).FontSize(12).Muted()
            )
        );

        stack.Add(content);
        card.Child = stack;
        return card;
    }
}
