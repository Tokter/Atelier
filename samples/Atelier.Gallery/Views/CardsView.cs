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

public class CardsView : Grid
{
    private readonly CardsViewModel _viewModel;
    private readonly ScrollViewer _scrollViewer;

    public CardsView() : this(new CardsViewModel())
    {
    }

    public CardsView(CardsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        this.Rows(GridLength.Auto, GridLength.Star);
        this.RowSpacing(16);

        // 1. Master Controls & Quick Actions Banner (Fixed, Non-Scrolling Header)
        this.Add(CreateMasterBanner().Row(0));

        // 2. Scrollable Showcase Cards Container
        var cardsStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 16
        }.Children(
            CreateVariantsComparisonCard(),
            CreateRichMediaCards(),
            CreatePlaygroundCard()
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
            .Padding(20)
            .CornerRadius(14);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // Header text
        stack.Add(new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
            .Children(
                new TextBlock("Cards (Material Design 3)").TitleLarge(),
                new TextBlock("Cards contain content and actions about a single subject. MD3 defines three core variants: Elevated, Filled, and Outlined.")
                    .Subtext()
            )
        );

        // Quick action row
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Button("Reset Playground")
                    .Variant(ButtonVariant.Tonal)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ResetPlaygroundCommand),

                new Button("Clear Counters")
                    .Variant(ButtonVariant.Outlined)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ClearInteractionsCommand)
            );

        stack.Add(actionRow);
        card.Child = stack;
        return card;
    }

    private UIElement CreateVariantsComparisonCard()
    {
        var grid = new Grid()
            .Columns(GridLength.Star, GridLength.Star, GridLength.Star)
            .ColumnSpacing(16);

        // 1. Elevated Card
        var elevatedCard = new Card(CardVariant.Elevated)
            .Elevation(2f)
            .CornerRadius(12)
            .Padding(18)
            .MinHeight(200)
            .Child(
                new Grid()
                    .Rows(GridLength.Auto, GridLength.Star, GridLength.Auto)
                    .RowSpacing(14)
                    .Children(
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                            .Children(
                                new Icon(MaterialIconKind.Layers, 24) { Foreground = Color.FromHex("#6750A4"), VerticalAlignment = VerticalAlignment.Center },
                                new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
                                    .Children(
                                        new TextBlock("Elevated Card").TitleMedium(),
                                        new TextBlock("SurfaceContainerLow • 2dp Shadow").Caption()
                                    )
                            ).Row(0),
                        new TextBlock("Elevated cards have a subtle drop shadow and container fill for clear separation against flat surfaces and busy backgrounds.")
                            .Subtext()
                            .TextWrapping(TextWrapping.Wrap)
                            .Row(1),
                        new Button("Elevated Action")
                            .Variant(ButtonVariant.Filled)
                            .HorizontalAlign(HorizontalAlignment.Left)
                            .Row(2)
                    )
            ).Column(0);

        // 2. Filled Card
        var filledCard = new Card(CardVariant.Filled)
            .CornerRadius(12)
            .Padding(18)
            .MinHeight(200)
            .Child(
                new Grid()
                    .Rows(GridLength.Auto, GridLength.Star, GridLength.Auto)
                    .RowSpacing(14)
                    .Children(
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                            .Children(
                                new Icon(MaterialIconKind.Dashboard, 24) { Foreground = Color.FromHex("#7D5260"), VerticalAlignment = VerticalAlignment.Center },
                                new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
                                    .Children(
                                        new TextBlock("Filled Card").TitleMedium(),
                                        new TextBlock("SurfaceContainerHighest • No Shadow").Caption()
                                    )
                            ).Row(0),
                        new TextBlock("Filled cards use distinct container fill color without casting a shadow, offering visual containment on light or dark pages.")
                            .Subtext()
                            .TextWrapping(TextWrapping.Wrap)
                            .Row(1),
                        new Button("Filled Action")
                            .Variant(ButtonVariant.Tonal)
                            .HorizontalAlign(HorizontalAlignment.Left)
                            .Row(2)
                    )
            ).Column(1);

        // 3. Outlined Card
        var outlinedCard = new Card(CardVariant.Outlined)
            .CornerRadius(12)
            .Padding(18)
            .MinHeight(200)
            .Child(
                new Grid()
                    .Rows(GridLength.Auto, GridLength.Star, GridLength.Auto)
                    .RowSpacing(14)
                    .Children(
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                            .Children(
                                new Icon(MaterialIconKind.CropSquare, 24) { Foreground = Color.FromHex("#2E7D32"), VerticalAlignment = VerticalAlignment.Center },
                                new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
                                    .Children(
                                        new TextBlock("Outlined Card").TitleMedium(),
                                        new TextBlock("Surface • 1dp OutlineVariant").Caption()
                                    )
                            ).Row(0),
                        new TextBlock("Outlined cards feature a clean border outline on the standard surface color, providing boundary separation with minimal visual weight.")
                            .Subtext()
                            .TextWrapping(TextWrapping.Wrap)
                            .Row(1),
                        new Button("Outlined Action")
                            .Variant(ButtonVariant.Outlined)
                            .HorizontalAlign(HorizontalAlignment.Left)
                            .Row(2)
                    )
            ).Column(2);

        grid.Add(elevatedCard);
        grid.Add(filledCard);
        grid.Add(outlinedCard);

        return CreateSectionCard(
            "Material Design 3 Card Variants",
            "Direct side-by-side comparison of the three Material Design 3 card specifications.",
            grid
        );
    }

    private UIElement CreateRichMediaCards()
    {
        var grid = new Grid()
            .Columns(GridLength.Star, GridLength.Star)
            .ColumnSpacing(16);

        // Card 1: Product / Media Banner Card
        var mediaCard = new Card(CardVariant.Filled)
            .CornerRadius(14)
            .Padding(20)
            .MinHeight(220)
            .Child(
                new Grid()
                    .Rows(GridLength.Auto, GridLength.Star, GridLength.Auto)
                    .RowSpacing(14)
                    .Children(
                        new Card(CardVariant.Outlined)
                            .Padding(12)
                            .CornerRadius(8)
                            .Child(
                                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                                    .Children(
                                        new Icon(MaterialIconKind.Palette, 28) { Foreground = Color.FromHex("#1E88E5"), VerticalAlignment = VerticalAlignment.Center },
                                        new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
                                            .Children(
                                                new TextBlock("FEATURED FRAMEWORK RELEASE").LabelSmall().Foreground(Color.FromHex("#1E88E5")),
                                                new TextBlock("Atelier UI Studio v2.5").TitleMedium()
                                            )
                                    )
                            ).Row(0),
                        new TextBlock("High-performance cross-platform desktop UI framework with hardware-accelerated SkiaSharp rendering, sub-pixel animation, and complete Material Design 3 tokens.")
                            .Subtext()
                            .TextWrapping(TextWrapping.Wrap)
                            .Row(1),
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                            .Children(
                                new Button("Explore Studio")
                                    .Variant(ButtonVariant.Filled)
                                    .VerticalAlign(VerticalAlignment.Center),
                                new Button()
                                    .Variant(ButtonVariant.Outlined)
                                    .VerticalAlign(VerticalAlignment.Center)
                                    .Command(_viewModel.ToggleFavoriteCommand)
                                    .Content(
                                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                                            .Children(
                                                new Icon()
                                                    .BindKind(_viewModel, x => x.FavoriteSelected ? MaterialIconKind.Bookmark : MaterialIconKind.BookmarkBorder)
                                                    .BindForeground(_viewModel, x => x.FavoriteSelected ? Color.FromHex("#FFB300") : Color.FromHex("#757575")),
                                                new TextBlock()
                                                    .LabelMedium()
                                                    .BindText(_viewModel, x => x.FavoriteSelected ? "Saved" : "Save")
                                            )
                                    )
                            ).Row(2)
                    )
            ).Column(0);

        // Card 2: Interactive Controls & Embedded Settings Card
        var controlsCard = new Card(CardVariant.Elevated)
            .Elevation(2f)
            .CornerRadius(14)
            .Padding(20)
            .MinHeight(220)
            .Child(
                new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 }
                    .Children(
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                            .Children(
                                new Icon(MaterialIconKind.Tune, 24) { Foreground = Color.FromHex("#E65100"), VerticalAlignment = VerticalAlignment.Center },
                                new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
                                    .Children(
                                        new TextBlock("Embedded Interactive Controls").TitleMedium(),
                                        new TextBlock("Cards can host switches, text fields, and buttons.").Caption()
                                    )
                            ),
                        new Switch("Card Notifications")
                            .ShowThumbIcon()
                            .BindIsChecked(_viewModel, x => x.NotificationsCardEnabled, (vm, v) => vm.NotificationsCardEnabled = v),

                        new TextBox("Notes inside card...")
                            .Variant(TextBoxVariant.Outlined)
                            .Label("Card Annotation")
                            .Placeholder("Enter notes..."),

                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center }
                            .Children(
                                new Button("Tap Card Action")
                                    .Variant(ButtonVariant.Tonal)
                                    .VerticalAlign(VerticalAlignment.Center)
                                    .Command(_viewModel.CardClickCommand),

                                new Card(CardVariant.Filled)
                                    .Padding(10, 4)
                                    .CornerRadius(6)
                                    .VerticalAlign(VerticalAlignment.Center)
                                    .Child(
                                        new TextBlock()
                                            .LabelSmall()
                                            .BindText(_viewModel, x => $"Interactions: {x.InteractiveCardClickCount} taps")
                                    )
                            )
                    )
            ).Column(1);

        grid.Add(mediaCard);
        grid.Add(controlsCard);

        return CreateSectionCard(
            "Rich Content & Interactive Cards",
            "Cards hosting complex composite layouts, media headers, embedded inputs, and action buttons.",
            grid
        );
    }

    private UIElement CreatePlaygroundCard()
    {
        var grid = new Grid()
            .Columns(GridLength.Star, GridLength.Star)
            .ColumnSpacing(20);

        // Left Panel: Controls Customizer
        var controlsStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // 1. Variant selection
        controlsStack.Add(new TextBlock("Card Variant").TitleSmall());
        var variantStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new RadioButton("Elevated")
                    .GroupName("PlaygroundVariant")
                    .BindIsChecked(_viewModel, x => x.PlaygroundVariant, (vm, v) => vm.PlaygroundVariant = v, CardVariant.Elevated),
                new RadioButton("Filled")
                    .GroupName("PlaygroundVariant")
                    .BindIsChecked(_viewModel, x => x.PlaygroundVariant, (vm, v) => vm.PlaygroundVariant = v, CardVariant.Filled),
                new RadioButton("Outlined")
                    .GroupName("PlaygroundVariant")
                    .BindIsChecked(_viewModel, x => x.PlaygroundVariant, (vm, v) => vm.PlaygroundVariant = v, CardVariant.Outlined)
            );
        controlsStack.Add(variantStack);

        // 2. Elevation slider
        controlsStack.Add(
            new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
                .Children(
                    new TextBlock().LabelMedium().BindText(_viewModel, x => $"Elevation: {x.PlaygroundElevation:F0}dp"),
                    new Slider()
                        .Minimum(0f)
                        .Maximum(8f)
                        .BindValue(_viewModel, x => x.PlaygroundElevation, (vm, v) => vm.PlaygroundElevation = v)
                )
        );

        // 3. Corner Radius slider
        controlsStack.Add(
            new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
                .Children(
                    new TextBlock().LabelMedium().BindText(_viewModel, x => $"Corner Radius: {x.PlaygroundCornerRadius:F0}dp"),
                    new Slider()
                        .Minimum(0f)
                        .Maximum(28f)
                        .BindValue(_viewModel, x => x.PlaygroundCornerRadius, (vm, v) => vm.PlaygroundCornerRadius = v)
                )
        );

        // 4. Padding slider
        controlsStack.Add(
            new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
                .Children(
                    new TextBlock().LabelMedium().BindText(_viewModel, x => $"Padding: {x.PlaygroundPadding:F0}dp"),
                    new Slider()
                        .Minimum(8f)
                        .Maximum(36f)
                        .BindValue(_viewModel, x => x.PlaygroundPadding, (vm, v) => vm.PlaygroundPadding = v)
                )
        );

        // Right Panel: Live Card Preview
        var previewCard = new Card()
            .BindVariant(_viewModel, x => x.PlaygroundVariant)
            .BindElevation(_viewModel, x => x.PlaygroundElevation)
            .BindCornerRadius(_viewModel, x => x.PlaygroundCornerRadius)
            .BindPadding(_viewModel, x => x.PlaygroundPadding)
            .VerticalAlign(VerticalAlignment.Center)
            .Child(
                new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
                    .Children(
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                            .Children(
                                new Icon(MaterialIconKind.AutoAwesome, 22) { Foreground = Color.FromHex("#6750A4"), VerticalAlignment = VerticalAlignment.Center },
                                new TextBlock("Live Card Preview").TitleMedium()
                            ),
                        new TextBlock()
                            .Caption()
                            .BindText(_viewModel, x => $"Variant: {x.PlaygroundVariant}  |  Elevation: {x.PlaygroundElevation:F0}dp"),
                        new TextBlock()
                            .Caption()
                            .BindText(_viewModel, x => $"Corner Radius: {x.PlaygroundCornerRadius:F0}dp  |  Padding: {x.PlaygroundPadding:F0}dp"),
                        new Button("Interactive Preview Action")
                            .Variant(ButtonVariant.Filled)
                            .HorizontalAlign(HorizontalAlignment.Left)
                    )
            );

        grid.Add(controlsStack.Column(0));
        grid.Add(previewCard.Column(1));

        return CreateSectionCard(
            "Live Interactive Card Playground",
            "Dynamically modify card variant, elevation, corner radius, and padding in real-time.",
            grid
        );
    }

    private static UIElement CreateSectionCard(string title, string description, UIElement content)
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(20)
            .CornerRadius(12);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        stack.Add(new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
            .Children(
                new TextBlock(title).TitleMedium(),
                new TextBlock(description).Subtext()
            )
        );

        stack.Add(content);
        card.Child = stack;
        return card;
    }
}
