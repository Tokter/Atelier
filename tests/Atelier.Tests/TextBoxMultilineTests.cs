using System;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Platform;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Markup;

namespace Atelier.Tests;

public class TextBoxMultilineTests
{
    public TextBoxMultilineTests()
    {
        Clipboard.Current = new Clipboard.NullClipboard();
    }

    // A focused multi-line box laid out at its desired height (or a fixed one).
    private static TextBox Layout(TextBox tb, float width = 300, float? height = null)
    {
        tb.Measure(new Size(width, float.PositiveInfinity));
        tb.Arrange(new Rect(0, 0, width, height ?? tb.DesiredSize.Height));
        FocusManager.SetFocus(tb);
        return tb;
    }

    private static TextBox Area(string text, float width = 300, float? height = null) =>
        Layout(new TextBox(text).AcceptsReturn(), width, height);

    private static void Key(TextBox tb, Key key, ModifierKeys modifiers = ModifierKeys.None) =>
        tb.OnKeyDown(new KeyEventArgs(key, 0, modifiers, true));

    private static void Type(TextBox tb, string text)
    {
        foreach (char c in text)
        {
            tb.OnTextInput(new TextInputEventArgs(c.ToString()));
        }
    }

    // Lays the box out again after its text changed, as the window would before the next frame.
    private static void Relayout(TextBox tb) => Layout(tb, tb.Bounds.Width);

    #region Modes

    [Fact]
    public void ByDefault_TheBoxIsSingleLine()
    {
        var tb = new TextBox("a\nb");

        Assert.False(tb.IsMultiline);
        Assert.Equal(1, tb.LineCount);
    }

    [Fact]
    public void AcceptsReturnOrWrapping_MakeTheBoxMultiline()
    {
        Assert.True(new TextBox().AcceptsReturn().IsMultiline);
        Assert.True(new TextBox().TextWrapping(TextWrapping.Wrap).IsMultiline);
    }

    [Fact]
    public void PasswordFields_StaySingleLine()
    {
        var tb = new TextBox("secret").AcceptsReturn().PasswordChar();

        Assert.False(tb.IsMultiline);
    }

    [Fact]
    public void Multiline_Markup_SetsAllLineOptions()
    {
        var tb = new TextBox().Multiline(minLines: 4, maxLines: 10);

        Assert.True(tb.AcceptsReturn);
        Assert.Equal(TextWrapping.Wrap, tb.TextWrapping);
        Assert.Equal(4, tb.MinLines);
        Assert.Equal(10, tb.MaxLines);
    }

    #endregion

    #region Lines

    [Fact]
    public void Lines_SplitAtEachKindOfLineBreak()
    {
        var tb = Area("one\ntwo\r\nthree\rfour");

        Assert.Equal(4, tb.LineCount);
        Assert.Equal("one", tb.GetLineText(0));
        Assert.Equal("two", tb.GetLineText(1));
        Assert.Equal("three", tb.GetLineText(2));
        Assert.Equal("four", tb.GetLineText(3));
        Assert.Equal(4, tb.GetLineStart(1));
        Assert.Equal(9, tb.GetLineStart(2)); // after "\r\n"
    }

    [Fact]
    public void TextEndingInALineBreak_HasAnEmptyLastLine()
    {
        var tb = Area("one\n");

        Assert.Equal(2, tb.LineCount);
        Assert.Equal(string.Empty, tb.GetLineText(1));
        Assert.Equal(1, tb.GetLineIndexFromCharacterIndex(4));
    }

    [Fact]
    public void LineBreaks_TakeNoWidth()
    {
        var tb = Area("ab\ncd");

        Assert.Equal(tb.GetCharacterOffset(2), tb.GetCharacterOffset(3));
        Assert.Equal(tb.GetCharacterX(0), tb.GetCharacterX(3)); // both lines start at the same x
    }

    [Fact]
    public void Wrapping_BreaksAfterTheLastSpaceThatFits()
    {
        var tb = Layout(new TextBox("alpha beta gamma delta epsilon zeta").TextWrapping(TextWrapping.Wrap), width: 160);

        Assert.True(tb.LineCount > 1);
        for (int line = 0; line < tb.LineCount; line++)
        {
            string text = tb.GetLineText(line);
            Assert.False(text.StartsWith(' '), $"line {line} starts with a space: \"{text}\"");
            Assert.True(tb.GetLineWidth(line) <= tb.GetViewportWidth() || text.EndsWith(' '), $"line {line} overflows: \"{text}\"");
        }
        Assert.Equal(tb.Text, string.Concat(Enumerable(tb)));
    }

