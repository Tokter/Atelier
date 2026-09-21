using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Styling;
using Atelier.Markup;
using Atelier.Theming.Material;
using Atelier.Theming.Material.Renderers;
using Xunit;

namespace Atelier.Tests;

public class TypographyTests
{
    [Fact]
    public void MaterialTypography_RegisterStyles_PopulatesAllExpectedKeys()
    {
        var styles = new StyleCollection();
        var colors = MaterialColorScheme.Light();

        MaterialTypography.RegisterStyles(styles, colors);

        foreach (var key in MaterialTypography.AllKeys)
        {
            var found = false;
            for (int i = 0; i < styles.Count; i++)
            {
                if (styles[i].Key == key)
                {
                    found = true;
                    break;
                }
            }
            Assert.True(found, $"Expected style key '{key}' to be registered.");
        }
    }

    [Fact]
    public void MaterialTypography_HeadingsAndBody_HaveCorrectTypographyMetrics()
    {
        var styles = new StyleCollection();
        var colors = MaterialColorScheme.Light();
        MaterialTypography.RegisterStyles(styles, colors);

        Style? FindStyle(string key)
        {
            for (int i = 0; i < styles.Count; i++)
            {
                if (styles[i].Key == key) return styles[i];
            }
            return null;
        }

        // Heading 1 / Headline Large: 32pt, Bold
        var h1 = FindStyle(MaterialTypography.Heading1Key);
        Assert.NotNull(h1);
        Assert.Equal(32f, h1.Setters.Find(s => s.Property == TextBlock.FontSizeProperty)?.Value);
        Assert.Equal(true, h1.Setters.Find(s => s.Property == TextBlock.BoldProperty)?.Value);

        // Heading 2 / Headline Medium: 28pt, Bold
        var h2 = FindStyle(MaterialTypography.Heading2Key);
        Assert.NotNull(h2);
        Assert.Equal(28f, h2.Setters.Find(s => s.Property == TextBlock.FontSizeProperty)?.Value);
        Assert.Equal(true, h2.Setters.Find(s => s.Property == TextBlock.BoldProperty)?.Value);

        // Heading 3 / Headline Small: 24pt, Bold
        var h3 = FindStyle(MaterialTypography.Heading3Key);
        Assert.NotNull(h3);
        Assert.Equal(24f, h3.Setters.Find(s => s.Property == TextBlock.FontSizeProperty)?.Value);
        Assert.Equal(true, h3.Setters.Find(s => s.Property == TextBlock.BoldProperty)?.Value);

        // NormalText / BodyMedium: 14pt, Regular
        var normal = FindStyle(MaterialTypography.NormalTextKey);
        Assert.NotNull(normal);
        Assert.Equal(14f, normal.Setters.Find(s => s.Property == TextBlock.FontSizeProperty)?.Value);
        Assert.Equal(false, normal.Setters.Find(s => s.Property == TextBlock.BoldProperty)?.Value);

        // Subtext / BodySmall: 12pt, Muted
        var subtext = FindStyle(MaterialTypography.SubtextKey);
        Assert.NotNull(subtext);
        Assert.Equal(12f, subtext.Setters.Find(s => s.Property == TextBlock.FontSizeProperty)?.Value);
        Assert.Equal(true, subtext.Setters.Find(s => s.Property == TextBlock.MutedProperty)?.Value);

        // Caption: 11pt, Muted
        var caption = FindStyle(MaterialTypography.CaptionKey);
        Assert.NotNull(caption);
        Assert.Equal(11f, caption.Setters.Find(s => s.Property == TextBlock.FontSizeProperty)?.Value);
        Assert.Equal(true, caption.Setters.Find(s => s.Property == TextBlock.MutedProperty)?.Value);
    }

