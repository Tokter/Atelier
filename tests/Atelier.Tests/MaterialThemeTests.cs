using System;
using System.IO;
using SkiaSharp;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Styling;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Rendering;
using Atelier.Theming;
using Atelier.Theming.Material;
using Atelier.Theming.Material.Renderers;

namespace Atelier.Tests;

internal static class ThemeRendering
{
    /// <summary>Styles, lays out and renders <paramref name="root"/> with the active theme onto a white bitmap.</summary>
    public static SKBitmap Render(UIElement root, int width, int height, PaintRegistry? registry = null)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        RenderInto(root, canvas, width, height, registry);
        return bitmap;
    }

    public static void RenderInto(UIElement root, SKCanvas canvas, int width, int height, PaintRegistry? registry = null)
    {
        root.ApplyStylesToTree();
        root.UseLayoutRounding = true;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));

        var ownRegistry = registry == null ? new PaintRegistry() : null;
        try
        {
            var context = new DrawingContext(canvas, registry ?? ownRegistry!);
            VisualTreeRenderer.Render(root, ref context, ThemeVisualPresenter.Instance);
        }
        finally
        {
            ownRegistry?.Dispose();
        }
    }

    public static bool IsClose(SKColor actual, Color expected, int tolerance = 8) =>
        Math.Abs(actual.Red - expected.R) <= tolerance &&
        Math.Abs(actual.Green - expected.G) <= tolerance &&
        Math.Abs(actual.Blue - expected.B) <= tolerance;
}

public class MaterialFocusRingTests
{
    private static (Canvas Root, Button Button) CreateFocusedButton()
    {
        var button = new Button("Focus me");
        Canvas.SetLeft(button, 20);
        Canvas.SetTop(button, 20);
        var root = new Canvas { Width = 200, Height = 100 };
        root.Add(button);
        FocusManager.SetFocus(button);
        return (root, button);
    }

    [Fact]
    public void KeyboardFocus_DrawsA3PxSecondaryRing_2PxOutsideTheButton()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateLight());
        var colors = ((MaterialTheme)ThemeManager.Current).Colors;
        var (root, button) = CreateFocusedButton();
        FocusManager.NotifyKeyboardInteraction();
        try
        {
            using var bitmap = ThemeRendering.Render(root, 200, 100);
            SaveArtifact(bitmap, "focus-ring.png");

            var b = button.Bounds;
            int midY = (int)(b.Y + b.Height / 2);
            int left = (int)b.X;

            // Gap (2 px), then the ring (3 px), then nothing.
            Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(left - 1, midY), Color.White), $"gap: {bitmap.GetPixel(left - 1, midY)}");
            Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(left - 2, midY), Color.White), $"gap: {bitmap.GetPixel(left - 2, midY)}");
            for (int x = left - 5; x <= left - 3; x++)
            {
                Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(x, midY), colors.Secondary), $"ring at {x}: {bitmap.GetPixel(x, midY)}");
            }
            Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(left - 6, midY), Color.White), $"outside: {bitmap.GetPixel(left - 6, midY)}");
        }
        finally
        {
            FocusManager.ClearFocus(root);
        }
    }

    [Fact]
    public void PointerInteraction_HidesTheRing_KeyboardShowsItAgain()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateLight());
        var (root, button) = CreateFocusedButton();
        try
        {
            FocusManager.NotifyPointerInteraction();
            Assert.True(button.IsFocused);
            Assert.False(button.IsFocusVisible);

            using (var bitmap = ThemeRendering.Render(root, 200, 100))
            {
                var b = button.Bounds;
                Assert.True(ThemeRendering.IsClose(bitmap.GetPixel((int)b.X - 4, (int)(b.Y + b.Height / 2)), Color.White));
            }

            FocusManager.NotifyKeyboardInteraction();
            Assert.True(button.IsFocusVisible);
        }
        finally
        {
            FocusManager.ClearFocus(root);
        }
    }

    [Fact]
    public void TabNavigation_AfterAClick_ShowsTheRingOnTheNextControl()
    {
        // Platforms handle Tab by calling FocusNext directly (without dispatching the key), as in the property grid:
        // click a text box, then Tab to a check box.
        var panel = new StackPanel();
        var text = new TextBox();
        var check = new CheckBox("Option");
        panel.Children(text, check);
        try
        {
            FocusManager.SetFocus(text);
            FocusManager.NotifyPointerInteraction();
            Assert.False(text.IsFocusVisible);

            FocusManager.FocusNext(panel);

            Assert.True(check.IsFocused);
            Assert.True(check.IsFocusVisible);
        }
        finally
        {
            FocusManager.ClearFocus(panel);
            FocusManager.NotifyPointerInteraction();
        }
    }

    [Fact]
    public void CheckBoxFocusRing_HugsTheBox()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateLight());
        var colors = ((MaterialTheme)ThemeManager.Current).Colors;
        var check = new CheckBox("Option");
        Canvas.SetLeft(check, 30);
        Canvas.SetTop(check, 20);
        var root = new Canvas { Width = 200, Height = 80 };
        root.Add(check);
        FocusManager.SetFocus(check);
        FocusManager.NotifyKeyboardInteraction();
        try
        {
            using var bitmap = ThemeRendering.Render(root, 200, 80);
            SaveArtifact(bitmap, "checkbox-focus.png");

            var box = check.GetIndicatorBounds();
            int left = (int)MathF.Round(check.Bounds.X + box.X);
            int midY = (int)(check.Bounds.Y + box.Y + box.Height / 2);

            // Ring: 2 px gap then 3 px of secondary right next to the 18 px box; nothing further out (no 40 px halo).
            Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(left - 4, midY), colors.Secondary), $"ring: {bitmap.GetPixel(left - 4, midY)}");
            Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(left - 1, midY), Color.White), $"gap: {bitmap.GetPixel(left - 1, midY)}");
            Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(left - 8, midY), Color.White), $"outside: {bitmap.GetPixel(left - 8, midY)}");
        }
        finally
        {
            FocusManager.ClearFocus(root);
            FocusManager.NotifyPointerInteraction();
        }
    }

    [Fact]
    public void ModifierKeys_DoNotShowFocusRings()
    {
        FocusManager.NotifyPointerInteraction();
        FocusManager.DispatchKeyDown(new Core.Events.KeyEventArgs(Core.Events.Key.LeftShift));
        Assert.False(FocusManager.IsFocusVisible);

        FocusManager.DispatchKeyDown(new Core.Events.KeyEventArgs(Core.Events.Key.Tab));
        Assert.True(FocusManager.IsFocusVisible);
        FocusManager.NotifyPointerInteraction();
    }

    private static void SaveArtifact(SKBitmap bitmap, string name)
    {
        // Written for visual inspection; not part of the assertion.
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "atelier-" + name), data.ToArray());
    }
}

