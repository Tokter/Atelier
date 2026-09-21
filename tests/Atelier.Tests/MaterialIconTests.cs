using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Markup;
using Atelier.Rendering;
using Atelier.Theming.Material;
using Atelier.Theming.Material.Renderers;
using SkiaSharp;
using Xunit;

namespace Atelier.Tests;

public class MaterialIconTests
{
    [Fact]
    public void FontManager_LoadsEmbeddedFont_ReturnsValidTypeface()
    {
        var baseTypeface = MaterialIconFontManager.BaseTypeface;
        Assert.NotNull(baseTypeface);
        Assert.NotEqual(IntPtr.Zero, baseTypeface.Handle);

        var typeface = MaterialIconFontManager.GetTypeface(fill: 0, weight: 400, grade: 0, opticalSize: 24);
        Assert.NotNull(typeface);
        Assert.NotEqual(IntPtr.Zero, typeface.Handle);
    }

    [Fact]
    public void FontManager_CachesTypefaces()
    {
        var tf1 = MaterialIconFontManager.GetTypeface(fill: 1, weight: 700, grade: 200, opticalSize: 48);
        var tf2 = MaterialIconFontManager.GetTypeface(fill: 1, weight: 700, grade: 200, opticalSize: 48);

        Assert.Same(tf1, tf2);
    }

    [Fact]
    public void FontManager_ClampsAxisValues()
    {
        // FILL is 0..1, wght is 100..700, GRAD is -25..200, opsz is 20..48
        // Out of range values should clamp and match clamped lookups
        var clampedTf = MaterialIconFontManager.GetTypeface(fill: 0, weight: 100, grade: -25, opticalSize: 20);
        var outOfRangeTf = MaterialIconFontManager.GetTypeface(fill: -5, weight: 50, grade: -100, opticalSize: 10);

        Assert.Same(clampedTf, outOfRangeTf);
    }

    [Fact]
    public void MaterialIconKind_HasAllExpectedIcons()
    {
        var names = Enum.GetNames<MaterialIconKind>();
        Assert.True(names.Length >= 2000, $"Expected at least 2000 icons, got {names.Length}");

        // Check well-known icons
        Assert.True(Enum.IsDefined(MaterialIconKind.Home));
        Assert.True(Enum.IsDefined(MaterialIconKind.Settings));
        Assert.True(Enum.IsDefined(MaterialIconKind.Favorite));
        Assert.True(Enum.IsDefined(MaterialIconKind.Search));
        Assert.True(Enum.IsDefined(MaterialIconKind.Check));
        Assert.True(Enum.IsDefined(MaterialIconKind.Close));
        Assert.True(Enum.IsDefined(MaterialIconKind.Star));
        Assert.True(Enum.IsDefined(MaterialIconKind.Palette));

        // Codepoint validity: Material Symbols are in PUA (0xE000 - 0xF8FF)
        Assert.True((int)MaterialIconKind.Favorite >= 0xE000);
        Assert.True((int)MaterialIconKind.Home >= 0xE000);
    }

    [Fact]
    public void Icon_DefaultProperties_AreSensible()
    {
        var icon = new Icon();
        Assert.Equal(MaterialIconKind.None, icon.Kind);
        Assert.Equal(24.0f, icon.Size);
        Assert.Equal(0.0f, icon.Fill);
        Assert.Equal(400.0f, icon.Weight);
        Assert.Equal(0.0f, icon.Grade);
        Assert.Equal(24.0f, icon.OpticalSize);
        Assert.Equal(Color.Black, icon.Foreground);
        Assert.False(icon.IsFilled);
    }

    [Fact]
    public void Icon_IsFilled_TogglesFillProperty()
    {
        var icon = new Icon();
        Assert.False(icon.IsFilled);
        Assert.Equal(0.0f, icon.Fill);

        icon.IsFilled = true;
        Assert.True(icon.IsFilled);
        Assert.Equal(1.0f, icon.Fill);

        icon.IsFilled = false;
        Assert.False(icon.IsFilled);
        Assert.Equal(0.0f, icon.Fill);
    }