    [Fact]
    public void TextBlock_StyleKey_AppliesTypographyStyleGlobally()
    {
        // Ensure MaterialTheme registers styles globally
        _ = MaterialTheme.CreateLight();

        var tb = new TextBlock("Heading Sample").Heading1();
        Assert.Equal(MaterialTypography.Heading1Key, tb.StyleKey);
        Assert.Equal(32f, tb.FontSize);
        Assert.True(tb.Bold);

        var subtext = new TextBlock("Footnote").Subtext();
        Assert.Equal(MaterialTypography.SubtextKey, subtext.StyleKey);
        Assert.Equal(12f, subtext.FontSize);
        Assert.True(subtext.Muted);

        var normal = new TextBlock("Body content").NormalText();
        Assert.Equal(MaterialTypography.NormalTextKey, normal.StyleKey);
        Assert.Equal(14f, normal.FontSize);
        Assert.False(normal.Bold);
    }

    [Fact]
    public void TextBlock_ItalicProperty_WorksAndAffectsMeasure()
    {
        var tb = new TextBlock("Italic Text Sample");
        Assert.False(tb.Italic);

        tb.Italic = true;
        Assert.True(tb.Italic);

        tb.Measure(new Size(1000, 1000));
        Assert.True(tb.DesiredSize.Width > 0);
        Assert.True(tb.DesiredSize.Height > 0);
    }

    [Fact]
    public void TextBlock_FontFamilyProperty_WorksAndAffectsMeasure()
    {
        var tb = new TextBlock("Sample Text").FontFamily("Arial");
        Assert.Equal("Arial", tb.FontFamily);

        tb.Measure(new Size(1000, 1000));
        Assert.True(tb.DesiredSize.Width > 0);
        Assert.True(tb.DesiredSize.Height > 0);
    }

    [Fact]
    public void TextBlock_LocalProperties_OverrideStyleSetters()
    {
        _ = MaterialTheme.CreateLight();

        // Style sets FontSize 32, Bold true
        var tb = new TextBlock("Overridden Heading").Heading1();
        Assert.Equal(32f, tb.FontSize);
        Assert.True(tb.Bold);

        // Local explicit overrides take precedence over style
        tb.FontSize = 40f;
        tb.Bold = false;
        tb.Italic = true;

        Assert.Equal(40f, tb.FontSize);
        Assert.False(tb.Bold);
        Assert.True(tb.Italic);
    }

    [Fact]
    public void TextBlock_FluentExtensions_ChainCorrectly()
    {
        var tb = new TextBlock("Fluent Chained Text")
            .Heading2()
            .Italic()
            .FontFamily("Georgia")
            .Foreground(Color.FromHex("#EF4444"))
            .TextAlignment(TextAlignment.Center)
            .TextWrapping(TextWrapping.Wrap);

        Assert.Equal(MaterialTypography.Heading2Key, tb.StyleKey);
        Assert.True(tb.Italic);
        Assert.Equal("Georgia", tb.FontFamily);
        Assert.Equal(Color.FromHex("#EF4444"), tb.Foreground);
        Assert.Equal(TextAlignment.Center, tb.TextAlignment);
        Assert.Equal(TextWrapping.Wrap, tb.TextWrapping);
    }

    [Fact]
    public void MaterialTypography_GetRegisteredStyle_ReturnsExpectedStyles()
    {
        var h1 = MaterialTypography.GetRegisteredStyle(MaterialTypography.Heading1Key);
        Assert.NotNull(h1);
        Assert.Equal(32f, h1.Setters.Find(s => s.Property == TextBlock.FontSizeProperty)?.Value);
        Assert.Equal(true, h1.Setters.Find(s => s.Property == TextBlock.BoldProperty)?.Value);

        var displayLarge = MaterialTypography.GetRegisteredStyle(MaterialTypography.DisplayLargeKey);
        Assert.NotNull(displayLarge);
        Assert.Equal(57f, displayLarge.Setters.Find(s => s.Property == TextBlock.FontSizeProperty)?.Value);
        Assert.Equal(false, displayLarge.Setters.Find(s => s.Property == TextBlock.BoldProperty)?.Value);

        var subtext = MaterialTypography.GetRegisteredStyle(MaterialTypography.SubtextKey);
        Assert.NotNull(subtext);
        Assert.Equal(12f, subtext.Setters.Find(s => s.Property == TextBlock.FontSizeProperty)?.Value);
        Assert.Equal(true, subtext.Setters.Find(s => s.Property == TextBlock.MutedProperty)?.Value);
    }