public class MaterialContentColorTests
{
    [Theory]
    [InlineData(ButtonVariant.Filled)]
    [InlineData(ButtonVariant.Tonal)]
    [InlineData(ButtonVariant.Outlined)]
    public void ButtonLabels_UseTheVariantsContentColor_WithoutSettingForeground(ButtonVariant variant)
    {
        var theme = MaterialTheme.CreateLight();
        var renderer = new MaterialTextBlockRenderer(theme.Colors, theme.Renderers);
        var button = new Button("Label") { Variant = variant };
        var label = (TextBlock)button.Content!;

        var expected = variant switch
        {
            ButtonVariant.Filled => theme.Colors.OnPrimary,
            ButtonVariant.Tonal => theme.Colors.OnSecondaryContainer,
            _ => theme.Colors.Primary,
        };
        Assert.Equal(expected, renderer.GetTextColor(label));
    }

    [Fact]
    public void DisabledButtonLabel_IsOnSurfaceAt38Percent()
    {
        var theme = MaterialTheme.CreateLight();
        var renderer = new MaterialTextBlockRenderer(theme.Colors, theme.Renderers);
        var button = new Button("Label") { IsEnabled = false };

        Assert.Equal(theme.Colors.OnSurface.WithAlpha(0.38f), renderer.GetTextColor((TextBlock)button.Content!));
    }

