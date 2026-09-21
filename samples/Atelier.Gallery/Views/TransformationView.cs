using System;
using System.Numerics;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Platform.Silk;
using Atelier.Theming;
using Atelier.Theming.Material;
using Atelier.Gallery.ViewModels;

namespace Atelier.Gallery.Views;

public class TransformationView : Grid
{
    private readonly TransformationViewModel _viewModel;
    private readonly ScrollViewer _scrollViewer;

    // Interactive transformed container
    private readonly UIElement _previewBox;
    private readonly Border _pivotMarker = new Border
    {
        Width = 14,
        Height = 14,
        CornerRadius = new CornerRadius(7),
        Background = Color.FromHex("#EF4444"),
        BorderThickness = new Thickness(2),
        BorderBrush = Color.White,
        Elevation = 4,
        IsHitTestVisible = false,
        Child = new Border
        {
            Width = 4,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = Color.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }
    };

    // Zoom container
    private readonly Border _zoomPreviewBox;

    // Matrix inspector labels
    private readonly TextBlock _m11Label;
    private readonly TextBlock _m12Label;
    private readonly TextBlock _m21Label;
    private readonly TextBlock _m22Label;
    private readonly TextBlock _m31Label;
    private readonly TextBlock _m32Label;
    private readonly TextBlock _detLabel;
    private readonly TextBlock _invLabel;

    // Pipeline mode switch
    private Button? _btnModeRender;
    private Button? _btnModeLayout;
    private TextBlock? _pipelineDescText;

    // Pivot origin buttons
    private Button? _btnCenter;
    private Button? _btnTopLeft;
    private Button? _btnTopRight;
    private Button? _btnBottomLeft;
    private Button? _btnBottomRight;

    // Master Zoom controls
    private Slider? _zoomSlider;
    private TextBlock? _zoomLabel;

    public TransformationView() : this(new TransformationViewModel())
    {
    }

    public TransformationView(TransformationViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        this.Rows(GridLength.Auto, GridLength.Star);
        this.RowSpacing(16);

        // 1. Initialize elements
        _m11Label = new TextBlock("1.000").Bold().FontSize(13);
        _m12Label = new TextBlock("0.000").Bold().FontSize(13);
        _m21Label = new TextBlock("0.000").Bold().FontSize(13);
        _m22Label = new TextBlock("1.000").Bold().FontSize(13);
        _m31Label = new TextBlock("0.0 px").Bold().FontSize(13);
        _m32Label = new TextBlock("0.0 px").Bold().FontSize(13);
        _detLabel = new TextBlock("det(M) = 1.0000").Caption().Bold();
        _invLabel = new TextBlock("Invertible (Hit-test active)").Caption().Bold().Foreground(Color.FromHex("#10B981"));

        _previewBox = CreateInteractiveTarget();
        _zoomPreviewBox = CreateZoomTarget();

        // 2. Master Controls Banner
        this.Add(CreateMasterBanner().Row(0));

        // 3. Showcase Stack
        var cardsStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 16
        }.Children(
            CreateAccessibilityZoomCard(),
            CreateTransformationPlaygroundCard(),
            CreateMatrixInspectorCard(),
            CreatePresetShowcasesCard()
        );

        cardsStack.Margin = new Thickness(0, 0, 10, 20);