    [Fact]
    public void Wrapping_BreaksAWordThatIsTooLongBetweenCharacters()
    {
        var tb = Layout(new TextBox(new string('m', 80)).TextWrapping(TextWrapping.Wrap), width: 160);

        Assert.True(tb.LineCount > 1);
        Assert.Equal(tb.Text, string.Concat(Enumerable(tb)));
        Assert.All(Enumerable(tb), line => Assert.NotEmpty(line));
    }

    [Fact]
    public void WithoutWrapping_LongLinesStayWhole()
    {
        var tb = Area(new string('m', 80), width: 160);

        Assert.Equal(1, tb.LineCount);
    }

    private static string[] Enumerable(TextBox tb)
    {
        var lines = new string[tb.LineCount];
        for (int i = 0; i < lines.Length; i++) lines[i] = tb.GetLineText(i);
        return lines;
    }

    #endregion

    #region Measure

    [Fact]
    public void Height_GrowsByOneLineHeightPerLine()
    {
        var one = Area("one");
        var three = Area("one\ntwo\nthree");

        Assert.Equal(one.DesiredSize.Height + 2 * three.LineHeight, three.DesiredSize.Height, 2);
    }

    [Fact]
    public void AOneLineMultilineBox_IsAsTallAsASingleLineBox()
    {
        var single = new TextBox("one");
        single.Measure(new Size(300, float.PositiveInfinity));
        var multi = Area("one");

        Assert.Equal(single.DesiredSize.Height, multi.DesiredSize.Height, 0);
    }

    [Fact]
    public void MinLines_ReservesRoom_AndMaxLines_CapsTheHeight()
    {
        var empty = Area("");
        var min = Layout(new TextBox().AcceptsReturn().MinLines(3));
        var capped = Layout(new TextBox("1\n2\n3\n4\n5\n6").AcceptsReturn().MaxLines(3));

        Assert.Equal(empty.DesiredSize.Height + 2 * min.LineHeight, min.DesiredSize.Height, 2);
        Assert.Equal(min.DesiredSize.Height, capped.DesiredSize.Height, 2);
        Assert.Equal(6, capped.LineCount);
    }

    #endregion

    #region Editing

    [Fact]
    public void Enter_InsertsALineBreak()
    {
        var tb = Area("ab");
        tb.CaretIndex = 1;

        Key(tb, Atelier.Core.Events.Key.Enter);

        Assert.Equal("a\nb", tb.Text);
        Assert.Equal(2, tb.CaretIndex);
        Assert.Equal(1, tb.GetLineIndexFromCharacterIndex(tb.CaretIndex));
    }

    [Fact]
    public void Enter_IsLeftAlone_WithoutAcceptsReturn_OrWithCtrl()
    {
        var single = Layout(new TextBox("ab"));
        var wrapOnly = Layout(new TextBox("ab").TextWrapping(TextWrapping.Wrap));
        var area = Area("ab");

        var e1 = new KeyEventArgs(Atelier.Core.Events.Key.Enter, 0, ModifierKeys.None, true);
        single.OnKeyDown(e1);
        var e2 = new KeyEventArgs(Atelier.Core.Events.Key.Enter, 0, ModifierKeys.None, true);
        wrapOnly.OnKeyDown(e2);
        var e3 = new KeyEventArgs(Atelier.Core.Events.Key.Enter, 0, ModifierKeys.Control, true);
        area.OnKeyDown(e3);

        Assert.False(e1.Handled);
        Assert.False(e2.Handled);
        Assert.False(e3.Handled);
        Assert.Equal("ab", single.Text);
        Assert.Equal("ab", wrapOnly.Text);
        Assert.Equal("ab", area.Text);
    }

    [Fact]
    public void Enter_DoesNothing_WhenReadOnly()
    {
        var tb = Area("ab");
        tb.IsReadOnly = true;

        Key(tb, Atelier.Core.Events.Key.Enter);

        Assert.Equal("ab", tb.Text);
    }

    [Fact]
    public void Paste_KeepsLineBreaks_AsNewlines()
    {
        var tb = Area("");
        Clipboard.SetText("one\r\ntwo\rthree");

        tb.Paste();

        Assert.Equal("one\ntwo\nthree", tb.Text);
    }

    [Fact]
    public void Paste_StillCutsAtTheFirstLineBreak_WithoutAcceptsReturn()
    {
        var tb = Layout(new TextBox("").TextWrapping(TextWrapping.Wrap));
        Clipboard.SetText("one\ntwo");

        tb.Paste();

        Assert.Equal("one", tb.Text);
    }

