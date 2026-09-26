using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Platform;
using Atelier.Core.Primitives;
using Atelier.Core.Threading;
using Atelier.Core.Tree;

namespace Atelier.Tests;

public class TextBoxEditingTests
{
    private const string Emoji = "\U0001F600"; // two UTF-16 code units

    public TextBoxEditingTests()
    {
        Clipboard.Current = new Clipboard.NullClipboard();
    }

    private static TextBox Focused(string text, float width = 300)
    {
        var tb = new TextBox(text);
        tb.Measure(new Size(width, 56));
        tb.Arrange(new Rect(0, 0, width, 56));
        FocusManager.SetFocus(tb);
        return tb;
    }

    private static void Key(TextBox tb, Key key, ModifierKeys modifiers = ModifierKeys.None) =>
        tb.OnKeyDown(new KeyEventArgs(key, 0, modifiers, true));

    private static void Type(TextBox tb, string text)
    {
        foreach (char c in text)
        {
            tb.OnTextInput(new TextInputEventArgs(c.ToString()));
        }
    }

    private static PointerEventArgs Press(float x, int clickCount = 1, ModifierKeys modifiers = ModifierKeys.None) =>
        new(new Point(x, 20), new Point(x, 20), PointerButtons.Left, 0, modifiers, clickCount);

    #region Surrogate pairs and grapheme clusters

    [Fact]
    public void Backspace_RemovesWholeSurrogatePair()
    {
        var tb = Focused("a" + Emoji + "b");
        tb.CaretIndex = 3; // after the emoji

        Key(tb, Atelier.Core.Events.Key.Backspace);

        Assert.Equal("ab", tb.Text);
        Assert.Equal(1, tb.CaretIndex);
    }

    [Fact]
    public void Delete_RemovesWholeSurrogatePair()
    {
        var tb = Focused("a" + Emoji + "b");
        tb.CaretIndex = 1;

        Key(tb, Atelier.Core.Events.Key.Delete);

        Assert.Equal("ab", tb.Text);
        Assert.Equal(1, tb.CaretIndex);
    }

    [Fact]
    public void ArrowKeys_StepOverSurrogatePair()
    {
        var tb = Focused("a" + Emoji + "b");
        tb.CaretIndex = 1;

        Key(tb, Atelier.Core.Events.Key.Right);
        Assert.Equal(3, tb.CaretIndex);

        Key(tb, Atelier.Core.Events.Key.Left);
        Assert.Equal(1, tb.CaretIndex);

        Key(tb, Atelier.Core.Events.Key.Right, ModifierKeys.Shift);
        Assert.Equal(Emoji, tb.SelectedText);
    }

    [Fact]
    public void CaretIndex_InsideSurrogatePair_SnapsToItsStart()
    {
        var tb = Focused("a" + Emoji + "b");

        tb.CaretIndex = 2;
        Assert.Equal(1, tb.CaretIndex);

        tb.Select(2, 1); // just the low surrogate: widened to the whole emoji
        Assert.Equal(1, tb.SelectionStart);
        Assert.Equal(Emoji, tb.SelectedText);

        tb.Select(2, 2);
        Assert.Equal(Emoji + "b", tb.SelectedText);
    }

    [Fact]
    public void Click_NeverLandsInsideSurrogatePair()
    {
        var tb = Focused("a" + Emoji + "b");
        float origin = tb.GetTextOriginX();
        float end = origin + tb.GetTextWidth();

        for (float x = origin - 5; x <= end + 5; x += 0.5f)
        {
            tb.OnPointerPressed(Press(x));
            tb.OnPointerReleased(new PointerEventArgs(new Point(x, 20), PointerButtons.Left));
            Assert.NotEqual(2, tb.CaretIndex);
        }
    }

    [Fact]
    public void Backspace_RemovesWholeGraphemeCluster()
    {
        var tb = Focused("xé"); // 'e' + combining acute accent
        Assert.Equal(3, tb.CaretIndex);

        Key(tb, Atelier.Core.Events.Key.Backspace);

        Assert.Equal("x", tb.Text);
    }

    #endregion

    #region Null text, read-only, paste, TextChanged, supporting text