    [Fact]
    public void SetForegrounds_WinOverTheProvidedContentColor()
    {
        var theme = MaterialTheme.CreateLight();
        var renderer = new MaterialTextBlockRenderer(theme.Colors, theme.Renderers);

        var red = Color.FromHex("#FF0000");
        var onLabel = new Button("A");
        ((TextBlock)onLabel.Content!).Foreground = red;
        Assert.Equal(red, renderer.GetTextColor((TextBlock)onLabel.Content!));

        var onButton = new Button("B") { Foreground = red };
        Assert.Equal(red, renderer.GetTextColor((TextBlock)onButton.Content!));

        // A foreground set on a panel outside the button doesn't override the button's own label color.
        var panel = new StackPanel();
        var inner = new Button("C");
        var outerText = new TextBlock("outside");
        panel.Children(inner, outerText);
        var themedPanel = new Border { Child = panel };
        panel.SetValue(Control.ForegroundProperty, red);
        Assert.Equal(theme.Colors.OnPrimary, renderer.GetTextColor((TextBlock)inner.Content!));
        Assert.Equal(red, renderer.GetTextColor(outerText));
        Assert.NotNull(themedPanel);
    }

    [Fact]
    public void TreeNodeText_UsesItsOwnNodesState_NotItsSelectedParents()
    {
        var theme = MaterialTheme.CreateLight();
        var renderer = new MaterialTextBlockRenderer(theme.Colors, theme.Renderers);
        var tree = new TreeView();
        var parent = new TreeViewItem("Parent");
        var child = new TreeViewItem("Child");
        parent.AddChildItem(child);
        tree.AddRootItem(parent);
        parent.IsExpanded = true;
        parent.IsSelected = true;

        var parentText = FindText(parent, "Parent");
        var childText = FindText(child, "Child");

        Assert.Equal(theme.Colors.OnSecondaryContainer, renderer.GetTextColor(parentText));
        Assert.Equal(theme.Colors.OnSurface, renderer.GetTextColor(childText));
    }

    [Fact]
    public void SelectedListItemText_IsOnSecondaryContainer()
    {
        var theme = MaterialTheme.CreateLight();
        var renderer = new MaterialTextBlockRenderer(theme.Colors, theme.Renderers);
        var list = new ListBox { ItemsSource = new[] { "a", "b" } };
        list.SelectedIndex = 1;
        list.Measure(new Size(300, 200)); // the list virtualizes: containers exist once laid out
        list.Arrange(new Rect(0, 0, 300, 200));

        var selectedText = FindText(list.ContainerFromIndex(1)!, "b");
        var otherText = FindText(list.ContainerFromIndex(0)!, "a");

        Assert.Equal(theme.Colors.OnSecondaryContainer, renderer.GetTextColor(selectedText));
        Assert.Equal(theme.Colors.OnSurface, renderer.GetTextColor(otherText));
    }

    private static TextBlock FindText(VisualNode node, string text)
    {
        if (node is TextBlock tb && tb.Text == text) return tb;
        for (int i = 0; i < node.Children.Count; i++)
        {
            try { return FindText(node.Children[i], text); }
            catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException($"No text block with '{text}'.");
    }
}

public class MaterialStyleTests
{
    [Fact]
    public void CreatingATheme_HasNoGlobalEffects_ActivatingInstallsItsStyles()
    {
        ThemeManager.Reset();
        int globalBefore = StyleManager.GlobalStyles.Count;

        var theme = MaterialTheme.CreateLight();
        Assert.Equal(globalBefore, StyleManager.GlobalStyles.Count);
        Assert.Empty(StyleManager.ThemeStyles);

        using (ActiveTheme.Use(theme))
        {
            Assert.Equal(theme.Styles.Count, StyleManager.ThemeStyles.Count);
        }
        Assert.Empty(StyleManager.ThemeStyles);
    }

    [Fact]
    public void Buttons_GetMd3Defaults_WithTouchSizing()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateLight(MaterialSizing.Touch));
        var button = new Button("Save");
        button.ApplyStyles();

        Assert.Equal(new Thickness(24, 10), button.Padding);
        Assert.Equal(40f, button.MinHeight);
        Assert.Equal(HorizontalAlignment.Center, button.HorizontalContentAlignment);
        Assert.Equal(VerticalAlignment.Center, button.VerticalContentAlignment);
        Assert.Equal(FontWeight.Medium, button.FontWeight);
        Assert.Equal(FontWeight.Medium, ((TextBlock)button.Content!).FontWeight); // inherited by the label

