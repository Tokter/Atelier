using System;
using System.Collections.Generic;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Gallery.Views;

public class LayoutView : Grid
{
    private readonly LayoutViewModel _viewModel;

    // Filter Tab Buttons
    private readonly Dictionary<LayoutTab, Button> _tabButtons = new();

    // Section Cards
    private UIElement? _stackCard;
    private UIElement? _dockCard;
    private UIElement? _gridCard;
    private UIElement? _wrapCard;
    private UIElement? _canvasCard;
    private UIElement? _borderCard;

    // Dynamic Viewports
    private StackPanel? _liveStackPanel;
    private TextBlock? _stackInfoText;

    private DockPanel? _liveDockPanel;
    private Border? _dockRightPanel;
    private Button? _btnLastChildFill;
    private Button? _btnToggleRightDock;
    private TextBlock? _dockInfoText;

    private Border? _gridContainer;
    private Button? _btnGridUniform;
    private Button? _btnGridAppShell;
    private Button? _btnGridSpanning;
    private TextBlock? _gridSpacingLabel;

    private Border? _wrapWrapper;
    private WrapPanel? _liveWrapPanel;
    private TextBlock? _wrapWidthLabel;
    private Button? _btnWrapOrientation;
    private Button? _btnWrapUniform;

    private Canvas? _liveCanvas;
    private Border? _movableCanvasItem;
    private TextBlock? _canvasCoordText;

    private Border? _liveBorder;
    private TextBlock? _borderMetricsText;

    public LayoutView() : this(new LayoutViewModel())
    {
    }

    public LayoutView(LayoutViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        this.Rows(GridLength.Auto, GridLength.Star);
        this.RowSpacing(16);

        // 1. Master Controls Banner
        this.Add(CreateMasterBanner().Row(0));

        // 2. Scrollable Showcase Content
        var scrollViewer = new ScrollViewer();
        var contentStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 16
        };

        _stackCard = CreateStackPanelCard();
        _dockCard = CreateDockPanelCard();
        _gridCard = CreateGridCard();
        _wrapCard = CreateWrapPanelCard();
        _canvasCard = CreateCanvasCard();
        _borderCard = CreateBorderCard();

        contentStack.Add(_stackCard);
        contentStack.Add(_dockCard);
        contentStack.Add(_gridCard);
        contentStack.Add(_wrapCard);
        contentStack.Add(_canvasCard);
        contentStack.Add(_borderCard);

        scrollViewer.Content = contentStack;
        this.Add(scrollViewer.Row(1));

        // 3. Hook refresh requests
        _viewModel.RequestLayoutRefresh += () =>
        {
            UpdateTabVisibility();
            RefreshLiveViewports();
        };

