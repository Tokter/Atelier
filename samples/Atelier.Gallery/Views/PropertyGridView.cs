using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Models;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Gallery.Views;

public class PropertyGridView : Grid
{
    private readonly PropertyGridViewModel _viewModel;
    private readonly PropertyGrid _propertyGrid;

    // Master controls
    private Button? _btnGraphicModel;
    private Button? _btnMicroserviceModel;
    private Button? _btnParticleModel;
    private Button? _btnSortMode;
    private Button? _btnToolbarToggle;
    private Button? _btnElevationCycle;
    private Button? _btnLabelWidth130;
    private Button? _btnLabelWidth160;
    private Button? _btnLabelWidth200;

    // Live preview containers
    private readonly Border _previewCard;
    private readonly StackPanel _previewContent;

    // Audit log container
    private readonly TextBlock _lastEditedText;
    private readonly StackPanel _logListPanel;

    public PropertyGridView() : this(new PropertyGridViewModel())
    {
    }

    public PropertyGridView(PropertyGridViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        this.Rows(GridLength.Auto, GridLength.Star);
        this.RowSpacing(16);

        // 1. Initialize PropertyGrid
        _propertyGrid = new PropertyGrid
        {
            SelectedObject = _viewModel.CurrentInspectableObject,
            SortMode = _viewModel.SortMode,
            IsToolbarVisible = _viewModel.IsToolbarVisible,
            ToolbarElevation = _viewModel.ToolbarElevation,
            LabelWidth = _viewModel.LabelWidth,
        };

        _propertyGrid.PropertyValueChanged += (s, e) =>
        {
            _viewModel.LogChange(e.Property.DisplayName, e.OldValue, e.NewValue);
            UpdateLivePreview();
            UpdateLogDisplay();
        };

        // Hook up viewmodel requests
        _viewModel.RequestRebuild += () =>
        {
            _propertyGrid.SelectedObject = _viewModel.CurrentInspectableObject;
            UpdateControlsVisuals();
            UpdateLivePreview();
        };

        _viewModel.RequestExpandAll += () => _propertyGrid.ExpandAll();
        _viewModel.RequestCollapseAll += () => _propertyGrid.CollapseAll();

        // 2. Initialize Preview & Log Containers
        _previewContent = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };
        _previewCard = new Card(CardVariant.Filled)
            .Padding(18)
            .CornerRadius(12)
            .Child(_previewContent);

        _lastEditedText = new TextBlock(_viewModel.LastEditedInfo)
            .FontSize(12)
            .Bold()
            .Foreground(Color.FromHex("#1E88E5"));

