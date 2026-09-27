using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;
using SkiaSharp;

namespace Atelier.Gallery.Views;

public class IconsView : GalleryPage
{
    // A house outline in SVG path syntax (24×24 viewbox).
    private const string HouseSvg = "M3 10.5 12 3l9 7.5V21h-6v-6H9v6H3z";

    private static readonly Lazy<SKPath> StarPath = new(() => CreateStarPath(12, 12, 10.5f, 4.5f, 5));

    private static readonly MaterialIconKind[] QuickPicks =
    [
        MaterialIconKind.Favorite, MaterialIconKind.Home, MaterialIconKind.Settings, MaterialIconKind.Star,
        MaterialIconKind.Search, MaterialIconKind.Cloud, MaterialIconKind.Bolt, MaterialIconKind.Pets,
        MaterialIconKind.Rocket, MaterialIconKind.Palette,
    ];

    private readonly IconsViewModel _vm;
    private readonly WrapPanel _catalog = new WrapPanel().Spacing(8, 8);

    public IconsView(IconsViewModel viewModel)
        : base(MaterialIconKind.Mood, "Icons & Images",
            "Icon draws Material Symbols glyphs with their four variable font axes, or your own vector paths. Image shows " +
            "bitmaps with different stretch modes.")
    {
        _vm = viewModel;

        Settings(
            new Button("Toggle filled").Variant(ButtonVariant.Tonal).Command(_vm.ToggleFilledCommand),
            new Button("Reset").Variant(ButtonVariant.Outlined).Command(_vm.ResetCommand));

        Sections(PlaygroundSection(), AxesSection(), SizeAndColorSection(), CustomGeometrySection(), CatalogSection(), ImageSection());

        // The catalog is rebuilt when the search changes, only while the page is shown.
        _catalog.OnAttachedToVisualTree(() => { _vm.CatalogChanged += RebuildCatalog; RebuildCatalog(); });
        _catalog.OnDetachedFromVisualTree(() => _vm.CatalogChanged -= RebuildCatalog);
        RebuildCatalog();
    }

    #region Playground

    private UIElement PlaygroundSection()
    {
        var preview = new Icon()
            .Center()
            .BindKind(_vm, v => v.Kind)
            .BindFill(_vm, v => v.Fill)
            .BindWeight(_vm, v => v.Weight)
            .BindGrade(_vm, v => v.Grade)
            .BindSize(_vm, v => v.Size)
            .BindColorRole(Control.ForegroundProperty, _vm, nameof(IconsViewModel.ColorRole), v => v.ColorRole);
        FollowOpticalSize(preview);

        return Ui.Section("Playground",
            "Change the glyph and its axes. Fill morphs between outlined and filled, weight sets the stroke thickness, grade " +
            "fine-tunes it without changing the size, and optical size adapts the detail to the display size.",
            Ui.Columns(320,
                Ui.Stack(
                    new Border()
                        .Height(200)
                        .CornerRadius(16)
                        .Themed(Border.BackgroundProperty, c => c.SurfaceContainerHigh)
                        .Child(preview),
                    Ui.Row(Array.ConvertAll(QuickPicks, QuickPickButton)),
                    Ui.Row(
                        Ui.Readout(_vm, v => $"Kind = {v.Kind}"),
                        Ui.Readout(_vm, v => $"Weight = {v.Weight:0} ({v.WeightName})"),
                        Ui.Readout(_vm, v => $"OpticalSize = {(v.AutoOpticalSize ? "auto" : v.OpticalSize.ToString("0"))}"))),
                Ui.Stack(
                    Ui.SliderSetting("Fill", _vm, v => v.Fill, (v, x) => v.Fill = x, 0, 1, "0.00"),
                    Ui.SliderSetting("Weight", _vm, v => v.Weight, (v, x) => v.Weight = x, 100, 700),
                    Ui.SliderSetting("Grade", _vm, v => v.Grade, (v, x) => v.Grade = x, -25, 200),
                    new Switch("Optical size follows the size").BindIsChecked(_vm, v => v.AutoOpticalSize, (v, on) => v.AutoOpticalSize = on),
                    Ui.SliderSetting("Optical size", _vm, v => v.OpticalSize, (v, x) => v.OpticalSize = x, 20, 48)
                        .BindIsEnabled(_vm, v => !v.AutoOpticalSize),
                    Ui.SliderSetting("Size", _vm, v => v.Size, (v, x) => v.Size = x, 16, 160),
                    Ui.Labeled("Color", Ui.Row(Array.ConvertAll(Enum.GetValues<ColorRole>(), ColorOption))))),
            Ui.Code("new Icon(MaterialIconKind.Favorite, 48)\n    .Fill(1).Weight(600).Grade(0).OpticalSize(48)\n    .Themed(Control.ForegroundProperty, c => c.Primary)"));
    }