    [Fact]
    public void Icon_MeasureOverride_ReturnsSquareSize()
    {
        var icon = new Icon { Size = 36 };
        icon.Measure(new Size(1000, 1000));

        Assert.Equal(36, icon.DesiredSize.Width);
        Assert.Equal(36, icon.DesiredSize.Height);

        icon.Size = 64;
        icon.Measure(new Size(1000, 1000));
        Assert.Equal(64, icon.DesiredSize.Width);
        Assert.Equal(64, icon.DesiredSize.Height);
    }

    [Fact]
    public void Icon_MarkupExtensions_ChainFluently()
    {
        var icon = new Icon()
            .Kind(MaterialIconKind.Star)
            .Size(40)
            .Filled()
            .Weight(700)
            .Grade(200)
            .OpticalSize(40)
            .Foreground(Color.FromRgb(255, 215, 0));

        Assert.Equal(MaterialIconKind.Star, icon.Kind);
        Assert.Equal(40.0f, icon.Size);
        Assert.Equal(1.0f, icon.Fill);
        Assert.True(icon.IsFilled);
        Assert.Equal(700.0f, icon.Weight);
        Assert.Equal(200.0f, icon.Grade);
        Assert.Equal(40.0f, icon.OpticalSize);
        Assert.Equal(Color.FromRgb(255, 215, 0), icon.Foreground);

        // Test enum extension
        var icon2 = MaterialIconKind.Favorite.ToIcon(size: 32, isFilled: true);
        Assert.Equal(MaterialIconKind.Favorite, icon2.Kind);
        Assert.Equal(32.0f, icon2.Size);
        Assert.True(icon2.IsFilled);
    }

    [Fact]
    public void MaterialIconRenderer_RendersOnCanvasWithoutError()
    {
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        var paintRegistry = new PaintRegistry();
        var context = new DrawingContext(canvas, paintRegistry);

        var colors = MaterialColorScheme.Light();
        var renderer = new MaterialIconRenderer(colors);
        var icon = new Icon
        {
            Kind = MaterialIconKind.Favorite,
            Size = 48,
            Fill = 1.0f,
            Weight = 600,
            Foreground = Color.FromRgb(255, 0, 0)
        };

        icon.Measure(new Size(100, 100));
        icon.Arrange(new Rect(10, 10, 48, 48));

        // Should not throw
        renderer.Render(icon, ref context);
    }

    [Fact]
    public void CaptionButtons_GlyphTest()
    {
        var tf = MaterialIconFontManager.BaseTypeface;
        var font = new SKFont(tf, 24f);

        string minGlyph = char.ConvertFromUtf32((int)MaterialIconKind.Minimize);
        string remGlyph = char.ConvertFromUtf32((int)MaterialIconKind.Remove);
        string cropGlyph = char.ConvertFromUtf32((int)MaterialIconKind.CropSquare);
        string squareGlyph = char.ConvertFromUtf32((int)MaterialIconKind.Square);
        string closeGlyph = char.ConvertFromUtf32((int)MaterialIconKind.Close);

        font.MeasureText(minGlyph, out var minBounds);
        font.MeasureText(remGlyph, out var remBounds);
        font.MeasureText(cropGlyph, out var cropBounds);
        font.MeasureText(squareGlyph, out var squareBounds);
        font.MeasureText(closeGlyph, out var closeBounds);

        // Assert valid metrics
        Assert.True(minBounds.Width > 0);
        Assert.True(remBounds.Width > 0);
        Assert.True(cropBounds.Width > 0);
        Assert.True(squareBounds.Width > 0);
        Assert.True(cropBounds.Height > 0);
    }