        _logListPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };

        // 3. Add Banner Card
        this.Add(CreateMasterBanner().Row(0));

        // 4. Scrollable 2-Column Content Layout
        var scrollViewer = new ScrollViewer();
        var mainGrid = new Grid()
            .Columns(GridLength.Stars(1.15f), GridLength.Stars(1.0f))
            .ColumnSpacing(16);

        // Left Column: PropertyGrid Control Card
        mainGrid.Add(CreateGridHostCard().Column(0));

        // Right Column: Live Target Preview + Reference Matrix + Audit Log
        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 16 };
        rightStack.Add(_previewCard);
        rightStack.Add(CreateEditorsReferenceCard());
        rightStack.Add(CreateAuditLogCard());

        mainGrid.Add(rightStack.Column(1));

        scrollViewer.Content = mainGrid;
        this.Add(scrollViewer.Row(1));

        // 5. Initial state synchronization
        UpdateControlsVisuals();
        UpdateLivePreview();
        UpdateLogDisplay();
    }

    private UIElement CreateMasterBanner()
    {
        var card = new Card(CardVariant.Filled)
            .Padding(20)
            .CornerRadius(14);

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Tune, 26, foreground: Color.FromHex("#1E88E5")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("PropertyGrid & Native AOT Property Editors").TitleLarge().Bold().VerticalAlign(VerticalAlignment.Center),
                new Border
                {
                    Background = Color.FromHex("#10B981").WithAlpha(0.15f),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 3),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock("100% Native AOT & Zero Reflection")
                    {
                        FontSize = 11,
                        Bold = true,
                        Foreground = Color.FromHex("#059669")
                    }
                }
            );

        var descText = new TextBlock("A high-performance property inspector designed for Native AOT and single-file bundling. Includes built-in editors for strings, booleans, integer types, floating-point types, enums, colors, and read-only properties, featuring real-time search filtering and categorized/alphabetical grouping.")
            .BodyMedium()
            .Foreground(Color.FromHex("#757575"));

        // Target Model Selector Chips
        var modelSelectorLabel = new TextBlock("Inspect Target:").LabelMedium().Bold().VerticalAlign(VerticalAlignment.Center);

        _btnGraphicModel = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.Palette, 16, foreground: Color.FromHex("#1E88E5")) { VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock("2D Graphic Element") { FontSize = 12, VerticalAlignment = VerticalAlignment.Center }
                ),
            Padding = new Thickness(12, 6),
            CornerRadius = new CornerRadius(16),
            Command = _viewModel.SelectGraphicModelCommand
        };

        _btnMicroserviceModel = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.Cloud, 16, foreground: Color.FromHex("#10B981")) { VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock("Microservice Config") { FontSize = 12, VerticalAlignment = VerticalAlignment.Center }
                ),
            Padding = new Thickness(12, 6),
            CornerRadius = new CornerRadius(16),
            Command = _viewModel.SelectMicroserviceModelCommand
        };

        _btnParticleModel = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.AutoAwesome, 16, foreground: Color.FromHex("#FF9800")) { VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock("Particle Simulation") { FontSize = 12, VerticalAlignment = VerticalAlignment.Center }
                ),
            Padding = new Thickness(12, 6),
            CornerRadius = new CornerRadius(16),
            Command = _viewModel.SelectParticleModelCommand
        };

        var modelButtonsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(modelSelectorLabel, _btnGraphicModel, _btnMicroserviceModel, _btnParticleModel);

        // Actions & Customization Row
        _btnSortMode = new Button
        {
            Padding = new Thickness(10, 6),
            CornerRadius = new CornerRadius(16),
            Content = new TextBlock("Sort: Categorized") { FontSize = 12 }
        };
        _btnSortMode.Click += (s, e) =>
        {
            _viewModel.ToggleSortMode();
            _propertyGrid.SortMode = _viewModel.SortMode;
            UpdateControlsVisuals();
        };

        var btnExpandAll = new Button("Expand All")
        {
            Variant = ButtonVariant.Outlined,
            Padding = new Thickness(10, 6),
            CornerRadius = new CornerRadius(16),
            Command = _viewModel.ExpandAllCommand
        };

        var btnCollapseAll = new Button("Collapse All")
        {
            Variant = ButtonVariant.Outlined,
            Padding = new Thickness(10, 6),
            CornerRadius = new CornerRadius(16),
            Command = _viewModel.CollapseAllCommand
        };

        _btnToolbarToggle = new Button
        {
            Variant = ButtonVariant.Outlined,
            Padding = new Thickness(10, 6),
            CornerRadius = new CornerRadius(16),
            Content = new TextBlock("Toolbar: Visible") { FontSize = 12 }
        };
        _btnToolbarToggle.Click += (s, e) =>
        {
            _viewModel.ToggleToolbar();
            _propertyGrid.IsToolbarVisible = _viewModel.IsToolbarVisible;
            UpdateControlsVisuals();
        };

        _btnElevationCycle = new Button
        {
            Variant = ButtonVariant.Outlined,
            Padding = new Thickness(10, 6),
            CornerRadius = new CornerRadius(16),
            Content = new TextBlock("Elevation: 2dp") { FontSize = 12 }
        };
        _btnElevationCycle.Click += (s, e) =>
        {
            _viewModel.CycleElevation();
            _propertyGrid.ToolbarElevation = _viewModel.ToolbarElevation;
            UpdateControlsVisuals();
        };

        var widthLabel = new TextBlock("Label Width:") { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

        _btnLabelWidth130 = new Button("130px")
        {
            Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(12),
            VerticalAlignment = VerticalAlignment.Center
        };
        _btnLabelWidth130.Click += (s, e) => SetLabelWidth(130f);

        _btnLabelWidth160 = new Button("160px")
        {
            Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(12),
            VerticalAlignment = VerticalAlignment.Center
        };
        _btnLabelWidth160.Click += (s, e) => SetLabelWidth(160f);

        _btnLabelWidth200 = new Button("200px")
        {
            Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(12),
            VerticalAlignment = VerticalAlignment.Center
        };
        _btnLabelWidth200.Click += (s, e) => SetLabelWidth(200f);

        var btnReset = new Button("Reset Model")
        {
            Variant = ButtonVariant.Text,
            Padding = new Thickness(10, 6),
            CornerRadius = new CornerRadius(16),
            Command = _viewModel.ResetCurrentModelCommand
        };

        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                _btnSortMode,
                btnExpandAll,
                btnCollapseAll,
                _btnToolbarToggle,
                _btnElevationCycle,
                widthLabel,
                _btnLabelWidth130,
                _btnLabelWidth160,
                _btnLabelWidth200,
                btnReset
            );

        return card.Child(new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 }
            .Children(titleStack, descText, modelButtonsRow, actionsRow));
    }

    private void SetLabelWidth(float width)
    {
        _viewModel.SetLabelWidth(width);
        _propertyGrid.LabelWidth = width;
        UpdateControlsVisuals();
    }

    private void UpdateControlsVisuals()
    {
        if (_btnGraphicModel != null)
            _btnGraphicModel.Variant = _viewModel.ActiveModel == ActivePropertyModel.GraphicElement ? ButtonVariant.Filled : ButtonVariant.Outlined;
        if (_btnMicroserviceModel != null)
            _btnMicroserviceModel.Variant = _viewModel.ActiveModel == ActivePropertyModel.MicroserviceConfig ? ButtonVariant.Filled : ButtonVariant.Outlined;
        if (_btnParticleModel != null)
            _btnParticleModel.Variant = _viewModel.ActiveModel == ActivePropertyModel.ParticleEmitter ? ButtonVariant.Filled : ButtonVariant.Outlined;

        if (_btnSortMode != null)
        {
            _btnSortMode.Variant = _propertyGrid.SortMode == PropertySortMode.Categorized ? ButtonVariant.Tonal : ButtonVariant.Outlined;
            _btnSortMode.Content = new TextBlock($"Sort: {_propertyGrid.SortMode}") { FontSize = 12 };
        }

        if (_btnToolbarToggle != null)
        {
            _btnToolbarToggle.Variant = _propertyGrid.IsToolbarVisible ? ButtonVariant.Tonal : ButtonVariant.Outlined;
            _btnToolbarToggle.Content = new TextBlock($"Toolbar: {(_propertyGrid.IsToolbarVisible ? "Visible" : "Hidden")}") { FontSize = 12 };
        }

        if (_btnElevationCycle != null)
        {
            _btnElevationCycle.Content = new TextBlock($"Elevation: {_propertyGrid.ToolbarElevation:0}dp") { FontSize = 12 };
        }

        if (_btnLabelWidth130 != null)
            _btnLabelWidth130.Variant = MathF.Abs(_propertyGrid.LabelWidth - 130f) < 5f ? ButtonVariant.Filled : ButtonVariant.Outlined;
        if (_btnLabelWidth160 != null)
            _btnLabelWidth160.Variant = MathF.Abs(_propertyGrid.LabelWidth - 160f) < 5f ? ButtonVariant.Filled : ButtonVariant.Outlined;
        if (_btnLabelWidth200 != null)
            _btnLabelWidth200.Variant = MathF.Abs(_propertyGrid.LabelWidth - 200f) < 5f ? ButtonVariant.Filled : ButtonVariant.Outlined;
    }

    private UIElement CreateGridHostCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(16)
            .CornerRadius(12);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.ViewList, 20, foreground: Color.FromHex("#1E88E5")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Live Property Inspector Grid").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center)
            );

        var tipText = new TextBlock("Use the built-in toolbar below to filter properties or switch sort modes. Expand/collapse categories by clicking section headers.")
            .FontSize(12)
            .Foreground(Color.FromHex("#757575"));

        var hostStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(header, tipText, _propertyGrid);

        return card.Child(hostStack);
    }

    private void UpdateLivePreview()
    {
        _previewContent.Clear();

        switch (_viewModel.ActiveModel)
        {
            case ActivePropertyModel.GraphicElement:
                BuildGraphicElementPreview(_viewModel.GraphicModel);
                break;
            case ActivePropertyModel.MicroserviceConfig:
                BuildMicroservicePreview(_viewModel.MicroserviceModel);
                break;
            case ActivePropertyModel.ParticleEmitter:
                BuildParticlePreview(_viewModel.ParticleModel);
                break;
        }

        _previewCard.InvalidateMeasure();
        _previewCard.InvalidateVisual();
    }

    private void BuildGraphicElementPreview(GraphicElementModel model)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.AutoAwesome, 20, foreground: Color.FromHex("#1E88E5")),
                new TextBlock("Live 2D Graphic Output").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                new Border
                {
                    Background = model.Visible ? Color.FromHex("#10B981").WithAlpha(0.15f) : Color.FromHex("#EF4444").WithAlpha(0.15f),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(6, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock(model.Visible ? "Visible" : "Hidden")
                    {
                        FontSize = 10,
                        Bold = true,
                        Foreground = model.Visible ? Color.FromHex("#059669") : Color.FromHex("#DC2626")
                    }
                }
            );

        // Render actual shape based on model properties
        CornerRadius radius = model.ShapeType switch
        {
            ShapeKind.Circle => new CornerRadius(Math.Min(model.Width, model.Height) / 2),
            ShapeKind.Pill => new CornerRadius(Math.Min(model.Width, model.Height) / 2),
            ShapeKind.RoundedRect => new CornerRadius(model.CornerRadius),
            _ => new CornerRadius(0)
        };

        var renderedShape = new Border
        {
            Width = Math.Clamp(model.Width, 40, 320),
            Height = Math.Clamp(model.Height, 40, 180),
            CornerRadius = radius,
            Background = model.FillColor,
            BorderThickness = new Thickness(2),
            BorderBrush = model.StrokeColor,
            Opacity = Math.Clamp(model.Opacity, 0f, 1f),
            Elevation = Math.Clamp(model.ShadowElevation, 0f, 24f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = model.Visible ? Visibility.Visible : Visibility.Hidden,
            Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new TextBlock(model.Name)
                    {
                        FontSize = 13,
                        Bold = true,
                        Foreground = Color.White,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock($"{model.Width} × {model.Height} px • {model.ShapeType}")
                    {
                        FontSize = 11,
                        Foreground = Color.White.WithAlpha(0.85f),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                )
        };

        var shapeContainer = new Border
        {
            Height = 200,
            Background = Color.FromHex("#000000").WithAlpha(0.04f),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.08f),
            Padding = new Thickness(16),
            Child = renderedShape
        };

        // Telemetry metadata badges
        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(
                CreateBadge("Blend", model.BlendMode.ToString(), Color.FromHex("#8B5CF6")),
                CreateBadge("Anti-Alias", model.AntiAliasing ? "ON" : "OFF", model.AntiAliasing ? Color.FromHex("#10B981") : Color.FromHex("#EF4444")),
                CreateBadge("Fill Hex", model.FillColor.ToString(), Color.FromHex("#1E88E5")),
                CreateBadge("Device", model.HardwareId, Color.FromHex("#6B7280"))
            );

        _previewContent.Add(header);
        _previewContent.Add(shapeContainer);
        _previewContent.Add(infoRow);
    }

    private void BuildMicroservicePreview(MicroserviceConfigModel model)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Dns, 20, foreground: Color.FromHex("#10B981")),
                new TextBlock("Live Microservice Status").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                new Border
                {
                    Background = model.StatusColor.WithAlpha(0.15f),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(6, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock(model.Environment.ToString())
                    {
                        FontSize = 10,
                        Bold = true,
                        Foreground = model.StatusColor
                    }
                }
            );

        var endpointBox = new Border
        {
            Background = Color.FromHex("#000000").WithAlpha(0.04f),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.08f),
            Padding = new Thickness(14),
            Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 }
                .Children(
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                        .Children(
                            new Border
                            {
                                Width = 12,
                                Height = 12,
                                CornerRadius = new CornerRadius(6),
                                Background = model.StatusColor,
                                VerticalAlignment = VerticalAlignment.Center
                            },
                            new TextBlock(model.ServiceName).Bold().FontSize(14).VerticalAlign(VerticalAlignment.Center),
                            new TextBlock($"https://{model.HostAddress}:{model.Port}").FontSize(12).Foreground(Color.FromHex("#6B7280")).VerticalAlign(VerticalAlignment.Center)
                        ),
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 }
                        .Children(
                            CreateBadge("TLS 1.3", model.EnableTls ? "🔒 Encrypted" : "⚠️ Insecure", model.EnableTls ? Color.FromHex("#059669") : Color.FromHex("#DC2626")),
                            CreateBadge("Region", model.Region.ToString(), Color.FromHex("#2563EB")),
                            CreateBadge("Log", model.LogLevel.ToString(), Color.FromHex("#D97706")),
                            CreateBadge("Max Conns", $"{model.MaxConnections:N0}", Color.FromHex("#4B5563"))
                        ),
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 }
                        .Children(
                            new TextBlock($"⏱️ Timeout: {model.TimeoutSeconds:F1}s").FontSize(12),
                            new TextBlock($"🎯 Target Cache: {(model.TargetCacheRatio * 100):F0}%").FontSize(12),
                            new TextBlock($"🚀 Uptime: {model.Uptime}").FontSize(12).Foreground(Color.FromHex("#6B7280"))
                        )
                )
        };

        _previewContent.Add(header);
        _previewContent.Add(endpointBox);
    }

    private void BuildParticlePreview(ParticleEmitterModel model)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Storm, 20, foreground: Color.FromHex("#FF9800")),
                new TextBlock("Live Particle Simulation Dashboard").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                new Border
                {
                    Background = model.IsActive ? Color.FromHex("#10B981").WithAlpha(0.15f) : Color.FromHex("#9E9E9E").WithAlpha(0.15f),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(6, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock(model.IsActive ? "ACTIVE" : "PAUSED")
                    {
                        FontSize = 10,
                        Bold = true,
                        Foreground = model.IsActive ? Color.FromHex("#059669") : Color.FromHex("#6B7280")
                    }
                }
            );

        var previewBox = new Border
        {
            Background = Color.FromHex("#000000").WithAlpha(0.04f),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.08f),
            Padding = new Thickness(14),
            Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
                .Children(
                    new TextBlock(model.EmitterName).Bold().FontSize(14),
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, VerticalAlignment = VerticalAlignment.Center }
                        .Children(
                            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                                .Children(
                                    new TextBlock("Particle Tint:") { FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                                    new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(4), Background = model.ParticleColor, VerticalAlignment = VerticalAlignment.Center },
                                    new TextBlock(model.ParticleColor.ToString()) { FontSize = 11, VerticalAlignment = VerticalAlignment.Center }
                                ),
                            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                                .Children(
                                    new TextBlock("Trail Tint:") { FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                                    new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(4), Background = model.TrailColor, VerticalAlignment = VerticalAlignment.Center },
                                    new TextBlock(model.TrailColor.ToString()) { FontSize = 11, VerticalAlignment = VerticalAlignment.Center }
                                )
                        ),
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
                        .Children(
                            CreateBadge("Shape", model.Shape.ToString(), Color.FromHex("#8B5CF6")),
                            CreateBadge("Collision", model.CollisionMode.ToString(), Color.FromHex("#EC4899")),
                            CreateBadge("Rate", $"{model.EmissionRate} p/s", Color.FromHex("#10B981")),
                            CreateBadge("FPS", $"{model.TargetFps} fps", Color.FromHex("#3B82F6")),
                            CreateBadge("Gravity", $"{model.GravityForce:F2} m/s²", Color.FromHex("#6B7280"))
                        )
                )
        };

        _previewContent.Add(header);
        _previewContent.Add(previewBox);
    }

    private static Border CreateBadge(string label, string value, Color accent)
    {
        return new Border
        {
            Background = accent.WithAlpha(0.12f),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new TextBlock($"{label}:") { FontSize = 11, Foreground = accent.WithAlpha(0.85f), VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock(value) { FontSize = 11, Bold = true, Foreground = accent, VerticalAlignment = VerticalAlignment.Center }
                )
        };
    }

    private UIElement CreateEditorsReferenceCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(16)
            .CornerRadius(12);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Extension, 20, foreground: Color.FromHex("#8B5CF6")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Built-in Editors Reference Matrix").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center)
            );

        static Border CreateEditorRow(string typeName, string editorName, string description, Color badgeColor)
        {
            var badge = new Border
            {
                Background = badgeColor.WithAlpha(0.15f),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2),
                Width = 110,
                Child = new TextBlock(typeName) { FontSize = 11, Bold = true, Foreground = badgeColor, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };

            var editorLabel = new TextBlock(editorName).Bold().FontSize(12);
            var desc = new TextBlock(description).FontSize(11).Foreground(Color.FromHex("#757575"));

            var colStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
                .Children(editorLabel, desc);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                .Children(badge, colStack);

            return new Border
            {
                Padding = new Thickness(4, 3),
                Child = row
            };
        }

        var list = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 }
            .Children(
                CreateEditorRow("string", "TextBox Editor", "Direct text editing with two-way binding & instant update", Color.FromHex("#1E88E5")),
                CreateEditorRow("bool", "CheckBox Editor", "Material 3 interactive checkbox toggle", Color.FromHex("#10B981")),
                CreateEditorRow("int / long / byte", "Integer Editor", "Culture-invariant integer parser with invalid input revert on LostFocus", Color.FromHex("#3B82F6")),
                CreateEditorRow("float / double", "Floating-Point Editor", "Decimal/float parser supporting standard & thousands formats", Color.FromHex("#06B6D4")),
                CreateEditorRow("Enum", "ComboBox Editor", "Auto-discovered enum names dropdown with zero reflection", Color.FromHex("#8B5CF6")),
                CreateEditorRow("Color", "Swatch + Hex Editor", "Interactive 24×24 color preview swatch with live hex code editor", Color.FromHex("#EC4899")),
                CreateEditorRow("IsReadOnly", "Dimmed Text Editor", "Non-editable dimmed display for getters without setters", Color.FromHex("#6B7280"))
            );

        return card.Child(new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(header, list));
    }

    private UIElement CreateAuditLogCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(16)
            .CornerRadius(12);

        var clearBtn = new Button("Clear Log")
        {
            Variant = ButtonVariant.Text,
            Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(12),
            Command = _viewModel.ClearLogCommand
        };
        clearBtn.Click += (s, e) => UpdateLogDisplay();

        var headerGrid = new Grid()
            .Columns(GridLength.Star, GridLength.Auto);

        var headerTitle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.History, 20, foreground: Color.FromHex("#F59E0B")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Property Change Audit Log").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center)
            ).Column(0);

        clearBtn.Column(1);
        headerGrid.Children(headerTitle, clearBtn);

        var logContainer = new Border
        {
            MinHeight = 90,
            MaxHeight = 160,
            Background = Color.FromHex("#000000").WithAlpha(0.04f),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.08f),
            Padding = new Thickness(10),
            Child = new ScrollViewer { Content = _logListPanel }
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 }
            .Children(headerGrid, _lastEditedText, logContainer);

        return card.Child(stack);
    }

    private void UpdateLogDisplay()
    {
        _lastEditedText.Text = _viewModel.LastEditedInfo;
        _logListPanel.Clear();

        if (_viewModel.ChangeLogs.Count == 0)
        {
            _logListPanel.Add(new TextBlock("No property modifications logged yet.")
            {
                FontSize = 11,
                Foreground = Color.FromHex("#9E9E9E")
            });
            return;
        }

        foreach (var log in _viewModel.ChangeLogs)
        {
            _logListPanel.Add(new TextBlock(log)
            {
                FontSize = 11,
                Foreground = Color.FromHex("#374151")
            });
        }
    }
}