        // A stretched button keeps its label centered.
        var host = new Border { Width = 300, Child = button };
        host.ApplyStylesToTree();
        host.Measure(new Size(300, 100));
        host.Arrange(new Rect(0, 0, 300, 100));
        var label = (TextBlock)button.Content!;
        Assert.Equal(button.Bounds.Width / 2, label.Bounds.X + label.Bounds.Width / 2, 1);
        Assert.True(button.Bounds.Height >= 40f);
    }

    [Fact]
    public void DesktopSizing_IsTheDefault_AndMakesControlsDenser()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateLight());
        Assert.Same(MaterialSizing.Desktop, ((MaterialTheme)ThemeManager.Current).Sizing);

        var button = new Button("Save");
        var field = new TextBox { Label = "Name" };
        var toggle = new Switch();
        var item = new ListBoxItem { Content = "Row" };
        var panel = new StackPanel();
        panel.Children(button, field, toggle, item);
        panel.ApplyStylesToTree();
        panel.Measure(new Size(400, 800));

        Assert.Equal(32f, button.MinHeight);                 // 40 at density 0
        Assert.Equal(32f, button.DesiredSize.Height);
        Assert.Equal(48f, field.FieldHeight);                // 56 at density 0
        Assert.Equal(48f, field.DesiredSize.Height);
        Assert.Equal(40f, toggle.TrackWidth);                // 52×32 in MD3
        Assert.Equal(24f, toggle.TrackHeight);
        Assert.True(item.DesiredSize.Height is >= 38f and <= 42f, $"list row: {item.DesiredSize.Height}"); // 48 at density 0
    }

    [Fact]
    public void TouchSizing_UsesTheMd3Sizes()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateDark(MaterialSizing.Touch));
        var field = new TextBox { Label = "Name" };
        var toggle = new Switch();
        field.ApplyStyles();
        toggle.ApplyStyles();

        Assert.Equal(56f, field.FieldHeight);
        Assert.Equal(52f, toggle.TrackWidth);
        Assert.Equal(32f, toggle.TrackHeight);
    }

    [Fact]
    public void Density_FollowsTheMd3Scale()
    {
        var densest = new MaterialSizing { Density = -3 };
        Assert.Equal(28f, densest.Height(40f));
        Assert.Equal(28f, densest.StateLayerSize);
        Assert.Equal(40f, MaterialSizing.Touch.StateLayerSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MaterialSizing { Density = -4 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MaterialSizing { Density = 1 });
    }

    [Fact]
    public void FilledFieldWithLabel_StaysTallEnoughForTheFloatingLabel()
    {
        var field = new TextBox { Label = "Name", Variant = TextBoxVariant.Filled, FieldHeight = 40f };
        field.Measure(new Size(300, 200));
        Assert.Equal(52f, field.DesiredSize.Height);

        var outlined = new TextBox { Label = "Name", FieldHeight = 40f };
        outlined.Measure(new Size(300, 200));
        Assert.Equal(40f, outlined.DesiredSize.Height);
    }

    [Fact]
    public void AppStylesAndLocalValues_LayerOnTopOfTheThemeStyle()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateLight());
        var appStyle = new Style(typeof(Button)).Set(Control.CornerRadiusProperty, new CornerRadius(4));
        StyleManager.GlobalStyles.Add(appStyle);
        try
        {
            var button = new Button("x") { Padding = new Thickness(2) };
            button.ApplyStyles();

            Assert.Equal(new CornerRadius(4), button.CornerRadius);          // app style
            Assert.Equal(new Thickness(2), button.Padding);                  // local value
            Assert.Equal(32f, button.MinHeight);                            // theme style still applies (desktop)
        }
        finally
        {
            StyleManager.GlobalStyles.Remove(appStyle);
        }
    }

    [Fact]
    public void ImplicitStyles_MatchTheMostSpecificType()
    {
        var baseStyle = new Style(typeof(Button)).Set(UIElement.OpacityProperty, 0.5f);
        var repeatStyle = new Style(typeof(RepeatButton)).Set(UIElement.OpacityProperty, 0.25f);
        StyleManager.GlobalStyles.Add(baseStyle);     // registered first
        StyleManager.GlobalStyles.Add(repeatStyle);
        try
        {
            var repeat = new RepeatButton("r");
            repeat.ApplyStyles();
            var plain = new Button("p");
            plain.ApplyStyles();

            Assert.Equal(0.25f, repeat.Opacity);
            Assert.Equal(0.5f, plain.Opacity);
        }
        finally
        {
            StyleManager.GlobalStyles.Remove(baseStyle);
            StyleManager.GlobalStyles.Remove(repeatStyle);
        }
    }

    [Fact]
    public void CaptionButtons_AreNotResizedByTheButtonStyle()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateLight());
        var caption = new TitleBarButton();
        caption.ApplyStyles();

        Assert.Equal(Thickness.Zero, caption.Padding);
        Assert.Equal(0f, caption.MinHeight);
    }

    [Fact]
    public void TypeScale_UsesMd3WeightsAndLineHeights()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateLight());
        var title = new TextBlock("t") { StyleKey = MaterialTypography.TitleMediumKey };
        var headline = new TextBlock("h") { StyleKey = MaterialTypography.HeadlineSmallKey };

        Assert.Equal(16f, title.FontSize);
        Assert.Equal(24f, title.LineHeight);
        Assert.Equal(FontWeight.Medium, title.EffectiveFontWeight);
        Assert.Equal(24f, headline.FontSize);
        Assert.Equal(FontWeight.Normal, headline.EffectiveFontWeight);
    }
}

