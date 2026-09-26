using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Inspection;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Xunit;

namespace Atelier.Tests;

#region Test models

[Inspectable]
public partial class PgObservableModel : INotifyPropertyChanged
{
    private int _port = 8080;
    private double _width = 10;

    [InspectableProperty("Observed Port", "Network")]
    public int Port
    {
        get => _port;
        set { _port = value; OnChanged(nameof(Port)); }
    }

    [InspectableProperty("Observed Width", "Size")]
    public double Width
    {
        get => _width;
        set { _width = value; OnChanged(nameof(Width)); OnChanged(nameof(Area)); }
    }

    [InspectableProperty("Observed Area", "Size")]
    public double Area => _width * 2;

    public event PropertyChangedEventHandler? PropertyChanged;

    [InspectableIgnore]
    public int HandlerCount => PropertyChanged?.GetInvocationList().Length ?? 0;

    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

[Inspectable]
public partial class PgPlainRectModel
{
    [InspectableProperty("Rect Width", "Size")]
    public double Width { get; set; } = 10;

    [InspectableProperty("Rect Height", "Size")]
    public double Height { get; set; } = 5;

    [InspectableProperty("Rect Area", "Size")]
    public double Area => Width * Height;
}

[Inspectable]
public partial class PgFaultyModel
{
    private int _minimum = 10;
    private string _title = "ok";

    [InspectableProperty("Broken Getter", "Faults")]
    public int Broken => throw new InvalidOperationException("getter boom");

    [InspectableProperty("Validated Minimum", "Faults")]
    public int Minimum
    {
        get => _minimum;
        set
        {
            if (value < 5) throw new ArgumentOutOfRangeException(nameof(value), "Must be at least 5");
            _minimum = value;
        }
    }

    [InspectableProperty("Validated Title", "Faults")]
    public string Title
    {
        get => _title;
        set
        {
            if (value.Contains('!')) throw new ArgumentException("No exclamation marks");
            _title = value;
        }
    }
}

[Inspectable]
public partial class PgNullableModel
{
    [InspectableProperty("Maybe Count", "Nullable")]
    public int? Count { get; set; }

    [InspectableProperty("Maybe Ratio", "Nullable")]
    public double? Ratio { get; set; } = 1.5;

    [InspectableProperty("Maybe Flag", "Nullable")]
    public bool? Flag { get; set; }

    [InspectableProperty("Maybe Tint", "Nullable")]
    public Color? Tint { get; set; }

    [InspectableProperty("Maybe Level", "Nullable")]
    public TestLogLevel? Level { get; set; }
}

[Flags]
public enum PgPermissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    All = Read | Write | Execute
}

[Inspectable]
public partial class PgFlagsModel
{
    [InspectableProperty("Access Rights", "Security")]
    public PgPermissions Access { get; set; } = PgPermissions.Read | PgPermissions.Write;
}

[Inspectable]
public partial struct PgPointStruct
{
    public int X { get; set; }
    public int Y { get; set; }
}

[Inspectable]
public partial class PgDescribedModel
{
    [Description("The TCP port the server listens on.")]
    [InspectableProperty("Described Port", "Network")]
    public int Port { get; set; } = 80;

    [InspectableProperty("Described Host", "Network", Description = "Host name or IP address.")]
    public string Host { get; set; } = "localhost";

    [InspectableProperty("Undescribed", "Network")]
    public string Other { get; set; } = "x";
}

[Inspectable]
public partial class PgOrderingModel
{
    [InspectableProperty(Order = 1)] public int P01 { get; set; }
    [InspectableProperty(Order = 1)] public int P02 { get; set; }
    [InspectableProperty(Order = 1)] public int P03 { get; set; }
    [InspectableProperty(Order = 1)] public int P04 { get; set; }
    [InspectableProperty(Order = 1)] public int P05 { get; set; }
    [InspectableProperty(Order = 1)] public int P06 { get; set; }
    [InspectableProperty(Order = 1)] public int P07 { get; set; }
    [InspectableProperty(Order = 1)] public int P08 { get; set; }
    [InspectableProperty(Order = 1)] public int P09 { get; set; }
    [InspectableProperty(Order = 1)] public int P10 { get; set; }
    [InspectableProperty(Order = 1)] public int P11 { get; set; }
    [InspectableProperty(Order = 1)] public int P12 { get; set; }
    [InspectableProperty(Order = 1)] public int P13 { get; set; }
    [InspectableProperty(Order = 1)] public int P14 { get; set; }
    [InspectableProperty(Order = 1)] public int P15 { get; set; }
    [InspectableProperty(Order = 1)] public int P16 { get; set; }
    [InspectableProperty(Order = 1)] public int P17 { get; set; }
    [InspectableProperty(Order = 1)] public int P18 { get; set; }
    [InspectableProperty(Order = 0)] public int Z00 { get; set; }
}