    [Fact]
    public void Undo_RevertsALineBreak()
    {
        var tb = Area("ab");
        tb.CaretIndex = 2;
        Key(tb, Atelier.Core.Events.Key.Enter);
        Type(tb, "cd");

        tb.Undo();
        Assert.Equal("ab\n", tb.Text);
        tb.Undo();
        Assert.Equal("ab", tb.Text);
    }

    #endregion

    #region Navigation

    [Fact]
    public void UpAndDown_MoveBetweenLines_KeepingTheColumn()
    {
        var tb = Area("abcdef\nab\nabcdef");
        tb.CaretIndex = 5; // "abcde|f"

        Key(tb, Atelier.Core.Events.Key.Down);
        Assert.Equal(9, tb.CaretIndex); // end of the short line "ab"

        Key(tb, Atelier.Core.Events.Key.Down);
        Assert.Equal(15, tb.CaretIndex); // back to column 5 on the third line

        Key(tb, Atelier.Core.Events.Key.Up);
        Key(tb, Atelier.Core.Events.Key.Up);
        Assert.Equal(5, tb.CaretIndex);
    }

    [Fact]
    public void Up_OnTheFirstLine_GoesToTheStart_AndDown_OnTheLastLine_ToTheEnd()
    {
        var tb = Area("abc\ndef");
        tb.CaretIndex = 2;

        Key(tb, Atelier.Core.Events.Key.Up);
        Assert.Equal(0, tb.CaretIndex);

        tb.CaretIndex = 5;
        Key(tb, Atelier.Core.Events.Key.Down);
        Assert.Equal(7, tb.CaretIndex);
    }

    [Fact]
    public void ShiftDown_ExtendsTheSelectionAcrossLines()
    {
        var tb = Area("abc\ndef");
        tb.CaretIndex = 1;

        Key(tb, Atelier.Core.Events.Key.Down, ModifierKeys.Shift);

        Assert.Equal(1, tb.SelectionStart);
        Assert.Equal("bc\nd", tb.SelectedText);
    }

    [Fact]
    public void HomeAndEnd_GoToTheLineEnds_CtrlToTheTextEnds()
    {
        var tb = Area("abc\ndef\nghi");
        tb.CaretIndex = 5; // "d|ef"

        Key(tb, Atelier.Core.Events.Key.Home);
        Assert.Equal(4, tb.CaretIndex);
        Key(tb, Atelier.Core.Events.Key.End);
        Assert.Equal(7, tb.CaretIndex);
        Key(tb, Atelier.Core.Events.Key.Home, ModifierKeys.Control);
        Assert.Equal(0, tb.CaretIndex);
        Key(tb, Atelier.Core.Events.Key.End, ModifierKeys.Control);
        Assert.Equal(11, tb.CaretIndex);
    }

    [Fact]
    public void End_OnAWrappedLine_StopsBeforeTheSpaceItWrappedAt()
    {
        var tb = Layout(new TextBox("alpha beta gamma delta epsilon zeta").TextWrapping(TextWrapping.Wrap), width: 160);
        Assert.True(tb.LineCount > 1);
        tb.CaretIndex = 0;

        Key(tb, Atelier.Core.Events.Key.End);

        Assert.Equal(0, tb.GetLineIndexFromCharacterIndex(tb.CaretIndex));
        Assert.Equal(' ', tb.Text[tb.CaretIndex]);
    }

    [Fact]
    public void PageDown_MovesAViewportOfLines()
    {
        var tb = Layout(new TextBox("0\n1\n2\n3\n4\n5\n6\n7\n8\n9").AcceptsReturn().MaxLines(3));
        tb.CaretIndex = 0;

        Key(tb, Atelier.Core.Events.Key.PageDown);

        Assert.Equal(3, tb.GetLineIndexFromCharacterIndex(tb.CaretIndex));
    }

    [Fact]
    public void UpAndDown_AreLeftAlone_InASingleLineBox()
    {
        var tb = Layout(new TextBox("abc"));
        var e = new KeyEventArgs(Atelier.Core.Events.Key.Down, 0, ModifierKeys.None, true);

        tb.OnKeyDown(e);

        Assert.False(e.Handled);
    }

    #endregion

    #region Pointer

    [Fact]
    public void Click_OnTheSecondLine_PlacesTheCaretThere()
    {
        var tb = Area("abc\ndef");
        float y = tb.GetLineTop(1) + tb.LineHeight * 0.5f;
        float x = tb.GetCharacterX(5) + 0.5f; // just past "de"

        tb.OnPointerPressed(new PointerEventArgs(new Point(x, y), new Point(x, y), PointerButtons.Left, 0, ModifierKeys.None, 1));

        Assert.Equal(5, tb.CaretIndex);
    }

