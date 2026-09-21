using System;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Platform;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;

namespace Atelier.Tests;

public class TextBoxSelectionAndClipboardTests
{
    public TextBoxSelectionAndClipboardTests()
    {
        // Use in-memory clipboard for tests
        Clipboard.Current = new Clipboard.NullClipboard();
    }

    [Fact]
    public void TextBox_Select_SetsSelectionStartAndLength()
    {
        var tb = new TextBox("Hello World");
        tb.Select(0, 5);

        Assert.True(tb.HasSelection);
        Assert.Equal(0, tb.SelectionStart);
        Assert.Equal(5, tb.SelectionLength);
        Assert.Equal("Hello", tb.SelectedText);
    }

    [Fact]
    public void TextBox_SelectAll_SelectsEntireText()
    {
        var tb = new TextBox("Hello World");
        tb.SelectAll();

        Assert.True(tb.HasSelection);
        Assert.Equal(0, tb.SelectionStart);
        Assert.Equal(11, tb.SelectionLength);
        Assert.Equal("Hello World", tb.SelectedText);
    }

    [Fact]
    public void TextBox_ClearSelection_ClearsSelectionLength()
    {
        var tb = new TextBox("Hello World");
        tb.Select(0, 5);
        Assert.True(tb.HasSelection);

        tb.ClearSelection();
        Assert.False(tb.HasSelection);
        Assert.Equal(0, tb.SelectionLength);
        Assert.Equal(string.Empty, tb.SelectedText);
    }

    [Fact]
    public void TextBox_KeyboardSelection_ShiftRight_ExpandsSelection()
    {
        var tb = new TextBox("Hello World");
        FocusManager.SetFocus(tb);
        tb.Select(0, 0); // Clear at start

        // Press Shift + Right twice
        tb.OnKeyDown(new KeyEventArgs(Key.Right, 0, ModifierKeys.Shift, true));
        Assert.Equal(1, tb.SelectionLength);
        Assert.Equal("H", tb.SelectedText);

        tb.OnKeyDown(new KeyEventArgs(Key.Right, 0, ModifierKeys.Shift, true));
        Assert.Equal(2, tb.SelectionLength);
        Assert.Equal("He", tb.SelectedText);
    }

    [Fact]
    public void TextBox_KeyboardSelection_ShiftEnd_SelectsToEnd()
    {
        var tb = new TextBox("Hello World");
        FocusManager.SetFocus(tb);
        tb.Select(6, 0); // Position caret at "World"

        tb.OnKeyDown(new KeyEventArgs(Key.End, 0, ModifierKeys.Shift, true));
        Assert.Equal("World", tb.SelectedText);
        Assert.Equal(6, tb.SelectionStart);
        Assert.Equal(5, tb.SelectionLength);
    }

    [Fact]
    public void TextBox_KeyboardSelection_ShiftHome_SelectsToStart()
    {
        var tb = new TextBox("Hello World");
        FocusManager.SetFocus(tb);
        tb.Select(5, 0); // Position caret after "Hello"

        tb.OnKeyDown(new KeyEventArgs(Key.Home, 0, ModifierKeys.Shift, true));
        Assert.Equal("Hello", tb.SelectedText);
        Assert.Equal(0, tb.SelectionStart);
        Assert.Equal(5, tb.SelectionLength);
    }

    [Fact]
    public void TextBox_NavigationWithoutShift_ClearsSelection()
    {
        var tb = new TextBox("Hello World");
        FocusManager.SetFocus(tb);
        tb.Select(0, 5);
        Assert.True(tb.HasSelection);

        // Press Right without shift: collapses to end of selection
        tb.OnKeyDown(new KeyEventArgs(Key.Right, 0, ModifierKeys.None, true));
        Assert.False(tb.HasSelection);
        Assert.Equal(5, tb.CaretIndex);

        // Press Left without shift: moves caret left
        tb.OnKeyDown(new KeyEventArgs(Key.Left, 0, ModifierKeys.None, true));
        Assert.False(tb.HasSelection);
        Assert.Equal(4, tb.CaretIndex);
    }