// A hand-written descriptor with null metadata and no Description override.
public sealed class PgLooseDescriptor : IPropertyDescriptor
{
    public string Name => "Loose";
    public string DisplayName => null!;
    public string Category => null!;
    public Type PropertyType => typeof(string);
    public bool IsReadOnly => true;
    public object? GetValue(object target) => "loose value";
    public void SetValue(object target, object? value) => throw new InvalidOperationException();
}

public sealed class PgLooseObject : IInspectableObject
{
    private static readonly IPropertyDescriptor[] s_properties = [new PgLooseDescriptor()];
    public IReadOnlyList<IPropertyDescriptor> GetProperties() => s_properties;
}

#endregion

public class PropertyGridBehaviorTests
{
    #region Helpers

    private static IEnumerable<VisualNode> Descendants(VisualNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var d in Descendants(child))
                yield return d;
        }
    }

    private static Grid RowGrid(PropertyGrid grid, string label) =>
        (Grid)Descendants(grid).OfType<TextBlock>().First(t => t.Text == label).Parent!.Parent!;

    private static T Editor<T>(PropertyGrid grid, string label) where T : UIElement =>
        (T)RowGrid(grid, label).Children[1];

    private static Border Row(PropertyGrid grid, string label) => (Border)RowGrid(grid, label).Parent!;

    private static string? ErrorText(PropertyGrid grid, string label)
    {
        var rowGrid = RowGrid(grid, label);
        if (rowGrid.Children.Count < 3) return null;
        var text = (TextBlock)rowGrid.Children[2];
        return text.Visibility == Visibility.Visible ? text.Text : null;
    }

    private static Border CategoryHeader(PropertyGrid grid, string category) =>
        (Border)Descendants(grid).OfType<TextBlock>().First(t => t.Text == category && t.Bold).Parent!.Parent!;

    private static ScrollViewer ContentScrollViewer(PropertyGrid grid) =>
        (ScrollViewer)((Grid)((Border)grid.Children[0]).Child!).Children[0];

    private static void Enter(TextBox box) => box.OnKeyDown(new KeyEventArgs(Key.Enter));
    private static void Escape(TextBox box) => box.OnKeyDown(new KeyEventArgs(Key.Escape));

    private static void Layout(PropertyGrid grid, float width = 500, float height = 200)
    {
        grid.Measure(new Size(width, height));
        grid.Arrange(new Rect(0, 0, width, height));
    }

    #endregion

    #region External changes and Refresh

    [Fact]
    public void ExternalInpcChange_UpdatesEditor()
    {
        var model = new PgObservableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        grid.AttachToHost();

        var editor = Editor<TextBox>(grid, "Observed Port");
        Assert.Equal("8080", editor.Text);

        model.Port = 1;
        Assert.Equal("1", editor.Text);
    }

    [Fact]
    public void InpcDerivedProperty_RefreshesAfterEdit()
    {
        var model = new PgObservableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        grid.AttachToHost();

        var width = Editor<TextBox>(grid, "Observed Width");
        width.Text = "21";
        Enter(width);

        Assert.Equal(21, model.Width);
        Assert.Equal("42", Editor<TextBox>(grid, "Observed Area").Text);
    }

    [Fact]
    public void DerivedReadOnlyRow_OfPlainObject_RefreshesAfterEdit()
    {
        var model = new PgPlainRectModel();
        var grid = new PropertyGrid { SelectedObject = model };

        var area = Editor<TextBox>(grid, "Rect Area");
        Assert.Equal("50", area.Text);

        var width = Editor<TextBox>(grid, "Rect Width");
        width.Text = "20";
        Enter(width);

        Assert.Equal("100", area.Text);
    }

    [Fact]
    public void Refresh_ReReadsValuesOfPlainObject()
    {
        var model = new PgPlainRectModel();
        var grid = new PropertyGrid { SelectedObject = model };

        model.Height = 7;
        Assert.Equal("5", Editor<TextBox>(grid, "Rect Height").Text);

        grid.Refresh();
        Assert.Equal("7", Editor<TextBox>(grid, "Rect Height").Text);
        Assert.Equal("70", Editor<TextBox>(grid, "Rect Area").Text);
    }

    #endregion

    #region Errors

    [Fact]
    public void ThrowingGetter_ShowsErrorWithoutBreakingTheGrid()
    {
        var errors = new List<PropertyValueErrorEventArgs>();
        var grid = new PropertyGrid();
        grid.PropertyValueError += (s, e) => errors.Add(e);

        grid.SelectedObject = new PgFaultyModel();

        Assert.Contains("getter boom", ErrorText(grid, "Broken Getter"));
        Assert.Null(ErrorText(grid, "Validated Minimum"));
        var error = Assert.Single(errors);
        Assert.True(error.IsReadError);
        Assert.Equal("Broken", error.Property.Name);
        Assert.IsType<InvalidOperationException>(error.Exception);

        // Other rows keep working.
        Assert.Equal("10", Editor<TextBox>(grid, "Validated Minimum").Text);
    }

    [Fact]
    public void ThrowingSetter_ShowsErrorKeepsTextAndRaisesEvent()
    {
        var model = new PgFaultyModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var errors = new List<PropertyValueErrorEventArgs>();
        int changed = 0;
        grid.PropertyValueError += (s, e) => errors.Add(e);
        grid.PropertyValueChanged += (s, e) => changed++;

        var editor = Editor<TextBox>(grid, "Validated Minimum");
        editor.Text = "3";
        Enter(editor);

        Assert.Equal(10, model.Minimum);
        Assert.Equal("3", editor.Text);
        Assert.Contains("Must be at least 5", ErrorText(grid, "Validated Minimum"));
        var error = Assert.Single(errors);
        Assert.False(error.IsReadError);
        Assert.Equal(3, error.AttemptedValue);
        Assert.Equal(0, changed);

        editor.Text = "7";
        Enter(editor);
        Assert.Equal(7, model.Minimum);
        Assert.Null(ErrorText(grid, "Validated Minimum"));
        Assert.Equal(1, changed);
    }

    [Fact]
    public void ThrowingSetter_WhileTypingInStringEditor_DoesNotThrow()
    {
        var model = new PgFaultyModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var editor = Editor<TextBox>(grid, "Validated Title");

        editor.Text = "hey!";

        Assert.Equal("ok", model.Title);
        Assert.Equal("hey!", editor.Text);
        Assert.Contains("No exclamation marks", ErrorText(grid, "Validated Title"));

        editor.Text = "hey";
        Assert.Equal("hey", model.Title);
        Assert.Null(ErrorText(grid, "Validated Title"));
    }

    [Fact]
    public void Escape_ClearsSetterErrorAndRevertsText()
    {
        var model = new PgFaultyModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var editor = Editor<TextBox>(grid, "Validated Minimum");

        editor.Text = "1";
        Enter(editor);
        Assert.NotNull(ErrorText(grid, "Validated Minimum"));

        Escape(editor);
        Assert.Equal("10", editor.Text);
        Assert.Null(ErrorText(grid, "Validated Minimum"));
    }

    #endregion

    #region Commit behavior

    [Fact]
    public void NumericEditor_CommitsOnlyOnEnterOrLostFocus()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var events = new List<PropertyValueChangedEventArgs>();
        grid.PropertyValueChanged += (s, e) => events.Add(e);

        var editor = Editor<TextBox>(grid, "Port Number");
        editor.Text = "1";
        editor.Text = "10";
        Assert.Empty(events);
        Assert.Equal(8080, model.Port);

        Enter(editor);
        var e1 = Assert.Single(events);
        Assert.Equal(8080, e1.OldValue);
        Assert.Equal(10, e1.NewValue);
        Assert.Equal(10, model.Port);

        editor.Text = "12";
        editor.OnLostFocus();
        Assert.Equal(2, events.Count);
        Assert.Equal(10, events[1].OldValue);
        Assert.Equal(12, events[1].NewValue);

        // Committing again without changes raises nothing.
        editor.OnLostFocus();
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void ColorEditor_CommitsOnlyOnEnterOrLostFocus()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var events = new List<PropertyValueChangedEventArgs>();
        grid.PropertyValueChanged += (s, e) => events.Add(e);

        var hexBox = Descendants(Editor<StackPanel>(grid, "Theme Color")).OfType<TextBox>().Single();
        hexBox.Text = "#1";
        hexBox.Text = "#123";
        hexBox.Text = "#12345";
        hexBox.Text = "#123456";
        Assert.Empty(events);

        Enter(hexBox);
        var e = Assert.Single(events);
        Assert.Equal(Color.FromRgb(255, 0, 0), e.OldValue);
        Assert.Equal(Color.FromHex("#123456"), model.ThemeColor);
        Assert.Equal("#FF123456", hexBox.Text);
    }

    [Fact]
    public void Escape_RevertsUncommittedText()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        int changed = 0;
        grid.PropertyValueChanged += (s, e) => changed++;

        var editor = Editor<TextBox>(grid, "Port Number");
        editor.Text = "55";
        Escape(editor);

        Assert.Equal("8080", editor.Text);
        Assert.Equal(8080, model.Port);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void NumericEditor_RejectsThousandsSeparator()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var editor = Editor<TextBox>(grid, "Timeout Ratio");

        editor.Text = "1,5";
        Enter(editor);

        Assert.Equal(2.5, model.TimeoutRatio);
        Assert.Equal("2.5", editor.Text);
    }

    [Fact]
    public void NumericEditor_NormalizesTextAfterCommit()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var editor = Editor<TextBox>(grid, "Port Number");

        editor.Text = "007";
        Enter(editor);

        Assert.Equal(7, model.Port);
        Assert.Equal("7", editor.Text);
    }

    [Fact]
    public void PropertyValueChanging_CanCancel()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        PropertyValueChangingEventArgs? changing = null;
        int changed = 0;
        grid.PropertyValueChanging += (s, e) =>
        {
            changing = e;
            if (Equals(e.NewValue, 666)) e.Cancel = true;
        };
        grid.PropertyValueChanged += (s, e) => changed++;

        var editor = Editor<TextBox>(grid, "Port Number");
        editor.Text = "666";
        Enter(editor);

        Assert.NotNull(changing);
        Assert.Equal(8080, changing!.OldValue);
        Assert.Equal(666, changing.NewValue);
        Assert.Equal(8080, model.Port);
        Assert.Equal("8080", editor.Text);
        Assert.Equal(0, changed);
        Assert.Null(ErrorText(grid, "Port Number"));

        editor.Text = "667";
        Enter(editor);
        Assert.Equal(667, model.Port);
        Assert.Equal(1, changed);
    }

    #endregion

    #region Nullable and flags editors

    [Fact]
    public void NullableInt_ShowsEmptyForNullAndClearsToNull()
    {
        var model = new PgNullableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var editor = Editor<TextBox>(grid, "Maybe Count");
        Assert.Equal(string.Empty, editor.Text);

        editor.Text = "12";
        Enter(editor);
        Assert.Equal(12, model.Count);

        editor.Text = "";
        Enter(editor);
        Assert.Null(model.Count);

        var ratio = Editor<TextBox>(grid, "Maybe Ratio");
        Assert.Equal("1.5", ratio.Text);
        ratio.Text = "  ";
        ratio.OnLostFocus();
        Assert.Null(model.Ratio);
    }

    [Fact]
    public void NonNullableNumeric_EmptyTextReverts()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var editor = Editor<TextBox>(grid, "Port Number");

        editor.Text = "";
        Enter(editor);

        Assert.Equal(8080, model.Port);
        Assert.Equal("8080", editor.Text);
    }

    [Fact]
    public void NullableBool_UsesThreeStateCheckBox()
    {
        var model = new PgNullableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var checkBox = Editor<CheckBox>(grid, "Maybe Flag");

        Assert.True(checkBox.IsThreeState);
        Assert.Null(checkBox.IsChecked);

        checkBox.IsChecked = true;
        Assert.True(model.Flag);

        checkBox.IsChecked = null;
        Assert.Null(model.Flag);

        Assert.False(Editor<CheckBox>(new PropertyGrid { SelectedObject = new TestInspectableModel() }, "Enabled").IsThreeState);
    }

    [Fact]
    public void NullableColor_ShowsEmptyAndAcceptsClearing()
    {
        var model = new PgNullableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var hexBox = Descendants(Editor<StackPanel>(grid, "Maybe Tint")).OfType<TextBox>().Single();
        Assert.Equal(string.Empty, hexBox.Text);

        hexBox.Text = "#FF0000";
        Enter(hexBox);
        Assert.Equal(Color.FromRgb(255, 0, 0), model.Tint);

        hexBox.Text = "";
        Enter(hexBox);
        Assert.Null(model.Tint);
        Assert.Equal(string.Empty, hexBox.Text);
    }

    [Fact]
    public void NullableEnum_OffersNoneEntry()
    {
        var model = new PgNullableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var combo = Editor<ComboBox>(grid, "Maybe Level");
        Assert.Equal("(none)", combo.SelectedItem);

        combo.SelectedItem = "Error";
        Assert.Equal(TestLogLevel.Error, model.Level);

        combo.SelectedItem = "(none)";
        Assert.Null(model.Level);
    }

    [Fact]
    public void FlagsEnum_ShowsCheckBoxPerFlagAndTogglesBits()
    {
        var model = new PgFlagsModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var boxes = Editor<WrapPanel>(grid, "Access Rights").Children.OfType<CheckBox>().ToList();

        CheckBox Box(string name) => boxes.Single(b => b.Content is TextBlock t && t.Text == name);

        Assert.Equal(4, boxes.Count); // Read, Write, Execute, All ("None" is every box cleared)
        Assert.True(Box("Read").IsChecked);
        Assert.True(Box("Write").IsChecked);
        Assert.False(Box("Execute").IsChecked);
        Assert.False(Box("All").IsChecked);

        Box("Execute").IsChecked = true;
        Assert.Equal(PgPermissions.All, model.Access);
        Assert.True(Box("All").IsChecked);

        Box("Read").IsChecked = false;
        Assert.Equal(PgPermissions.Write | PgPermissions.Execute, model.Access);
        Assert.False(Box("All").IsChecked);

        Box("All").IsChecked = true;
        Assert.Equal(PgPermissions.All, model.Access);
        Assert.True(Box("Read").IsChecked);
    }

    #endregion

    #region Rows, filtering, expansion, scrolling

    [Fact]
    public void CategoryHeaderClick_WithActiveFilter_TogglesDisplayedState()
    {
        var model = new TestServerConfigModel();
        var grid = new PropertyGrid { SelectedObject = model };
        grid.SetCategoryExpanded("Network", false);
        Assert.False(grid.IsCategoryExpanded("Network"));

        grid.FilterText = "Port";
        Assert.True(grid.IsCategoryExpanded("Network"));

        var header = CategoryHeader(grid, "Network");
        header.OnPointerPressed(new PointerEventArgs(new Point(1, 1)));
        Assert.False(grid.IsCategoryExpanded("Network"));

        header.OnPointerPressed(new PointerEventArgs(new Point(1, 1)));
        Assert.True(grid.IsCategoryExpanded("Network"));

        // Clearing the filter restores the stored (collapsed) state.
        grid.FilterText = "";
        Assert.False(grid.IsCategoryExpanded("Network"));

        header.OnPointerPressed(new PointerEventArgs(new Point(1, 1)));
        Assert.True(grid.IsCategoryExpanded("Network"));
    }

    [Fact]
    public void Filtering_SortingAndLabelWidth_ReuseRows()
    {
        var model = new TestServerConfigModel();
        var grid = new PropertyGrid { SelectedObject = model };

        var row = Row(grid, "Port Number");
        var editor = Editor<TextBox>(grid, "Port Number");
        var hostRow = Row(grid, "Server Host");

        foreach (var filter in new[] { "P", "Po", "Por", "Port", "" })
        {
            grid.FilterText = filter;
            Assert.Same(row, Row(grid, "Port Number"));
            Assert.Same(editor, Editor<TextBox>(grid, "Port Number"));
        }

        grid.FilterText = "Port";
        Assert.Equal(Visibility.Visible, row.Visibility);
        Assert.Equal(Visibility.Collapsed, hostRow.Visibility);
        Assert.Equal(Visibility.Collapsed, CategoryHeader(grid, "Security").Visibility);

        grid.SortMode = PropertySortMode.Alphabetical;
        grid.SortMode = PropertySortMode.Categorized;
        grid.CollapseAll();
        grid.ExpandAll();
        grid.LabelWidth = 210;

        Assert.Same(row, Row(grid, "Port Number"));
        Assert.Same(editor, Editor<TextBox>(grid, "Port Number"));
        Assert.Equal(GridLength.Pixels(210), RowGrid(grid, "Port Number").ColumnDefinitions[0].Width);
    }

    [Fact]
    public void Filter_WithNoMatches_ShowsPlaceholder_AndNullMetadataIsTolerated()
    {
        var grid = new PropertyGrid { SelectedObject = new PgLooseObject() };
        Assert.Equal("loose value", Editor<TextBox>(grid, "Loose").Text);
        Assert.True(grid.IsCategoryExpanded("General"));

        grid.FilterText = "zzz";
        Assert.Contains(Descendants(grid).OfType<TextBlock>(), t => t.Text == "No properties match \"zzz\"" && t.Parent is Border { Visibility: Visibility.Visible });

        grid.FilterText = "loo";
        Assert.Equal(Visibility.Visible, Row(grid, "Loose").Visibility);
    }

    [Fact]
    public void RegisterEditor_AfterSelectedObject_RecreatesEditors()
    {
        var grid = new PropertyGrid { SelectedObject = new CustomDataModel() };
        Assert.DoesNotContain(Descendants(grid).OfType<TextBlock>(), t => t.Text == "CUSTOM-DATE");

        grid.RegisterEditor<DateTime>(ctx => new TextBlock("CUSTOM-DATE"));

        Assert.Contains(Descendants(grid).OfType<TextBlock>(), t => t.Text == "CUSTOM-DATE");
    }

    [Fact]
    public void ScrollOffset_PreservedOnCollapseFilterAndRebuild_ResetOnNewObject()
    {
        var grid = new PropertyGrid { SelectedObject = new PgOrderingModel(), IsToolbarVisible = false };
        var scrollViewer = ContentScrollViewer(grid);
        Layout(grid);

        scrollViewer.ScrollOffsetY = 120;
        Layout(grid);
        Assert.Equal(120, scrollViewer.ScrollOffsetY);

        grid.FilterText = "P";
        Layout(grid);
        Assert.Equal(120, scrollViewer.ScrollOffsetY);

        grid.FilterText = "";
        grid.ExpandAll();
        grid.LabelWidth = 120;
        grid.RebuildProperties();
        Layout(grid);
        Assert.Equal(120, scrollViewer.ScrollOffsetY);

        grid.SelectedObject = new PgOrderingModel();
        Layout(grid);
        Assert.Equal(0, scrollViewer.ScrollOffsetY);
    }

    #endregion

    #region Leaks

    [Fact]
    public void ChangingSelectedObject_Unsubscribes_AndReleasesOldObject()
    {
        var grid = new PropertyGrid();
        grid.AttachToHost();

        var weak = SelectAndReplace(grid);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weak.IsAlive);
        GC.KeepAlive(grid);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference SelectAndReplace(PropertyGrid grid)
    {
        var model = new PgObservableModel();
        grid.SelectedObject = model;
        Assert.Equal(1, model.HandlerCount);

        grid.SelectedObject = new PgPlainRectModel();
        Assert.Equal(0, model.HandlerCount);
        return new WeakReference(model);
    }

    [Fact]
    public void LongLivedModel_DoesNotKeepDetachedGridAlive()
    {
        var model = new PgObservableModel();

        var weak = AttachAndDetach(model);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weak.IsAlive);
        Assert.Equal(0, model.HandlerCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AttachAndDetach(PgObservableModel model)
    {
        var grid = new PropertyGrid { SelectedObject = model };
        Assert.Equal(0, model.HandlerCount); // subscribes only while attached

        grid.AttachToHost();
        Assert.Equal(1, model.HandlerCount);

        grid.DetachFromHost();
        Assert.Equal(0, model.HandlerCount);
        return new WeakReference(grid);
    }

    [Fact]
    public void Reattach_Resubscribes_AndPicksUpChangesMadeWhileDetached()
    {
        var model = new PgObservableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        grid.AttachToHost();
        grid.DetachFromHost();

        model.Port = 42;
        var editor = Editor<TextBox>(grid, "Observed Port");
        Assert.Equal("8080", editor.Text);

        grid.AttachToHost();
        Assert.Equal(1, model.HandlerCount);
        Assert.Equal("42", editor.Text);

        model.Port = 43;
        Assert.Equal("43", editor.Text);
    }

    #endregion

    #region Inspection model and generator

    [Fact]
    public void StructTarget_PropertiesAreReadOnly()
    {
        var descriptors = ObjectInspector.GetProperties(new PgPointStruct { X = 1 });
        Assert.All(descriptors, d => Assert.True(d.IsReadOnly));

        var grid = new PropertyGrid { SelectedObject = new PgPointStruct { X = 3 } };
        var editor = Editor<TextBox>(grid, "X");
        Assert.True(editor.IsReadOnly);
        Assert.Equal("3", editor.Text);
    }

    [Fact]
    public void Generator_SortsByOrderThenDeclaration()
    {
        var names = ObjectInspector.GetProperties(new PgOrderingModel()).Select(p => p.Name).ToList();
        var expected = new List<string> { "Z00" };
        expected.AddRange(Enumerable.Range(1, 18).Select(i => $"P{i:00}"));
        Assert.Equal(expected, names);
    }

    [Fact]
    public void Generator_ReadsDescriptions()
    {
        var model = new PgDescribedModel();
        Assert.Equal("The TCP port the server listens on.", ObjectInspector.GetProperty(model, "Port")!.Description);
        Assert.Equal("Host name or IP address.", ObjectInspector.GetProperty(model, "Host")!.Description);
        Assert.Null(ObjectInspector.GetProperty(model, "Other")!.Description);
        Assert.Null(((IPropertyDescriptor)new PgLooseDescriptor()).Description);
    }

    [Fact]
    public void DescriptionPanel_ShowsDescriptionOfHoveredRow()
    {
        var grid = new PropertyGrid { SelectedObject = new PgDescribedModel() };
        var panel = (Border)((Grid)((Border)grid.Children[0]).Child!).Children[3];
        Assert.Equal(Visibility.Visible, panel.Visibility);

        Row(grid, "Described Port").OnPointerEntered(new PointerEventArgs(new Point(1, 1)));
        Assert.Contains(Descendants(panel).OfType<TextBlock>(), t => t.Text == "The TCP port the server listens on.");

        Row(grid, "Described Host").OnPointerEntered(new PointerEventArgs(new Point(1, 1)));
        Assert.Contains(Descendants(panel).OfType<TextBlock>(), t => t.Text == "Host name or IP address.");

        grid.IsDescriptionVisible = false;
        Assert.Equal(Visibility.Collapsed, panel.Visibility);

        // Objects without descriptions don't show the panel.
        grid.IsDescriptionVisible = true;
        grid.SelectedObject = new TestInspectableModel();
        Assert.Equal(Visibility.Collapsed, panel.Visibility);
    }

    [Fact]
    public void Color_TryParseHex_ParsesWithoutExceptions()
    {
        Assert.True(Color.TryParseHex("#123", out var c3));
        Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), c3);
        Assert.True(Color.TryParseHex(" 80FF0000 ", out var c8));
        Assert.Equal(Color.FromArgb(0x80, 0xFF, 0, 0), c8);
        Assert.False(Color.TryParseHex("#12", out _));
        Assert.False(Color.TryParseHex("#12345G", out _));
        Assert.False(Color.TryParseHex("", out _));
        Assert.Throws<FormatException>(() => Color.FromHex("#XYZ"));
    }

    #endregion
}