    [Fact]
    public void Click_PastTheEndOfALine_PutsTheCaretAtItsEnd()
    {
        var tb = Area("abc\nde");
        float y = tb.GetLineTop(0) + tb.LineHeight * 0.5f;

        Assert.Equal(3, tb.GetCharacterIndexFromPoint(new Point(290, y)));
        Assert.Equal(6, tb.GetCharacterIndexFromPoint(new Point(290, 10_000))); // below the lines: the last line
    }

    [Fact]
    public void TripleClick_SelectsTheLine()
    {
        var tb = Area("abc\ndef\nghi");
        float y = tb.GetLineTop(1) + tb.LineHeight * 0.5f;
        float x = tb.GetCharacterX(5);

        tb.OnPointerPressed(new PointerEventArgs(new Point(x, y), new Point(x, y), PointerButtons.Left, 0, ModifierKeys.None, 3));

        Assert.Equal("def", tb.SelectedText);
    }

    #endregion

    #region Scrolling

    [Fact]
    public void TheCaret_IsScrolledIntoView_BeyondMaxLines()
    {
        var tb = Layout(new TextBox("0\n1\n2\n3\n4\n5\n6\n7\n8\n9").AcceptsReturn().MaxLines(3));

        tb.CaretIndex = tb.Text.Length;

        Assert.Equal(7 * tb.LineHeight, tb.VerticalScrollOffset, 2);
        Assert.Equal(3 * tb.LineHeight, tb.GetViewportHeight(), 2);

        tb.CaretIndex = 0;
        Assert.Equal(0f, tb.VerticalScrollOffset);
    }

    [Fact]
    public void TypingAtTheBottom_OfAFixedHeightBox_ScrollsDown()
    {
        var tb = new TextBox().AcceptsReturn();
        Layout(tb);
        float height = tb.DesiredSize.Height; // one line tall, fixed

        for (int i = 0; i < 4; i++)
        {
            Key(tb, Atelier.Core.Events.Key.Enter);
            Layout(tb, 300, height);
        }

        Assert.Equal(4 * tb.LineHeight, tb.VerticalScrollOffset, 2);
    }

    [Fact]
    public void TheWheel_ScrollsTheLines_AndPassesOnAtTheEnds()
    {
        var tb = Layout(new TextBox("0\n1\n2\n3\n4\n5\n6\n7\n8\n9").AcceptsReturn().MaxLines(3));
        tb.CaretIndex = 0;

        var down = new PointerWheelEventArgs(new Point(10, 10), 0, -1);
        tb.OnPointerWheel(down);
        Assert.True(down.Handled);
        Assert.Equal(3 * tb.LineHeight, tb.VerticalScrollOffset, 2);

        // A layout pass keeps the wheel's offset instead of jumping back to the caret.
        Relayout(tb);
        Assert.Equal(3 * tb.LineHeight, tb.VerticalScrollOffset, 2);

        var up = new PointerWheelEventArgs(new Point(10, 10), 0, 5);
        tb.OnPointerWheel(up);
        Assert.Equal(0f, tb.VerticalScrollOffset);

        var beyond = new PointerWheelEventArgs(new Point(10, 10), 0, 1);
        tb.OnPointerWheel(beyond);
        Assert.False(beyond.Handled);
    }

    [Fact]
    public void TheWheel_IsIgnored_ByASingleLineBox()
    {
        var tb = Layout(new TextBox("abc"));
        var e = new PointerWheelEventArgs(new Point(10, 10), 0, -1);

        tb.OnPointerWheel(e);

        Assert.False(e.Handled);
    }

    [Fact]
    public void LinesDontScrollHorizontally_WhenTheyWrap()
    {
        var tb = Layout(new TextBox(new string('m', 80)).TextWrapping(TextWrapping.Wrap), width: 160);

        tb.CaretIndex = tb.Text.Length;

        Assert.Equal(0f, tb.ScrollOffset);
    }

    #endregion

    [Fact]
    public void ClosingEventArgs_CollectDeferredDecisions()
    {
        var e = new WindowClosingEventArgs();
        Assert.Empty(e.Deferrals);

        var decision = System.Threading.Tasks.Task.FromResult(true);
        e.Defer(decision);

        Assert.Same(decision, Assert.Single(e.Deferrals));
        Assert.False(e.Cancel);
    }
}
