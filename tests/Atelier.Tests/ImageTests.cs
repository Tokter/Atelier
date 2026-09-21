using System;
using SkiaSharp;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;
using Atelier.Theming;
using Atelier.Theming.Material;
using Atelier.Theming.Material.Renderers;

namespace Atelier.Tests;

public class ImageTests
{
    [Fact]
    public void Image_LoadImage_LoadsAtelierPngSuccessfully()
    {
        using var img = Image.LoadImage("Assets/Icons/Atelier.png");
        Assert.NotNull(img);
        Assert.True(img.Width > 0);
        Assert.True(img.Height > 0);
    }

    [Fact]
    public void Image_LoadBitmap_LoadsAtelierPngSuccessfully()
    {
        using var bmp = Image.LoadBitmap("Assets/Icons/Atelier.png");
        Assert.NotNull(bmp);
        Assert.True(bmp.Width > 0);
        Assert.True(bmp.Height > 0);
    }

    [Fact]
    public void Image_Measure_UniformPreservesAspectRatio()
    {
        // 100x50 image (2:1 aspect ratio)
        using var bmp = new SKBitmap(100, 50);
        using var skImg = SKImage.FromBitmap(bmp);

        var image = new Image(skImg) { Stretch = Stretch.Uniform };
        image.Measure(new Size(200, 200));

        // Aspect ratio 2:1 inside 200x200 constraint should yield 200x100
        Assert.Equal(200, image.DesiredSize.Width);
        Assert.Equal(100, image.DesiredSize.Height);
    }

    [Fact]
    public void Image_Measure_FillStretchesToAvailable()
    {
        using var bmp = new SKBitmap(100, 50);
        using var skImg = SKImage.FromBitmap(bmp);

        var image = new Image(skImg) { Stretch = Stretch.Fill };
        image.Measure(new Size(200, 200));

        Assert.Equal(200, image.DesiredSize.Width);
        Assert.Equal(200, image.DesiredSize.Height);
    }

    [Fact]
    public void Image_Measure_NonePreservesNaturalSize()
    {
        using var bmp = new SKBitmap(100, 50);
        using var skImg = SKImage.FromBitmap(bmp);

        var image = new Image(skImg) { Stretch = Stretch.None };
        image.Measure(new Size(200, 200));

        Assert.Equal(100, image.DesiredSize.Width);
        Assert.Equal(50, image.DesiredSize.Height);
    }

    [Fact]
    public void TitleBar_WithImageControl_RendersIcon()
    {
        var image = new Image("Assets/Icons/Atelier.png") { Width = 22, Height = 22 };
        var titleBar = new TitleBar
        {
            Title = "Atelier Test",
            Icon = image
        };

        titleBar.Measure(new Size(800, 44));
        titleBar.Arrange(new Rect(0, 0, 800, 44));

        Assert.Equal(image, titleBar.Icon);
        Assert.Equal(Visibility.Visible, image.Visibility);
        Assert.True(titleBar.Bounds.Width > 0);
    }

    [Fact]
    public void TitleBar_WithStringImagePath_AutomaticallyCreatesImageControl()
    {
        var titleBar = new TitleBar
        {
            Title = "Atelier Test",
            Icon = "Assets/Icons/Atelier.png"
        };

        titleBar.Measure(new Size(800, 44));
        titleBar.Arrange(new Rect(0, 0, 800, 44));

        Assert.NotNull(titleBar.Icon);
    }

    [Fact]
    public void MaterialImageRenderer_RendersToCanvasWithoutError()
    {
        using var bmp = new SKBitmap(40, 40);
        using var skImg = SKImage.FromBitmap(bmp);

        var image = new Image(skImg) { Width = 40, Height = 40 };
        image.Measure(new Size(40, 40));
        image.Arrange(new Rect(0, 0, 40, 40));

        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var paintRegistry = new PaintRegistry();
        var context = new DrawingContext(surface.Canvas, paintRegistry);

        var renderer = new MaterialImageRenderer();
        renderer.Render(image, ref context);
    }
}