public class FontWeightTests
{
    [Fact]
    public void BoldRaisesTheWeight_AndWeightsInherit()
    {
        var button = new Button("x") { FontWeight = FontWeight.Medium };
        var label = (TextBlock)button.Content!;
        Assert.Equal(FontWeight.Medium, label.EffectiveFontWeight);

        label.Bold = true;
        Assert.Equal(FontWeight.Bold, label.EffectiveFontWeight);

        label.FontWeight = FontWeight.Black;
        Assert.Equal(FontWeight.Black, label.EffectiveFontWeight); // Bold never lowers a heavier weight
    }

    [Fact]
    public void DefaultWeight_IsNormal_AndInvalidWeightsAreRejected()
    {
        Assert.Equal(400, default(FontWeight).Value);
        Assert.Equal(FontWeight.Normal, default(FontWeight));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FontWeight(0));
    }

    [Fact]
    public void ChangingTheWeight_ChangesTheMeasuredWidth()
    {
        var text = new TextBlock("Weighted text sample");
        text.Measure(new Size(1000, 100));
        float normal = text.DesiredSize.Width;

        text.FontWeight = FontWeight.Black;
        text.Measure(new Size(1000, 100));

        Assert.NotEqual(normal, text.DesiredSize.Width);
    }
}

public class MaterialRenderingTests
{
    [Fact]
    public void Border_DrawsEachSidesThickness()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateLight());
        var border = new Border
        {
            Width = 100, Height = 50,
            BorderBrush = Color.FromHex("#FF0000"),
            BorderThickness = new Thickness(0, 0, 0, 4),
        };
        var root = new Canvas { Width = 120, Height = 70 };
        Canvas.SetLeft(border, 10);
        Canvas.SetTop(border, 10);
        root.Add(border);

        using var bitmap = ThemeRendering.Render(root, 120, 70);

        Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(60, 58), Color.FromHex("#FF0000")), "bottom edge");
        Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(60, 11), Color.White), "no top edge");
        Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(11, 35), Color.White), "no left edge");
    }

    [Fact]
    public void RenderingCommonControls_DoesNotAllocateAfterTheFirstFrame()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateLight());
        var panel = new StackPanel { Spacing = 8 };
        var filled = new Button("Filled");
        var elevated = new Button("Elevated") { Variant = ButtonVariant.Elevated };
        var check = new CheckBox("Check") { IsChecked = true };
        var toggle = new Switch { IsChecked = true, ShowThumbIcon = true };
        var card = new Card(CardVariant.Elevated) { Child = new TextBlock("Card text") };
        var combo = new ComboBox { ItemsSource = new[] { "One", "Two" }, SelectedIndex = 0 };
        var slider = new Slider { Value = 40 };
        var text = new TextBox { Text = "Input", Label = "Label" };
        var rounded = new Border { Width = 40, Height = 20, Background = Color.FromHex("#336699"), CornerRadius = new CornerRadius(8, 0, 8, 0), Elevation = 2 };
        panel.Children(filled, elevated, check, toggle, card, combo, slider, text, rounded);

        using var bitmap = new SKBitmap(400, 700);
        using var canvas = new SKCanvas(bitmap);
        using var registry = new PaintRegistry();

        ThemeRendering.RenderInto(panel, canvas, 400, 700, registry); // warm-up: caches fonts, paints, filters
        ThemeRendering.RenderInto(panel, canvas, 400, 700, registry);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 5; i++)
        {
            var context = new DrawingContext(canvas, registry);
            VisualTreeRenderer.Render(panel, ref context, ThemeVisualPresenter.Instance);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
