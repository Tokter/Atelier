using System;
using System.Collections.Generic;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Styling;
using Atelier.Layout;
using Atelier.Rendering;
using Atelier.Theming.Material;
using Atelier.Theming.Material.Renderers;

namespace Atelier.Tests;

public class TextBlockLayoutTests
{
    private static float Spacing(float fontSize = 14f) => TextMeasurer.GetFontSpacing(fontSize);

    private static List<string> LineTexts(TextBlock tb)
    {
        var lines = tb.GetLines();
        var result = new List<string>();
        for (int i = 0; i < lines.Count; i++)
        {
            result.Add(tb.GetLineText(i));
        }
        return result;
    }

    [Fact]
    public void NoWrap_HonorsLineBreaks_InMeasureAndLayout()
    {
        var tb = new TextBlock("alpha\nb");
        tb.Measure(new Size(float.PositiveInfinity, float.PositiveInfinity));

        Assert.Equal(2 * Spacing(), tb.DesiredSize.Height, 2);
        Assert.Equal(TextMeasurer.Measure("alpha", 14f).Width, tb.DesiredSize.Width, 2);
        Assert.Equal(new[] { "alpha", "b" }, LineTexts(tb));
    }

    [Fact]
    public void Wrap_WithInfiniteWidth_StillHonorsLineBreaks()
    {
        var tb = new TextBlock("one\ntwo\nthree") { TextWrapping = TextWrapping.Wrap };
        tb.Measure(new Size(float.PositiveInfinity, float.PositiveInfinity));

        Assert.Equal(3 * Spacing(), tb.DesiredSize.Height, 2);
        Assert.Equal(new[] { "one", "two", "three" }, LineTexts(tb));
    }

    [Fact]
    public void CarriageReturnLineFeed_IsOneBreak_AndCarriageReturnIsNotDrawn()
    {
        var tb = new TextBlock("a\r\nb");
        tb.Measure(new Size(500, 500));

        Assert.Equal(new[] { "a", "b" }, LineTexts(tb));
        Assert.Equal(2 * Spacing(), tb.DesiredSize.Height, 2);
    }

    [Fact]
    public void TrailingLineBreak_AddsEmptyLine()
    {
        var tb = new TextBlock("a\n");
        tb.Measure(new Size(500, 500));

        Assert.Equal(new[] { "a", "" }, LineTexts(tb));
    }

    [Fact]
    public void Wrap_BreaksAtSpaces_AndLinesFit()
    {
        var tb = new TextBlock("the quick brown fox jumps over the lazy dog") { TextWrapping = TextWrapping.Wrap };
        float width = TextMeasurer.Measure("the quick brown", 14f).Width + 1f;
        tb.Measure(new Size(width, float.PositiveInfinity));

        var lines = LineTexts(tb);
        Assert.Equal("the quick brown", lines[0]);
        Assert.Equal("the quick brown fox jumps over the lazy dog", string.Join(" ", lines));
        foreach (var line in tb.GetLines())
        {
            Assert.True(line.Width <= width, $"line width {line.Width} > {width}");
        }
        Assert.Equal(lines.Count * Spacing(), tb.DesiredSize.Height, 2);
    }

    [Fact]
    public void Wrap_LongSingleWord_IsBrokenBetweenCharacters()
    {
        string word = new string('W', 60);
        var tb = new TextBlock(word) { TextWrapping = TextWrapping.Wrap };
        tb.Measure(new Size(100, float.PositiveInfinity));

        var lines = LineTexts(tb);
        Assert.True(lines.Count > 1);
        Assert.Equal(word, string.Concat(lines));
        Assert.True(tb.DesiredSize.Width <= 100f);
        foreach (var line in tb.GetLines())
        {
            Assert.True(line.Width <= 100f);
        }
    }

    [Fact]
    public void Wrap_NeverSplitsSurrogatePairs()
    {
        string text = string.Concat(System.Linq.Enumerable.Repeat("\U0001F600", 30));
        var tb = new TextBlock(text) { TextWrapping = TextWrapping.Wrap, FontFamily = null };
        tb.Measure(new Size(40, float.PositiveInfinity));

        foreach (var line in LineTexts(tb))
        {
            Assert.Equal(0, line.Length % 2);
            if (line.Length > 0)
            {
                Assert.True(char.IsHighSurrogate(line[0]));
            }
        }
    }