    [Fact]
    public void TextBox_TextInput_ReplacesSelection()
    {
        var tb = new TextBox("Hello World");
        FocusManager.SetFocus(tb);
        tb.Select(0, 5);

        tb.OnTextInput(new TextInputEventArgs("Hi"));

        Assert.Equal("Hi World", tb.Text);
        Assert.Equal(2, tb.CaretIndex);
        Assert.False(tb.HasSelection);
    }

    [Fact]
    public void TextBox_Backspace_DeletesSelection()
    {
        var tb = new TextBox("Hello World");
        FocusManager.SetFocus(tb);
        tb.Select(6, 5); // Select "World"

        tb.OnKeyDown(new KeyEventArgs(Key.Backspace, 8, ModifierKeys.None, true));

        Assert.Equal("Hello ", tb.Text);
        Assert.Equal(6, tb.CaretIndex);
        Assert.False(tb.HasSelection);
    }

    [Fact]
    public void TextBox_Delete_DeletesSelection()
    {
        var tb = new TextBox("Hello World");
        FocusManager.SetFocus(tb);
        tb.Select(0, 6); // Select "Hello "

        tb.OnKeyDown(new KeyEventArgs(Key.Delete, 46, ModifierKeys.None, true));

        Assert.Equal("World", tb.Text);
        Assert.Equal(0, tb.CaretIndex);
        Assert.False(tb.HasSelection);
    }

    [Fact]
    public void TextBox_CopyAndPaste_ViaClipboard()
    {
        var tb1 = new TextBox("Copy Me");
        tb1.SelectAll();
        tb1.Copy();

        Assert.Equal("Copy Me", Clipboard.GetText());

        var tb2 = new TextBox("Paste Here: ");
        FocusManager.SetFocus(tb2);
        tb2.CaretIndex = tb2.Text.Length;
        tb2.Paste();

        Assert.Equal("Paste Here: Copy Me", tb2.Text);
    }

    [Fact]
    public void TextBox_CutAndPaste_ViaClipboard()
    {
        var tb = new TextBox("Cut And Paste");
        FocusManager.SetFocus(tb);
        tb.Select(0, 4); // Select "Cut "
        tb.Cut();

        Assert.Equal("And Paste", tb.Text);
        Assert.Equal("Cut ", Clipboard.GetText());

        tb.CaretIndex = tb.Text.Length;
        tb.Paste();

        Assert.Equal("And PasteCut ", tb.Text);
    }

    [Fact]
    public void TextBox_KeyboardShortcuts_CtrlA_CtrlC_CtrlX_CtrlV()
    {
        var tb = new TextBox("Quick Brown Fox");
        FocusManager.SetFocus(tb);

        // Ctrl + A
        tb.OnKeyDown(new KeyEventArgs(Key.A, 0, ModifierKeys.Control, true));
        Assert.Equal("Quick Brown Fox", tb.SelectedText);

        // Ctrl + C
        tb.OnKeyDown(new KeyEventArgs(Key.C, 0, ModifierKeys.Control, true));
        Assert.Equal("Quick Brown Fox", Clipboard.GetText());

        // Ctrl + X
        tb.OnKeyDown(new KeyEventArgs(Key.X, 0, ModifierKeys.Control, true));
        Assert.Equal(string.Empty, tb.Text);

        // Ctrl + V
        tb.OnKeyDown(new KeyEventArgs(Key.V, 0, ModifierKeys.Control, true));
        Assert.Equal("Quick Brown Fox", tb.Text);
    }

    [Fact]
    public void TextBox_MouseDragSelection()
    {
        var tb = new TextBox("Mouse Drag Selection Test");
        FocusManager.SetFocus(tb);

        // Click at X=14 (start of text after padding)
        tb.OnPointerPressed(new PointerEventArgs(new Point(14, 20), PointerButtons.Left));
        Assert.Equal(0, tb.SelectionAnchor);
        Assert.Equal(0, tb.CaretIndex);

        // Drag to X=150
        tb.OnPointerMoved(new PointerEventArgs(new Point(150, 20), PointerButtons.Left));
        Assert.True(tb.CaretIndex > 0);
        Assert.True(tb.HasSelection);
        Assert.True(tb.SelectionLength > 0);

        // Release
        tb.OnPointerReleased(new PointerEventArgs(new Point(150, 20), PointerButtons.Left));
        Assert.True(tb.HasSelection);
    }