    [Fact]
    public void TitleBar_CaptionButtons_UseMaterialIcons()
    {
        var titleBar = new TitleBar { Title = "Test Window" };

        // Verify button content types
        Assert.IsType<Icon>(titleBar.MinimizeButton.Content);
        Assert.IsType<Icon>(titleBar.MaximizeButton.Content);
        Assert.IsType<Icon>(titleBar.CloseButton.Content);

        // Verify icon kinds
        Assert.Equal(MaterialIconKind.Minimize, titleBar.MinimizeIcon.Kind);
        Assert.Equal(MaterialIconKind.CropSquare, titleBar.MaximizeIcon.Kind);
        Assert.Equal(MaterialIconKind.Close, titleBar.CloseIcon.Kind);

        // Verify IsMaximized toggles between CropSquare and FilterNone (restore)
        Assert.False(titleBar.IsMaximized);
        titleBar.IsMaximized = true;
        Assert.Equal(MaterialIconKind.FilterNone, titleBar.MaximizeIcon.Kind);

        titleBar.IsMaximized = false;
        Assert.Equal(MaterialIconKind.CropSquare, titleBar.MaximizeIcon.Kind);
    }

    [Fact]
    public void Icon_FromSKPath_MeasuresAndRendersProperly()
    {
        using var builder = new SKPathBuilder();
        builder.MoveTo(0, 0);
        builder.LineTo(50, 100);
        builder.LineTo(100, 0);
        builder.Close();
        using var path = builder.Detach();

        var icon = new Icon(path, size: 32f, foreground: Color.FromRgb(0, 128, 255));
        Assert.Same(path, icon.Data);
        Assert.Equal(32f, icon.Size);
        Assert.Equal(Color.FromRgb(0, 128, 255), icon.Foreground);

        icon.Measure(new Size(100, 100));
        icon.Arrange(new Rect(0, 0, 32, 32));
        Assert.Equal(32f, icon.DesiredSize.Width);
        Assert.Equal(32f, icon.DesiredSize.Height);

        // Render test
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var paintRegistry = new PaintRegistry();
        var context = new DrawingContext(surface.Canvas, paintRegistry);
        var renderer = new MaterialIconRenderer(MaterialColorScheme.Light());

        renderer.Render(icon, ref context);
    }

    [Fact]
    public void Icon_FromSvgPathData_ParsesAndCreatesPath()
    {
        string svg = "M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z";
        var icon = new Icon(svg, size: 24f);

        Assert.Equal(svg, icon.PathData);
        Assert.NotNull(icon.Data);
        Assert.False(icon.Data!.IsEmpty);

        // Clearing PathData clears Data
        icon.PathData = null;
        Assert.Null(icon.Data);
    }

    [Fact]
    public void Icon_CustomPath_StrokeAndFill_RenderWithoutError()
    {
        string svg = "M12 2L2 22h20L12 2z";
        var icon = new Icon(svg, size: 48f)
        {
            StrokeWidth = 2.5f,
            Foreground = Color.FromHex("#FF5722")
        };

        icon.Measure(new Size(48, 48));
        icon.Arrange(new Rect(0, 0, 48, 48));

        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var paintRegistry = new PaintRegistry();
        var context = new DrawingContext(surface.Canvas, paintRegistry);
        var renderer = new MaterialIconRenderer(MaterialColorScheme.Light());

        // Render stroked
        renderer.Render(icon, ref context);

        // Render filled
        icon.StrokeWidth = 0f;
        renderer.Render(icon, ref context);
    }

    [Fact]
    public void Icon_MarkupExtensions_WorkWithCustomPaths()
    {
        using var builder = new SKPathBuilder();
        builder.AddCircle(50, 50, 40);
        using var path = builder.Detach();

        var icon1 = path.ToIcon(size: 28f, foreground: Color.Red);
        Assert.Same(path, icon1.Data);
        Assert.Equal(28f, icon1.Size);
        Assert.Equal(Color.Red, icon1.Foreground);

        string svg = "M0 0h24v24H0z";
        var icon2 = svg.ToIcon(size: 20f).StrokeWidth(1.5f);
        Assert.Equal(svg, icon2.PathData);
        Assert.NotNull(icon2.Data);
        Assert.Equal(20f, icon2.Size);
        Assert.Equal(1.5f, icon2.StrokeWidth);
    }
}
