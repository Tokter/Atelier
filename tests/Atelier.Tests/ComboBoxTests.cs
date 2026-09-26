using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Xunit;

namespace Atelier.Tests;

public class ComboBoxTests
{
    private static Popup PopupOf(ComboBox cb) => cb.Children.OfType<Popup>().Single();

    private static ListBox DropDownOf(ComboBox cb) => (ListBox)((Border)PopupOf(cb).Child!).Child!;

    private static void Key_(ComboBox cb, Key key, ModifierKeys modifiers = ModifierKeys.None) =>
        cb.OnKeyDown(new KeyEventArgs(key, 0, modifiers));

    private static ComboBox CreateLaidOut(params string[] items)
    {
        PopupManager.CloseAllPopups();
        var cb = new ComboBox();
        foreach (var item in items) cb.Items.Add(item);
        var root = new StackPanel().Children(cb);
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));
        return cb;
    }

    [Fact]
    public void ArrowKeys_WhileOpen_MoveHighlight_WithoutCommittingOrClosing()
    {
        var cb = CreateLaidOut("A", "B", "C");
        int raised = 0;
        cb.SelectionChanged += (s, e) => raised++;

        Key_(cb, Key.Enter);
        Assert.True(cb.IsDropDownOpen);

        Key_(cb, Key.Down);
        Key_(cb, Key.Down);

        Assert.True(cb.IsDropDownOpen);
        Assert.Null(cb.SelectedItem);
        Assert.Equal(0, raised);
        Assert.Equal(1, DropDownOf(cb).SelectedIndex); // highlight

        Key_(cb, Key.Enter);

        Assert.False(cb.IsDropDownOpen);
        Assert.Equal("B", cb.SelectedItem);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Escape_RevertsHighlight_AndKeepsSelection()
    {
        var cb = CreateLaidOut("A", "B", "C");
        cb.SelectedIndex = 0;
        int raised = 0;
        cb.SelectionChanged += (s, e) => raised++;

        cb.IsDropDownOpen = true;
        Key_(cb, Key.Down);
        Key_(cb, Key.Down);
        Key_(cb, Key.Escape);

        Assert.False(cb.IsDropDownOpen);
        Assert.Equal(0, cb.SelectedIndex);
        Assert.Equal(0, raised);
        Assert.Equal(0, DropDownOf(cb).SelectedIndex);
    }

    [Fact]
    public void ClickingItem_WithoutPriorSelection_RaisesOnce_AndReturnsFocus()
    {
        var cb = CreateLaidOut("Tokyo", "Paris", "London");
        var raised = new List<object?>();
        cb.SelectionChanged += (s, item) => raised.Add(item);

        cb.IsDropDownOpen = true;
        PopupManager.UpdatePopups(new Size(800, 600));
        var popup = PopupManager.TopmostPopup!;
        var itemPoint = new Point(popup.ActualBounds.X + 20, popup.ActualBounds.Y + 15);
        UIElement? hovered = null;
        PopupManager.HandleMouseMove(itemPoint, ref hovered);
        PopupManager.HandleMouseDown(itemPoint, PointerButtons.Left);
        PopupManager.HandleMouseUp(itemPoint, PointerButtons.Left);

        Assert.Equal(new object?[] { "Tokyo" }, raised);
        Assert.False(cb.IsDropDownOpen);
        Assert.Same(cb, FocusManager.CurrentFocused);
    }

    [Fact]
    public void UpDown_WhenClosed_ClampInsteadOfWrapping()
    {
        var cb = CreateLaidOut("A", "B", "C");

        cb.SelectedIndex = 2;
        Key_(cb, Key.Down);
        Assert.Equal(2, cb.SelectedIndex);

        cb.SelectedIndex = 0;
        Key_(cb, Key.Up);
        Assert.Equal(0, cb.SelectedIndex);

        Key_(cb, Key.End);
        Assert.Equal(2, cb.SelectedIndex);
        Key_(cb, Key.Home);
        Assert.Equal(0, cb.SelectedIndex);
    }

    [Fact]
    public void DefaultMaxDropDownHeight_IsAppliedToDropDown()
    {
        var cb = new ComboBox();

        Assert.Equal(240f, DropDownOf(cb).MaxHeight);
        Assert.Equal(256f, PopupOf(cb).MaxHeight);
    }

    [Fact]
    public void ItemsSourceChanges_KeepSelectionConsistent()
    {
        var source = new ObservableCollection<string> { "a", "b", "c" };
        var cb = new ComboBox { ItemsSource = source, SelectedItem = "b" };
        int raised = 0;
        cb.SelectionChanged += (s, e) => raised++;

        source.Insert(0, "first");
        Assert.Equal(2, cb.SelectedIndex);
        Assert.Equal("b", cb.SelectedItem);

        source.Move(2, 0);
        Assert.Equal(0, cb.SelectedIndex);
        Assert.Equal("b", cb.SelectedItem);
        Assert.Equal(0, raised);

        source[0] = "B";
        Assert.Null(cb.SelectedItem);
        Assert.Equal(-1, cb.SelectedIndex);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void ItemsSourceReplaceAndReset_UpdateDropDownItems()
    {
        var source = new CountingObservableCollection<string>(["a", "b"]);
        var cb = new ComboBox { ItemsSource = source };

        source[1] = "B";
        Assert.Equal(new object[] { "a", "B" }, cb.Items);

        source.ResetTo(["x"]);
        Assert.Equal(new object[] { "x" }, cb.Items);
        Assert.Single(DropDownOf(cb).Items);
    }

    [Fact]
    public void SelectedItem_SetBeforeItemsSource_IsApplied()
    {
        var cb = new ComboBox { SelectedItem = "Berlin" };
        cb.ItemsSource = new[] { "Paris", "Berlin" };

        Assert.Equal("Berlin", cb.SelectedItem);
        Assert.Equal(1, cb.SelectedIndex);
        Assert.Equal("Berlin", cb.SelectionBoxText);
    }

    [Fact]
    public void ItemsChange_WithUnchangedSelection_KeepsDisplayElement_AndRaisesNothing()
    {
        var source = new ObservableCollection<string> { "a", "b" };
        var cb = new ComboBox { ItemTemplate = item => new TextBlock((string)item), ItemsSource = source, SelectedIndex = 0 };
        var display = cb.SelectionDisplayElement;
        int raised = 0;
        cb.SelectionChanged += (s, e) => raised++;

        source.Add("c");

        Assert.Same(display, cb.SelectionDisplayElement);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void SettingSameSelection_DoesNotRaise()
    {
        var cb = new ComboBox { ItemsSource = new[] { "a", "b" } };
        int raised = 0;
        cb.SelectionChanged += (s, e) => raised++;

        cb.SelectedIndex = 1;
        cb.SelectedIndex = 1;
        cb.SelectedItem = "b";

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Measure_FitsWidestItem_InsteadOfFixedWidth()
    {
        var shortBox = new ComboBox { ItemsSource = new[] { "a" }, Placeholder = "" };
        var longBox = new ComboBox
        {
            ItemsSource = new[] { "a", "An item with a considerably longer text than the others" },
            Placeholder = ""
        };

        shortBox.Measure(new Size(float.PositiveInfinity, 100));
        longBox.Measure(new Size(float.PositiveInfinity, 100));

        Assert.Equal(160f, shortBox.DesiredSize.Width);
        Assert.True(longBox.DesiredSize.Width > 200f);

        // The width doesn't jump when the selection changes.
        float width = longBox.DesiredSize.Width;
        longBox.SelectedIndex = 0;
        longBox.Measure(new Size(float.PositiveInfinity, 100));
        Assert.Equal(width, longBox.DesiredSize.Width);
    }

    [Fact]
    public void SelectionBoxText_IsCachedWhenSelectionChanges()
    {
        var cb = new ComboBox { ItemsSource = new object[] { 1, 2 } };
        Assert.Null(cb.SelectionBoxText);

        cb.SelectedIndex = 1;
        Assert.Equal("2", cb.SelectionBoxText);

        cb.SelectedItem = null;
        Assert.Null(cb.SelectionBoxText);
    }

    [Fact]
    public void TextSearch_WhenClosed_SelectsByPrefix()
    {
        var cb = new ComboBox { ItemsSource = new[] { "Paris", "Berlin", "Bern" } };

        cb.OnTextInput(new TextInputEventArgs("b"));
        Assert.Equal("Berlin", cb.SelectedItem);

        cb.OnTextInput(new TextInputEventArgs("e"));
        cb.OnTextInput(new TextInputEventArgs("r"));
        cb.OnTextInput(new TextInputEventArgs("n"));
        Assert.Equal("Bern", cb.SelectedItem);
    }

    [Fact]
    public void TextSearch_WhenOpen_OnlyMovesHighlight()
    {
        var cb = CreateLaidOut("Paris", "Berlin");
        cb.IsDropDownOpen = true;

        cb.OnTextInput(new TextInputEventArgs("b"));

        Assert.Null(cb.SelectedItem);
        Assert.Equal(1, DropDownOf(cb).SelectedIndex);
        Assert.True(cb.IsDropDownOpen);

        Key_(cb, Key.Enter);
        Assert.Equal("Berlin", cb.SelectedItem);
    }

    [Fact]
    public void LightDismiss_DoesNotCommitHighlight()
    {
        var cb = CreateLaidOut("A", "B");
        cb.IsDropDownOpen = true;
        Key_(cb, Key.Down);

        PopupOf(cb).IsOpen = false; // what a click outside does

        Assert.False(cb.IsDropDownOpen);
        Assert.Null(cb.SelectedItem);
    }
}