    [Fact]
    public void CharacterEllipsis_TrimsToWidth()
    {
        var tb = new TextBlock("A fairly long line of text that will not fit") { TextTrimming = TextTrimming.CharacterEllipsis };
        tb.Measure(new Size(100, 100));

        var lines = tb.GetLines();
        Assert.Single(lines);
        Assert.True(lines[0].HasEllipsis);
        Assert.True(lines[0].Width <= 100f);
        Assert.EndsWith(TextMeasurer.Ellipsis, tb.GetLineText(0));
        Assert.True(tb.DesiredSize.Width <= 100f);
    }

    [Fact]
    public void WordEllipsis_TrimsAtWordBoundary()
    {
        const string text = "Alpha Beta Gamma Delta Epsilon";
        var tb = new TextBlock(text) { TextTrimming = TextTrimming.WordEllipsis };
        float width = TextMeasurer.Measure("Alpha Beta Ga", 14f).Width;
        tb.Measure(new Size(width, 100));

        Assert.Equal("Alpha Beta" + TextMeasurer.Ellipsis, tb.GetLineText(0));
    }

    [Fact]
    public void TextThatFits_IsNotTrimmed()
    {
        var tb = new TextBlock("short") { TextTrimming = TextTrimming.CharacterEllipsis };
        tb.Measure(new Size(500, 100));

        Assert.False(tb.GetLines()[0].HasEllipsis);
        Assert.Equal("short", tb.GetLineText(0));
    }

    [Fact]
    public void MaxLines_LimitsLines_AndAddsEllipsisWhenTrimming()
    {
        var tb = new TextBlock("one two three four five six seven eight nine ten")
        {
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
        };
        float width = TextMeasurer.Measure("one two", 14f).Width + 1f;
        tb.Measure(new Size(width, float.PositiveInfinity));

        Assert.Equal(2, tb.GetLines().Count);
        Assert.Equal(2 * Spacing(), tb.DesiredSize.Height, 2);
        Assert.False(tb.GetLines()[1].HasEllipsis);

        tb.TextTrimming = TextTrimming.WordEllipsis;
        tb.Measure(new Size(width, float.PositiveInfinity));
        Assert.True(tb.GetLines()[1].HasEllipsis);
        Assert.EndsWith(TextMeasurer.Ellipsis, tb.GetLineText(1));
        Assert.True(tb.GetLines()[1].Width <= width);
    }

    [Fact]
    public void LineHeight_SetsLineAdvanceAndHeight()
    {
        var tb = new TextBlock("a\nb\nc") { LineHeight = 30f };
        tb.Measure(new Size(500, 500));

        Assert.Equal(90f, tb.DesiredSize.Height, 2);
        Assert.Equal(30f, tb.LineAdvance);
        Assert.Throws<ArgumentException>(() => tb.LineHeight = -1f);
        Assert.Throws<ArgumentException>(() => tb.MaxLines = -1);
    }

    [Fact]
    public void Text_Null_IsStoredAsEmpty()
    {
        var tb = new TextBlock("x");
        tb.Text = null!;
        Assert.Equal(string.Empty, tb.Text);
        tb.Measure(new Size(100, 100));
        Assert.Equal(Size.Zero, tb.DesiredSize);
    }

    [Fact]
    public void Arrange_WithSlightlySmallerWidth_ReusesMeasuredLines()
    {
        var tb = new TextBlock("the quick brown fox jumps over the lazy dog") { TextWrapping = TextWrapping.Wrap };
        tb.Measure(new Size(120, float.PositiveInfinity));
        var measured = LineTexts(tb);
        string firstLine = tb.GetLineText(0);

        // Layout rounding can make the arranged width a fraction smaller than the widest line.
        float arrangedWidth = tb.DesiredSize.Width - 0.3f;
        tb.Arrange(new Rect(0, 0, arrangedWidth, tb.DesiredSize.Height));

        Assert.Equal(measured, LineTexts(tb));
        Assert.Same(firstLine, tb.GetLineText(0)); // not re-laid out
    }

