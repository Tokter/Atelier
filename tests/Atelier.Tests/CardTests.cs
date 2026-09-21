using System;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Rendering;
using Atelier.Theming;
using Atelier.Theming.Material;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Atelier.Tests;

public partial class CardTests
{
    [Fact]
    public void Card_DefaultProperties_AreMD3Compliant()
    {
        var card = new Card();

        Assert.Equal(CardVariant.Outlined, card.Variant);
        Assert.Equal(new CornerRadius(12f), card.CornerRadius);
        Assert.Equal(new Thickness(16f), card.Padding);
        Assert.True(card.ClipToBounds);
    }

    [Fact]
    public void Card_Constructors_SetInitialState()
    {
        var elevated = new Card(CardVariant.Elevated);
        Assert.Equal(CardVariant.Elevated, elevated.Variant);

        var filled = new Card(CardVariant.Filled);
        Assert.Equal(CardVariant.Filled, filled.Variant);

        var child = new TextBlock("Content");
        var withChild = new Card(CardVariant.Outlined, child);
        Assert.Same(child, withChild.Child);
    }

    [Fact]
    public void Card_FluentExtensions_ChainCorrectly()
    {
        var child = new TextBlock("Child text");
        var card = new Card()
            .Variant(CardVariant.Elevated)
            .Elevation(3f)
            .CornerRadius(18f)
            .Padding(24f)
            .Child(child);

        Assert.Equal(CardVariant.Elevated, card.Variant);
        Assert.Equal(3f, card.Elevation);
        Assert.Equal(new CornerRadius(18f), card.CornerRadius);
        Assert.Equal(new Thickness(24f), card.Padding);
        Assert.Same(child, card.Child);
    }

    public partial class CardTestVM : ObservableObject
    {
        [ObservableProperty]
        private CardVariant _variant = CardVariant.Outlined;

        [ObservableProperty]
        private float _elevation = 1f;

        [ObservableProperty]
        private float _radius = 8f;

        [ObservableProperty]
        private float _pad = 12f;
    }

    [Fact]
    public void Card_DataBinding_ReflectsViewModelChanges()
    {
        var vm = new CardTestVM();
        var card = new Card()
            .BindVariant(vm, x => x.Variant, (m, v) => m.Variant = v)
            .BindElevation(vm, x => x.Elevation, (m, v) => m.Elevation = v)
            .BindCornerRadius(vm, x => x.Radius)
            .BindPadding(vm, x => x.Pad);

        Assert.Equal(CardVariant.Outlined, card.Variant);
        Assert.Equal(1f, card.Elevation);
        Assert.Equal(new CornerRadius(8f), card.CornerRadius);
        Assert.Equal(new Thickness(12f), card.Padding);

        // Mutate ViewModel
        vm.Variant = CardVariant.Filled;
        vm.Elevation = 4f;
        vm.Radius = 24f;
        vm.Pad = 32f;

        Assert.Equal(CardVariant.Filled, card.Variant);
        Assert.Equal(4f, card.Elevation);
        Assert.Equal(new CornerRadius(24f), card.CornerRadius);
        Assert.Equal(new Thickness(32f), card.Padding);
    }

    [Fact]
    public void Grid_TextWrapping_CalculatesHeightFromColumnWidthConstraint()
    {
        var longText = "This is a very long text description designed to test multi-line text wrapping inside a narrower grid column constraint.";
        var textBlock = new TextBlock(longText)
        {
            FontSize = 14f,
            TextWrapping = TextWrapping.Wrap
        };

        // Measure single line unwrapped height
        var singleLineHeight = TextMeasurer.Measure(longText, 14f).Height;

        var grid = new Atelier.Layout.Grid()
            .Columns(GridLength.Star, GridLength.Star);

        grid.Add(textBlock.Column(0));

        // Total grid available width is 400. Column 0 gets 200.
        grid.Measure(new Size(400, float.PositiveInfinity));

        // At 200px width, this text wraps into at least 3-4 lines
        Assert.True(textBlock.DesiredSize.Height > singleLineHeight * 2,
            $"Expected wrapped text height ({textBlock.DesiredSize.Height}) to be greater than 2x single-line height ({singleLineHeight})");
        Assert.True(grid.DesiredSize.Height >= textBlock.DesiredSize.Height,
            $"Expected grid height ({grid.DesiredSize.Height}) to fit wrapped text ({textBlock.DesiredSize.Height})");
    }