    // An icon-only button: the theme's button padding is meant for text labels, so icon buttons set their own.
    private Button QuickPickButton(MaterialIconKind kind) =>
        new Button()
            .Padding(8)
            .MinWidth(0)
            .BindVariant(_vm, v => v.Kind == kind ? ButtonVariant.Tonal : ButtonVariant.Text)
            .Command(_vm.SelectIconCommand, kind)
            .Content(new Icon(kind, 22));

    private RadioButton ColorOption(ColorRole role) =>
        new RadioButton(ColorRoleBinding.DisplayName(role))
            .GroupName("icon-color")
            .BindIsChecked(_vm, v => v.ColorRole, (v, r) => v.ColorRole = r, role);

    // OpticalSize follows Size while it has no value of its own, so "automatic" clears it instead of binding it.
    private void FollowOpticalSize(Icon icon)
    {
        void Apply()
        {
            if (_vm.AutoOpticalSize)
            {
                icon.ClearValue(Icon.OpticalSizeProperty);
            }
            else
            {
                icon.OpticalSize = _vm.OpticalSize;
            }
        }

        void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(IconsViewModel.AutoOpticalSize) or nameof(IconsViewModel.OpticalSize))
            {
                Apply();
            }
        }

        Apply();
        icon.OnAttachedToVisualTree(() => { _vm.PropertyChanged += OnChanged; Apply(); });
        icon.OnDetachedFromVisualTree(() => _vm.PropertyChanged -= OnChanged);
    }

    #endregion

    #region Axes, sizes and colors

    private static UIElement AxesSection() => Ui.Section("Variable font axes",
        "The same glyph along each axis. All four can be animated and bound like any other property.",
        Ui.Demo("Weight: 100 to 700", AxisRow(w => new Icon(MaterialIconKind.Settings, 40).Weight(w), 100, 200, 300, 400, 500, 600, 700)),
        Ui.Demo("Fill: 0 to 1", AxisRow(f => new Icon(MaterialIconKind.Favorite, 40).Fill(f), 0, 0.25f, 0.5f, 0.75f, 1)),
        Ui.Demo("Grade: -25 to 200", AxisRow(g => new Icon(MaterialIconKind.Star, 40).Grade(g), -25, 0, 100, 200)),
        Ui.Demo("Optical size at 40 px: 20 to 48", AxisRow(o => new Icon(MaterialIconKind.Pets, 40).OpticalSize(o), 20, 24, 40, 48)));

    private static WrapPanel AxisRow(Func<float, Icon> create, params float[] values) =>
        Ui.Row(Array.ConvertAll(values, value => (UIElement)new StackPanel().Spacing(4).Width(72).Children(
            create(value).HorizontalAlignment(HorizontalAlignment.Center),
            new TextBlock(value.ToString("0.##", CultureInfo.InvariantCulture)).LabelMedium().Muted()
                .HorizontalAlignment(HorizontalAlignment.Center))));

    private static UIElement SizeAndColorSection() => Ui.Section("Size and color",
        "Size sets the width and height. Without a foreground of its own, an icon uses the color of its container, like " +
        "the label of a button; set Foreground (here with theme colors) to color it explicitly.",
        Ui.Demo("Sizes",
            Ui.Row(Array.ConvertAll(new float[] { 16, 20, 24, 32, 40, 48, 64 },
                s => (UIElement)new Icon(MaterialIconKind.Rocket, s).VerticalAlignment(VerticalAlignment.Bottom)))),
        Ui.Demo("Theme colors",
            Ui.Row(
                new Icon(MaterialIconKind.Circle, 32).Fill(1),
                new Icon(MaterialIconKind.Circle, 32).Fill(1).Themed(Control.ForegroundProperty, c => c.Primary),
                new Icon(MaterialIconKind.Circle, 32).Fill(1).Themed(Control.ForegroundProperty, c => c.Secondary),
                new Icon(MaterialIconKind.Circle, 32).Fill(1).Themed(Control.ForegroundProperty, c => c.Tertiary),
                new Icon(MaterialIconKind.Circle, 32).Fill(1).Themed(Control.ForegroundProperty, c => c.Error),
                new Icon(MaterialIconKind.Circle, 32).Fill(1).Themed(Control.ForegroundProperty, c => c.OutlineVariant))),
        Ui.Demo("Inherited content color",
            Ui.Row(
                Ui.IconButton(MaterialIconKind.Send, "Filled"),
                Ui.IconButton(MaterialIconKind.Edit, "Tonal", ButtonVariant.Tonal),
                Ui.IconButton(MaterialIconKind.Share, "Outlined", ButtonVariant.Outlined),
                Ui.IconButton(MaterialIconKind.Delete, "Disabled", ButtonVariant.Filled).IsEnabled(false))));

    #endregion

    #region Custom geometry

    private UIElement CustomGeometrySection() => Ui.Section("Custom vector icons",
        "PathData takes SVG path data and Data takes an SKPath, both in a 24×24 viewbox. They are filled, or stroked " +
        "with StrokeWidth. ToIcon() creates icons from glyphs and paths.",
        Ui.Columns(260,
            Ui.Demo("SVG path data",
                Ui.Row(
                    new Icon(HouseSvg, 48).Themed(Control.ForegroundProperty, c => c.Primary),
                    new Icon(HouseSvg, 48).BindStrokeWidth(_vm, v => v.StrokeWidth).Themed(Control.ForegroundProperty, c => c.Primary))),
            Ui.Demo("SKPath built in code",
                Ui.Row(
                    StarPath.Value.ToIcon(48).Themed(Control.ForegroundProperty, c => c.Tertiary),
                    new Icon().Data(StarPath.Value).Size(48).BindStrokeWidth(_vm, v => v.StrokeWidth)
                        .Themed(Control.ForegroundProperty, c => c.Tertiary))),
            Ui.Demo("ToIcon()",
                Ui.Row(
                    MaterialIconKind.Home.ToIcon(32),
                    MaterialIconKind.Home.ToIcon(32, isFilled: true),
                    MaterialIconKind.Notifications.ToIcon(32, isFilled: true, foreground: ThemeColors.Current.Secondary)))),
        Ui.SliderSetting("Stroke width of the stroked icons", _vm, v => v.StrokeWidth, (v, x) => v.StrokeWidth = x, 0.5f, 4, "0.0"),
        Ui.Code($"new Icon(\"{HouseSvg}\", 48).StrokeWidth(2)\nnew Icon().Data(path).Size(48)   // an SKPath; the icon doesn't dispose it"));

    private static SKPath CreateStarPath(float cx, float cy, float outer, float inner, int points)
    {
        var svg = new StringBuilder();
        for (int i = 0; i < points * 2; i++)
        {
            float radius = i % 2 == 0 ? outer : inner;
            double angle = Math.PI * i / points - Math.PI / 2;
            svg.Append(i == 0 ? 'M' : 'L')
               .Append((cx + radius * Math.Cos(angle)).ToString("0.###", CultureInfo.InvariantCulture)).Append(' ')
               .Append((cy + radius * Math.Sin(angle)).ToString("0.###", CultureInfo.InvariantCulture)).Append(' ');
        }
        return SKPath.ParseSvgPathData(svg.Append('Z').ToString());
    }

    #endregion

    #region Catalog

    private UIElement CatalogSection() => Ui.Section("Catalog",
        $"All {IconsViewModel.TotalIconCount:N0} Material Symbols (rounded). Search by name and click an icon to load it " +
        "into the playground.",
        new Grid().Columns(GridLength.Star, GridLength.Auto).ColumnSpacing(12).Children(
            new TextBox()
                .LeadingIconKind(MaterialIconKind.Search)
                .Placeholder("Search icons, e.g. arrow, cloud, person")
                .BindText(_vm, v => v.SearchText, (v, text) => v.SearchText = text),
            new Button("Clear").Variant(ButtonVariant.Outlined).Column(1).VerticalAlignment(VerticalAlignment.Center)
                .Command(_vm.ClearSearchCommand)),
        new TextBlock().BodySmall().Muted().BindText(_vm, v => v.SearchStatus),
        _catalog,
        new Button("Show more").Variant(ButtonVariant.Tonal).HorizontalAlignment(HorizontalAlignment.Center)
            .Command(_vm.ShowMoreCommand)
            .BindIsVisible(_vm, v => v.HasMore));

    private void RebuildCatalog()
    {
        _catalog.Clear();
        foreach (var item in _vm.VisibleIcons)
        {
            _catalog.Add(CatalogTile(item));
        }
    }

    private Button CatalogTile(IconItemInfo item)
    {
        var kind = item.Kind;
        return new Button()
            .Size(108, 84)
            .Padding(6)
            .CornerRadius(12)
            .BindVariant(_vm, v => v.Kind == kind ? ButtonVariant.Tonal : ButtonVariant.Text)
            .Command(_vm.SelectIconCommand, kind)
            .Content(new StackPanel().Spacing(6).Children(
                new Icon(kind, 28).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock(item.Name)
                    .LabelSmall()
                    .FontWeight(FontWeight.Normal)
                    .TextTrimming()
                    .HorizontalAlignment(HorizontalAlignment.Center)));
    }

    #endregion

    #region Images

    private static UIElement ImageSection() => Ui.Section("Images",
        "Image shows an SKImage, or loads one from a file or embedded resource with Source(path). Stretch decides how it " +
        "fills its box, StretchDirection whether it may be scaled up or down. Put it in a Border with ClipToBounds for " +
        "rounded corners.",
        Ui.Demo("Stretch (a square image in a wide box)",
            Ui.Row(
                StretchSample(Stretch.None, "None"),
                StretchSample(Stretch.Fill, "Fill"),
                StretchSample(Stretch.Uniform, "Uniform (default)"),
                StretchSample(Stretch.UniformToFill, "UniformToFill"))),
        Ui.Demo("StretchDirection (120 px boxes)",
            Ui.Row(
                DirectionSample(SampleImages.AppIcon, StretchDirection.Both, "Both: 64 px icon scaled up"),
                DirectionSample(SampleImages.AppIcon, StretchDirection.DownOnly, "DownOnly: icon stays 64 px"),
                DirectionSample(SampleImages.Logo, StretchDirection.UpOnly, "UpOnly: large logo not shrunk"))),
        Ui.Demo("Shape, padding and opacity",
            Ui.Row(
                Captioned("Rounded (Border + ClipToBounds)", new Border().Size(120, 120).CornerRadius(24).ClipToBounds().Child(
                    new Image().Source(SampleImages.Logo).Stretch(Stretch.UniformToFill))),
                Captioned("Circle", new Border().Size(120, 120).CornerRadius(60).ClipToBounds().Child(
                    new Image().Source(SampleImages.Logo).Stretch(Stretch.UniformToFill))),
                Captioned("Padding(20)", Frame(new Image().Source(SampleImages.Logo).Padding(20)).Size(120, 120)),
                Captioned("Opacity(0.4)", Frame(new Image().Source(SampleImages.Logo).Opacity(0.4f)).Size(120, 120)))),
        Ui.Code("new Image().Source(\"Assets/Logo.png\").Stretch(Stretch.UniformToFill).Size(160, 100)"));

    private static UIElement StretchSample(Stretch stretch, string label) =>
        Captioned(label, Frame(new Image().Source(SampleImages.Logo).Stretch(stretch)).Size(180, 100));

    private static UIElement DirectionSample(SKImage? source, StretchDirection direction, string label) =>
        Captioned(label, Frame(new Image().Source(source).StretchDirection(direction)).Size(120, 120));

    // A tinted, clipped frame that makes the image's box visible.
    private static Border Frame(Image image) =>
        new Border()
            .CornerRadius(8)
            .ClipToBounds()
            .BorderThickness(1)
            .Themed(Border.BackgroundProperty, c => c.SurfaceContainerHigh)
            .Themed(Border.BorderBrushProperty, c => c.OutlineVariant)
            .Child(image);

    private static UIElement Captioned(string caption, UIElement content) =>
        new StackPanel().Spacing(6).Children(content, new TextBlock(caption).LabelMedium().Muted());

    #endregion
}