    [Fact]
    public void NullStrings_AreStoredAsEmpty()
    {
        var tb = new TextBox("abc");
        tb.Text = null!;
        tb.Placeholder = null!;
        tb.Label = null!;
        tb.SupportingText = null!;

        Assert.Equal(string.Empty, tb.Text);
        Assert.Equal(string.Empty, tb.Placeholder);
        Assert.Equal(string.Empty, tb.Label);
        Assert.Equal(string.Empty, tb.SupportingText);
        Assert.Equal(0, tb.CaretIndex);

        tb.Measure(new Size(300, 100)); // must not throw
    }

    private sealed class NullableNameViewModel : INotifyPropertyChanged
    {
        private string? _name = "start";
        public event PropertyChangedEventHandler? PropertyChanged;

        public string? Name
        {
            get => _name;
            set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
        }
    }

    [Fact]
    public void NullFromBinding_BecomesEmptyText()
    {
        var vm = new NullableNameViewModel();
        var tb = new TextBox();
        tb.SetBinding(TextBox.TextProperty, vm, x => x.Name!);
        Assert.Equal("start", tb.Text);

        vm.Name = null;

        Assert.Equal(string.Empty, tb.Text);
    }

    [Fact]
    public void ReadOnly_BlocksEditing_ButAllowsNavigationSelectionAndCopy()
    {
        var tb = Focused("Hello World");
        tb.IsReadOnly = true;
        tb.CaretIndex = 5;
        Clipboard.SetText("clip");

        Type(tb, "x");
        Key(tb, Atelier.Core.Events.Key.Backspace);
        Key(tb, Atelier.Core.Events.Key.Delete);
        Key(tb, Atelier.Core.Events.Key.V, ModifierKeys.Control);
        Key(tb, Atelier.Core.Events.Key.Z, ModifierKeys.Control);
        Assert.Equal("Hello World", tb.Text);

        Key(tb, Atelier.Core.Events.Key.Left);
        Assert.Equal(4, tb.CaretIndex);
        Key(tb, Atelier.Core.Events.Key.End, ModifierKeys.Shift);
        Assert.Equal("o World", tb.SelectedText);

        Key(tb, Atelier.Core.Events.Key.X, ModifierKeys.Control);
        Assert.Equal("Hello World", tb.Text);

        Key(tb, Atelier.Core.Events.Key.A, ModifierKeys.Control);
        Key(tb, Atelier.Core.Events.Key.C, ModifierKeys.Control);
        Assert.Equal("Hello World", Clipboard.GetText());
    }

    [Theory]
    [InlineData("first\r\nsecond")]
    [InlineData("first\nsecond")]
    [InlineData("first\rsecond")]
    public void Paste_MultiLineText_InsertsOnlyFirstLine(string clip)
    {
        var tb = Focused("[]");
        tb.CaretIndex = 1;
        Clipboard.SetText(clip);

        tb.Paste();

        Assert.Equal("[first]", tb.Text);
        Assert.Equal(6, tb.CaretIndex);
    }

    [Fact]
    public void TextChanged_SeesUpdatedCaretAndSelection()
    {
        var tb = Focused("ab");
        var observed = new System.Collections.Generic.List<(string Text, int Caret, bool HasSelection)>();
        tb.TextChanged += (s, text) => observed.Add((text, tb.CaretIndex, tb.HasSelection));

        Type(tb, "c");
        tb.SelectAll();
        Type(tb, "z");
        Key(tb, Atelier.Core.Events.Key.Backspace);

        Assert.Equal(("abc", 3, false), observed[0]);
        Assert.Equal(("z", 1, false), observed[1]);
        Assert.Equal(("", 0, false), observed[2]);
    }

    [Fact]
    public void TextChanged_RaisedOncePerEdit_AndForCodeChanges()
    {
        var tb = Focused("ab");
        int count = 0;
        tb.TextChanged += (s, text) => count++;

        Type(tb, "c");
        tb.Text = "xyz";

        Assert.Equal(2, count);
    }

