using System;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming;
using Atelier.Gallery.ViewModels;

namespace Atelier.Gallery.Views;

public class TextBoxesView : Grid
{
    private readonly TextBoxesViewModel _viewModel;
    private readonly ScrollViewer _scrollViewer;

    public TextBoxesView() : this(new TextBoxesViewModel())
    {
    }

    public TextBoxesView(TextBoxesViewModel viewModel)
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
            CreateOutlinedCard(),
            CreateFilledCard(),
            CreateDataBindingCard()
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
            CornerRadius = new CornerRadius(14)
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // Header text
        stack.Add(new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
            .Children(
                new TextBlock("Text Fields (Material Design 3)").Bold().FontSize(18),
                new TextBlock("Demonstrating Outlined and Filled text boxes with animated floating labels, leading icons, supporting text, and live data-binding.")
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

                new Button("Reset to Defaults")
                    .Variant(ButtonVariant.Tonal)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ResetDefaultsCommand),

                new Button("Clear All Fields")
                    .Variant(ButtonVariant.Outlined)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ClearAllCommand)
            );

        stack.Add(toggleRow);
        card.Child = stack;
        return card;
    }

    private UIElement CreateOutlinedCard()
    {
        var children = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        children.Add(new TextBlock("Standard, Leading Icon & Supporting Text Configurations").Bold().FontSize(13));

        var grid = new Grid()
            .Columns(GridLength.Star, GridLength.Star)
            .Rows(GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto)
            .RowSpacing(14)
            .ColumnSpacing(20)
            .Children(
                // Row 0: Basic Outlined Empty vs Populated
                new TextBox()
                    .Variant(TextBoxVariant.Outlined)
                    .Label("Username")
                    .Placeholder("Enter account username")
                    .BindText(_viewModel, x => x.OutlinedUsername, (vm, v) => vm.OutlinedUsername = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(0).Column(0),

                new TextBox()
                    .Variant(TextBoxVariant.Outlined)
                    .Label("Email Address")
                    .BindText(_viewModel, x => x.OutlinedEmail, (vm, v) => vm.OutlinedEmail = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(0).Column(1),

                // Row 1: Leading Icon & Supporting Text
                new TextBox()
                    .Variant(TextBoxVariant.Outlined)
                    .Label("Search Symbols")
                    .LeadingIcon(MaterialIconKind.Search)
                    .Placeholder("Type symbol or keyword...")
                    .BindText(_viewModel, x => x.OutlinedSearch, (vm, v) => vm.OutlinedSearch = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(1).Column(0),

                new TextBox()
                    .Variant(TextBoxVariant.Outlined)
                    .Label("Repository Name")
                    .SupportingText("Visible to members of this organization")
                    .Placeholder("my-awesome-repo")
                    .BindText(_viewModel, x => x.OutlinedRepo, (vm, v) => vm.OutlinedRepo = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(1).Column(1),

                // Row 2: Combined Leading Icon + Supporting Text
                new TextBox()
                    .Variant(TextBoxVariant.Outlined)
                    .Label("Mobile Number")
                    .LeadingIcon(MaterialIconKind.Phone)
                    .SupportingText("Include country code (e.g. +1)")
                    .BindText(_viewModel, x => x.OutlinedPhone, (vm, v) => vm.OutlinedPhone = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(2).Column(0),

                new TextBox()
                    .Variant(TextBoxVariant.Outlined)
                    .Label("Account Security Key")
                    .LeadingIcon(MaterialIconKind.Lock)
                    .SupportingText("Minimum 8 characters with symbols")
                    .BindText(_viewModel, x => x.OutlinedSecurityKey, (vm, v) => vm.OutlinedSecurityKey = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(2).Column(1),

                // Row 3: Disabled States
                new TextBox()
                    .Variant(TextBoxVariant.Outlined)
                    .Label("Disabled Empty")
                    .Placeholder("Cannot enter text")
                    .IsEnabled(false)
                    .Row(3).Column(0),

                new TextBox("Protected system configuration")
                    .Variant(TextBoxVariant.Outlined)
                    .Label("Disabled Populated")
                    .IsEnabled(false)
                    .Row(3).Column(1)
            );

        children.Add(grid);

        return CreateCard(
            "Outlined Text Fields",
            "Outlined fields feature a border around the entire container with a transparent background. When focused or populated, the label smoothly animates into the top border notch.",
            children
        );
    }

    private UIElement CreateFilledCard()
    {
        var children = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        children.Add(new TextBlock("Container Background & Active Underline Indicator").Bold().FontSize(13));

        var grid = new Grid()
            .Columns(GridLength.Star, GridLength.Star)
            .Rows(GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto)
            .RowSpacing(14)
            .ColumnSpacing(20)
            .Children(
                // Row 0: Basic Filled Empty vs Populated
                new TextBox()
                    .Variant(TextBoxVariant.Filled)
                    .Label("Organization")
                    .Placeholder("e.g. Acme Corp")
                    .BindText(_viewModel, x => x.FilledOrganization, (vm, v) => vm.FilledOrganization = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(0).Column(0),

                new TextBox()
                    .Variant(TextBoxVariant.Filled)
                    .Label("Environment Name")
                    .BindText(_viewModel, x => x.FilledEnvironment, (vm, v) => vm.FilledEnvironment = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(0).Column(1),

                // Row 1: Leading Icon & Supporting Text
                new TextBox()
                    .Variant(TextBoxVariant.Filled)
                    .Label("API Bearer Token")
                    .LeadingIcon(MaterialIconKind.Key)
                    .Placeholder("Paste OAuth or bearer token...")
                    .BindText(_viewModel, x => x.FilledApiToken, (vm, v) => vm.FilledApiToken = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(1).Column(0),

                new TextBox()
                    .Variant(TextBoxVariant.Filled)
                    .Label("Deployment Branch")
                    .SupportingText("Target branch for continuous deployment")
                    .Placeholder("main")
                    .BindText(_viewModel, x => x.FilledBranch, (vm, v) => vm.FilledBranch = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(1).Column(1),

                // Row 2: Combined Leading Icon + Supporting Text
                new TextBox()
                    .Variant(TextBoxVariant.Filled)
                    .Label("Primary Office")
                    .LeadingIcon(MaterialIconKind.LocationOn)
                    .SupportingText("Headquarters campus location")
                    .BindText(_viewModel, x => x.FilledLocation, (vm, v) => vm.FilledLocation = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(2).Column(0),

                new TextBox()
                    .Variant(TextBoxVariant.Filled)
                    .Label("Inquiry Inbox")
                    .LeadingIcon(MaterialIconKind.Mail)
                    .SupportingText("Monitored during standard business hours")
                    .BindText(_viewModel, x => x.FilledInbox, (vm, v) => vm.FilledInbox = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(2).Column(1),

                // Row 3: Disabled States
                new TextBox()
                    .Variant(TextBoxVariant.Filled)
                    .Label("Disabled Empty")
                    .Placeholder("Read-only access")
                    .IsEnabled(false)
                    .Row(3).Column(0),

                new TextBox("Cluster ID: 9821-XCA-09")
                    .Variant(TextBoxVariant.Filled)
                    .Label("Disabled Populated")
                    .IsEnabled(false)
                    .Row(3).Column(1)
            );

        children.Add(grid);

        return CreateCard(
            "Filled Text Fields",
            "Filled text fields have a colored container fill and an underline active indicator that brightens to Primary color upon focus, with rounded top corners (4dp).",
            children
        );
    }

    private UIElement CreateDataBindingCard()
    {
        var children = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        children.Add(new TextBlock("Two-Way MVVM Property Binding & Live Feedback").Bold().FontSize(13));

        var grid = new Grid()
            .Columns(GridLength.Star, GridLength.Star)
            .Rows(GridLength.Auto, GridLength.Auto)
            .RowSpacing(14)
            .ColumnSpacing(20)
            .Children(
                new TextBox()
                    .Variant(TextBoxVariant.Outlined)
                    .Label("Username")
                    .LeadingIcon(MaterialIconKind.Person)
                    .BindText(_viewModel, x => x.Username, (vm, v) => vm.Username = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(0).Column(0),

                new TextBox()
                    .Variant(TextBoxVariant.Outlined)
                    .Label("Email Address")
                    .LeadingIcon(MaterialIconKind.Email)
                    .BindText(_viewModel, x => x.Email, (vm, v) => vm.Email = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(0).Column(1),

                new TextBox()
                    .Variant(TextBoxVariant.Filled)
                    .Label("Direct Contact")
                    .LeadingIcon(MaterialIconKind.Phone)
                    .BindText(_viewModel, x => x.Phone, (vm, v) => vm.Phone = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(1).Column(0),

                new TextBox()
                    .Variant(TextBoxVariant.Filled)
                    .Label("Biography")
                    .SupportingText("Short professional summary")
                    .BindText(_viewModel, x => x.Bio, (vm, v) => vm.Bio = v)
                    .BindIsEnabled(_viewModel, x => x.InteractiveControlsEnabled)
                    .Row(1).Column(1)
            );

        children.Add(grid);

        // Live Preview card displaying ViewModel values in real-time
        var previewCard = new Card(CardVariant.Filled)
        {
            Padding = new Thickness(16, 12),
            CornerRadius = new CornerRadius(8)
        }.Child(
            new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 }
                .Children(
                    new TextBlock("Live ViewModel State").Bold().FontSize(12),
                    new TextBlock()
                        .FontSize(11)
                        .BindText(_viewModel, x => $"Username: \"{x.Username}\"  |  Email: \"{x.Email}\""),
                    new TextBlock()
                        .FontSize(11)
                        .BindText(_viewModel, x => $"Phone: \"{x.Phone}\"  |  Bio: \"{x.Bio}\"")
                )
        );

        children.Add(previewCard);

        return CreateCard(
            "Two-Way Data Binding",
            "Text boxes bound two-way update the ViewModel as you type and immediately reflect programmatic updates.",
            children
        );
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