    [Fact]
    public void TextBox_CaretWidth_DefaultAndAssignment()
    {
        var tb = new TextBox("Hello");
        Assert.Equal(2f, tb.CaretWidth);

        tb.CaretWidth = 1f;
        Assert.Equal(1f, tb.CaretWidth);
    }

    [Fact]
    public void DrawingContext_DrawPixelRect_RendersCrispPixelsWithoutBlur()
    {
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(20, 20, SkiaSharp.SKColorType.Rgba8888));
        surface.Canvas.Clear(SkiaSharp.SKColors.Transparent);

        var paintRegistry = new Atelier.Rendering.PaintRegistry();
        var context = new Atelier.Rendering.DrawingContext(surface.Canvas, paintRegistry);

        // Draw pixel rect with fractional coordinates that would normally blur:
        // X = 5.2f (snaps to 5), Y = 4.1f (snaps to 4), W = 2f, H = 10f
        // Expected filled pixel columns: X=5, X=6
        // Expected unfilled pixel columns: X=4, X=7
        context.DrawPixelRect(new Rect(5.2f, 4.1f, 2f, 10f), Color.FromRgb(255, 0, 0));
        surface.Canvas.Flush();

        using var image = surface.Snapshot();
        using var bitmap = SkiaSharp.SKBitmap.FromImage(image);

        // Test inside the snapped rect (X=5, Y=6) -> Should be 100% solid red (A=255, R=255)
        var insidePixel1 = bitmap.GetPixel(5, 6);
        Assert.Equal(255, insidePixel1.Alpha);
        Assert.Equal(255, insidePixel1.Red);

        var insidePixel2 = bitmap.GetPixel(6, 6);
        Assert.Equal(255, insidePixel2.Alpha);
        Assert.Equal(255, insidePixel2.Red);

        // Test outside the snapped rect (X=4, Y=6) -> Should be untouched 0% alpha (NOT blurred/semi-transparent!)
        var outsideLeft = bitmap.GetPixel(4, 6);
        Assert.Equal(0, outsideLeft.Alpha);