    [Fact]
    public void Card_InsideGridColumn_TextAndButton_DoNotOverlap()
    {
        var description = new TextBlock("This is a multi-line card description designed to test layout arrangement so that it never overlaps with the button.")
        {
            FontSize = 14f,
            TextWrapping = TextWrapping.Wrap
        };
        var button = new Button("Action Button") { Height = 40f };

        var cardGrid = new Atelier.Layout.Grid()
            .Rows(GridLength.Auto, GridLength.Auto)
            .RowSpacing(12)
            .Children(description.Row(0), button.Row(1));

        var card = new Card(cardGrid)
            .Padding(16);

        var outerGrid = new Atelier.Layout.Grid()
            .Columns(GridLength.Star, GridLength.Star);

        outerGrid.Add(card.Column(0));

        outerGrid.Measure(new Size(400, float.PositiveInfinity));
        outerGrid.Arrange(new Rect(0, 0, 400, outerGrid.DesiredSize.Height));

        // In column 0 (width 200), card has width 200.
        // description is in Row 0, button is in Row 1.
        // Button top must be strictly >= description bottom.
        Assert.True(button.Bounds.Top >= description.Bounds.Bottom,
            $"Button Top ({button.Bounds.Top}) must be >= Description Bottom ({description.Bounds.Bottom})");
    }

    [Fact]
    public void Card_ClipToBounds_IsTrueByDefault_ToClipChildren()
    {
        var card = new Card(CardVariant.Elevated);
        Assert.True(card.ClipToBounds);
        Assert.Equal(0f, card.Elevation);

        card.Elevation = 2f;
        Assert.Equal(2f, card.Elevation);
        Assert.True(card.ClipToBounds);
    }

    [Fact]
    public void Card_Rendering_ShadowDrawsOutsideBounds_AndChildrenAreClipped()
    {
        ThemeManager.Current = MaterialTheme.CreateLight();

        using var bitmap = new SkiaSharp.SKBitmap(200, 200);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.White);

        using var paintRegistry = new PaintRegistry();
        var context = new DrawingContext(canvas, paintRegistry);

        // Child that deliberately extends beyond the card bounds
        var overflowChild = new Border
        {
            Background = Color.FromHex("#FF0000") // Red
        };

        var card = new Card(CardVariant.Elevated)
        {
            Elevation = 4f
        };
        card.Child = overflowChild;

        var root = new Border { Width = 200, Height = 200 };
        root.Child = card;

        root.Measure(new Size(200, 200));
        root.Arrange(new Rect(0, 0, 200, 200));

        // Position card at (50, 50, 100, 80) inside root
        card.Arrange(new Rect(50, 50, 100, 80));

        // Arrange child so it extends well outside the card bounds
        overflowChild.Measure(new Size(100, 120));
        overflowChild.Arrange(new Rect(0, 0, 100, 120));

        VisualTreeRenderer.Render(root, ref context, ThemeVisualPresenter.Instance);

        // 1. Verify shadow was drawn OUTSIDE card bounds (below card at y=133, x=100)
        var shadowPixel = bitmap.GetPixel(100, 133);
        Assert.True(shadowPixel.Alpha > 0, "Shadow should be rendered on canvas");
        Assert.True(shadowPixel.Red < 255 || shadowPixel.Green < 255 || shadowPixel.Blue < 255,
            $"Expected shadow pixel below card to be darker than white, got {shadowPixel}");

        // 2. Verify child was CLIPPED at card bounds (card bottom is y=130 on canvas).
        // At y=135, x=100, the red child should NOT be drawn (clipped by ClipToBounds).
        var belowCardPixel = bitmap.GetPixel(100, 135);
        Assert.True(belowCardPixel.Green > 0,
            $"Expected overflowing red child to be clipped, but found red pixel at y=135: {belowCardPixel}");
    }
}
