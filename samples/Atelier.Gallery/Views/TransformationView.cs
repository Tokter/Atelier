using System;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class TransformationView : GalleryPage
{
    private readonly TransformationViewModel _vm;
    private readonly Border _zoomPreview;

    public TransformationView(TransformationViewModel viewModel)
        : base(MaterialIconKind.CropRotate, "Transform & Zoom",
            "Any element can be rotated, scaled, skewed and moved. A render transform only changes how the element is " +
            "drawn; a layout transform also changes the space it takes. Transformed elements stay fully interactive.")
    {
        _vm = viewModel;

        Settings(new Button("Reset transform").Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        _zoomPreview = ZoomPreview();
        Sections(PlaygroundSection(), ShortcutsSection(), ZoomSection());

        // Zoom applies to this page's preview or to the whole window (the root of the tree this page is shown in).
        this.OnAttachedToVisualTree(() => { _vm.PropertyChanged += OnViewModelChanged; ApplyZoom(); });
        this.OnDetachedFromVisualTree(() => _vm.PropertyChanged -= OnViewModelChanged);
    }

    private UIElement PlaygroundSection()
    {
        var target = new Card(CardVariant.Elevated)
            .Width(240)
            .Padding(16)
            .Bind(VisualNode.RenderTransformProperty, _vm, v => v.UseLayoutTransform ? Matrix3x2.Identity : v.Matrix)
            .Bind(VisualNode.TransformProperty, _vm, v => v.UseLayoutTransform ? v.Matrix : Matrix3x2.Identity)
            .Bind(VisualNode.RenderTransformOriginProperty, _vm, v => v.Origin)
            .Bind(VisualNode.TransformOriginProperty, _vm, v => v.Origin)
            .Child(new StackPanel().Spacing(10).Children(
                new TextBlock("Transformed card").TitleMedium(),
                new Button("Click me").Command(_vm.CardClickedCommand),
                new Slider().BindValue(_vm, v => v.CardSliderValue, (v, x) => v.CardSliderValue = x),
                new Switch("Still interactive").BindIsChecked(_vm, v => v.CardSwitch, (v, on) => v.CardSwitch = on)));

        // Neighbors show the difference: a layout transform pushes them away, a render transform draws over them.
        var stage = new Border()
            .MinHeight(340)
            .Padding(24)
            .CornerRadius(12)
            .ClipToBounds()
            .Themed(Border.BackgroundProperty, c => c.SurfaceContainer)
            .Child(new StackPanel()
                .Orientation(Orientation.Horizontal)
                .Spacing(16)
                .Center()
                .Children(Neighbor(), target.VerticalAlignment(VerticalAlignment.Center), Neighbor()));

        return Ui.Section("Transform playground",
            "Change the transform of the card and compare both kinds. The origin is the point the card rotates and scales " +
            "around. Clicks, drags and keyboard input still reach the transformed controls.",
            Ui.Row(
                new RadioButton("Render transform").GroupName("transform-kind")
                    .BindIsChecked(_vm, v => v.UseLayoutTransform, (v, x) => v.UseLayoutTransform = x, false),
                new RadioButton("Layout transform").GroupName("transform-kind")
                    .BindIsChecked(_vm, v => v.UseLayoutTransform, (v, x) => v.UseLayoutTransform = x, true)),
            Ui.Columns(200,
                Ui.SliderSetting("Rotation (°)", _vm, v => v.Rotation, (v, x) => v.Rotation = x, -180, 180),
                Ui.SliderSetting("Scale X", _vm, v => v.ScaleX, (v, x) => v.ScaleX = x, 0.25f, 2, "0.00"),
                Ui.SliderSetting("Scale Y", _vm, v => v.ScaleY, (v, x) => v.ScaleY = x, 0.25f, 2, "0.00"),
                Ui.SliderSetting("Skew X (°)", _vm, v => v.SkewX, (v, x) => v.SkewX = x, -45, 45),
                Ui.SliderSetting("Skew Y (°)", _vm, v => v.SkewY, (v, x) => v.SkewY = x, -45, 45),
                Ui.SliderSetting("Translate X", _vm, v => v.TranslateX, (v, x) => v.TranslateX = x, -120, 120),
                Ui.SliderSetting("Translate Y", _vm, v => v.TranslateY, (v, x) => v.TranslateY = x, -120, 120),
                new Switch("Uniform scale").BindIsChecked(_vm, v => v.UniformScale, (v, on) => v.UniformScale = on).VerticalAlignment(VerticalAlignment.Center)),
            Ui.Labeled("Origin", Ui.Row(Enum.GetValues<TransformOriginPreset>()
                .Select(o => (UIElement)new RadioButton(o.ToString()).GroupName("transform-origin")
                    .BindIsChecked(_vm, v => v.OriginPreset, (v, x) => v.OriginPreset = x, o))
                .ToArray())),
            Ui.Labeled("Presets", Ui.Row(
                new Button("Tilt").Variant(ButtonVariant.Outlined).Command(_vm.PresetCommand, "Tilt"),
                new Button("Shear").Variant(ButtonVariant.Outlined).Command(_vm.PresetCommand, "Shear"),
                new Button("Stamp").Variant(ButtonVariant.Outlined).Command(_vm.PresetCommand, "Stamp"),
                new Button("Stretch").Variant(ButtonVariant.Outlined).Command(_vm.PresetCommand, "Stretch"))),
            stage,
            Ui.Row(
                Ui.Readout(_vm, v => $"Matrix {v.MatrixText}"),
                Ui.Readout(_vm, v => $"Clicks: {v.ClickCount}"),
                Ui.Readout(_vm, v => $"Slider: {v.CardSliderValue:0}"),
                Ui.Readout(_vm, v => $"Switch: {(v.CardSwitch ? "on" : "off")}")));
    }

    private UIElement ShortcutsSection() => Ui.Section("Transform shortcuts",
        "Markup methods for the common transforms. Render* methods set the render transform around the element's center; " +
        "the others set the layout transform.",
        Ui.Row(
            Shortcut(".RenderRotate(15)", Tile().RenderRotate(15)),
            Shortcut(".RenderScale(1.25)", Tile().RenderScale(1.25f)),
            Shortcut(".RenderSkew(20, 0)", Tile().RenderSkew(20, 0)),
            Shortcut(".RenderTranslate(16, 12)", Tile().RenderTranslate(16, 12)),
            Shortcut(".Rotate(90)", Tile().Rotate(90)),
            Shortcut(".RotateCenter(45)", Tile().RotateCenter(45)),
            Shortcut(".Scale(0.75)", Tile().Scale(0.75f))),
        Ui.Code("new Border().RenderTransform(Matrix3x2.CreateRotation(angle)).RenderTransformOrigin(0, 0)\n" +
                "new Border().Transform(Matrix3x2.CreateScale(2)).TransformOrigin(0.5f, 0.5f)"));

    private UIElement ZoomSection() => Ui.Section("Zoom",
        "A layout transform on a container zooms everything inside it, text and hit-testing included. Applied to the " +
        "window's root element, it zooms the whole window, for example for accessibility.",
        Ui.Row(
            Ui.SliderSetting("Zoom", _vm, v => v.Zoom, (v, x) => v.Zoom = x, 0.5f, 2, "0.00"),
            new Button("100%").Variant(ButtonVariant.Outlined).Command(_vm.SetZoomCommand, "100"),
            new Button("125%").Variant(ButtonVariant.Outlined).Command(_vm.SetZoomCommand, "125"),
            new Button("150%").Variant(ButtonVariant.Outlined).Command(_vm.SetZoomCommand, "150"),
            new Switch("Zoom the whole window").BindIsChecked(_vm, v => v.ZoomWholeWindow, (v, on) => v.ZoomWholeWindow = on)),
        new Border()
            .Padding(16)
            .CornerRadius(12)
            .ClipToBounds()
            .Themed(Border.BackgroundProperty, c => c.SurfaceContainer)
            .Child(_zoomPreview));

    private Border ZoomPreview() =>
        new Border()
            .HorizontalAlignment(HorizontalAlignment.Left)
            .TransformOrigin(0, 0)
            .Child(new StackPanel().Spacing(12).Children(
                new TextBlock("Sign in").TitleMedium(),
                new TextBox().Label("Email").Width(260),
                Ui.Row(new CheckBox("Remember me"), new Button("Continue"))));

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TransformationViewModel.Zoom) or nameof(TransformationViewModel.ZoomWholeWindow))
        {
            ApplyZoom();
        }
    }

    private void ApplyZoom()
    {
        var scale = Matrix3x2.CreateScale(_vm.Zoom);
        VisualNode root = this;
        while (root.Parent != null)
        {
            root = root.Parent;
        }

        if (_vm.ZoomWholeWindow && root != this)
        {
            root.Transform = scale;
            _zoomPreview.Transform = Matrix3x2.Identity;
        }
        else
        {
            if (root != this)
            {
                root.Transform = Matrix3x2.Identity;
            }
            _zoomPreview.Transform = scale;
        }
    }

    private static UIElement Shortcut(string code, UIElement tile) =>
        new StackPanel().Spacing(8).Width(150).VerticalAlignment(VerticalAlignment.Top).Children(
            new Border()
                .Size(150, 120)
                .HorizontalAlignment(HorizontalAlignment.Left)
                .CornerRadius(12)
                .Themed(Border.BackgroundProperty, c => c.SurfaceContainer)
                .Child(tile.Center()),
            new TextBlock(code).FontFamily(Ui.MonospaceFont).FontSize(12).Muted().TextWrapping());

    private static Border Tile() =>
        new Border()
            .Size(64, 48)
            .CornerRadius(8)
            .Themed(Border.BackgroundProperty, c => c.PrimaryContainer)
            .Child(new Icon(MaterialIconKind.ArrowUpward, 30).Center().Themed(Control.ForegroundProperty, c => c.OnPrimaryContainer));

    private static Border Neighbor() =>
        new Border()
            .Size(72, 120)
            .CornerRadius(8)
            .VerticalAlignment(VerticalAlignment.Center)
            .Themed(Border.BackgroundProperty, c => c.SecondaryContainer)
            .Child(new TextBlock("Neighbor").LabelMedium().Center().Themed(TextBlock.ForegroundProperty, c => c.OnSecondaryContainer));
}