    [Fact]
    public void Measure_UsesDisplayedValidationError()
    {
        var vm = new OrderViewModel();
        var tb = new TextBox { SupportingText = "ok" };
        tb.SetBinding(TextBox.TextProperty, vm, x => x.Email!, (x, v) => x.Email = v);

        tb.Measure(new Size(float.PositiveInfinity, 200));
        float narrow = tb.DesiredSize.Width;

        string error = new string('E', 120);
        vm.SetErrors(nameof(OrderViewModel.Email), error);
        tb.Measure(new Size(float.PositiveInfinity, 200));

        float expected = tb.Padding.Left + Rendering.TextMeasurer.Measure(error, 12f).Width + tb.Padding.Right;
        Assert.Equal(error, tb.DisplayedSupportingText);
        Assert.Same(tb.DisplayedSupportingText, tb.DisplayedSupportingText); // cached, not re-formatted per call
        Assert.True(tb.DesiredSize.Width > narrow);
        Assert.Equal(expected, tb.DesiredSize.Width, 1);
    }

    [Fact]
    public void PeriodKey_WithKeyCode46_DoesNotDelete()
    {
        var tb = Focused("abc");
        tb.CaretIndex = 1;

        // GLFW reports '.' with key code 46, which is VK_DELETE on Windows.
        tb.OnKeyDown(new KeyEventArgs(Atelier.Core.Events.Key.Period, 46, ModifierKeys.None, true));

        Assert.Equal("abc", tb.Text);
    }

    #endregion

    #region MaxLength, undo/redo

    [Fact]
    public void MaxLength_TruncatesTypingAndPaste()
    {
        var tb = Focused("");
        tb.MaxLength = 5;

        Type(tb, "abcdefg");
        Assert.Equal("abcde", tb.Text);

        tb.Select(1, 2); // "bc"
        Clipboard.SetText("123456");
        tb.Paste();
        Assert.Equal("a12de", tb.Text);

        tb.Text = "code can exceed the limit";
        Assert.Equal("code can exceed the limit", tb.Text);
    }

    [Fact]
    public void MaxLength_DoesNotSplitSurrogatePair()
    {
        var tb = Focused("abc");
        tb.MaxLength = 4;

        Clipboard.SetText(Emoji + Emoji);
        tb.Paste();

        Assert.Equal("abc", tb.Text);

        tb.MaxLength = 5;
        tb.Paste();
        Assert.Equal("abc" + Emoji, tb.Text);
    }

    [Fact]
    public void Undo_RevertsTypingInWordSizedSteps_AndRedoReapplies()
    {
        var tb = Focused("");
        Type(tb, "hello world");

        Key(tb, Atelier.Core.Events.Key.Z, ModifierKeys.Control);
        Assert.Equal("hello ", tb.Text);
        Assert.Equal(6, tb.CaretIndex);

        Key(tb, Atelier.Core.Events.Key.Z, ModifierKeys.Control);
        Assert.Equal("", tb.Text);
        Assert.False(tb.CanUndo);

        Key(tb, Atelier.Core.Events.Key.Y, ModifierKeys.Control);
        Assert.Equal("hello ", tb.Text);

        Key(tb, Atelier.Core.Events.Key.Z, ModifierKeys.Control | ModifierKeys.Shift);
        Assert.Equal("hello world", tb.Text);
        Assert.Equal(11, tb.CaretIndex);
        Assert.False(tb.CanRedo);
    }

    [Fact]
    public void Undo_GroupsConsecutiveBackspaces_AndRestoresSelection()
    {
        var tb = Focused("abcdef");
        Key(tb, Atelier.Core.Events.Key.Backspace);
        Key(tb, Atelier.Core.Events.Key.Backspace);
        Key(tb, Atelier.Core.Events.Key.Backspace);
        Assert.Equal("abc", tb.Text);

        Assert.True(tb.Undo());
        Assert.Equal("abcdef", tb.Text);
        Assert.Equal(6, tb.CaretIndex);

        tb.Select(1, 2);
        Type(tb, "X");
        Assert.Equal("aXdef", tb.Text);

        tb.Undo();
        Assert.Equal("abcdef", tb.Text);
        Assert.Equal("bc", tb.SelectedText);
    }

    [Fact]
    public void NewEdit_ClearsRedo_AndCodeChangeClearsHistory()
    {
        var tb = Focused("");
        Type(tb, "ab");
        tb.Undo();
        Assert.True(tb.CanRedo);

        Type(tb, "x");
        Assert.False(tb.CanRedo);
        Assert.True(tb.CanUndo);

        tb.Text = "reset";
        Assert.False(tb.CanUndo);
    }

