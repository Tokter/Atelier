using System;
using System.Collections.Generic;
using SkiaSharp;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming;
using Atelier.Gallery.ViewModels;

namespace Atelier.Gallery.Views;

public class IconsView : Grid
{
    private readonly IconsViewModel _viewModel;
    private readonly ScrollViewer _scrollViewer;
    private WrapPanel? _catalogWrapPanel;
    private TextBlock? _statusText;
    private StackPanel? _footerRow;
    private readonly List<(MaterialIconKind Kind, Card Card)> _catalogTiles = [];

    public IconsView() : this(new IconsViewModel())
    {
    }

    public IconsView(IconsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        this.Rows(GridLength.Auto, GridLength.Star);
        this.RowSpacing(16);

        // 1. Master Controls & Header Banner (Fixed, Non-Scrolling Header)
        this.Add(CreateMasterBanner().Row(0));

        // 2. Scrollable Showcase Content
        var contentStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 16
        }.Children(
            CreatePlaygroundSection(),
            CreateSearchCatalogSection(),
            CreateAxesDeepDiveSection(),
            CreateCustomVectorSection()
        );

        contentStack.Margin = new Thickness(0, 0, 10, 20);

        _scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = contentStack
        }.Row(1);

        this.Add(_scrollViewer);

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IconsViewModel.FilterRevision))
            {
                RebuildCatalogTiles();
            }
            else if (e.PropertyName == nameof(IconsViewModel.PlaygroundKind))
            {
                UpdateCatalogTileSelection();
            }
            else if (e.PropertyName == nameof(IconsViewModel.SearchStatus) && _statusText != null)
            {
                _statusText.Text = _viewModel.SearchStatus;
            }
            else if (e.PropertyName == nameof(IconsViewModel.HasMoreIcons) && _footerRow != null)
            {
                _footerRow.Visibility = _viewModel.HasMoreIcons ? Visibility.Visible : Visibility.Collapsed;
            }
        };
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
                new TextBlock("Icons & Material Symbols (4 Variable Axes)").TitleLarge(),
                new TextBlock("High-performance vector rendering of Google Material Symbols Rounded across 4 variable font axes (FILL, wght, GRAD, opsz) and custom SVG/SKPath geometry.")
                    .Subtext()
            )
        );

        // Action row
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Button("Toggle Outlined / Filled")
                    .Variant(ButtonVariant.Filled)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ToggleFillCommand),

                new Button("Reset All Axes")
                    .Variant(ButtonVariant.Outlined)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ResetPlaygroundCommand)
            );

        stack.Add(actionRow);
        card.Child = stack;
        return card;
    }

    #region Section 1: Live Interactive 4-Axes Playground

    private UIElement CreatePlaygroundSection()
    {
        var grid = new Grid()
            .Columns(GridLength.Star, GridLength.Star)
            .ColumnSpacing(16);

        // --- Left Column: Hero Preview Card ---
        var heroIcon = new Icon()
            .BindKind(_viewModel, x => x.PlaygroundKind)
            .BindForeground(_viewModel, x => x.PlaygroundColor)
            .BindFill(_viewModel, x => x.PlaygroundFill)
            .BindWeight(_viewModel, x => x.PlaygroundWeight)
            .BindGrade(_viewModel, x => x.PlaygroundGrade)
            .BindOpticalSize(_viewModel, x => x.PlaygroundOpticalSize)
            .BindSize(_viewModel, x => x.PlaygroundSize)
            .HorizontalAlign(HorizontalAlignment.Center)
            .VerticalAlign(VerticalAlignment.Center);

        var heroBox = new Card(CardVariant.Filled)
            .CornerRadius(16)
            .Padding(24)
            .MinHeight(160)
            .HorizontalAlign(HorizontalAlignment.Stretch)
            .VerticalAlign(VerticalAlignment.Center)
            .Child(heroIcon);

        var readoutsCard = new Card(CardVariant.Outlined)
            .Padding(12, 10)
            .CornerRadius(8)
            .Child(
                new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
                    .Children(
                        new TextBlock()
                            .LabelMedium()
                            .BindText(_viewModel, x => $"Icon: {x.PlaygroundKind}   |   Size: {x.PlaygroundSize:F0}dp"),
                        new TextBlock()
                            .Caption()
                            .BindText(_viewModel, x => $"FILL: {x.PlaygroundFill:F2} ({(x.PlaygroundFill >= 0.5f ? "Filled" : "Outlined")})   |   wght: {x.PlaygroundWeight:F0} ({x.WeightName})"),
                        new TextBlock()
                            .Caption()
                            .BindText(_viewModel, x => $"GRAD: {x.PlaygroundGrade:F0}   |   opsz: {x.PlaygroundOpticalSize:F0}dp")
                    )
            );

        // Quick icon picker buttons
        var iconButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center }
            .Children(
                CreatePickerButton(MaterialIconKind.Favorite, Color.FromHex("#E11D48")),
                CreatePickerButton(MaterialIconKind.Star, Color.FromHex("#EAB308")),
                CreatePickerButton(MaterialIconKind.Settings, Color.FromHex("#3B82F6")),
                CreatePickerButton(MaterialIconKind.Palette, Color.FromHex("#A855F7")),
                CreatePickerButton(MaterialIconKind.RocketLaunch, Color.FromHex("#F97316")),
                CreatePickerButton(MaterialIconKind.Bolt, Color.FromHex("#10B981")),
                CreatePickerButton(MaterialIconKind.Verified, Color.FromHex("#6366F1")),
                CreatePickerButton(MaterialIconKind.Diamond, Color.FromHex("#06B6D4"))
            );

        var leftPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }
            .Children(heroBox, readoutsCard, iconButtons);

        // --- Right Column: Sliders for 4 Axes + Size ---
        var slidersStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 };

        // 1. FILL Axis Slider
        var fillLabel = new TextBlock().LabelMedium();
        fillLabel.BindText(_viewModel, x => $"FILL Axis: {x.PlaygroundFill:F2} ({(x.PlaygroundFill >= 0.5f ? "Solid Fill" : "Outlined")})");
        var fillSlider = new Slider { Minimum = 0, Maximum = 100, Value = _viewModel.PlaygroundFill * 100f };
        fillSlider.ValueChanged += (s, v) => _viewModel.PlaygroundFill = v / 100f;

        // 2. Weight Axis Slider (100 to 700)
        var weightLabel = new TextBlock().LabelMedium();
        weightLabel.BindText(_viewModel, x => $"wght (Weight) Axis: {x.PlaygroundWeight:F0} ({x.WeightName})");
        var weightSlider = new Slider { Minimum = 100, Maximum = 700, Value = _viewModel.PlaygroundWeight };
        weightSlider.ValueChanged += (s, v) => _viewModel.PlaygroundWeight = v;

        // 3. Grade Axis Slider (-25 to 200)
        var gradeLabel = new TextBlock().LabelMedium();
        gradeLabel.BindText(_viewModel, x => $"GRAD (Grade) Axis: {x.PlaygroundGrade:F0} ({(x.PlaygroundGrade == 0 ? "Normal" : x.PlaygroundGrade > 0 ? "Heavy Emphasis" : "Low Emphasis")})");
        var gradeSlider = new Slider { Minimum = -25, Maximum = 200, Value = _viewModel.PlaygroundGrade };
        gradeSlider.ValueChanged += (s, v) => _viewModel.PlaygroundGrade = v;

        // 4. Optical Size Axis Slider (20 to 48)
        var opszLabel = new TextBlock().LabelMedium();
        opszLabel.BindText(_viewModel, x => $"opsz (Optical Size) Axis: {x.PlaygroundOpticalSize:F0}dp");
        var opszSlider = new Slider { Minimum = 20, Maximum = 48, Value = _viewModel.PlaygroundOpticalSize };
        opszSlider.ValueChanged += (s, v) => _viewModel.PlaygroundOpticalSize = v;

        // 5. Display Size Slider (24 to 120dp)
        var sizeLabel = new TextBlock().LabelMedium();
        sizeLabel.BindText(_viewModel, x => $"Display Size: {x.PlaygroundSize:F0}dp");
        var sizeSlider = new Slider { Minimum = 24, Maximum = 120, Value = _viewModel.PlaygroundSize };
        sizeSlider.ValueChanged += (s, v) => _viewModel.PlaygroundSize = v;

        slidersStack.Children(
            fillLabel, fillSlider,
            weightLabel, weightSlider,
            gradeLabel, gradeSlider,
            opszLabel, opszSlider,
            sizeLabel, sizeSlider
        );

        grid.Add(leftPanel.Column(0));
        grid.Add(slidersStack.Column(1));

        return CreateSectionCard(
            "Live Interactive 4-Axes Playground",
            "Adjust the 4 variable font axes in real-time to observe instant SkiaSharp typeface morphing and scaling.",
            grid
        );
    }

    private Button CreatePickerButton(MaterialIconKind kind, Color color)
    {
        var icon = new Icon(kind, 20, isFilled: true, foreground: color)
            .HorizontalAlign(HorizontalAlignment.Center)
            .VerticalAlign(VerticalAlignment.Center);

        var btn = new Button
        {
            Content = icon,
            Variant = ButtonVariant.Outlined,
            Width = 38,
            Height = 36,
            CornerRadius = new CornerRadius(8)
        };
        btn.OnClick(() => _viewModel.SelectIconWithColor(kind, color));
        return btn;
    }

    #endregion

    #region Section 2: The 4 Axes Deep Dive

    private UIElement CreateAxesDeepDiveSection()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 16 };

        // --- Subsection A: Weight Spectrum (100 to 700) ---
        var weightHeader = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
            .Children(
                new TextBlock("1. Weight Axis (wght: 100 to 700)").TitleSmall(),
                new TextBlock("Seven standard stroke weight tiers ranging from ultra-thin (100) to bold (700):").Subtext()
            );

        var weightsRow = new Grid()
            .Columns(
                GridLength.Star, GridLength.Star, GridLength.Star,
                GridLength.Star, GridLength.Star, GridLength.Star, GridLength.Star)
            .ColumnSpacing(8);

        weightsRow.Add(CreateAxisTile(MaterialIconKind.Favorite, "100 Thin", weight: 100f).Column(0));
        weightsRow.Add(CreateAxisTile(MaterialIconKind.Favorite, "200 Extra", weight: 200f).Column(1));
        weightsRow.Add(CreateAxisTile(MaterialIconKind.Favorite, "300 Light", weight: 300f).Column(2));
        weightsRow.Add(CreateAxisTile(MaterialIconKind.Favorite, "400 Regular", weight: 400f).Column(3));
        weightsRow.Add(CreateAxisTile(MaterialIconKind.Favorite, "500 Medium", weight: 500f).Column(4));
        weightsRow.Add(CreateAxisTile(MaterialIconKind.Favorite, "600 Semi", weight: 600f).Column(5));
        weightsRow.Add(CreateAxisTile(MaterialIconKind.Favorite, "700 Bold", weight: 700f).Column(6));

        // --- Subsection B: Fill Axis (Outlined vs Filled) ---
        var fillHeader = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
            .Children(
                new TextBlock("2. Fill Axis (FILL: 0.0 Outlined vs 1.0 Filled)").TitleSmall(),
                new TextBlock("Switching between outlined and solid filled states across active states and selections:").Subtext()
            );

        var fillGrid = new Grid()
            .Columns(
                GridLength.Star, GridLength.Star, GridLength.Star,
                GridLength.Star, GridLength.Star, GridLength.Star)
            .ColumnSpacing(10);

        fillGrid.Add(CreateFillPairTile(MaterialIconKind.Favorite, "Heart", Color.FromHex("#E11D48")).Column(0));
        fillGrid.Add(CreateFillPairTile(MaterialIconKind.Star, "Star", Color.FromHex("#EAB308")).Column(1));
        fillGrid.Add(CreateFillPairTile(MaterialIconKind.Bookmark, "Bookmark", Color.FromHex("#3B82F6")).Column(2));
        fillGrid.Add(CreateFillPairTile(MaterialIconKind.Notifications, "Alert", Color.FromHex("#A855F7")).Column(3));
        fillGrid.Add(CreateFillPairTile(MaterialIconKind.ThumbUp, "Like", Color.FromHex("#10B981")).Column(4));
        fillGrid.Add(CreateFillPairTile(MaterialIconKind.Lightbulb, "Idea", Color.FromHex("#F97316")).Column(5));

        // --- Subsection C: Grade Axis (-25, 0, +200) & Optical Size (20, 24, 40, 48) ---
        var subCHeader = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
            .Children(
                new TextBlock("3 & 4. Grade (GRAD) and Optical Size (opsz) Axes").TitleSmall(),
                new TextBlock("Grade fine-tunes stroke thickness without affecting layout metrics. Optical size tunes letterform proportion for small vs large scales:").Subtext()
            );

        var subCGrid = new Grid()
            .Columns(GridLength.Star, GridLength.Star)
            .ColumnSpacing(16);

        // Grade comparison
        var gradePanel = new Card(CardVariant.Filled)
            .Padding(12)
            .CornerRadius(10)
            .Child(
                new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
                    .Children(
                        new TextBlock("Grade (GRAD): -25 to +200").TitleSmall(),
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, HorizontalAlignment = HorizontalAlignment.Center }
                            .Children(
                                CreateMiniTile(MaterialIconKind.Settings, "GRAD: -25", grade: -25f),
                                CreateMiniTile(MaterialIconKind.Settings, "GRAD: 0", grade: 0f),
                                CreateMiniTile(MaterialIconKind.Settings, "GRAD: +200", grade: 200f)
                            )
                    )
            ).Column(0);

        // Optical size comparison
        var opszPanel = new Card(CardVariant.Filled)
            .Padding(12)
            .CornerRadius(10)
            .Child(
                new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
                    .Children(
                        new TextBlock("Optical Size (opsz): 20dp to 48dp").TitleSmall(),
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center }
                            .Children(
                                CreateMiniTile(MaterialIconKind.Search, "20dp", opsz: 20f, size: 24f),
                                CreateMiniTile(MaterialIconKind.Search, "24dp", opsz: 24f, size: 28f),
                                CreateMiniTile(MaterialIconKind.Search, "40dp", opsz: 40f, size: 34f),
                                CreateMiniTile(MaterialIconKind.Search, "48dp", opsz: 48f, size: 40f)
                            )
                    )
            ).Column(1);

        subCGrid.Add(gradePanel);
        subCGrid.Add(opszPanel);

        stack.Children(
            weightHeader, weightsRow,
            fillHeader, fillGrid,
            subCHeader, subCGrid
        );

        return CreateSectionCard(
            "Material Symbols 4 Axes Deep Dive",
            "Detailed visual matrix showcasing how each variable font axis independently affects glyph appearance.",
            stack
        );
    }

    private static UIElement CreateAxisTile(MaterialIconKind kind, string label, float weight = 400f, float fill = 0f, float grade = 0f, float opsz = 24f)
    {
        var icon = new Icon(kind, 28)
            .Weight(weight)
            .Fill(fill)
            .Grade(grade)
            .OpticalSize(opsz)
            .HorizontalAlign(HorizontalAlignment.Center);

        var text = new TextBlock(label)
            .LabelSmall()
            .HorizontalAlign(HorizontalAlignment.Center);

        return new Card(CardVariant.Filled)
            .Padding(6, 10)
            .CornerRadius(8)
            .Child(
                new StackPanel { Orientation = Orientation.Vertical, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center }
                    .Children(icon, text)
            );
    }

    private static UIElement CreateFillPairTile(MaterialIconKind kind, string name, Color color)
    {
        var outlined = new Icon(kind, 24, isFilled: false, color)
            .VerticalAlign(VerticalAlignment.Center);

        var filled = new Icon(kind, 24, isFilled: true, color)
            .VerticalAlign(VerticalAlignment.Center);

        var label = new TextBlock(name)
            .LabelSmall()
            .HorizontalAlign(HorizontalAlignment.Center);

        return new Card(CardVariant.Filled)
            .Padding(10, 8)
            .CornerRadius(8)
            .Child(
                new StackPanel { Orientation = Orientation.Vertical, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center }
                    .Children(
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center }
                            .Children(outlined, filled),
                        label
                    )
            );
    }

    private static UIElement CreateMiniTile(MaterialIconKind kind, string label, float grade = 0f, float opsz = 24f, float size = 28f)
    {
        var icon = new Icon(kind, size)
            .Grade(grade)
            .OpticalSize(opsz)
            .HorizontalAlign(HorizontalAlignment.Center);

        var text = new TextBlock(label)
            .Caption()
            .HorizontalAlign(HorizontalAlignment.Center);

        return new StackPanel { Orientation = Orientation.Vertical, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center }
            .Children(icon, text);
    }

    #endregion

    #region Section 3: Custom Vector Geometry (SKPath & SVG Path Data)

    private UIElement CreateCustomVectorSection()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // 1. Build custom SKPath shield
        using var shieldBuilder = new SKPathBuilder();
        shieldBuilder.MoveTo(12, 2);
        shieldBuilder.LineTo(22, 6);
        shieldBuilder.LineTo(22, 12);
        shieldBuilder.CubicTo(22, 18, 17, 22, 12, 24);
        shieldBuilder.CubicTo(7, 22, 2, 18, 2, 12);
        shieldBuilder.LineTo(2, 6);
        shieldBuilder.Close();
        var customShield = shieldBuilder.Detach();

        // 2. Standard SVG paths
        string svgHeart = "M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z";
        string svgSparkle = "M12 2L9.19 8.63L2 12l7.19 3.37L12 22l2.81-6.63L22 12l-7.19-3.37L12 2z";
        string svgDiamond = "M12 2L2 9l10 13 10-13-10-7zm0 3.5L18.5 9 12 18.5 5.5 9 12 5.5z";
        string svgBookmark = "M17 3H7c-1.1 0-1.99.9-1.99 2L5 21l7-3 7 3V5c0-1.1-.9-2-2-2z";
        string svgRocket = "M12 2.5s-4 4-4 9c0 2.5 1 4.5 2 5.5v3l2-1 2 1v-3c1-1 2-3 2-5.5 0-5-4-9-4-9z";

        var vectorGrid = new Grid()
            .Columns(
                GridLength.Star, GridLength.Star, GridLength.Star,
                GridLength.Star, GridLength.Star, GridLength.Star)
            .ColumnSpacing(10);

        vectorGrid.Add(CreateCustomTile(new Icon(customShield, 28, Color.FromHex("#3B82F6")), "SKPath Fill").Column(0));
        vectorGrid.Add(CreateCustomTile(new Icon(customShield, 28, Color.FromHex("#3B82F6")) { StrokeWidth = 1.8f }, "SKPath Stroke").Column(1));
        vectorGrid.Add(CreateCustomTile(new Icon(svgHeart, 28, Color.FromHex("#E11D48")), "SVG Heart").Column(2));
        vectorGrid.Add(CreateCustomTile(new Icon(svgSparkle, 28, Color.FromHex("#EAB308")), "SVG Sparkle").Column(3));
        vectorGrid.Add(CreateCustomTile(new Icon(svgDiamond, 28, Color.FromHex("#06B6D4")), "SVG Diamond").Column(4));
        vectorGrid.Add(CreateCustomTile(new Icon(svgBookmark, 28, Color.FromHex("#10B981")) { StrokeWidth = 1.8f }, "SVG Stroke").Column(5));

        // Interactive StrokeWidth slider for vector geometry
        var strokeSliderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, VerticalAlignment = VerticalAlignment.Center };
        var strokeLabel = new TextBlock("Adjust StrokeWidth (0 = Solid Fill, >0 = Stroked Outline):").LabelMedium();
        var strokeSlider = new Slider { Minimum = 0, Maximum = 40, Value = 18, Width = 160 };
        var strokeValText = new TextBlock("1.8dp").Subtext();

        var dynamicRocket = new Icon(svgRocket, 32, Color.FromHex("#F97316")) { StrokeWidth = 1.8f };
        var dynamicDiamond = new Icon(svgDiamond, 32, Color.FromHex("#8B5CF6")) { StrokeWidth = 1.8f };

        strokeSlider.ValueChanged += (s, v) =>
        {
            float sw = v / 10f;
            strokeValText.Text = sw == 0 ? "0.0dp (Fill)" : $"{sw:F1}dp";
            dynamicRocket.StrokeWidth = sw;
            dynamicDiamond.StrokeWidth = sw;
        };

        strokeSliderRow.Children(strokeLabel, strokeSlider, strokeValText, dynamicRocket, dynamicDiamond);

        stack.Children(vectorGrid, strokeSliderRow);

        return CreateSectionCard(
            "Custom Vector Geometry (SKPath & SVG Path Data)",
            "In addition to variable font glyphs, the Icon control natively supports custom vector shapes with automatic scaling, aspect ratio preservation, fill, and stroked outlines.",
            stack
        );
    }

    private static UIElement CreateCustomTile(Icon icon, string label)
    {
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        var text = new TextBlock(label).LabelSmall().HorizontalAlign(HorizontalAlignment.Center);

        return new Card(CardVariant.Filled)
            .Padding(8, 10)
            .CornerRadius(8)
            .Child(
                new StackPanel { Orientation = Orientation.Vertical, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center }
                    .Children(icon, text)
            );
    }

    #endregion

    #region Section 4: Searchable Material Symbols Catalog

    private UIElement CreateSearchCatalogSection()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(20)
            .CornerRadius(12);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // Header title & description
        stack.Add(new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
            .Children(
                new TextBlock("Material Symbols Catalog (2,130+ Icons)").TitleMedium(),
                new TextBlock("Search the complete Google Material Symbols catalog in real-time. Click any icon to load and customize it in the playground above.")
                    .Subtext()
            )
        );

        // Search bar row (TextBox + Clear button)
        var searchBox = new TextBox()
            .LeadingIcon(MaterialIconKind.Search)
            .Placeholder("Search 2,100+ icons (e.g. arrow, favorite, heart, user, cloud)...")
            .BindText(_viewModel, vm => vm.SearchText, (vm, v) => vm.SearchText = v);

        var clearBtn = new Button("Clear") { VerticalAlignment = VerticalAlignment.Center }
            .Variant(ButtonVariant.Outlined)
            .Command(_viewModel.ClearSearchCommand);

        var searchGrid = new Grid()
            .Columns(GridLength.Star, GridLength.Auto)
            .ColumnSpacing(10);
        searchGrid.Add(searchBox.Column(0));
        searchGrid.Add(clearBtn.Column(1));

        stack.Add(searchGrid);

        // Search Status readout
        _statusText = new TextBlock(_viewModel.SearchStatus)
            .Subtext();
        stack.Add(_statusText);

        // WrapPanel containing icon tiles
        _catalogWrapPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalSpacing = 8,
            VerticalSpacing = 8
        };
        stack.Add(_catalogWrapPanel);

        // Footer buttons (Show More / Show All)
        var showMoreBtn = new Button("Show More (+120)")
            .Variant(ButtonVariant.Filled)
            .Command(_viewModel.LoadMoreCommand);

        var showAllBtn = new Button("Show All")
            .Variant(ButtonVariant.Outlined)
            .Command(_viewModel.ShowAllCommand);

        _footerRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = _viewModel.HasMoreIcons ? Visibility.Visible : Visibility.Collapsed
        }.Children(showMoreBtn, showAllBtn);

        stack.Add(_footerRow);

        // Populate initial tiles
        RebuildCatalogTiles();

        card.Child = stack;
        return card;
    }

    private void RebuildCatalogTiles()
    {
        if (_catalogWrapPanel == null) return;

        _catalogWrapPanel.Clear();
        _catalogTiles.Clear();

        var filtered = _viewModel.FilteredIcons;
        for (int i = 0; i < filtered.Count; i++)
        {
            var item = filtered[i];
            var tile = CreateDynamicCatalogTile(item);
            _catalogTiles.Add((item.Kind, tile));
            _catalogWrapPanel.Add(tile);
        }

        if (_statusText != null)
        {
            _statusText.Text = _viewModel.SearchStatus;
        }

        if (_footerRow != null)
        {
            _footerRow.Visibility = _viewModel.HasMoreIcons ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateCatalogTileSelection()
    {
        var currentKind = _viewModel.PlaygroundKind;
        for (int i = 0; i < _catalogTiles.Count; i++)
        {
            var (kind, card) = _catalogTiles[i];
            bool isSelected = kind == currentKind;
            card.BorderThickness = isSelected ? new Thickness(2) : new Thickness(0);
            card.BorderBrush = isSelected ? Color.FromHex("#3B82F6") : Color.Transparent;
        }
    }

    private Card CreateDynamicCatalogTile(IconItemInfo item)
    {
        bool isSelected = item.Kind == _viewModel.PlaygroundKind;

        var icon = new Icon(item.Kind, 26, isFilled: false, foreground: item.Color)
            .HorizontalAlign(HorizontalAlignment.Center);

        var label = new TextBlock(item.DisplayName)
            .Caption()
            .HorizontalAlign(HorizontalAlignment.Center)
            .TextAlignment(TextAlignment.Center)
            .TextWrapping(TextWrapping.Wrap);

        var card = new Card(CardVariant.Filled)
            .Padding(6, 8)
            .CornerRadius(8);

        card.Width = 104;
        card.Height = 76;
        card.BorderThickness = isSelected ? new Thickness(2) : new Thickness(0);
        card.BorderBrush = isSelected ? Color.FromHex("#3B82F6") : Color.Transparent;

        card.Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center }
            .Children(icon, label);

        card.PointerPressed += (s, e) =>
        {
            _viewModel.SelectIconWithColor(item.Kind, item.Color);
        };

        return card;
    }

    #endregion

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