    [Fact]
    public void MaterialTextBlockRenderer_MutedProperty_ResolvesToOnSurfaceVariant_WhenUnsetOrOnSurface()
    {
        var colors = MaterialColorScheme.Light();
        var renderer = new MaterialTextBlockRenderer(colors);

        // 1. Default text block (no explicit color)
        var defaultTb = new TextBlock("Default Text");
        Assert.Equal(colors.OnSurface, renderer.GetTextColor(defaultTb));

        // 2. Muted text block without explicit color -> OnSurfaceVariant
        var mutedTb = new TextBlock("Muted Text").Muted();
        Assert.Equal(colors.OnSurfaceVariant, renderer.GetTextColor(mutedTb));

        // 3. Styled text block (NormalText has Foreground = colors.OnSurface) + Muted -> OnSurfaceVariant
        _ = MaterialTheme.CreateLight();
        var styledNormalTb = new TextBlock("Styled Normal").NormalText().Muted();
        Assert.Equal(colors.OnSurfaceVariant, renderer.GetTextColor(styledNormalTb));

        // 4. Subtext style (already has Foreground = colors.OnSurfaceVariant and Muted = true)
        var subtextTb = new TextBlock("Subtext").Subtext();
        Assert.Equal(colors.OnSurfaceVariant, renderer.GetTextColor(subtextTb));
    }

    [Fact]
    public void MaterialTextBlockRenderer_MutedProperty_DeemphasizesCustomColor_WithAlpha()
    {
        var colors = MaterialColorScheme.Light();
        var renderer = new MaterialTextBlockRenderer(colors);

        var customColor = Color.FromHex("#3B82F6"); // Primary Blue
        var tb = new TextBlock("Custom Color").Foreground(customColor);

        // Not muted: full color
        Assert.Equal(customColor, renderer.GetTextColor(tb));

        // Muted: custom color with 0.6f alpha
        tb.Muted = true;
        var expectedMutedColor = customColor.WithAlpha(customColor.Af * 0.6f);
        Assert.Equal(expectedMutedColor, renderer.GetTextColor(tb));
    }

    [Fact]
    public void Button_BindVariant_UpdatesVariantOnStateChange()
    {
        var vm = new TestButtonViewModel { IsActive = false };
        var button = new Button().BindVariant(vm, x => x.IsActive ? ButtonVariant.Filled : ButtonVariant.Outlined);

        Assert.Equal(ButtonVariant.Outlined, button.Variant);

        vm.IsActive = true;
        Assert.Equal(ButtonVariant.Filled, button.Variant);

        vm.IsActive = false;
        Assert.Equal(ButtonVariant.Outlined, button.Variant);
    }

    [Fact]
    public void MaterialTextBlockRenderer_DarkMode_ResolvesBrightOnSurface()
    {
        var darkColors = MaterialColorScheme.Dark();
        var darkRenderer = new MaterialTextBlockRenderer(darkColors);

        // TitleLarge in dark mode resolves to darkColors.OnSurface (crisp bright text!)
        var titleTb = new TextBlock("Heading").TitleLarge();
        Assert.Equal(darkColors.OnSurface, darkRenderer.GetTextColor(titleTb));

        // Subtext in dark mode resolves to darkColors.OnSurfaceVariant (soft bright silver!)
        var subtextTb = new TextBlock("Muted Footnote").Subtext();
        Assert.Equal(darkColors.OnSurfaceVariant, darkRenderer.GetTextColor(subtextTb));
    }

    private class TestButtonViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsActive)));
            }
        }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}