    [Fact]
    public void UndoHistory_IsBounded()
    {
        var tb = Focused("");
        tb.UndoLimit = 3;
        for (int i = 0; i < 6; i++)
        {
            Type(tb, "a");
            tb.CaretIndex = tb.Text.Length; // moving the caret ends the typing group
        }

        int undone = 0;
        while (tb.Undo()) undone++;

        Assert.Equal(3, undone);
        Assert.Equal("aaa", tb.Text);
    }

    #endregion

    #region Password, pointer, clipboard aliases, alignment

    [Fact]
    public void PasswordChar_MasksText_AndDisablesCopyAndCut()
    {
        var tb = Focused("ab" + Emoji);
        tb.PasswordChar = '•';
        Clipboard.SetText("unchanged");

        Assert.Equal("•••", tb.DisplayText); // one mask per text element
        Assert.Equal(tb.GetCharacterOffset(1) * 3, tb.GetTextWidth(), 2);

        tb.SelectAll();
        Key(tb, Atelier.Core.Events.Key.C, ModifierKeys.Control);
        Key(tb, Atelier.Core.Events.Key.X, ModifierKeys.Control);

        Assert.Equal("unchanged", Clipboard.GetText());
        Assert.Equal("ab" + Emoji, tb.Text);

        // Typing still works.
        Type(tb, "z");
        Assert.Equal("z", tb.Text);
    }

    [Fact]
    public void TripleClick_SelectsAll_DoubleClickSelectsWord()
    {
        var tb = Focused("alpha beta gamma");
        float x = tb.GetTextOriginX() + tb.GetCharacterOffset(7);

        tb.OnPointerPressed(Press(x, clickCount: 2));
        Assert.Equal("beta", tb.SelectedText);

        tb.OnPointerPressed(Press(x, clickCount: 3));
        Assert.Equal("alpha beta gamma", tb.SelectedText);
        tb.OnPointerReleased(new PointerEventArgs(new Point(x, 20), PointerButtons.Left));
    }

    [Fact]
    public void ShiftClick_ExtendsSelectionFromCaret()
    {
        var tb = Focused("alpha beta gamma");
        tb.CaretIndex = 2;
        float x = tb.GetTextOriginX() + tb.GetCharacterOffset(10);

        tb.OnPointerPressed(Press(x, modifiers: ModifierKeys.Shift));
        tb.OnPointerReleased(new PointerEventArgs(new Point(x, 20), PointerButtons.Left));

        Assert.Equal(2, tb.SelectionAnchor);
        Assert.Equal(10, tb.CaretIndex);
        Assert.Equal("pha beta", tb.SelectedText);
    }

    [Fact]
    public void ClipboardKeyAliases_Work()
    {
        var tb = Focused("copy me");
        tb.SelectAll();

        Key(tb, Atelier.Core.Events.Key.Insert, ModifierKeys.Control);
        Assert.Equal("copy me", Clipboard.GetText());

        tb.Select(0, 5);
        Key(tb, Atelier.Core.Events.Key.Delete, ModifierKeys.Shift);
        Assert.Equal("me", tb.Text);
        Assert.Equal("copy ", Clipboard.GetText());

        tb.CaretIndex = 2;
        Key(tb, Atelier.Core.Events.Key.Insert, ModifierKeys.Shift);
        Assert.Equal("mecopy ", tb.Text);
    }

    [Fact]
    public void TextAlignment_CentersShortText_AndHitTestingFollows()
    {
        var tb = Focused("abc", width: 300);
        float leftOrigin = tb.GetTextOriginX();

        tb.TextAlignment = TextAlignment.Center;
        float centeredOrigin = tb.GetTextOriginX();
        Assert.True(centeredOrigin > leftOrigin + 50);

        tb.OnPointerPressed(Press(centeredOrigin - 2));
        Assert.Equal(0, tb.CaretIndex);
        tb.OnPointerPressed(Press(centeredOrigin + tb.GetTextWidth() + 2, clickCount: 1));
        Assert.Equal(3, tb.CaretIndex);
        tb.OnPointerReleased(new PointerEventArgs(new Point(0, 20), PointerButtons.Left));

        tb.TextAlignment = TextAlignment.Right;
        Assert.True(tb.GetTextOriginX() > centeredOrigin);
    }