        // Test outside the snapped rect (X=7, Y=6) -> Should be untouched 0% alpha
        var outsideRight = bitmap.GetPixel(7, 6);
        Assert.Equal(0, outsideRight.Alpha);
    }

    [Fact]
    public void TextBox_HorizontalScroll_WhenCaretPastViewport_ScrollsRight()
    {
        var tb = new TextBox("The quick brown fox jumps over the lazy dog and runs across the field");
        tb.Arrange(new Rect(0, 0, 100, 48));

        // Viewport width is 100 - 16 - 16 = 68px
        Assert.Equal(68f, tb.GetViewportWidth());

        // Place caret at start: ScrollOffset should be 0
        tb.CaretIndex = 0;
        Assert.Equal(0f, tb.ScrollOffset);

        // Move caret to end
        tb.CaretIndex = tb.Text.Length;
        Assert.True(tb.ScrollOffset > 0f);

        // Caret position relative to viewport should be <= viewport width
        float textWidth = Atelier.Rendering.TextMeasurer.Measure(tb.Text, tb.FontSize, tb.FontFamily).Width;
        float relativeCaretX = textWidth - tb.ScrollOffset;
        Assert.True(relativeCaretX <= tb.GetViewportWidth());
    }

    [Fact]
    public void TextBox_HorizontalScroll_WhenCaretAtStart_ScrollsBackToZero()
    {
        var tb = new TextBox("The quick brown fox jumps over the lazy dog and runs across the field");
        tb.Arrange(new Rect(0, 0, 100, 48));
        FocusManager.SetFocus(tb);

        // Move to end (scrolls right)
        tb.OnKeyDown(new KeyEventArgs(Key.End, 0, ModifierKeys.None, true));
        Assert.True(tb.ScrollOffset > 0f);

        // Press Home key: scrolls back to start
        tb.OnKeyDown(new KeyEventArgs(Key.Home, 0, ModifierKeys.None, true));
        Assert.Equal(0, tb.CaretIndex);
        Assert.Equal(0f, tb.ScrollOffset);
    }

    [Fact]
    public void TextBox_WordNavigation_CtrlLeft_And_CtrlRight()
    {
        var tb = new TextBox("The quick brown fox");
        FocusManager.SetFocus(tb);
        tb.CaretIndex = 0;

        // Ctrl + Right: jumps word by word
        tb.OnKeyDown(new KeyEventArgs(Key.Right, 0, ModifierKeys.Control, true));
        Assert.Equal(4, tb.CaretIndex); // start of "quick"

        tb.OnKeyDown(new KeyEventArgs(Key.Right, 0, ModifierKeys.Control, true));
        Assert.Equal(10, tb.CaretIndex); // start of "brown"

        tb.OnKeyDown(new KeyEventArgs(Key.Right, 0, ModifierKeys.Control, true));
        Assert.Equal(16, tb.CaretIndex); // start of "fox"

        tb.OnKeyDown(new KeyEventArgs(Key.Right, 0, ModifierKeys.Control, true));
        Assert.Equal(19, tb.CaretIndex); // end of "fox"

        // Ctrl + Left: jumps back word by word
        tb.OnKeyDown(new KeyEventArgs(Key.Left, 0, ModifierKeys.Control, true));
        Assert.Equal(16, tb.CaretIndex);

        tb.OnKeyDown(new KeyEventArgs(Key.Left, 0, ModifierKeys.Control, true));
        Assert.Equal(10, tb.CaretIndex);

        tb.OnKeyDown(new KeyEventArgs(Key.Left, 0, ModifierKeys.Control, true));
        Assert.Equal(4, tb.CaretIndex);

        tb.OnKeyDown(new KeyEventArgs(Key.Left, 0, ModifierKeys.Control, true));
        Assert.Equal(0, tb.CaretIndex);
    }

    [Fact]
    public void TextBox_WordSelection_CtrlShiftLeft_And_CtrlShiftRight()
    {
        var tb = new TextBox("The quick brown fox");
        FocusManager.SetFocus(tb);
        tb.CaretIndex = 0;

        // Ctrl + Shift + Right: selects word by word
        tb.OnKeyDown(new KeyEventArgs(Key.Right, 0, ModifierKeys.Control | ModifierKeys.Shift, true));
        Assert.Equal(4, tb.CaretIndex);
        Assert.Equal(0, tb.SelectionAnchor);
        Assert.Equal("The ", tb.SelectedText);

        tb.OnKeyDown(new KeyEventArgs(Key.Right, 0, ModifierKeys.Control | ModifierKeys.Shift, true));
        Assert.Equal(10, tb.CaretIndex);
        Assert.Equal("The quick ", tb.SelectedText);

        // Ctrl + Shift + Left: contracts selection word by word
        tb.OnKeyDown(new KeyEventArgs(Key.Left, 0, ModifierKeys.Control | ModifierKeys.Shift, true));
        Assert.Equal(4, tb.CaretIndex);
        Assert.Equal("The ", tb.SelectedText);

        tb.OnKeyDown(new KeyEventArgs(Key.Left, 0, ModifierKeys.Control | ModifierKeys.Shift, true));
        Assert.Equal(0, tb.CaretIndex);
        Assert.False(tb.HasSelection);
    }

    [Fact]
    public void TextBox_WordDeletion_CtrlBackspace_And_CtrlDelete()
    {
        var tb = new TextBox("The quick brown fox");
        FocusManager.SetFocus(tb);
        tb.CaretIndex = 10; // at "brown"

        // Ctrl + Backspace deletes "quick "
        tb.OnKeyDown(new KeyEventArgs(Key.Backspace, 8, ModifierKeys.Control, true));
        Assert.Equal("The brown fox", tb.Text);
        Assert.Equal(4, tb.CaretIndex);

        // Ctrl + Delete deletes "brown "
        tb.OnKeyDown(new KeyEventArgs(Key.Delete, 46, ModifierKeys.Control, true));
        Assert.Equal("The fox", tb.Text);
        Assert.Equal(4, tb.CaretIndex);
    }

    [Fact]
    public void TextBox_DoubleClick_SelectsWordUnderCursor()
    {
        var tb = new TextBox("The quick brown fox");
        tb.Arrange(new Rect(0, 0, 400, 48));
        FocusManager.SetFocus(tb);

        // Determine X coordinate for "brown"
        float prefixWidth = Atelier.Rendering.TextMeasurer.Measure("The quick ", tb.FontSize, tb.FontFamily).Width;
        float wordMiddleWidth = Atelier.Rendering.TextMeasurer.Measure("bro", tb.FontSize, tb.FontFamily).Width;
        float clickX = tb.GetTextContentStartX() + prefixWidth + wordMiddleWidth;

        // First click
        tb.OnPointerPressed(new PointerEventArgs(new Point(clickX, 24), new Point(clickX, 24), PointerButtons.Left, 1000));
        tb.OnPointerReleased(new PointerEventArgs(new Point(clickX, 24), new Point(clickX, 24), PointerButtons.Left, 1050));

        // Second click within 200ms at same spot
        tb.OnPointerPressed(new PointerEventArgs(new Point(clickX, 24), new Point(clickX, 24), PointerButtons.Left, 1200));

        Assert.True(tb.HasSelection);
        Assert.Equal("brown", tb.SelectedText);
    }

    [Fact]
    public void TextBox_DoubleClick_WhenScrolled_SelectsWordUnderCursor()
    {
        var tb = new TextBox("Start Word and some very long filler in between and then EndWord");
        tb.Arrange(new Rect(0, 0, 150, 48));
        FocusManager.SetFocus(tb);

        // Scroll to end
        tb.CaretIndex = tb.Text.Length;
        Assert.True(tb.ScrollOffset > 0f);

        // Compute local X for "EndWord"
        float textBeforeEnd = Atelier.Rendering.TextMeasurer.Measure("Start Word and some very long filler in between and then ", tb.FontSize, tb.FontFamily).Width;
        float endMiddle = Atelier.Rendering.TextMeasurer.Measure("End", tb.FontSize, tb.FontFamily).Width;
        float clickX = tb.GetTextContentStartX() + (textBeforeEnd + endMiddle) - tb.ScrollOffset;

        // Double click at clickX
        tb.OnPointerPressed(new PointerEventArgs(new Point(clickX, 24), new Point(clickX, 24), PointerButtons.Left, 2000));
        tb.OnPointerReleased(new PointerEventArgs(new Point(clickX, 24), new Point(clickX, 24), PointerButtons.Left, 2050));
        tb.OnPointerPressed(new PointerEventArgs(new Point(clickX, 24), new Point(clickX, 24), PointerButtons.Left, 2200));

        Assert.True(tb.HasSelection);
        Assert.Equal("EndWord", tb.SelectedText);
    }

    [Fact]
    public void TextBox_MaterialRenderer_RendersWithClippingAndScrollOffset()
    {
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(200, 60, SkiaSharp.SKColorType.Rgba8888));
        var paintRegistry = new Atelier.Rendering.PaintRegistry();
        var context = new Atelier.Rendering.DrawingContext(surface.Canvas, paintRegistry);

        var colors = Atelier.Theming.Material.MaterialColorScheme.Light();
        var renderer = new Atelier.Theming.Material.Renderers.MaterialTextBoxRenderer(colors);

        var tb = new TextBox("This is a very long text that exceeds the bounds of the text box");
        tb.Arrange(new Rect(0, 0, 120, 48));
        FocusManager.SetFocus(tb);
        tb.CaretIndex = tb.Text.Length; // Scrolled

        // Should render without exception, pushing clip and applying ScrollOffset
        renderer.Render(tb, ref context);
        surface.Canvas.Flush();
        Assert.True(tb.ScrollOffset > 0f);
    }
}