    [Fact]
    public void Arrange_WithMuchWiderWidth_RewrapsToFinalWidth()
    {
        var tb = new TextBlock("the quick brown fox jumps over the lazy dog") { TextWrapping = TextWrapping.Wrap };
        tb.Measure(new Size(80, float.PositiveInfinity));
        int measuredLines = tb.GetLines().Count;

        tb.Arrange(new Rect(0, 0, 1000, 100));

        Assert.True(measuredLines > 1);
        Assert.Single(tb.GetLines());
    }

    [Fact]
    public void RepeatedMeasure_WithUnchangedInputs_DoesNotAllocate()
    {
        var tb = new TextBlock("the quick brown fox jumps over the lazy dog") { TextWrapping = TextWrapping.Wrap };
        var size = new Size(150, float.PositiveInfinity);
        for (int i = 0; i < 200; i++) // warm up (JIT tiering)
        {
            tb.InvalidateMeasure();
            tb.Measure(size);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            tb.InvalidateMeasure();
            tb.Measure(size);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void WrappedRemeasure_AtNewWidths_AllocatesLittle()
    {
        var tb = new TextBlock("the quick brown fox jumps over the lazy dog and keeps running far away") { TextWrapping = TextWrapping.Wrap };
        tb.Measure(new Size(60, float.PositiveInfinity)); // grows the line list to its largest size

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            tb.InvalidateMeasure();
            tb.Measure(new Size(100 + i, float.PositiveInfinity));
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 1024, $"re-wrapping 1000 times allocated {allocated} bytes");
    }

    [Fact]
    public void GetLineText_IsCachedPerLayout()
    {
        var tb = new TextBlock("first line\nsecond line");
        tb.Measure(new Size(500, 500));
        tb.Arrange(new Rect(0, 0, 500, 500));

        Assert.Same(tb.GetLineText(1), tb.GetLineText(1));

        var single = new TextBlock("only");
        single.Measure(new Size(500, 500));
        Assert.Same(single.Text, single.GetLineText(0));
    }

    [Fact]
    public void FontCache_StaysBounded_UnderAnimatedFontSize()
    {
        for (float size = 8f; size < 60f; size += 0.01f)
        {
            TextMeasurer.Measure("x", size);
        }

        Assert.True(TextMeasurer.CachedFontCount <= 128, $"{TextMeasurer.CachedFontCount} fonts cached");
    }

    [Fact]
    public void GetTextColor_UsesThemeColor_WhileForegroundIsUnset()
    {
        var dark = MaterialColorScheme.Dark();
        var renderer = new MaterialTextBlockRenderer(dark);
        var tb = new TextBlock("x");

        Assert.Equal(dark.OnSurface, renderer.GetTextColor(tb));

        tb.Muted = true;
        Assert.Equal(dark.OnSurfaceVariant, renderer.GetTextColor(tb));
    }

    [Fact]
    public void GetTextColor_HonorsExplicitBlack_InDarkTheme()
    {
        var renderer = new MaterialTextBlockRenderer(MaterialColorScheme.Dark());

        var local = new TextBlock("x") { Foreground = Color.Black };
        Assert.Equal(Color.Black, renderer.GetTextColor(local));

        // Inherited from an ancestor that set it.
        var inherited = new TextBlock("y");
        var card = new ContentControl { Content = inherited, Foreground = Color.Black };
        Assert.Equal(ValueSource.Inherited, inherited.GetValueSource(TextBlock.ForegroundProperty));
        Assert.Equal(Color.Black, renderer.GetTextColor(inherited));

        // Applied by a style.
        var panel = new StackPanel();
        panel.Styles.Add(new Style(typeof(TextBlock)).Set(TextBlock.ForegroundProperty, Color.Black));
        var styled = new TextBlock("z");
        panel.Add(styled);
        Assert.Equal(Color.Black, renderer.GetTextColor(styled));

        // Explicitly set to the theme color: still honored as is.
        var dark = MaterialColorScheme.Dark();
        var surface = new TextBlock("s") { Foreground = dark.OnSurface, Muted = true };
        Assert.Equal(dark.OnSurface.WithAlpha(dark.OnSurface.Af * 0.6f), renderer.GetTextColor(surface));
        GC.KeepAlive(card);
    }
}
