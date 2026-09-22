using System;
using System.Threading.Tasks;
using Atelier.Controls;
using Atelier.Core.Animation;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming;
using Atelier.Gallery.ViewModels;

namespace Atelier.Gallery.Views;

public class TransitionsView : Grid
{
    private readonly TransitionsViewModel _viewModel;
    private readonly TransitioningContentControl _transitionHost;
    private readonly TextBlock _durationLabel;
    private readonly TextBlock _statusLabel;
    private readonly TextBlock _activeModeLabel;
    private ComboBox _transitionCombo = null!;
    private ComboBox _easingCombo = null!;

    public TransitionsView() : this(new TransitionsViewModel())
    {
    }

    public TransitionsView(TransitionsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        this.Rows(GridLength.Auto, GridLength.Star);
        this.RowSpacing(16);

        // 1. Header Banner
        this.Add(CreateHeaderBanner().Row(0));

        // 2. Main Two-Column Showcase Area
        var mainGrid = new Grid()
            .Columns(new GridLength(380, GridUnitType.Pixel), GridLength.Star)
            .ColumnSpacing(20);

        _transitionHost = new TransitioningContentControl
        {
            ClipToBounds = true,
            Transition = _viewModel.CreateCurrentTransition(),
            ContentTemplate = RenderCardItem
        };

        _durationLabel = new TextBlock($"{_viewModel.DurationMs:F0} ms")
            .BodyMedium()
            .Bold()
            .VerticalAlign(VerticalAlignment.Center);

        _statusLabel = new TextBlock(_viewModel.StatusMessage)
            .BodySmall()
            .VerticalAlign(VerticalAlignment.Center);

        _activeModeLabel = new TextBlock(_viewModel.SelectedTransitionType.ToString())
            .BodyMedium()
            .Bold()
            .VerticalAlign(VerticalAlignment.Center);

        // Left: Controls Card
        mainGrid.Add(CreateControlsPanel().Column(0));

        // Right: Stage & Preview Card
        mainGrid.Add(CreateStagePanel().Column(1));

        this.Add(mainGrid.Row(1));

        // Initial setup
        UpdateTransitionHost();

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.CurrentCard))
            {
                UpdateTransitionHost();
                _statusLabel.Text = _viewModel.StatusMessage;
            }
            else if (e.PropertyName == nameof(_viewModel.SelectedTransitionType) || e.PropertyName == nameof(_viewModel.SelectedTransitionItem))
            {
                _activeModeLabel.Text = _viewModel.SelectedTransitionType.ToString();
                _statusLabel.Text = _viewModel.StatusMessage;
                if (_transitionCombo != null && _transitionCombo.SelectedItem != _viewModel.SelectedTransitionItem)
                {
                    _transitionCombo.SelectedItem = _viewModel.SelectedTransitionItem;
                }
                UpdateTransitionHost();
            }
            else if (e.PropertyName == nameof(_viewModel.SelectedEasingIndex) || e.PropertyName == nameof(_viewModel.SelectedEasingItem))
            {
                if (_easingCombo != null && _easingCombo.SelectedItem != _viewModel.SelectedEasingItem)
                {
                    _easingCombo.SelectedItem = _viewModel.SelectedEasingItem;
                }
                _statusLabel.Text = _viewModel.StatusMessage;
                UpdateTransitionHost();
            }
            else if (e.PropertyName == nameof(_viewModel.DurationMs))
            {
                _durationLabel.Text = $"{_viewModel.DurationMs:F0} ms";
                UpdateTransitionHost();
            }
            else if (e.PropertyName == nameof(_viewModel.StatusMessage))
            {
                _statusLabel.Text = _viewModel.StatusMessage;
            }
        };
    }

    private void UpdateTransitionHost()
    {
        _transitionHost.Transition = _viewModel.CreateCurrentTransition();
        _transitionHost.Content = _viewModel.CurrentCard;
    }

    private UIElement CreateHeaderBanner()
    {
        var card = new Card(CardVariant.Filled)
            .Padding(20)
            .CornerRadius(14);

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Animation, 28, foreground: Color.FromHex("#1E88E5")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Animated Transitions & TransitioningContentControl")
                    .TitleLarge()
                    .Bold()
                    .VerticalAlign(VerticalAlignment.Center),
                CreatePillBadge("AnimationClock Driven", Color.FromHex("#10B981")),
                CreatePillBadge("Dual-Child Layout", Color.FromHex("#8B5CF6")),
                CreatePillBadge("M3 Motion Curves", Color.FromHex("#F59E0B")),
                CreatePillBadge("Interruption Safe", Color.FromHex("#EC4899"))
            );

        var description = new TextBlock(
            "Seamless animated content swapping for ContentControl and future TabControl, Carousel, and page navigators. " +
            "Supports directional slides, cross-fades, zooms, and composite motions with complete hit-test isolation."
        )
        {
            TextWrapping = TextWrapping.Wrap
        }
        .BodyMedium()
        .Foreground(Color.FromHex("#9E9E9E"));

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(titleRow, description);

        card.Child = stack;
        return card;
    }

    private UIElement CreateControlsPanel()
    {
        var card = new Card(CardVariant.Elevated)
            .Padding(18)
            .CornerRadius(14);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 16 };

        // Section: Transition Type ComboBox
        var transitionHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Animation, 20, foreground: Color.FromHex("#1E88E5")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Transition Type").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center)
            );
        stack.Add(transitionHeader);

        _transitionCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = _viewModel.TransitionTypes,
            SelectedItem = _viewModel.SelectedTransitionItem,
            MaxDropDownHeight = 320f,
            ItemTemplate = item =>
            {
                if (item is TransitionTypeItem tItem)
                {
                    return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                        .Children(
                            new Icon(tItem.Icon, 18, foreground: Color.FromHex("#1E88E5")) { VerticalAlignment = VerticalAlignment.Center },
                            new TextBlock(tItem.Name).BodyMedium().VerticalAlign(VerticalAlignment.Center)
                        );
                }
                return new TextBlock(item?.ToString() ?? string.Empty);
            }
        };

        _transitionCombo.SelectionChanged += (s, item) =>
        {
            if (item is TransitionTypeItem tItem)
            {
                _viewModel.SelectedTransitionItem = tItem;
            }
        };
        stack.Add(_transitionCombo);

        // Section: Duration Slider
        var durationHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Timer, 20, foreground: Color.FromHex("#F59E0B")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Duration:").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                _durationLabel
            );
        stack.Add(durationHeader);

        var durationSlider = new Slider
        {
            Minimum = 150f,
            Maximum = 1200f,
            Value = _viewModel.DurationMs,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        durationSlider.ValueChanged += (s, val) =>
        {
            _viewModel.DurationMs = val;
        };
        stack.Add(durationSlider);

        // Section: Easing Selector ComboBox
        var easingHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Timeline, 20, foreground: Color.FromHex("#10B981")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Motion Curve (Easing)").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center)
            );
        stack.Add(easingHeader);

        _easingCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = _viewModel.EasingCurves,
            SelectedItem = _viewModel.SelectedEasingItem,
            MaxDropDownHeight = 240f,
            ItemTemplate = item =>
            {
                if (item is EasingItem eItem)
                {
                    return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                        .Children(
                            new Icon(MaterialIconKind.Timeline, 18, foreground: Color.FromHex("#10B981")) { VerticalAlignment = VerticalAlignment.Center },
                            new TextBlock(eItem.Name).BodyMedium().VerticalAlign(VerticalAlignment.Center)
                        );
                }
                return new TextBlock(item?.ToString() ?? string.Empty);
            }
        };

        _easingCombo.SelectionChanged += (s, item) =>
        {
            if (item is EasingItem eItem)
            {
                _viewModel.SelectedEasingItem = eItem;
            }
        };
        stack.Add(_easingCombo);

        // Section: Stress Test Button
        var stressHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Bolt, 20, foreground: Color.FromHex("#EC4899")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Stress Testing").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center)
            );
        stack.Add(stressHeader);

        var stressBtn = new Button("Rapid Fire (5x Switches)")
            .Variant(ButtonVariant.Tonal)
            .HorizontalAlign(HorizontalAlignment.Stretch);

        stressBtn.Click += async (s, e) =>
        {
            for (int i = 0; i < 5; i++)
            {
                _viewModel.NextCard();
                await Task.Delay(100);
            }
        };
        stack.Add(stressBtn);

        scroll.Content = stack;
        card.Child = scroll;
        return card;
    }

    private UIElement CreateStagePanel()
    {
        var card = new Card(CardVariant.Elevated)
            .Padding(20)
            .CornerRadius(14);

        var layoutGrid = new Grid()
            .Rows(GridLength.Auto, GridLength.Star, GridLength.Auto)
            .RowSpacing(16);

        // Top Navigation Bar (Previous, Indicators, Next)
        var navBar = new Grid()
            .Columns(GridLength.Auto, GridLength.Star, GridLength.Auto)
            .Rows(GridLength.Auto)
            .VerticalAlign(VerticalAlignment.Center);

        var prevBtn = new Button("◀ Previous")
            .Variant(ButtonVariant.Outlined);
        prevBtn.Click += (s, e) => _viewModel.PreviousCard();
        navBar.Add(prevBtn.Column(0));

        var indicatorStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        for (int i = 0; i < _viewModel.Cards.Count; i++)
        {
            int cardIndex = i;
            var chip = new Button($"#{cardIndex + 1} {_viewModel.Cards[cardIndex].Title.Split(' ')[0]}")
                .Variant(ButtonVariant.Text);
            chip.FontSize = 12;
            chip.Click += (s, e) => _viewModel.SelectCard(cardIndex);
            indicatorStack.Add(chip);
        }
        navBar.Add(indicatorStack.Column(1));

        var nextBtn = new Button("Next ▶")
            .Variant(ButtonVariant.Filled);
        nextBtn.Click += (s, e) => _viewModel.NextCard();
        navBar.Add(nextBtn.Column(2));

        layoutGrid.Add(navBar.Row(0));

        // Center Content Host Area
        var stageContainer = new Border
        {
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Padding = new Thickness(0),
            MinHeight = 280,
            Child = _transitionHost
        };
        layoutGrid.Add(stageContainer.Row(1));

        // Bottom Status Bar
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Info, 18, foreground: Color.FromHex("#1E88E5")) { VerticalAlignment = VerticalAlignment.Center },
                _statusLabel
            );

        layoutGrid.Add(statusRow.Row(2));

        card.Child = layoutGrid;
        return card;
    }

    private UIElement RenderCardItem(object? data)
    {
        if (data is not TransitionCardItem item)
        {
            return new TextBlock("No content available");
        }

        var rootCard = new Card(CardVariant.Outlined)
            .Padding(24)
            .CornerRadius(12)
            .HorizontalAlign(HorizontalAlignment.Stretch)
            .VerticalAlign(VerticalAlignment.Stretch);

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Header with Category Chip and Icon
        var header = new Grid()
            .Columns(GridLength.Auto, GridLength.Star, GridLength.Auto)
            .Rows(GridLength.Auto)
            .VerticalAlign(VerticalAlignment.Center);

        var iconContainer = new Border
        {
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(10),
            Background = Color.FromHex(item.AccentColor).WithAlpha(0.12f),
            Child = new Icon(item.Icon, 32, foreground: Color.FromHex(item.AccentColor))
        };
        header.Add(iconContainer.Column(0));

        var titleStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new TextBlock(item.Title).TitleLarge().Bold(),
                new TextBlock(item.Subtitle).BodyMedium().Foreground(Color.FromHex("#9E9E9E"))
            );
        header.Add(titleStack.Column(1));

        var catChip = CreatePillBadge(item.Category, Color.FromHex(item.AccentColor));
        header.Add(catChip.Column(2));

        stack.Add(header);

        // Body Content Details Card
        var detailsCard = new Card(CardVariant.Filled)
            .Padding(16)
            .CornerRadius(10);

        var detailsStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };

        detailsStack.Add(new TextBlock(item.DetailText).BodyMedium());

        // Interactive sample controls inside the transitioning view!
        var interactiveRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Button("Action A").Variant(ButtonVariant.Filled),
                new Button("Action B").Variant(ButtonVariant.Outlined),
                new Switch { IsChecked = true },
                new CheckBox("Enabled") { IsChecked = true }
            );

        detailsStack.Add(interactiveRow);
        detailsCard.Child = detailsStack;

        stack.Add(detailsCard);

        rootCard.Child = stack;
        return rootCard;
    }

    private static UIElement CreatePillBadge(string text, Color color)
    {
        return new Border
        {
            Background = color.WithAlpha(0.12f),
            BorderBrush = color.WithAlpha(0.35f),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4),
            Child = new TextBlock(text)
                .BodySmall()
                .Bold()
                .Foreground(color)
                .VerticalAlign(VerticalAlignment.Center)
        };
    }
}