    [Fact]
    public void PointerDragSelection_DoesNotAllocate()
    {
        var tb = Focused("The quick brown fox jumps over the lazy dog");
        float origin = tb.GetTextOriginX();
        tb.OnPointerPressed(Press(origin));

        var moves = new PointerEventArgs[40];
        for (int i = 0; i < moves.Length; i++)
        {
            moves[i] = new PointerEventArgs(new Point(origin + i * 6, 20), PointerButtons.Left);
        }
        foreach (var move in moves) tb.OnPointerMoved(move); // warm up

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int round = 0; round < 10; round++)
        {
            foreach (var move in moves) tb.OnPointerMoved(move);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        tb.OnPointerReleased(new PointerEventArgs(new Point(origin, 20), PointerButtons.Left));

        Assert.True(tb.HasSelection || tb.CaretIndex == 0);
        Assert.Equal(0, allocated);
    }

    #endregion

    [Fact]
    public void Renderer_DrawsSelectionCaretAndMaskedText()
    {
        var renderer = new Atelier.Theming.Material.Renderers.MaterialTextBoxRenderer(Atelier.Theming.Material.MaterialColorScheme.Dark());
        var tb = new TextBox("secret" + Emoji)
        {
            Label = "Password",
            LeadingIconKind = MaterialIconKind.Lock,
            SupportingText = "8+ characters",
            PasswordChar = '*',
            TextAlignment = TextAlignment.Center,
            Foreground = Color.Black,
        };
        tb.Measure(new Size(300, 100));
        tb.Arrange(new Rect(0, 0, 300, tb.DesiredSize.Height));
        FocusManager.SetFocus(tb);

        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(300, 100));
        using var registry = new Rendering.PaintRegistry();
        var context = new Rendering.DrawingContext(surface.Canvas, registry);

        renderer.Render(tb, ref context); // caret
        tb.Select(1, 3);
        renderer.Render(tb, ref context); // selection

        Assert.Equal("*******", tb.DisplayText);
        FocusManager.SetFocus(null);
    }

    #region Caret blinking

    private static DispatcherTimer? CaretTimer(TextBox tb) =>
        (DispatcherTimer?)typeof(TextBox).GetField("_caretTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tb);

    [Fact]
    public void CaretBlinkTimer_RunsOnlyWhileFocusedAndDisplayed()
    {
        var saved = TextBox.CaretBlinkInterval;
        TextBox.CaretBlinkInterval = TimeSpan.FromMilliseconds(15);
        try
        {
            var detached = new TextBox("x");
            FocusManager.SetFocus(detached);
            Assert.False(CaretTimer(detached)?.IsEnabled ?? false);
            FocusManager.SetFocus(null);

            var tb = new TextBox("hello");
            tb.AttachToHost();
            FocusManager.SetFocus(tb);
            Assert.True(tb.IsFocused);
            Assert.True(CaretTimer(tb)!.IsEnabled);

            // The caret blinks off at some point.
            var watch = Stopwatch.StartNew();
            bool sawHidden = false;
            while (watch.ElapsedMilliseconds < 3000 && !sawHidden)
            {
                sawHidden = !tb.CaretVisible;
                Thread.Sleep(1);
            }
            Assert.True(sawHidden, "caret never blinked");

            FocusManager.SetFocus(null);
            Assert.False(CaretTimer(tb)!.IsEnabled);
            Assert.False(tb.CaretVisible);

            // Detaching while focused stops it too.
            FocusManager.SetFocus(tb);
            Assert.True(CaretTimer(tb)!.IsEnabled);
            tb.DetachFromHost();
            Assert.False(CaretTimer(tb)!.IsEnabled);
            FocusManager.SetFocus(null);
        }
        finally
        {
            TextBox.CaretBlinkInterval = saved;
        }
    }

    [Fact]
    public void Typing_ShowsCaretImmediately()
    {
        var tb = Focused("abc");
        Assert.True(tb.CaretVisible);
        Type(tb, "d");
        Assert.True(tb.CaretVisible);
        FocusManager.SetFocus(null);
        Assert.False(tb.CaretVisible);
    }

    #endregion
}