        _scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = cardsStack
        }.Row(1);

        this.Add(_scrollViewer);

        // 4. Wire ViewModel Callbacks
        _viewModel.RequestTransformUpdate = UpdateTransformVisuals;
        _viewModel.RequestZoomUpdate = UpdateZoomVisuals;

        UpdateTransformVisuals();
    }

    public override void OnPointerWheel(PointerWheelEventArgs e)
    {
        base.OnPointerWheel(e);
        if (!e.Handled && _scrollViewer != null)
        {
            _scrollViewer.OnPointerWheel(e);
        }
    }

    private void UpdateTransformVisuals()
    {
        if (_viewModel.IsLayoutTransformMode)
        {
            _previewBox.TransformOrigin = _viewModel.CurrentOrigin;
            _previewBox.Transform = _viewModel.BaseMatrix;
            _previewBox.RenderTransform = Matrix3x2.Identity;
            _previewBox.RenderTransformOrigin = new Point(0.5f, 0.5f);

            if (_pipelineDescText != null)
            {
                _pipelineDescText.Text = "LayoutTransform: Active. Computes transformed bounding box & expands horizontal/vertical layout space.";
            }
        }
        else
        {
            _previewBox.Transform = Matrix3x2.Identity;
            _previewBox.TransformOrigin = Point.Zero;
            _previewBox.RenderTransformOrigin = _viewModel.CurrentOrigin;
            _previewBox.RenderTransform = _viewModel.BaseMatrix;

            if (_pipelineDescText != null)
            {
                _pipelineDescText.Text = "RenderTransform: Active. Preserves 240x180 layout slot; hinges visually around the active pivot anchor pin.";
            }
        }

        if (_btnModeRender != null) _btnModeRender.Variant = !_viewModel.IsLayoutTransformMode ? ButtonVariant.Filled : ButtonVariant.Outlined;
        if (_btnModeLayout != null) _btnModeLayout.Variant = _viewModel.IsLayoutTransformMode ? ButtonVariant.Filled : ButtonVariant.Outlined;

        float r = 7f;
        float px = _viewModel.OriginX * TransformationViewModel.TargetWidth;
        float py = _viewModel.OriginY * TransformationViewModel.TargetHeight;
        Canvas.SetLeft(_pivotMarker, px - r);
        Canvas.SetTop(_pivotMarker, py - r);

        if (_btnCenter != null) _btnCenter.Variant = _viewModel.IsCenterOrigin ? ButtonVariant.Filled : ButtonVariant.Outlined;
        if (_btnTopLeft != null) _btnTopLeft.Variant = _viewModel.IsTopLeftOrigin ? ButtonVariant.Filled : ButtonVariant.Outlined;
        if (_btnTopRight != null) _btnTopRight.Variant = _viewModel.IsTopRightOrigin ? ButtonVariant.Filled : ButtonVariant.Outlined;
        if (_btnBottomLeft != null) _btnBottomLeft.Variant = _viewModel.IsBottomLeftOrigin ? ButtonVariant.Filled : ButtonVariant.Outlined;
        if (_btnBottomRight != null) _btnBottomRight.Variant = _viewModel.IsBottomRightOrigin ? ButtonVariant.Filled : ButtonVariant.Outlined;

        _m11Label.Text = _viewModel.MatrixM11;
        _m12Label.Text = _viewModel.MatrixM12;
        _m21Label.Text = _viewModel.MatrixM21;
        _m22Label.Text = _viewModel.MatrixM22;
        _m31Label.Text = _viewModel.MatrixM31;
        _m32Label.Text = _viewModel.MatrixM32;
        _detLabel.Text = _viewModel.DeterminantText;
        _invLabel.Text = _viewModel.InvertibleStatus;
    }

    private void UpdateZoomVisuals(float zoom, bool wholeApp)
    {
        if (wholeApp && SilkWindow.Current?.Content != null)
        {
            SilkWindow.Current.Content.Transform = Matrix3x2.CreateScale(zoom);
            _zoomPreviewBox.Transform = Matrix3x2.Identity;
        }
        else
        {
            if (SilkWindow.Current?.Content != null)
            {
                SilkWindow.Current.Content.Transform = Matrix3x2.Identity;
            }
            _zoomPreviewBox.Transform = Matrix3x2.CreateScale(zoom);
        }

        if (_zoomSlider != null && !_zoomSlider.IsPointerCaptured)
        {
            _zoomSlider.Value = zoom * 100f;
        }
        if (_zoomLabel != null)
        {
            _zoomLabel.Text = $"Zoom Scale: {zoom * 100f:F0}% ({zoom:F2}x)";
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
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(MaterialIconKind.CropRotate, 28) { Foreground = Color.FromHex("#1E88E5"), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock("Transformation Matrix & Accessibility Zoom").TitleLarge()
                    ),
                new TextBlock("Arbitrary 2D affine matrix transformations (rotation, non-uniform scaling, skew shear, translation) with sub-pixel inverse hit-testing, plus full-window Accessibility Zoom.")
                    .Subtext()
            )
        );

        // Action row
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Button("Reset Transforms")
                    .Variant(ButtonVariant.Filled)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ResetTransformsCommand),

                new Button("Reset Zoom (100%)")
                    .Variant(ButtonVariant.Tonal)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ResetZoomCommand),

                new Button("Isometric Tilt")
                    .Variant(ButtonVariant.Outlined)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ApplyIsometricTiltCommand),

                new Button("Card Tilt")
                    .Variant(ButtonVariant.Outlined)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ApplyCardTiltCommand),

                new Button("Badge Stamp")
                    .Variant(ButtonVariant.Outlined)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ApplyBadgeStampCommand)
            );

        stack.Add(actionRow);
        card.Child = stack;
        return card;
    }

    private UIElement CreateAccessibilityZoomCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(20)
            .CornerRadius(12);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        stack.Add(new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
            .Children(
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(MaterialIconKind.ZoomIn, 22) { Foreground = Color.FromHex("#1E88E5"), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock("Accessibility Window & UI Zoom").TitleMedium()
                    ),
                new TextBlock("Enables visually impaired users and high-DPI displays to scale the interface smoothly without raster pixelation. All layouts, font metrics, and pointer hit-tests scale seamlessly.")
                    .Subtext()
            )
        );

        // Preset zoom buttons row
        var zoomButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Button("100% (Default)")
                    .Variant(ButtonVariant.Tonal)
                    .OnClick(() => _viewModel.WindowZoom = 1.0f),

                new Button("125% (Accessibility)")
                    .Variant(ButtonVariant.Tonal)
                    .OnClick(() => _viewModel.WindowZoom = 1.25f),

                new Button("150% (Large Text)")
                    .Variant(ButtonVariant.Tonal)
                    .OnClick(() => _viewModel.WindowZoom = 1.50f),

                new Button("175% (Ultra Large)")
                    .Variant(ButtonVariant.Tonal)
                    .OnClick(() => _viewModel.WindowZoom = 1.75f),

                new Button("200% (Max Zoom)")
                    .Variant(ButtonVariant.Tonal)
                    .OnClick(() => _viewModel.WindowZoom = 2.00f)
            );
        stack.Add(zoomButtons);

        // Continuous zoom slider & options
        var zoomSliderRow = new Grid()
            .Columns(GridLength.Pixels(180), GridLength.Pixels(240), GridLength.Star)
            .ColumnSpacing(16);

        _zoomLabel = new TextBlock("Zoom Scale: 100%")
            .LabelMedium()
            .VerticalAlign(VerticalAlignment.Center)
            .Column(0);

        _zoomSlider = new Slider
        {
            Minimum = 80,
            Maximum = 200,
            Value = 100,
            VerticalAlignment = VerticalAlignment.Center
        }.Column(1);

        _zoomSlider.ValueChanged += (s, v) =>
        {
            float factor = v / 100f;
            _viewModel.WindowZoom = factor;
            if (_zoomLabel != null)
            {
                _zoomLabel.Text = $"Zoom Scale: {v:F0}% ({factor:F2}x)";
            }
        };

        var appScopeSwitch = new Switch("Scale Entire App Window")
            .BindIsChecked(_viewModel, vm => vm.ScaleWholeApp, (vm, v) => vm.ScaleWholeApp = v)
            .VerticalAlign(VerticalAlignment.Center)
            .Column(2);

        zoomSliderRow.Add(_zoomLabel);
        zoomSliderRow.Add(_zoomSlider);
        zoomSliderRow.Add(appScopeSwitch);
        stack.Add(zoomSliderRow);

        // Zoom preview container
        var zoomPreviewWrapper = new Border
        {
            MinHeight = 130,
            Background = Color.FromRgb(128, 128, 128).WithAlpha(0.05f),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromRgb(128, 128, 128).WithAlpha(0.15f),
            Padding = new Thickness(16),
            ClipToBounds = true,
            Child = _zoomPreviewBox
        };
        stack.Add(zoomPreviewWrapper);

        card.Child = stack;
        return card;
    }

    private Border CreateZoomTarget()
    {
        var icon = new Icon(MaterialIconKind.Accessibility, 24) { Foreground = Color.FromHex("#1E88E5"), VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock("Accessibility Scaled Subtree").TitleSmall().VerticalAlign(VerticalAlignment.Center);
        var subtext = new TextBlock("This container scales dynamically. Text remains sharp at all magnification levels.").Caption();

        var testBtn = new Button("Click Scaled Button")
            .Variant(ButtonVariant.Filled)
            .OnClick(() => _viewModel.InteractionLog = $"Zoom test button clicked! Current zoom: {_viewModel.WindowZoom:F2}x");

        var testBox = new TextBox("High-DPI sharp text") { Width = 200 };

        var contentRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, VerticalAlignment = VerticalAlignment.Center }
            .Children(testBtn, testBox);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 }
            .Children(
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }.Children(icon, title),
                subtext,
                contentRow
            );

        return new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(10),
            Background = Color.FromHex("#1E88E5").WithAlpha(0.08f),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#1E88E5").WithAlpha(0.3f),
            Child = stack
        }.TransformOrigin(0f, 0f);
    }

    private UIElement CreateTransformationPlaygroundCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(20)
            .CornerRadius(12)
            .ClipToBounds(true);

        var mainStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        mainStack.Add(new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
            .Children(
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(MaterialIconKind.Tune, 22) { Foreground = Color.FromHex("#8B5CF6"), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock("2D Affine Transformation Matrix Playground").TitleMedium()
                    ),
                new TextBlock("Rotate, scale, skew, and translate any UIElement. Test that buttons, sliders, text boxes, and switches remain fully interactive and respond accurately to pointer clicks.")
                    .Subtext()
            )
        );

        var grid = new Grid()
            .Columns(GridLength.Pixels(380), GridLength.Star)
            .ColumnSpacing(20);

        // Left controls column
        var controlsStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }
            .ClipToBounds(true);

        // 0. Pipeline Mode Selector
        var modeLabel = new TextBlock("Transformation Pipeline Mode:").LabelSmall().Bold();

        _btnModeRender = new Button("RenderTransform")
            .Variant(!_viewModel.IsLayoutTransformMode ? ButtonVariant.Filled : ButtonVariant.Outlined)
            .OnClick(() => { _viewModel.IsLayoutTransformMode = false; });

        _btnModeLayout = new Button("LayoutTransform")
            .Variant(_viewModel.IsLayoutTransformMode ? ButtonVariant.Filled : ButtonVariant.Outlined)
            .OnClick(() => { _viewModel.IsLayoutTransformMode = true; });

        var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(_btnModeRender, _btnModeLayout);

        _pipelineDescText = new TextBlock(
            !_viewModel.IsLayoutTransformMode
                ? "RenderTransform: Post-layout GPU transform. Card hinges around active pivot pin."
                : "LayoutTransform: Layout-affecting. Computes rotated bounding box & expands DesiredSize."
        )
        .TextWrapping()
        .ClipToBounds(true)
        .Caption()
        .Muted();

        controlsStack.Add(modeLabel);
        controlsStack.Add(modeRow);
        controlsStack.Add(_pipelineDescText);

        // 1. Rotation Slider
        var rotLabel = new TextBlock("Rotation: 0°").LabelSmall();
        var rotSlider = new Slider { Minimum = -180, Maximum = 180, Value = 0 };
        rotSlider.ValueChanged += (s, v) =>
        {
            _viewModel.RotationDegrees = v;
            rotLabel.Text = $"Rotation: {v:F0}°";
        };

        var rotPresets = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }
            .Children(
                new Button("-45°").Variant(ButtonVariant.Tonal).OnClick(() => { rotSlider.Value = -45; }),
                new Button("-15°").Variant(ButtonVariant.Tonal).OnClick(() => { rotSlider.Value = -15; }),
                new Button("0°").Variant(ButtonVariant.Tonal).OnClick(() => { rotSlider.Value = 0; }),
                new Button("+15°").Variant(ButtonVariant.Tonal).OnClick(() => { rotSlider.Value = 15; }),
                new Button("+45°").Variant(ButtonVariant.Tonal).OnClick(() => { rotSlider.Value = 45; }),
                new Button("+90°").Variant(ButtonVariant.Tonal).OnClick(() => { rotSlider.Value = 90; })
            );

        controlsStack.Add(rotLabel);
        controlsStack.Add(rotSlider);
        controlsStack.Add(rotPresets);

        // 2. Scale Sliders
        var scaleLabel = new TextBlock("Scale: 1.00x").LabelSmall();
        var scaleSlider = new Slider { Minimum = 40, Maximum = 200, Value = 100 };
        scaleSlider.ValueChanged += (s, v) =>
        {
            float f = v / 100f;
            _viewModel.ScaleX = f;
            _viewModel.ScaleY = f;
            scaleLabel.Text = $"Scale: {f:F2}x";
        };

        controlsStack.Add(scaleLabel);
        controlsStack.Add(scaleSlider);

        // 3. Skew Sliders
        var skewLabel = new TextBlock("Skew X: 0°").LabelSmall();
        var skewSlider = new Slider { Minimum = -45, Maximum = 45, Value = 0 };
        skewSlider.ValueChanged += (s, v) =>
        {
            _viewModel.SkewXDegrees = v;
            skewLabel.Text = $"Skew X: {v:F0}°";
        };

        controlsStack.Add(skewLabel);
        controlsStack.Add(skewSlider);

        // 4. Transform Origin Controls
        var originLabel = new TextBlock("Transform Pivot Origin (Rotation & Scale Hinge):").LabelSmall();

        _btnCenter = new Button("Center")
            .Variant(_viewModel.IsCenterOrigin ? ButtonVariant.Filled : ButtonVariant.Outlined)
            .OnClick(() => { _viewModel.SetOrigin("center"); UpdateTransformVisuals(); });

        _btnTopLeft = new Button("Top-Left")
            .Variant(_viewModel.IsTopLeftOrigin ? ButtonVariant.Filled : ButtonVariant.Outlined)
            .OnClick(() => { _viewModel.SetOrigin("topleft"); UpdateTransformVisuals(); });

        _btnTopRight = new Button("Top-Right")
            .Variant(_viewModel.IsTopRightOrigin ? ButtonVariant.Filled : ButtonVariant.Outlined)
            .OnClick(() => { _viewModel.SetOrigin("topright"); UpdateTransformVisuals(); });

        _btnBottomLeft = new Button("Bottom-Left")
            .Variant(_viewModel.IsBottomLeftOrigin ? ButtonVariant.Filled : ButtonVariant.Outlined)
            .OnClick(() => { _viewModel.SetOrigin("bottomleft"); UpdateTransformVisuals(); });

        _btnBottomRight = new Button("Bottom-Right")
            .Variant(_viewModel.IsBottomRightOrigin ? ButtonVariant.Filled : ButtonVariant.Outlined)
            .OnClick(() => { _viewModel.SetOrigin("bottomright"); UpdateTransformVisuals(); });

        var originRow1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(_btnTopLeft, _btnCenter, _btnTopRight);

        var originRow2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(_btnBottomLeft, _btnBottomRight);

        controlsStack.Add(originLabel);
        controlsStack.Add(originRow1);
        controlsStack.Add(originRow2);

        grid.Add(controlsStack.Column(0));

        // Right side: Target surface wrapper
        var targetWrapper = new Border
        {
            MinHeight = 440,
            ClipToBounds = true,
            Background = Color.FromRgb(128, 128, 128).WithAlpha(0.04f),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromRgb(128, 128, 128).WithAlpha(0.15f),
            Padding = new Thickness(16),
            Child = _previewBox
        };

        var targetContainerStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(
                targetWrapper,
                new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 6),
                    Background = Color.FromRgb(128, 128, 128).WithAlpha(0.08f),
                    Child = new TextBlock()
                        .Caption()
                        .BindText(_viewModel, vm => vm.InteractionLog)
                }
            );

        grid.Add(targetContainerStack.Column(1));
        mainStack.Add(grid);

        card.Child = mainStack;
        return card;
    }

    private UIElement CreateInteractiveTarget()
    {
        var pivotBadge = new Border
        {
            Background = Color.FromHex("#8B5CF6").WithAlpha(0.20f),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2),
            Child = new TextBlock()
                .Caption()
                .Bold()
                .Foreground(Color.FromHex("#8B5CF6"))
                .BindText(_viewModel, vm => $"Pivot: {vm.ActiveOriginName}")
        };

        var titleRow = new Grid()
            .Columns(GridLength.Star, GridLength.Auto)
            .Children(
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(MaterialIconKind.AutoAwesome, 16) { Foreground = Color.FromHex("#8B5CF6"), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock("Transformed Target").Bold().FontSize(12).VerticalAlign(VerticalAlignment.Center)
                    ).Column(0),
                pivotBadge.Column(1)
            );

        var testButton = new Button("Click Me!")
            .Variant(ButtonVariant.Filled)
            .Padding(10, 4)
            .Command(_viewModel.TargetButtonClickedCommand);

        var testSwitch = new Switch("Active")
            .BindIsChecked(_viewModel, vm => vm.TargetSwitchState, (vm, v) => vm.TargetSwitchState = v);

        var buttonSwitchRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(testButton, testSwitch);

        var testSlider = new Slider { Minimum = 0, Maximum = 100, Value = 50, Height = 22 };
        testSlider.ValueChanged += (s, v) => _viewModel.TargetSliderValue = v;

        var testBox = new TextBox("Edit text...")
            .Padding(8, 4)
            .BindText(_viewModel, vm => vm.TargetInputText, (vm, v) => vm.TargetInputText = v);

        var clickCounterText = new TextBlock()
            .Caption()
            .Bold()
            .Foreground(Color.FromHex("#8B5CF6"))
            .BindText(_viewModel, vm => $"Clicks: {vm.TargetClickCount}  |  Slider: {vm.TargetSliderValue:F0}%");

        var contentStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 }
            .Children(
                titleRow,
                buttonSwitchRow,
                testSlider,
                testBox,
                clickCounterText
            );

        var cardBorder = new Border
        {
            Width = TransformationViewModel.TargetWidth,
            Height = TransformationViewModel.TargetHeight,
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(12),
            Background = Color.FromHex("#8B5CF6").WithAlpha(0.12f),
            BorderThickness = new Thickness(2),
            BorderBrush = Color.FromHex("#8B5CF6"),
            Child = contentStack
        };

        var overlayCanvas = new Canvas
        {
            Width = TransformationViewModel.TargetWidth,
            Height = TransformationViewModel.TargetHeight,
            IsHitTestVisible = false
        };
        overlayCanvas.Add(_pivotMarker);

        var rootGrid = new Grid
        {
            Width = TransformationViewModel.TargetWidth,
            Height = TransformationViewModel.TargetHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        rootGrid.Add(cardBorder);
        rootGrid.Add(overlayCanvas);

        return rootGrid;
    }

    private UIElement CreateMatrixInspectorCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(20)
            .CornerRadius(12);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };

        stack.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Functions, 20) { Foreground = Color.FromHex("#10B981"), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Affine Matrix 3x2 Mathematical Inspector").TitleMedium()
            )
        );

        stack.Add(new TextBlock("The 3x2 matrix maps local control geometry (x, y) into parent coordinates via: x' = x*M11 + y*M21 + M31, y' = x*M12 + y*M22 + M32. Notice how M31 and M32 dynamically reflect the pivot translation offset when changing the Transform Pivot Origin.")
            .Subtext()
        );

        // 3x2 Matrix Visual Display
        static Border CreateMatrixCell(string label, TextBlock valueText)
        {
            var cellStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center }
                .Children(
                    new TextBlock(label).Caption().Muted().HorizontalAlign(HorizontalAlignment.Center),
                    valueText.HorizontalAlign(HorizontalAlignment.Center)
                );

            return new Border
            {
                Padding = new Thickness(14, 8),
                CornerRadius = new CornerRadius(8),
                Background = Color.FromRgb(128, 128, 128).WithAlpha(0.08f),
                Child = cellStack
            };
        }

        var matrixGrid = new Grid()
            .Columns(GridLength.Star, GridLength.Star)
            .Rows(GridLength.Auto, GridLength.Auto, GridLength.Auto)
            .ColumnSpacing(10)
            .RowSpacing(8);

        matrixGrid.Add(CreateMatrixCell("M11 (Scale X / Cos)", _m11Label).Row(0).Column(0));
        matrixGrid.Add(CreateMatrixCell("M12 (Skew Y / Sin)", _m12Label).Row(0).Column(1));

        matrixGrid.Add(CreateMatrixCell("M21 (Skew X / -Sin)", _m21Label).Row(1).Column(0));
        matrixGrid.Add(CreateMatrixCell("M22 (Scale Y / Cos)", _m22Label).Row(1).Column(1));

        matrixGrid.Add(CreateMatrixCell("M31 (Translate X)", _m31Label).Row(2).Column(0));
        matrixGrid.Add(CreateMatrixCell("M32 (Translate Y)", _m32Label).Row(2).Column(1));

        var matrixBorder = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(10),
            Background = Color.FromRgb(128, 128, 128).WithAlpha(0.04f),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromRgb(128, 128, 128).WithAlpha(0.15f),
            Child = matrixGrid
        };

        var statsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, VerticalAlignment = VerticalAlignment.Center }
            .Children(_detLabel, _invLabel);

        stack.Add(matrixBorder);
        stack.Add(statsRow);

        card.Child = stack;
        return card;
    }

    private UIElement CreatePresetShowcasesCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(20)
            .CornerRadius(12);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        stack.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.DashboardCustomize, 20) { Foreground = Color.FromHex("#EC4899"), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Practical UI Transform Presets").TitleMedium()
            )
        );

        var grid = new Grid()
            .Columns(GridLength.Star, GridLength.Star, GridLength.Star)
            .ColumnSpacing(16);

        // 1. Angled Badge / Sale Ribbon
        var badgeRibbon = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 4),
            Background = Color.FromHex("#EF4444"),
            Child = new TextBlock("NEW 2026")
                .Caption()
                .Bold()
                .Foreground(Color.White)
        }.RotateCenter(-15f);

        var badgeCard = new Card(CardVariant.Filled)
            .Padding(16)
            .CornerRadius(12)
            .ClipToBounds(true)
            .Child(
                new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
                    .Children(
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                            .Children(new Icon(MaterialIconKind.LocalOffer, 18), new TextBlock("Angled Badge").TitleSmall()),
                        new TextBlock("Rotated stamps and corner ribbons for notifications and discounts.").Caption(),
                        badgeRibbon
                    )
            ).Column(0);

        // 2. Isometric Tilt Card
        var isoBox = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Background = Color.FromHex("#10B981").WithAlpha(0.2f),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#10B981"),
            Child = new TextBlock("3D Isometric Plane").Caption().Bold()
        }.Transform(Matrix3x2.CreateSkew(-0.2f, 0.1f) * Matrix3x2.CreateRotation(-0.2f));

        var isoCard = new Card(CardVariant.Filled)
            .Padding(16)
            .CornerRadius(12)
            .ClipToBounds(true)
            .Child(
                new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
                    .Children(
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                            .Children(new Icon(MaterialIconKind.Layers, 18), new TextBlock("Isometric Deck").TitleSmall()),
                        new TextBlock("Combine rotation with shear skew for mockups and game boards.").Caption(),
                        isoBox
                    )
            ).Column(1);

        // 3. Magnified Focus Card
        var magBox = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Background = Color.FromHex("#3B82F6").WithAlpha(0.2f),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#3B82F6"),
            Child = new TextBlock("1.15x Zoom Hover").Caption().Bold()
        }.Scale(1.15f);

        var magCard = new Card(CardVariant.Filled)
            .Padding(16)
            .CornerRadius(12)
            .ClipToBounds(true)
            .Child(
                new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
                    .Children(
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                            .Children(new Icon(MaterialIconKind.Search, 18), new TextBlock("Focal Magnification").TitleSmall()),
                        new TextBlock("Scale elements on hover or selection for modern micro-interactions.").Caption(),
                        magBox
                    )
            ).Column(2);

        grid.Add(badgeCard);
        grid.Add(isoCard);
        grid.Add(magCard);

        stack.Add(grid);
        card.Child = stack;
        return card;
    }
}
