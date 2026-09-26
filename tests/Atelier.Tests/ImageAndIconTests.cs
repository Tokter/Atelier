using System;
using System.ComponentModel;
using SkiaSharp;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Styling;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Rendering;
using Atelier.Theming.Material;
using Atelier.Theming.Material.Renderers;

namespace Atelier.Tests;

public class ImageAndIconTests
{
    private sealed class Presenter(MaterialColorScheme colors) : IElementVisualPresenter
    {
        private readonly MaterialImageRenderer _image = new();
        private readonly MaterialIconRenderer _icon = new(colors);

        public void Render(UIElement element, ref DrawingContext context)
        {
            if (element is Image image) _image.Render(image, ref context);
            else if (element is Icon icon) _icon.Render(icon, ref context);
        }
    }

    private static SKImage SolidImage(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        return SKImage.FromBitmap(bitmap);
    }

    private static SKBitmap RenderTree(UIElement root, int width, int height, MaterialColorScheme? colors = null)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);
        using var registry = new PaintRegistry();
        var context = new DrawingContext(surface.Canvas, registry);
        VisualTreeRenderer.Render(root, ref context, new Presenter(colors ?? MaterialColorScheme.Light()));
        surface.Canvas.Flush();
        using var snapshot = surface.Snapshot();
        return SKBitmap.FromImage(snapshot);
    }

    #region Image

    [Fact]
    public void Opacity_IsAppliedOnce()
    {
        using var source = SolidImage(10, 10, SKColors.Red);
        var image = new Image(source) { Stretch = Stretch.Fill, Width = 20, Height = 20, Opacity = 0.5f };
        var root = new Canvas().Children(image);
        root.Measure(new Size(40, 40));
        root.Arrange(new Rect(0, 0, 40, 40));

        using var bitmap = RenderTree(root, 40, 40);
        var pixel = bitmap.GetPixel(10, 10);

        Assert.InRange(pixel.Alpha, 120, 135); // 0.5, not 0.25
    }

    [Fact]
    public void UniformToFill_IsClippedToBounds()
    {
        using var source = SolidImage(40, 10, SKColors.Blue); // 4:1, overflows horizontally when filling 20x20
        var image = new Image(source) { Stretch = Stretch.UniformToFill, Width = 20, Height = 20 };
        Canvas.SetLeft(image, 10);
        Canvas.SetTop(image, 10);
        var root = new Canvas().Children(image);
        root.Measure(new Size(60, 60));
        root.Arrange(new Rect(0, 0, 60, 60));

        using var bitmap = RenderTree(root, 60, 60);

        Assert.Equal(255, bitmap.GetPixel(20, 20).Alpha); // inside
        Assert.Equal(0, bitmap.GetPixel(5, 20).Alpha);    // left of the element
        Assert.Equal(0, bitmap.GetPixel(40, 20).Alpha);   // right of the element
    }

    [Fact]
    public void Padding_InsetsTheImage()
    {
        using var source = SolidImage(10, 10, SKColors.Green);
        var image = new Image(source) { Stretch = Stretch.Fill, Width = 30, Height = 30, Padding = new Thickness(5) };
        var root = new Canvas().Children(image);
        root.Measure(new Size(40, 40));
        root.Arrange(new Rect(0, 0, 40, 40));

        using var bitmap = RenderTree(root, 40, 40);

        Assert.Equal(0, bitmap.GetPixel(2, 15).Alpha);
        Assert.Equal(255, bitmap.GetPixel(15, 15).Alpha);

        var measured = new Image(source) { Stretch = Stretch.None, Padding = new Thickness(4, 2) };
        measured.Measure(new Size(100, 100));
        Assert.Equal(new Size(18, 14), measured.DesiredSize);
    }

    [Theory]
    [InlineData(StretchDirection.Both, 100f)]
    [InlineData(StretchDirection.DownOnly, 10f)]
    [InlineData(StretchDirection.UpOnly, 100f)]
    public void StretchDirection_LimitsUpscaling(StretchDirection direction, float expected)
    {
        using var small = SolidImage(10, 10, SKColors.Red);
        var image = new Image(small) { Stretch = Stretch.Uniform, StretchDirection = direction };
        image.Measure(new Size(100, 100));

        Assert.Equal(expected, image.DesiredSize.Width);
    }

    [Theory]
    [InlineData(StretchDirection.Both, 100f)]
    [InlineData(StretchDirection.DownOnly, 100f)]
    [InlineData(StretchDirection.UpOnly, 200f)]
    public void StretchDirection_LimitsDownscaling(StretchDirection direction, float expected)
    {
        using var large = SolidImage(200, 200, SKColors.Red);
        var image = new Image(large) { Stretch = Stretch.Uniform, StretchDirection = direction };
        image.Measure(new Size(100, 100));

        Assert.Equal(expected, image.DesiredSize.Width);
    }

    [Fact]
    public void OwnedSource_IsDisposedWhenReplaced_ButAssignedSourceIsNot()
    {
        using var bitmap = new SKBitmap(4, 4);
        var image = new Image(bitmap);
        var owned = image.Source!;

        using var external = SolidImage(4, 4, SKColors.Red);
        image.Source = external;
        Assert.Equal(IntPtr.Zero, owned.Handle);

        image.Source = null;
        Assert.NotEqual(IntPtr.Zero, external.Handle);
    }

    #endregion

    #region Icon

    [Fact]
    public void AxisClamping_AppliesToSetValueBindingsAndStyles()
    {
        var icon = new Icon();
        icon.SetValue(Icon.FillProperty, 5f);
        icon.SetValue(Icon.WeightProperty, 50f);
        icon.SetValue(Icon.GradeProperty, 999f);
        icon.SetValue(Icon.OpticalSizeProperty, 5f);
        icon.SetValue(Icon.StrokeWidthProperty, -3f);

        Assert.Equal(1f, icon.Fill);
        Assert.Equal(100f, icon.Weight);
        Assert.Equal(200f, icon.Grade);
        Assert.Equal(20f, icon.OpticalSize);
        Assert.Equal(0f, icon.StrokeWidth);

        var vm = new MaterialIconTests.IconBindingTestVM();
        var bound = new Icon();
        bound.SetBinding(Icon.FillProperty, vm, x => x.Fill);
        vm.Fill = 7f;
        Assert.Equal(1f, bound.Fill);

        var panel = new StackPanel();
        panel.Styles.Add(new Style(typeof(Icon)).Set(Icon.WeightProperty, 2000f));
        var styled = new Icon();
        panel.Add(styled);
        Assert.Equal(700f, styled.Weight);
    }

    [Fact]
    public void OpticalSize_FollowsSize_UntilSetExplicitly()
    {
        var icon = new Icon();
        Assert.Equal(24f, icon.OpticalSize);

        icon.Size = 40;
        Assert.Equal(40f, icon.OpticalSize);
        icon.Size = 100;
        Assert.Equal(48f, icon.OpticalSize);
        icon.Size = 16;
        Assert.Equal(20f, icon.OpticalSize);

        // An explicit 24 is kept even though it equals the default.
        icon.OpticalSize = 24f;
        icon.Size = 40;
        Assert.Equal(24f, icon.OpticalSize);

        icon.ClearValue(Icon.OpticalSizeProperty);
        Assert.Equal(40f, icon.OpticalSize);
    }

    [Fact]
    public void PathData_Replacement_DisposesOwnedPath_ButNotAssignedPath()
    {
        var icon = new Icon("M0 0h10v10H0z");
        var parsed = icon.Data!;

        icon.PathData = "M0 0h5v5H0z";
        Assert.Equal(IntPtr.Zero, parsed.Handle);
        Assert.NotNull(icon.Data);

        using var builder = new SKPathBuilder();
        builder.AddRect(new SKRect(0, 0, 3, 3));
        using var external = builder.Detach();
        icon.Data = external;
        icon.Data = null;
        Assert.NotEqual(IntPtr.Zero, external.Handle);
    }

    [Fact]
    public void TypefaceCache_StaysBounded_UnderAnimatedAxes()
    {
        for (float fill = 0f; fill <= 1f; fill += 0.001f)
        {
            MaterialIconFontManager.GetTypeface(fill: fill, weight: 400, grade: 0, opticalSize: 24);
        }
        for (float weight = 100f; weight <= 700f; weight += 1f)
        {
            MaterialIconFontManager.GetTypeface(fill: 1f, weight: weight, grade: 0, opticalSize: 24);
        }

        Assert.True(MaterialIconFontManager.CachedTypefaceCount <= MaterialIconFontManager.MaxCachedTypefaces);

        // Evicted typefaces are recreated on demand and still usable.
        var tf = MaterialIconFontManager.GetTypeface(fill: 0.5f, weight: 300);
        using var font = new SKFont(tf, 24);
        Assert.True(font.MeasureText(MaterialIconFontManager.GetGlyph(MaterialIconKind.Home)) > 0);
    }

    [Fact]
    public void GetGlyph_IsCachedPerKind()
    {
        var first = MaterialIconFontManager.GetGlyph(MaterialIconKind.Star);
        Assert.Same(first, MaterialIconFontManager.GetGlyph(MaterialIconKind.Star));
        Assert.Equal(char.ConvertFromUtf32((int)MaterialIconKind.Star), first);
    }

    [Fact]
    public void IconRenderer_HonorsExplicitBlack_AndUsesThemeColorWhenUnset()
    {
        var dark = MaterialColorScheme.Dark();

        var unset = new Icon("M0 0h24v24H0z", size: 20);
        var root1 = new Canvas().Children(unset);
        root1.Measure(new Size(20, 20));
        root1.Arrange(new Rect(0, 0, 20, 20));
        using (var bitmap = RenderTree(root1, 20, 20, dark))
        {
            var pixel = bitmap.GetPixel(10, 10);
            Assert.Equal(new SKColor(dark.OnSurface.R, dark.OnSurface.G, dark.OnSurface.B, 255), pixel);
        }

        var black = new Icon("M0 0h24v24H0z", size: 20, foreground: Color.Black);
        var root2 = new Canvas().Children(black);
        root2.Measure(new Size(20, 20));
        root2.Arrange(new Rect(0, 0, 20, 20));
        using (var bitmap = RenderTree(root2, 20, 20, dark))
        {
            Assert.Equal(new SKColor(0, 0, 0, 255), bitmap.GetPixel(10, 10));
        }
    }

    #endregion
}