        UpdateTabVisibility();
        RefreshLiveViewports();
    }

    private UIElement CreateMasterBanner()
    {
        var card = new Card(CardVariant.Filled)
            .Padding(20)
            .CornerRadius(14);

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Dashboard, 26, foreground: Color.FromHex("#1E88E5")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Layout Systems & Panels").TitleLarge().Bold().VerticalAlign(VerticalAlignment.Center),
                new Border
                {
                    Background = Color.FromHex("#1E88E5").WithAlpha(0.12f),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 3),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock("Core Layout Architecture")
                    {
                        FontSize = 11,
                        Bold = true,
                        Foreground = Color.FromHex("#1E88E5")
                    }
                }
            );

        var descText = new TextBlock("Explore Atelier's suite of high-performance layout containers: StackPanel for linear flow, DockPanel for edge pinning, Grid for multi-cell proportional alignment, WrapPanel for responsive flow, Canvas for absolute coordinates, and Border for decorated containers.")
            .BodyMedium()
            .Foreground(Color.FromHex("#757575"));

        // Tab Selector Chips
        var tabsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };

        void AddTabBtn(LayoutTab tab, string label, MaterialIconKind icon)
        {
            var btn = new Button
            {
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(icon, 16) { VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock(label) { FontSize = 12, VerticalAlignment = VerticalAlignment.Center }
                    ),
                Padding = new Thickness(12, 6),
                CornerRadius = new CornerRadius(16),
                Variant = _viewModel.ActiveTab == tab ? ButtonVariant.Filled : ButtonVariant.Outlined
            };
            btn.Click += (s, e) =>
            {
                _viewModel.SetTab(tab);
                UpdateTabButtonVisuals();
            };
            _tabButtons[tab] = btn;
            tabsRow.Add(btn);
        }

        AddTabBtn(LayoutTab.All, "All Panels", MaterialIconKind.ViewQuilt);
        AddTabBtn(LayoutTab.StackPanel, "StackPanel", MaterialIconKind.ViewStream);
        AddTabBtn(LayoutTab.DockPanel, "DockPanel", MaterialIconKind.Dock);
        AddTabBtn(LayoutTab.Grid, "Grid", MaterialIconKind.GridView);
        AddTabBtn(LayoutTab.WrapPanel, "WrapPanel", MaterialIconKind.WrapText);
        AddTabBtn(LayoutTab.Canvas, "Canvas", MaterialIconKind.Brush);
        AddTabBtn(LayoutTab.Border, "Border & Card", MaterialIconKind.CropFree);

        return card.Child(new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 }
            .Children(titleStack, descText, tabsRow));
    }

    private void UpdateTabButtonVisuals()
    {
        foreach (var kvp in _tabButtons)
        {
            kvp.Value.Variant = _viewModel.ActiveTab == kvp.Key ? ButtonVariant.Filled : ButtonVariant.Outlined;
        }
    }

    private void UpdateTabVisibility()
    {
        UpdateTabButtonVisuals();

        void SetVis(UIElement? elem, bool visible)
        {
            if (elem != null)
            {
                elem.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        bool showAll = _viewModel.ActiveTab == LayoutTab.All;
        SetVis(_stackCard, showAll || _viewModel.ActiveTab == LayoutTab.StackPanel);
        SetVis(_dockCard, showAll || _viewModel.ActiveTab == LayoutTab.DockPanel);
        SetVis(_gridCard, showAll || _viewModel.ActiveTab == LayoutTab.Grid);
        SetVis(_wrapCard, showAll || _viewModel.ActiveTab == LayoutTab.WrapPanel);
        SetVis(_canvasCard, showAll || _viewModel.ActiveTab == LayoutTab.Canvas);
        SetVis(_borderCard, showAll || _viewModel.ActiveTab == LayoutTab.Border);
    }

    #region 1. StackPanel Card

    private UIElement CreateStackPanelCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.ViewStream, 20, foreground: Color.FromHex("#1E88E5")),
                new TextBlock("StackPanel — Linear Flow & Spacing").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                new Border
                {
                    Background = Color.FromHex("#1E88E5").WithAlpha(0.12f),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(6, 2),
                    Child = new TextBlock("Orientation • Spacing") { FontSize = 10, Bold = true, Foreground = Color.FromHex("#1E88E5") }
                }
            );

        var desc = new TextBlock("Arranges child elements sequentially in a single line, vertically or horizontally. Supports configurable inter-element spacing and flexible alignment.") { TextWrapping = TextWrapping.Wrap }
            .FontSize(12).Foreground(Color.FromHex("#757575"));

        // Controls
        var btnOrientation = new Button("Toggle Orientation")
        {
            Variant = ButtonVariant.Tonal,
            Padding = new Thickness(10, 5),
            CornerRadius = new CornerRadius(14),
            Command = _viewModel.ToggleStackOrientationCommand,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var btnAdd = new Button("+ Add Item")
        {
            Variant = ButtonVariant.Outlined,
            Padding = new Thickness(10, 5),
            CornerRadius = new CornerRadius(14),
            Command = _viewModel.AddStackItemCommand,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var btnRemove = new Button("- Remove Item")
        {
            Variant = ButtonVariant.Outlined,
            Padding = new Thickness(10, 5),
            CornerRadius = new CornerRadius(14),
            Command = _viewModel.RemoveStackItemCommand,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var spacingSlider = new Slider { Minimum = 0, Maximum = 32, Value = _viewModel.StackSpacing, Width = 130, VerticalAlignment = VerticalAlignment.Center };
        var spacingLabel = new TextBlock($"Spacing: {_viewModel.StackSpacing:F0}px") { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        spacingSlider.ValueChanged += (s, v) =>
        {
            _viewModel.StackSpacing = v;
            spacingLabel.Text = $"Spacing: {v:F0}px";
            RefreshStackPanel();
        };

        var controlsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(btnOrientation, btnAdd, btnRemove, spacingSlider, spacingLabel);

        // Viewport
        _liveStackPanel = new StackPanel
        {
            Orientation = _viewModel.StackOrientation,
            Spacing = _viewModel.StackSpacing,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        var viewport = new Border
        {
            
            Background = Color.FromHex("#000000").WithAlpha(0.03f),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.08f),
            Padding = new Thickness(16),
            Child = _liveStackPanel
        };

        _stackInfoText = new TextBlock()
            .FontSize(11)
            .Foreground(Color.FromHex("#6B7280"));

        RefreshStackPanel();

        return card.Child(new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(header, desc, controlsRow, viewport, _stackInfoText));
    }

    private void RefreshStackPanel()
    {
        if (_liveStackPanel == null) return;

        _liveStackPanel.Orientation = _viewModel.StackOrientation;
        _liveStackPanel.Spacing = _viewModel.StackSpacing;
        _liveStackPanel.Clear();

        Color[] colors =
        [
            Color.FromHex("#1E88E5"),
            Color.FromHex("#10B981"),
            Color.FromHex("#F59E0B"),
            Color.FromHex("#8B5CF6"),
            Color.FromHex("#EC4899"),
            Color.FromHex("#06B6D4"),
            Color.FromHex("#6366F1"),
            Color.FromHex("#14B8A6")
        ];

        for (int i = 0; i < _viewModel.StackItemCount; i++)
        {
            Color c = colors[i % colors.Length];
            var item = new Border
            {
                Background = c.WithAlpha(0.15f),
                BorderThickness = new Thickness(1.5f),
                BorderBrush = c,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(MaterialIconKind.Layers, 16, foreground: c),
                        new TextBlock($"Item {i + 1}") { FontSize = 12, Bold = true, Foreground = c }
                    )
            };
            _liveStackPanel.Add(item);
        }

        if (_stackInfoText != null)
        {
            _stackInfoText.Text = $"StackPanel: Orientation={_viewModel.StackOrientation}, Spacing={_viewModel.StackSpacing:F0}px, Children={_viewModel.StackItemCount}";
        }
    }

    #endregion

    #region 2. DockPanel Card

    private UIElement CreateDockPanelCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Dock, 20, foreground: Color.FromHex("#10B981")),
                new TextBlock("DockPanel — Edge Pinning & LastChildFill").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                new Border
                {
                    Background = Color.FromHex("#10B981").WithAlpha(0.12f),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(6, 2),
                    Child = new TextBlock("Dock.Left • Top • Right • Bottom") { FontSize = 10, Bold = true, Foreground = Color.FromHex("#059669") }
                }
            );

        var desc = new TextBlock("Arranges child elements along outer container edges (Left, Top, Right, Bottom). The final child can automatically expand to fill remaining interior workspace when LastChildFill is true.") { TextWrapping = TextWrapping.Wrap }
            .FontSize(12).Foreground(Color.FromHex("#757575"));

        // Controls
        _btnLastChildFill = new Button("LastChildFill: True")
        {
            Variant = ButtonVariant.Filled,
            Padding = new Thickness(10, 5),
            CornerRadius = new CornerRadius(14),
            Command = _viewModel.ToggleLastChildFillCommand
        };

        _btnToggleRightDock = new Button("Right Panel: Visible")
        {
            Variant = ButtonVariant.Tonal,
            Padding = new Thickness(10, 5),
            CornerRadius = new CornerRadius(14),
            Command = _viewModel.ToggleRightDockCommand
        };

        var controlsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(_btnLastChildFill, _btnToggleRightDock);

        // Simulated Window Viewport with DockPanel
        _liveDockPanel = new DockPanel { LastChildFill = _viewModel.LastChildFill };

        // 1. Top Bar
        var topBar = new Border
        {
            Height = 36,
            Background = Color.FromHex("#1E88E5").WithAlpha(0.15f),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Color.FromHex("#1E88E5").WithAlpha(0.4f),
            Padding = new Thickness(10, 4),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.Window, 16, foreground: Color.FromHex("#1E88E5")),
                    new TextBlock("Top Ribbon / MenuBar (Dock.Top)") { FontSize = 11, Bold = true, Foreground = Color.FromHex("#1E88E5") }
                )
        };
        DockPanel.SetDock(topBar, Dock.Top);

        // 2. Bottom Status Bar
        var bottomBar = new Border
        {
            Height = 28,
            Background = Color.FromHex("#4B5563").WithAlpha(0.12f),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = Color.FromHex("#4B5563").WithAlpha(0.3f),
            Padding = new Thickness(10, 4),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.CheckCircle, 14, foreground: Color.FromHex("#059669")),
                    new TextBlock("Status Bar (Dock.Bottom) • Ready • Line 1, Col 1") { FontSize = 11, Foreground = Color.FromHex("#4B5563") }
                )
        };
        DockPanel.SetDock(bottomBar, Dock.Bottom);

        // 3. Left Explorer Sidebar
        var leftSidebar = new Border
        {
            Width = 140,
            Background = Color.FromHex("#059669").WithAlpha(0.12f),
            BorderThickness = new Thickness(0, 0, 1, 0),
            BorderBrush = Color.FromHex("#059669").WithAlpha(0.3f),
            Padding = new Thickness(10),
            Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 }
                .Children(
                    new TextBlock("Sidebar (Dock.Left)").Bold().FontSize(11).Foreground(Color.FromHex("#059669")),
                    new TextBlock("📁 Project Root").FontSize(10),
                    new TextBlock("📄 MainWindow.cs").FontSize(10),
                    new TextBlock("📄 Styles.xaml").FontSize(10)
                )
        };
        DockPanel.SetDock(leftSidebar, Dock.Left);

        // 4. Right Inspector Flyout
        _dockRightPanel = new Border
        {
            Width = 130,
            Background = Color.FromHex("#8B5CF6").WithAlpha(0.12f),
            BorderThickness = new Thickness(1, 0, 0, 0),
            BorderBrush = Color.FromHex("#8B5CF6").WithAlpha(0.3f),
            Padding = new Thickness(10),
            Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 }
                .Children(
                    new TextBlock("Inspector (Dock.Right)").Bold().FontSize(11).Foreground(Color.FromHex("#7C3AED")),
                    new TextBlock("Width: 100%").FontSize(10),
                    new TextBlock("Height: Auto").FontSize(10),
                    new TextBlock("Opacity: 1.0").FontSize(10)
                )
        };
        DockPanel.SetDock(_dockRightPanel, Dock.Right);

        // 5. Center Work Area (Last Child Fill)
        var centerWorkArea = new Border
        {
            Background = Color.FromHex("#F59E0B").WithAlpha(0.10f),
            Padding = new Thickness(16),
            Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.AspectRatio, 28, foreground: Color.FromHex("#D97706")),
                    new TextBlock("Center Document Workspace").Bold().FontSize(13).Foreground(Color.FromHex("#B45309")),
                    new TextBlock("Expands dynamically to fill remaining area (LastChildFill)")
                    {
                        FontSize = 11,
                        Foreground = Color.FromHex("#757575"),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                )
        };

        _liveDockPanel.Add(topBar);
        _liveDockPanel.Add(bottomBar);
        _liveDockPanel.Add(leftSidebar);
        _liveDockPanel.Add(_dockRightPanel);
        _liveDockPanel.Add(centerWorkArea);

        var viewport = new Border
        {
            Height = 220,
            Background = Color.FromHex("#000000").WithAlpha(0.03f),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.08f),
            Child = _liveDockPanel
        };

        _dockInfoText = new TextBlock()
            .FontSize(11)
            .Foreground(Color.FromHex("#6B7280"));

        RefreshDockPanel();

        return card.Child(new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(header, desc, controlsRow, viewport, _dockInfoText));
    }

    private void RefreshDockPanel()
    {
        if (_liveDockPanel == null) return;

        _liveDockPanel.LastChildFill = _viewModel.LastChildFill;

        if (_btnLastChildFill != null)
        {
            _btnLastChildFill.Variant = _viewModel.LastChildFill ? ButtonVariant.Filled : ButtonVariant.Outlined;
            _btnLastChildFill.Content = new TextBlock($"LastChildFill: {_viewModel.LastChildFill}");
        }

        if (_dockRightPanel != null)
        {
            _dockRightPanel.Visibility = _viewModel.ShowRightDock ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_btnToggleRightDock != null)
        {
            _btnToggleRightDock.Variant = _viewModel.ShowRightDock ? ButtonVariant.Tonal : ButtonVariant.Outlined;
            _btnToggleRightDock.Content = new TextBlock($"Right Panel: {(_viewModel.ShowRightDock ? "Visible" : "Hidden")}");
        }

        if (_dockInfoText != null)
        {
            _dockInfoText.Text = $"DockPanel: LastChildFill={_viewModel.LastChildFill}, RightDock={(_viewModel.ShowRightDock ? "Visible" : "Collapsed")}";
        }
    }

    #endregion

    #region 3. Grid Card

    private UIElement CreateGridCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.GridView, 20, foreground: Color.FromHex("#8B5CF6")),
                new TextBlock("Grid — Multi-Cell Proportional Matrix").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                new Border
                {
                    Background = Color.FromHex("#8B5CF6").WithAlpha(0.12f),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(6, 2),
                    Child = new TextBlock("Star (*) • Auto • Pixels • Spanning") { FontSize = 10, Bold = true, Foreground = Color.FromHex("#7C3AED") }
                }
            );

        var desc = new TextBlock("Defines a flexible matrix of rows and columns. Cells support Star (*) proportional weights, Auto content sizing, and absolute pixel dimensions, along with RowSpan and ColumnSpan.") { TextWrapping = TextWrapping.Wrap }
            .FontSize(12).Foreground(Color.FromHex("#757575"));

        // Preset buttons
        _btnGridUniform = new Button("3×3 Uniform Star (1*:1*:1*)")
        {
            Padding = new Thickness(10, 5),
            CornerRadius = new CornerRadius(14),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _btnGridUniform.Click += (s, e) => _viewModel.SetGridPreset(GridPreset.Uniform3x3);

        _btnGridAppShell = new Button("App Shell (Auto / Star)")
        {
            Padding = new Thickness(10, 5),
            CornerRadius = new CornerRadius(14),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _btnGridAppShell.Click += (s, e) => _viewModel.SetGridPreset(GridPreset.AppShell);

        _btnGridSpanning = new Button("Spanning Matrix (RowSpan / ColSpan)")
        {
            Padding = new Thickness(10, 5),
            CornerRadius = new CornerRadius(14),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _btnGridSpanning.Click += (s, e) => _viewModel.SetGridPreset(GridPreset.SpanningMatrix);

        var rowSpacingSlider = new Slider { Minimum = 0, Maximum = 20, Value = _viewModel.GridRowSpacing, Width = 110, VerticalAlignment = VerticalAlignment.Center };
        rowSpacingSlider.ValueChanged += (s, v) =>
        {
            _viewModel.GridRowSpacing = v;
            _viewModel.GridColumnSpacing = v;
            UpdateGridSpacingLabel();
            RefreshGrid();
        };

        _gridSpacingLabel = new TextBlock($"Spacing: {_viewModel.GridRowSpacing:F0}px") { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

        var presetsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(_btnGridUniform, _btnGridAppShell, _btnGridSpanning, rowSpacingSlider, _gridSpacingLabel);

        // Viewport
        _gridContainer = new Border
        {
            Height = 220,
            Background = Color.FromHex("#000000").WithAlpha(0.03f),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.08f),
            Padding = new Thickness(12)
        };

        RefreshGrid();

        return card.Child(new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(header, desc, presetsRow, _gridContainer));
    }

    private void UpdateGridSpacingLabel()
    {
        if (_gridSpacingLabel != null)
        {
            _gridSpacingLabel.Text = $"Spacing: {_viewModel.GridRowSpacing:F0}px";
        }
    }

    private void RefreshGrid()
    {
        if (_gridContainer == null) return;

        if (_btnGridUniform != null) _btnGridUniform.Variant = _viewModel.SelectedGridPreset == GridPreset.Uniform3x3 ? ButtonVariant.Filled : ButtonVariant.Outlined;
        if (_btnGridAppShell != null) _btnGridAppShell.Variant = _viewModel.SelectedGridPreset == GridPreset.AppShell ? ButtonVariant.Filled : ButtonVariant.Outlined;
        if (_btnGridSpanning != null) _btnGridSpanning.Variant = _viewModel.SelectedGridPreset == GridPreset.SpanningMatrix ? ButtonVariant.Filled : ButtonVariant.Outlined;

        static Border MakeCell(string label, string desc, Color color)
        {
            return new Border
            {
                Background = color.WithAlpha(0.15f),
                BorderThickness = new Thickness(1.5f),
                BorderBrush = color,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6),
                Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new TextBlock(label) { FontSize = 11, Bold = true, Foreground = color, HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock(desc) { FontSize = 10, Foreground = color.WithAlpha(0.8f), HorizontalAlignment = HorizontalAlignment.Center }
                    )
            };
        }

        var grid = new Grid()
            .Spacing(_viewModel.GridRowSpacing, _viewModel.GridColumnSpacing);

        switch (_viewModel.SelectedGridPreset)
        {
            case GridPreset.Uniform3x3:
                grid.Rows(GridLength.Star, GridLength.Star, GridLength.Star);
                grid.Columns(GridLength.Star, GridLength.Star, GridLength.Star);

                Color[] palette = [Color.FromHex("#1E88E5"), Color.FromHex("#10B981"), Color.FromHex("#F59E0B"), Color.FromHex("#8B5CF6")];
                for (int r = 0; r < 3; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        var cell = MakeCell($"R{r}, C{c}", "1* × 1*", palette[(r + c) % palette.Length]);
                        Grid.SetRow(cell, r);
                        Grid.SetColumn(cell, c);
                        grid.Add(cell);
                    }
                }
                break;

            case GridPreset.AppShell:
                // Header (Auto), Main (Star), Footer (32px)
                grid.Rows(GridLength.Auto, GridLength.Star, GridLength.Pixels(30));
                // Nav (130px), Content (Star)
                grid.Columns(GridLength.Pixels(130), GridLength.Star);

                var headerCell = MakeCell("Header Bar", "Row 0 (Auto), ColSpan 2", Color.FromHex("#1E88E5"));
                Grid.SetRow(headerCell, 0);
                Grid.SetColumn(headerCell, 0);
                Grid.SetColumnSpan(headerCell, 2);
                grid.Add(headerCell);

                var navCell = MakeCell("Nav Sidebar", "R1, C0 (130px)", Color.FromHex("#059669"));
                Grid.SetRow(navCell, 1);
                Grid.SetColumn(navCell, 0);
                grid.Add(navCell);

                var contentCell = MakeCell("Main Content Area", "R1, C1 (Star)", Color.FromHex("#8B5CF6"));
                Grid.SetRow(contentCell, 1);
                Grid.SetColumn(contentCell, 1);
                grid.Add(contentCell);

                var footerCell = MakeCell("Footer / Status", "Row 2 (30px), ColSpan 2", Color.FromHex("#6B7280"));
                Grid.SetRow(footerCell, 2);
                Grid.SetColumn(footerCell, 0);
                Grid.SetColumnSpan(footerCell, 2);
                grid.Add(footerCell);
                break;

            case GridPreset.SpanningMatrix:
                grid.Rows(GridLength.Star, GridLength.Star, GridLength.Star);
                grid.Columns(GridLength.Star, GridLength.Star, GridLength.Star);

                // Big cell spanning R0-1, C0-1
                var bigCell = MakeCell("Feature Hero", "RowSpan 2, ColSpan 2", Color.FromHex("#EC4899"));
                Grid.SetRow(bigCell, 0);
                Grid.SetColumn(bigCell, 0);
                Grid.SetRowSpan(bigCell, 2);
                Grid.SetColumnSpan(bigCell, 2);
                grid.Add(bigCell);

                var c2r0 = MakeCell("Card A", "R0, C2", Color.FromHex("#1E88E5"));
                Grid.SetRow(c2r0, 0);
                Grid.SetColumn(c2r0, 2);
                grid.Add(c2r0);

                var c2r1 = MakeCell("Card B", "R1, C2", Color.FromHex("#10B981"));
                Grid.SetRow(c2r1, 1);
                Grid.SetColumn(c2r1, 2);
                grid.Add(c2r1);

                var bot0 = MakeCell("Footer 1", "R2, C0", Color.FromHex("#F59E0B"));
                Grid.SetRow(bot0, 2);
                Grid.SetColumn(bot0, 0);
                grid.Add(bot0);

                var bot1 = MakeCell("Footer 2 & 3", "R2, ColSpan 2", Color.FromHex("#8B5CF6"));
                Grid.SetRow(bot1, 2);
                Grid.SetColumn(bot1, 1);
                Grid.SetColumnSpan(bot1, 2);
                grid.Add(bot1);
                break;
        }

        _gridContainer.Child = grid;
    }

    #endregion

    #region 4. WrapPanel Card

    private UIElement CreateWrapPanelCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.WrapText, 20, foreground: Color.FromHex("#F59E0B")),
                new TextBlock("WrapPanel — Responsive Flow & Dynamic Wrapping").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                new Border
                {
                    Background = Color.FromHex("#F59E0B").WithAlpha(0.12f),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(6, 2),
                    Child = new TextBlock("Dynamic Line Breaking • Tags & Chips") { FontSize = 10, Bold = true, Foreground = Color.FromHex("#D97706") }
                }
            );

        var desc = new TextBlock("Positions child elements sequentially from left to right, automatically breaking content to the next line at the edge of the containing box. Drag the width slider to watch chips wrap!") { TextWrapping = TextWrapping.Wrap }
            .FontSize(12).Foreground(Color.FromHex("#757575"));

        // Controls
        _btnWrapOrientation = new Button("Orientation: Horizontal")
        {
            Variant = ButtonVariant.Tonal,
            Padding = new Thickness(10, 5),
            CornerRadius = new CornerRadius(14),
            Command = _viewModel.ToggleWrapOrientationCommand,
            VerticalAlignment = VerticalAlignment.Center
        };

        _btnWrapUniform = new Button("Item Size: Auto")
        {
            Variant = ButtonVariant.Outlined,
            Padding = new Thickness(10, 5),
            CornerRadius = new CornerRadius(14),
            Command = _viewModel.ToggleWrapUniformCommand,
            VerticalAlignment = VerticalAlignment.Center
        };

        var widthSlider = new Slider { Minimum = 220, Maximum = 650, Value = _viewModel.WrapContainerWidth, Width = 150, VerticalAlignment = VerticalAlignment.Center };
        _wrapWidthLabel = new TextBlock($"Width: {_viewModel.WrapContainerWidth:F0}px") { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        widthSlider.ValueChanged += (s, v) =>
        {
            _viewModel.WrapContainerWidth = v;
            _wrapWidthLabel.Text = $"Width: {v:F0}px";
            if (_wrapWrapper != null)
            {
                _wrapWrapper.Width = v;
            }
        };

        var controlsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(_btnWrapOrientation, _btnWrapUniform, widthSlider, _wrapWidthLabel);

        // Viewport
        _liveWrapPanel = new WrapPanel
        {
            Orientation = _viewModel.WrapOrientation,
            HorizontalSpacing = _viewModel.WrapSpacing,
            VerticalSpacing = _viewModel.WrapSpacing
        };

        _wrapWrapper = new Border
        {
            Width = _viewModel.WrapContainerWidth,
            MinHeight = 160,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Color.FromHex("#000000").WithAlpha(0.03f),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1.5f),
            BorderBrush = Color.FromHex("#F59E0B").WithAlpha(0.4f),
            Padding = new Thickness(12),
            Child = _liveWrapPanel
        };

        RefreshWrapPanel();

        return card.Child(new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(header, desc, controlsRow, _wrapWrapper));
    }

    private void RefreshWrapPanel()
    {
        if (_liveWrapPanel == null) return;

        _liveWrapPanel.Orientation = _viewModel.WrapOrientation;
        _liveWrapPanel.Clear();

        if (_viewModel.WrapUniformItems)
        {
            _liveWrapPanel.ItemWidth = 110f;
            _liveWrapPanel.ItemHeight = 36f;
        }
        else
        {
            _liveWrapPanel.ItemWidth = float.NaN;
            _liveWrapPanel.ItemHeight = float.NaN;
        }

        if (_btnWrapOrientation != null)
        {
            _btnWrapOrientation.Content = new TextBlock($"Orientation: {_viewModel.WrapOrientation}");
        }

        if (_btnWrapUniform != null)
        {
            _btnWrapUniform.Variant = _viewModel.WrapUniformItems ? ButtonVariant.Filled : ButtonVariant.Outlined;
            _btnWrapUniform.Content = new TextBlock($"Item Size: {(_viewModel.WrapUniformItems ? "Uniform (110×36)" : "Auto")}");
        }

        string[] tags =
        [
            "#MaterialDesign3", "#SkiaSharp", "#NativeAOT", "#SilkNET", "#HotReload",
            "#DirectX12", "#Vulkan", "#CrossPlatform", "#ZeroReflection", "#Vectors",
            "#Typography", "#PropertyGrid", "#DialogHost", "#TreeView", "#Animation"
        ];

        Color[] colors =
        [
            Color.FromHex("#1E88E5"), Color.FromHex("#10B981"), Color.FromHex("#F59E0B"),
            Color.FromHex("#8B5CF6"), Color.FromHex("#EC4899"), Color.FromHex("#06B6D4")
        ];

        for (int i = 0; i < tags.Length; i++)
        {
            Color c = colors[i % colors.Length];
            var chip = new Border
            {
                Background = c.WithAlpha(0.12f),
                BorderThickness = new Thickness(1),
                BorderBrush = c.WithAlpha(0.5f),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 4),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(MaterialIconKind.Tag, 13, foreground: c),
                        new TextBlock(tags[i]) { FontSize = 11, Bold = true, Foreground = c }
                    )
            };
            _liveWrapPanel.Add(chip);
        }
    }

    #endregion

    #region 5. Canvas Card

    private UIElement CreateCanvasCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Brush, 20, foreground: Color.FromHex("#EC4899")),
                new TextBlock("Canvas — Absolute 2D Coordinate System").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                new Border
                {
                    Background = Color.FromHex("#EC4899").WithAlpha(0.12f),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(6, 2),
                    Child = new TextBlock("Canvas.Left • Canvas.Top") { FontSize = 10, Bold = true, Foreground = Color.FromHex("#DB2777") }
                }
            );

        var desc = new TextBlock("Provides absolute positioning of child elements using attached Canvas.Left and Canvas.Top coordinates. Ideal for diagrams, node graphs, CAD surfaces, and custom freeform overlays.") { TextWrapping = TextWrapping.Wrap }
            .FontSize(12).Foreground(Color.FromHex("#757575"));

        // Controls
        var xSlider = new Slider { Minimum = 10, Maximum = 400, Value = _viewModel.CanvasItemX, Width = 120, VerticalAlignment = VerticalAlignment.Center };
        var ySlider = new Slider { Minimum = 10, Maximum = 140, Value = _viewModel.CanvasItemY, Width = 100, VerticalAlignment = VerticalAlignment.Center };

        _canvasCoordText = new TextBlock($"X: {_viewModel.CanvasItemX:F0}px, Y: {_viewModel.CanvasItemY:F0}px")
        {
            FontSize = 12,
            Bold = true,
            Foreground = Color.FromHex("#EC4899"),
            VerticalAlignment = VerticalAlignment.Center
        };

        xSlider.ValueChanged += (s, v) =>
        {
            _viewModel.CanvasItemX = v;
            UpdateCanvasItemPos();
        };

        ySlider.ValueChanged += (s, v) =>
        {
            _viewModel.CanvasItemY = v;
            UpdateCanvasItemPos();
        };

        var btnReset = new Button("Reset")
        {
            Variant = ButtonVariant.Text,
            Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(12),
            Command = _viewModel.ResetCanvasPositionCommand
        };

        var controlsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new TextBlock("Left (X):") { FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                xSlider,
                new TextBlock("Top (Y):") { FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                ySlider,
                _canvasCoordText,
                btnReset
            );

        // Viewport
        _liveCanvas = new Canvas();

        // Fixed landmark items
        void AddLandmark(float x, float y, string name, Color c)
        {
            var node = new Border
            {
                Background = c.WithAlpha(0.15f),
                BorderThickness = new Thickness(1.5f),
                BorderBrush = c,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4),
                Child = new TextBlock(name) { FontSize = 11, Bold = true, Foreground = c }
            };
            Canvas.SetLeft(node, x);
            Canvas.SetTop(node, y);
            _liveCanvas.Add(node);
        }

        AddLandmark(20, 20, "Fixed Node (20, 20)", Color.FromHex("#6B7280"));
        AddLandmark(260, 30, "Target B (260, 30)", Color.FromHex("#10B981"));
        AddLandmark(380, 110, "Target C (380, 110)", Color.FromHex("#8B5CF6"));
        AddLandmark(80, 140, "Anchor (80, 140)", Color.FromHex("#F59E0B"));

        // Movable highlight element
        _movableCanvasItem = new Border
        {
            Background = Color.FromHex("#EC4899"),
            BorderThickness = new Thickness(2),
            BorderBrush = Color.White,
            Elevation = 6,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.ControlCamera, 16, foreground: Color.White),
                    new TextBlock("Movable Target") { FontSize = 12, Bold = true, Foreground = Color.White }
                )
        };
        Canvas.SetLeft(_movableCanvasItem, _viewModel.CanvasItemX);
        Canvas.SetTop(_movableCanvasItem, _viewModel.CanvasItemY);
        _liveCanvas.Add(_movableCanvasItem);

        var viewport = new Border
        {
            Height = 210,
            Background = Color.FromHex("#000000").WithAlpha(0.03f),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.08f),
            Child = _liveCanvas
        };

        return card.Child(new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(header, desc, controlsRow, viewport));
    }

    private void UpdateCanvasItemPos()
    {
        if (_movableCanvasItem != null)
        {
            Canvas.SetLeft(_movableCanvasItem, _viewModel.CanvasItemX);
            Canvas.SetTop(_movableCanvasItem, _viewModel.CanvasItemY);
        }

        if (_canvasCoordText != null)
        {
            _canvasCoordText.Text = $"X: {_viewModel.CanvasItemX:F0}px, Y: {_viewModel.CanvasItemY:F0}px";
        }
    }

    #endregion

    #region 6. Border & Card Container

    private UIElement CreateBorderCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.CropFree, 20, foreground: Color.FromHex("#06B6D4")),
                new TextBlock("Border & Card — Container Decorators").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                new Border
                {
                    Background = Color.FromHex("#06B6D4").WithAlpha(0.12f),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(6, 2),
                    Child = new TextBlock("CornerRadius • Thickness • Elevation • Padding") { FontSize = 10, Bold = true, Foreground = Color.FromHex("#0891B2") }
                }
            );

        var desc = new TextBlock("Draws a border, background, and optional drop shadow elevation around another single child element. Supports asymmetric or uniform corner radii and thickness.") { TextWrapping = TextWrapping.Wrap }
            .FontSize(12).Foreground(Color.FromHex("#757575"));

        // Sliders
        var radiusSlider = new Slider { Minimum = 0, Maximum = 32, Value = _viewModel.BorderCornerRadius, Width = 100, VerticalAlignment = VerticalAlignment.Center };
        radiusSlider.ValueChanged += (s, v) => { _viewModel.BorderCornerRadius = v; RefreshBorder(); };

        var thickSlider = new Slider { Minimum = 0, Maximum = 8, Value = _viewModel.BorderThickness, Width = 80, VerticalAlignment = VerticalAlignment.Center };
        thickSlider.ValueChanged += (s, v) => { _viewModel.BorderThickness = v; RefreshBorder(); };

        var elevSlider = new Slider { Minimum = 0, Maximum = 24, Value = _viewModel.BorderElevation, Width = 90, VerticalAlignment = VerticalAlignment.Center };
        elevSlider.ValueChanged += (s, v) => { _viewModel.BorderElevation = v; RefreshBorder(); };

        var padSlider = new Slider { Minimum = 4, Maximum = 32, Value = _viewModel.BorderPadding, Width = 90, VerticalAlignment = VerticalAlignment.Center };
        padSlider.ValueChanged += (s, v) => { _viewModel.BorderPadding = v; RefreshBorder(); };

        var controlsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new TextBlock("Radius:") { FontSize = 11, VerticalAlignment = VerticalAlignment.Center },
                radiusSlider,
                new TextBlock("Thickness:") { FontSize = 11, VerticalAlignment = VerticalAlignment.Center },
                thickSlider,
                new TextBlock("Elevation:") { FontSize = 11, VerticalAlignment = VerticalAlignment.Center },
                elevSlider,
                new TextBlock("Padding:") { FontSize = 11, VerticalAlignment = VerticalAlignment.Center },
                padSlider
            );

        // Live preview border
        _liveBorder = new Border
        {
            Width = 360,
            Background = Color.FromHex("#06B6D4").WithAlpha(0.12f),
            BorderBrush = Color.FromHex("#06B6D4"),
            BorderThickness = new Thickness(_viewModel.BorderThickness),
            CornerRadius = new CornerRadius(_viewModel.BorderCornerRadius),
            Elevation = _viewModel.BorderElevation,
            Padding = new Thickness(_viewModel.BorderPadding),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center }
                .Children(
                    new TextBlock("Decorated Border Container").Bold().FontSize(13).Foreground(Color.FromHex("#0891B2")).HorizontalAlign(HorizontalAlignment.Center),
                    new TextBlock("Hosts child elements with custom padding, borders & drop shadows.")
                    {
                        FontSize = 11,
                        Foreground = Color.FromHex("#6B7280"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    }
                )
        };

        var viewport = new Border
        {
            Height = 180,
            Background = Color.FromHex("#000000").WithAlpha(0.03f),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.08f),
            Child = _liveBorder
        };

        _borderMetricsText = new TextBlock()
            .FontSize(11)
            .Foreground(Color.FromHex("#6B7280"));

        RefreshBorder();

        return card.Child(new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(header, desc, controlsRow, viewport, _borderMetricsText));
    }

    private void RefreshBorder()
    {
        if (_liveBorder == null) return;

        _liveBorder.CornerRadius = new CornerRadius(_viewModel.BorderCornerRadius);
        _liveBorder.BorderThickness = new Thickness(_viewModel.BorderThickness);
        _liveBorder.Elevation = _viewModel.BorderElevation;
        _liveBorder.Padding = new Thickness(_viewModel.BorderPadding);

        if (_borderMetricsText != null)
        {
            _borderMetricsText.Text = $"Border: CornerRadius={_viewModel.BorderCornerRadius:F0}px, Thickness={_viewModel.BorderThickness:F0}px, Elevation={_viewModel.BorderElevation:F0}dp, Padding={_viewModel.BorderPadding:F0}px";
        }
    }

    #endregion

    private void RefreshLiveViewports()
    {
        RefreshStackPanel();
        RefreshDockPanel();
        RefreshGrid();
        RefreshWrapPanel();
        UpdateCanvasItemPos();
        RefreshBorder();
    }
}
