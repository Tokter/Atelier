using System;
using System.Collections.Generic;
using System.Globalization;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>The sort direction of a <see cref="DataGridColumn"/>.</summary>
public enum DataGridSortDirection
{
    /// <summary>Not sorted by this column.</summary>
    None,

    /// <summary>Smallest first.</summary>
    Ascending,

    /// <summary>Largest first.</summary>
    Descending,
}

/// <summary>
/// A column of a <see cref="DataGrid"/>: its header, width and behavior, and how its cells show and edit an item.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Width"/> is a pixel width, <see cref="GridLength.Auto"/> (fitted to the header and the text of the first
/// items when the items arrive) or a star width (sharing the space the other columns leave). Resizing a column makes
/// its width a pixel width.
/// </para>
/// <para>
/// Sorting uses <see cref="SortKey"/> (by default the column's value) and <see cref="Comparer"/>. Filtering combines
/// <see cref="FilterText"/> (matched against the cell text, or by <see cref="FilterPredicate"/>) and
/// <see cref="FilterValues"/> (the values checked in the column's filter menu).
/// </para>
/// <para>
/// A column with a <see cref="ValueSetter"/> can be edited (F2 in the grid): with the editor from
/// <see cref="EditorTemplate"/>, or the column type's own editor (a text box for text columns). The editor works on a
/// <see cref="DataGridEditContext"/>; the value is stored when the edit is committed.
/// </para>
/// </remarks>
public abstract class DataGridColumn : BindableObject
{
    /// <summary>Identifies the <see cref="Header"/> property.</summary>
    public static readonly BindableProperty<object?> HeaderProperty =
        BindableProperty.Register<DataGridColumn, object?>(nameof(Header), null, (s, o, n) => ((DataGridColumn)s).Owner?.OnColumnHeaderChanged((DataGridColumn)s));

    /// <summary>Identifies the <see cref="Width"/> property.</summary>
    public static readonly BindableProperty<GridLength> WidthProperty =
        BindableProperty.Register<DataGridColumn, GridLength>(nameof(Width), GridLength.Pixels(150), (s, o, n) => ((DataGridColumn)s).OnLayoutPropertyChanged());

    /// <summary>Identifies the <see cref="MinWidth"/> property.</summary>
    public static readonly BindableProperty<float> MinWidthProperty =
        BindableProperty.Register<DataGridColumn, float>(nameof(MinWidth), 48f, (s, o, n) => ((DataGridColumn)s).OnLayoutPropertyChanged());

    /// <summary>Identifies the <see cref="MaxWidth"/> property.</summary>
    public static readonly BindableProperty<float> MaxWidthProperty =
        BindableProperty.Register<DataGridColumn, float>(nameof(MaxWidth), float.PositiveInfinity, (s, o, n) => ((DataGridColumn)s).OnLayoutPropertyChanged());

    /// <summary>Identifies the <see cref="IsVisible"/> property.</summary>
    public static readonly BindableProperty<bool> IsVisibleProperty =
        BindableProperty.Register<DataGridColumn, bool>(nameof(IsVisible), true, (s, o, n) => ((DataGridColumn)s).Owner?.OnColumnsStructureChanged());

    /// <summary>Identifies the <see cref="FilterText"/> property.</summary>
    public static readonly BindableProperty<string> FilterTextProperty =
        BindableProperty.Register<DataGridColumn, string>(nameof(FilterText), string.Empty, (s, o, n) => ((DataGridColumn)s).Owner?.OnColumnFilterChanged((DataGridColumn)s));

    /// <summary>Identifies the <see cref="CellAlignment"/> property.</summary>
    public static readonly BindableProperty<HorizontalAlignment> CellAlignmentProperty =
        BindableProperty.Register<DataGridColumn, HorizontalAlignment>(nameof(CellAlignment), HorizontalAlignment.Left, (s, o, n) => ((DataGridColumn)s).Owner?.OnColumnsStructureChanged());

    /// <summary>Identifies the <see cref="IsReadOnly"/> property.</summary>
    public static readonly BindableProperty<bool> IsReadOnlyProperty =
        BindableProperty.Register<DataGridColumn, bool>(nameof(IsReadOnly), false, (s, o, n) => ((DataGridColumn)s).Owner?.OnColumnEditabilityChanged());

    private HashSet<string>? _filterValues;

    /// <summary>Initializes a column.</summary>
    protected DataGridColumn()
    {
    }

    /// <summary>
    /// Gets or sets the stable name of the column in saved layouts (see <see cref="DataGrid.SaveLayout"/>); <c>null</c>
    /// (the default) uses the header text.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>Gets the key of the column: <see cref="Key"/>, or the header text.</summary>
    public string EffectiveKey => Key ?? Header?.ToString() ?? string.Empty;

    /// <summary>Gets or sets the header: a string, an element, or any object shown as text.</summary>
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>Gets or sets a factory of the header content (e.g. an icon and text); <c>null</c> shows <see cref="Header"/>.</summary>
    public Func<DataGridColumn, UIElement>? HeaderTemplate { get; set; }

    /// <summary>Gets or sets the width: pixels, <see cref="GridLength.Auto"/> or a star share. The default is 150 pixels.</summary>
    public GridLength Width { get => GetValue(WidthProperty); set => SetValue(WidthProperty, value); }

    /// <summary>Gets or sets the smallest width. The default is 48.</summary>
    public float MinWidth { get => GetValue(MinWidthProperty); set => SetValue(MinWidthProperty, value); }

    /// <summary>Gets or sets the largest width. The default is unlimited.</summary>
    public float MaxWidth { get => GetValue(MaxWidthProperty); set => SetValue(MaxWidthProperty, value); }

    /// <summary>Gets or sets whether the column is shown (the column chooser toggles it). The default is <c>true</c>.</summary>
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }

    /// <summary>Gets or sets whether the column chooser can hide the column. The default is <c>true</c>.</summary>
    public bool CanHide { get; set; } = true;

    /// <summary>Gets or sets whether clicking the header sorts by the column (and the grid allows sorting). The default is <c>true</c>.</summary>
    public bool CanSort { get; set; } = true;

    /// <summary>Gets or sets whether the column can be resized (and the grid allows it). The default is <c>true</c>.</summary>
    public bool CanResize { get; set; } = true;

    /// <summary>Gets or sets whether the column can be dragged to a new place (and the grid allows it). The default is <c>true</c>.</summary>
    public bool CanReorder { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the header offers a filter menu (when the grid shows them and the column has values to
    /// filter by). The default is <c>true</c>.
    /// </summary>
    public bool CanFilter { get; set; } = true;

    /// <summary>Gets or sets the value an item is sorted by in this column; <c>null</c> (the default) uses the column's value.</summary>
    public Func<object, object?>? SortKey { get; set; }

    /// <summary>
    /// Gets or sets how sort values compare; <c>null</c> (the default) compares strings case-insensitively in the current
    /// culture and other values with their <see cref="IComparable"/>.
    /// </summary>
    public IComparer<object?>? Comparer { get; set; }

    /// <summary>
    /// Gets or sets the filter of the column: items whose cell text doesn't contain it (ignoring case) are hidden. Set by
    /// the grid's filter row. The default is empty (no filter).
    /// </summary>
    public string FilterText { get => GetValue(FilterTextProperty); set => SetValue(FilterTextProperty, value); }

    /// <summary>Gets or sets a custom filter: whether an item passes for a filter text; replaces the text match.</summary>
    public Func<object, string, bool>? FilterPredicate { get; set; }

    /// <summary>
    /// Gets or sets the values shown (as in the filter menu, see <see cref="GetFilterKey"/>): items with other values are
    /// hidden. <c>null</c> (the default) shows all values; an empty collection hides every item.
    /// </summary>
    public IReadOnlyCollection<string>? FilterValues
    {
        get => _filterValues;
        set
        {
            _filterValues = value == null ? null : new HashSet<string>(value, StringComparer.Ordinal);
            Owner?.OnColumnFilterChanged(this);
        }
    }

    /// <summary>Gets whether the column filters the items (by <see cref="FilterText"/> or <see cref="FilterValues"/>).</summary>
    public bool IsFiltered => !string.IsNullOrEmpty(FilterText) || _filterValues != null;

    /// <summary>Gets or sets the horizontal alignment of the cells, e.g. right for numbers. The default is left.</summary>
    public HorizontalAlignment CellAlignment { get => GetValue(CellAlignmentProperty); set => SetValue(CellAlignmentProperty, value); }

    /// <summary>Gets or sets whether the cells can't be edited even with a <see cref="ValueSetter"/>. The default is <c>false</c>.</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>
    /// Gets or sets the action that stores an edited value into an item; <c>null</c> (the default) makes the column
    /// read-only. It may throw (e.g. <see cref="FormatException"/> or <see cref="ArgumentException"/>) to reject the
    /// value: the editor stays open and shows the message.
    /// </summary>
    public Action<object, object?>? ValueSetter { get; set; }

    /// <summary>
    /// Gets or sets a factory of a custom editor, e.g. a date picker or a combo box. The editor reads the value to edit
    /// from <see cref="DataGridEditContext.Value"/>, writes changes back there, and can end the edit with
    /// <see cref="DataGridEditContext.Commit"/> or <see cref="DataGridEditContext.Cancel"/>. <c>null</c> (the default)
    /// uses the column type's editor, if it has one.
    /// </summary>
    public Func<DataGridEditContext, UIElement>? EditorTemplate { get; set; }

    /// <summary>Gets whether a cell of the column can be edited.</summary>
    public bool CanEdit => !IsReadOnly && ValueSetter != null && (EditorTemplate != null || HasDefaultEditor);

    /// <summary>Gets the current sort direction.</summary>
    public DataGridSortDirection SortDirection { get; internal set; }

    /// <summary>Gets the column's position among the sort columns (0 for the first), or -1 when not sorted.</summary>
    public int SortOrder { get; internal set; } = -1;

    /// <summary>Gets the width the column is laid out with.</summary>
    public float ActualWidth { get; internal set; }

    /// <summary>Gets the grid the column belongs to.</summary>
    public DataGrid? Owner { get; internal set; }

    // The fitted width of an Auto column, measured once per items source (NaN until measured).
    internal float AutoWidth { get; set; } = float.NaN;

    internal bool IsSortable => CanSort && (SortKey != null || HasValue);

    internal bool IsFilterable => CanFilter && (HasValue || SortKey != null || HasCellText);

    /// <summary>Gets whether the column has a value to sort by without a <see cref="SortKey"/>.</summary>
    protected virtual bool HasValue => false;

    /// <summary>Gets whether <see cref="GetCellText"/> returns text, for filtering; the default is <see cref="HasValue"/>.</summary>
    protected virtual bool HasCellText => HasValue;

    /// <summary>Gets whether the column type has an editor of its own (used without an <see cref="EditorTemplate"/>).</summary>
    protected virtual bool HasDefaultEditor => false;

    private void OnLayoutPropertyChanged() => Owner?.OnColumnLayoutChanged();

    /// <summary>Clears <see cref="FilterText"/> and <see cref="FilterValues"/>.</summary>
    public void ClearFilter()
    {
        _filterValues = null;
        if (FilterText.Length > 0) FilterText = string.Empty; // notifies the grid
        else Owner?.OnColumnFilterChanged(this);
    }

    /// <summary>Creates an empty cell element; <see cref="PrepareCell"/> fills it for an item.</summary>
    public abstract UIElement CreateCell();

    /// <summary>Makes <paramref name="cell"/> show <paramref name="item"/>.</summary>
    public abstract void PrepareCell(UIElement cell, object item);

    /// <summary>Releases <paramref name="cell"/> from its item before it is reused. The base implementation does nothing.</summary>
    public virtual void ClearCell(UIElement cell)
    {
    }

    /// <summary>Gets the text of the cell for <paramref name="item"/>, used for filtering and fitting the width; <c>null</c> if it has none.</summary>
    public virtual string? GetCellText(object item) => null;

    /// <summary>Gets the value <paramref name="item"/> sorts by: <see cref="SortKey"/>, or the column's value.</summary>
    public virtual object? GetSortValue(object item) => SortKey?.Invoke(item);

    /// <summary>
    /// Gets the value of <paramref name="item"/> listed in the filter menu and matched by <see cref="FilterValues"/>: the
    /// cell text, or else the sort value as text. Empty for blanks.
    /// </summary>
    public virtual string GetFilterKey(object item) => GetCellText(item) ?? GetSortValue(item)?.ToString() ?? string.Empty;

    /// <summary>Gets the value an editor starts with; the base implementation returns the sort value.</summary>
    public virtual object? GetEditValue(object item) => GetSortValue(item);

    /// <summary>
    /// Converts the value an editor produced into the value <see cref="ValueSetter"/> stores; returns <c>false</c> with
    /// an <paramref name="error"/> if it isn't valid. The base implementation keeps the value.
    /// </summary>
    public virtual bool TryConvertEditValue(DataGridEditContext context, out object? value, out string? error)
    {
        value = context.Value;
        error = null;
        return true;
    }

    /// <summary>Creates the column type's own editor; the base implementation has none.</summary>
    protected virtual UIElement? CreateDefaultEditor(DataGridEditContext context) => null;

    internal UIElement? CreateEditor(DataGridEditContext context) => EditorTemplate?.Invoke(context) ?? CreateDefaultEditor(context);

    internal bool PassesFilter(object item)
    {
        if (_filterValues != null && !_filterValues.Contains(GetFilterKey(item))) return false;

        string filter = FilterText;
        if (string.IsNullOrEmpty(filter)) return true;
        if (FilterPredicate != null) return FilterPredicate(item, filter);
        string? text = GetCellText(item);
        return text != null && text.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }

    internal int Compare(object? a, object? b)
    {
        if (Comparer != null) return Comparer.Compare(a, b);
        if (a == null) return b == null ? 0 : -1;
        if (b == null) return 1;
        if (a is string sa && b is string sb) return string.Compare(sa, sb, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase);
        if (a is IComparable ca && a.GetType() == b.GetType()) return ca.CompareTo(b);
        return string.Compare(a.ToString(), b.ToString(), CultureInfo.CurrentCulture, CompareOptions.IgnoreCase);
    }
}

/// <summary>
/// A column showing a value of each item as text, optionally formatted (e.g. <c>"N0"</c> or <c>"yyyy-MM-dd"</c>). With a
/// <see cref="DataGridColumn.ValueSetter"/> it is edited in a text box; the text is converted back to the type of the
/// value (see <see cref="Parser"/>).
/// </summary>
public class DataGridTextColumn : DataGridColumn
{
    /// <summary>Initializes a text column.</summary>
    public DataGridTextColumn()
    {
    }

    /// <summary>Initializes a text column with a header and a value selector.</summary>
    public DataGridTextColumn(object? header, Func<object, object?> value)
    {
        Header = header;
        Value = value;
    }

    /// <summary>Gets or sets the value an item shows in this column.</summary>
    public Func<object, object?>? Value { get; set; }

    /// <summary>Gets or sets the format of <see cref="IFormattable"/> values, e.g. <c>"N0"</c>; <c>null</c> for the default.</summary>
    public string? Format { get; set; }

    /// <summary>Gets or sets a function that turns the value into the displayed text, replacing <see cref="Format"/>.</summary>
    public Func<object?, string>? TextConverter { get; set; }

    /// <summary>
    /// Gets or sets how edited text becomes a value (throw <see cref="FormatException"/> to reject it); <c>null</c> (the
    /// default) converts it to the type of the item's current value with the current culture.
    /// </summary>
    public Func<string, object?>? Parser { get; set; }

    /// <inheritdoc/>
    protected override bool HasValue => Value != null;

    /// <inheritdoc/>
    protected override bool HasDefaultEditor => true;

    /// <inheritdoc/>
    public override UIElement CreateCell() => new TextBlock
    {
        TextTrimming = TextTrimming.CharacterEllipsis,
        ShowsToolTipWhenTrimmed = true,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <inheritdoc/>
    public override void PrepareCell(UIElement cell, object item) => ((TextBlock)cell).Text = GetCellText(item) ?? string.Empty;

    /// <inheritdoc/>
    public override string? GetCellText(object item)
    {
        object? value = Value?.Invoke(item);
        if (TextConverter != null) return TextConverter(value);
        return FormatValue(value);
    }

    private string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        IFormattable formattable when Format != null => formattable.ToString(Format, CultureInfo.CurrentCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <inheritdoc/>
    public override object? GetSortValue(object item) => SortKey != null ? SortKey(item) : Value?.Invoke(item);

    /// <inheritdoc/>
    /// <remarks>
    /// For the text box: the value as text (formatted with <see cref="Format"/>, not <see cref="TextConverter"/>), e.g.
    /// bytes rather than "1.2 MB". For an <see cref="DataGridColumn.EditorTemplate"/>: the value itself (e.g. a
    /// <see cref="DateTime"/> for a date picker).
    /// </remarks>
    public override object? GetEditValue(object item) =>
        EditorTemplate != null ? Value?.Invoke(item) : FormatValue(Value?.Invoke(item));

    /// <inheritdoc/>
    /// <remarks>Text is parsed (see <see cref="Parser"/>); other values, from custom editors, are stored as they are.</remarks>
    public override bool TryConvertEditValue(DataGridEditContext context, out object? value, out string? error)
    {
        value = null;
        error = null;
        if (context.Value is not string and not null)
        {
            value = context.Value;
            return true;
        }
        string text = context.Value as string ?? string.Empty;
        try
        {
            if (Parser != null)
            {
                value = Parser(text);
                return true;
            }

            var original = Value?.Invoke(context.Item);
            var type = original?.GetType() ?? typeof(string);
            if (type == typeof(string))
            {
                value = text;
            }
            else if (text.Length == 0 && original is not null && Nullable.GetUnderlyingType(type) == null && type.IsValueType)
            {
                error = "Enter a value";
                return false;
            }
            else if (type == typeof(DateTime) && Format != null && DateTime.TryParseExact(text, Format, CultureInfo.CurrentCulture, DateTimeStyles.None, out var exact))
            {
                value = exact;
            }
            else
            {
                value = Convert.ChangeType(text, type, CultureInfo.CurrentCulture);
            }
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <inheritdoc/>
    protected override UIElement? CreateDefaultEditor(DataGridEditContext context) => CreateTextBoxEditor(context);

    /// <summary>
    /// Creates the text box editor of text columns, for any column: it starts with the context's value as text (all
    /// selected) and keeps <see cref="DataGridEditContext.Value"/> at the typed text. Use it as an
    /// <see cref="DataGridColumn.EditorTemplate"/>, e.g. of a template column.
    /// </summary>
    public static TextBox CreateTextBoxEditor(DataGridEditContext context)
    {
        var box = new TextBox
        {
            // Typing on a cell replaces its text with the typed text, like a spreadsheet.
            Text = context.InitialText ?? context.Value as string ?? context.Value?.ToString() ?? string.Empty,
            FieldHeight = 32,
            Padding = new Thickness(8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        box.TextChanged += (_, text) => context.Value = text;
        if (context.InitialText != null) context.Value = context.InitialText;
        box.GotFocus += (_, _) =>
        {
            if (context.InitialText != null) box.Select(box.Text.Length, 0);
            else box.SelectAll();
        };
        return box;
    }
}

/// <summary>A text column over items of type <typeparamref name="T"/>, for typed value selectors.</summary>
/// <typeparam name="T">The item type.</typeparam>
public class DataGridTextColumn<T> : DataGridTextColumn
{
    /// <summary>Initializes a text column showing <paramref name="value"/> of each item.</summary>
    public DataGridTextColumn(object? header, Func<T, object?> value)
    {
        Header = header;
        Value = item => item is T typed ? value(typed) : null;
    }

    /// <summary>
    /// Initializes an editable text column showing <paramref name="value"/> of each item and storing edits with
    /// <paramref name="setValue"/> (which gets the converted value, e.g. a <see cref="long"/> for a <see cref="long"/> value).
    /// </summary>
    public DataGridTextColumn(object? header, Func<T, object?> value, Action<T, object?> setValue) : this(header, value)
    {
        ValueSetter = (item, v) =>
        {
            if (item is T typed) setValue(typed, v);
        };
    }
}

/// <summary>
/// A column whose cells are built by <see cref="CellTemplate"/>, e.g. an icon with text or a progress bar. Set
/// <see cref="DataGridColumn.SortKey"/> to make it sortable and <see cref="Text"/> to make it filterable and fit its
/// width. It is edited only with an <see cref="DataGridColumn.EditorTemplate"/>.
/// </summary>
public class DataGridTemplateColumn : DataGridColumn
{
    /// <summary>Initializes a template column.</summary>
    public DataGridTemplateColumn()
    {
    }

    /// <summary>Initializes a template column with a header and a cell template.</summary>
    public DataGridTemplateColumn(object? header, Func<object, UIElement> cellTemplate)
    {
        Header = header;
        CellTemplate = cellTemplate;
    }

    /// <summary>Gets or sets the factory of a cell's content for an item; its DataContext is the item.</summary>
    public Func<object, UIElement>? CellTemplate { get; set; }

    /// <summary>Gets or sets the text of a cell, used for filtering and fitting the width.</summary>
    public Func<object, string?>? Text { get; set; }

    /// <inheritdoc/>
    protected override bool HasCellText => Text != null;

    /// <inheritdoc/>
    public override UIElement CreateCell() => new ContentControl { VerticalAlignment = VerticalAlignment.Center };

    /// <inheritdoc/>
    public override void PrepareCell(UIElement cell, object item)
    {
        var host = (ContentControl)cell;
        var content = CellTemplate?.Invoke(item);
        if (content != null) content.DataContext = item;
        host.Content = content;
    }

    /// <inheritdoc/>
    public override void ClearCell(UIElement cell) => ((ContentControl)cell).Content = null;

    /// <inheritdoc/>
    public override string? GetCellText(object item) => Text?.Invoke(item);
}

/// <summary>A template column over items of type <typeparamref name="T"/>, for typed cell templates.</summary>
/// <typeparam name="T">The item type.</typeparam>
public class DataGridTemplateColumn<T> : DataGridTemplateColumn
{
    /// <summary>Initializes a template column building each cell with <paramref name="cellTemplate"/>.</summary>
    public DataGridTemplateColumn(object? header, Func<T, UIElement> cellTemplate)
    {
        Header = header;
        CellTemplate = item => item is T typed ? cellTemplate(typed) : new TextBlock();
    }
}

/// <summary>
/// A column showing a boolean value of each item as a check box; with a <see cref="DataGridColumn.ValueSetter"/> the
/// check box changes the item directly (no edit mode). The filter menu lists its values as "Checked" and "Unchecked".
/// </summary>
public class DataGridCheckBoxColumn : DataGridColumn
{
    /// <summary>The filter key of checked items.</summary>
    public const string CheckedKey = "Checked";

    /// <summary>The filter key of unchecked items.</summary>
    public const string UncheckedKey = "Unchecked";

    /// <summary>Initializes a check box column.</summary>
    public DataGridCheckBoxColumn()
    {
        Width = GridLength.Pixels(72);
        CellAlignment = HorizontalAlignment.Center;
    }

    /// <summary>Initializes a check box column with a header and a value selector.</summary>
    public DataGridCheckBoxColumn(object? header, Func<object, bool> value, Action<object, bool>? setValue = null) : this()
    {
        Header = header;
        Value = value;
        if (setValue != null) ValueSetter = (item, v) => setValue(item, v is true);
    }

    /// <summary>Gets or sets the value an item shows.</summary>
    public Func<object, bool>? Value { get; set; }

    /// <inheritdoc/>
    protected override bool HasValue => Value != null;

    /// <inheritdoc/>
    public override UIElement CreateCell()
    {
        var box = new CheckBox { IsFocusable = false, VerticalAlignment = VerticalAlignment.Center };
        box.CheckedChanged += (s, isChecked) =>
        {
            if (!_isPreparing && s is CheckBox { DataContext: { } item } && ValueSetter != null && !IsReadOnly)
            {
                ValueSetter(item, isChecked == true);
            }
        };
        return box;
    }

    private bool _isPreparing;

    /// <inheritdoc/>
    public override void PrepareCell(UIElement cell, object item)
    {
        var box = (CheckBox)cell;
        _isPreparing = true; // updating from the item, not the user
        try
        {
            box.DataContext = item;
            box.IsChecked = Value?.Invoke(item) ?? false;
            box.IsEnabled = ValueSetter != null && !IsReadOnly;
        }
        finally
        {
            _isPreparing = false;
        }
    }

    /// <inheritdoc/>
    public override string? GetCellText(object item) => Value?.Invoke(item) == true ? "true" : "false";

    /// <inheritdoc/>
    public override string GetFilterKey(object item) => Value?.Invoke(item) == true ? CheckedKey : UncheckedKey;

    /// <inheritdoc/>
    public override object? GetSortValue(object item) => SortKey != null ? SortKey(item) : Value?.Invoke(item);
}

/// <summary>
/// The state of a cell edit in a <see cref="DataGrid"/>: the item and column, and the value the editor works on. An
/// editor from <see cref="DataGridColumn.EditorTemplate"/> reads <see cref="Value"/>, writes changes to it, and may end the
/// edit with <see cref="Commit"/> (e.g. when a date is picked) or <see cref="Cancel"/>.
/// </summary>
public sealed class DataGridEditContext
{
    internal DataGridEditContext(DataGrid grid, DataGridColumn column, object item, int index)
    {
        Grid = grid;
        Column = column;
        Item = item;
        Index = index;
        OriginalValue = column.GetEditValue(item);
        Value = OriginalValue;
    }

    /// <summary>Gets the grid.</summary>
    public DataGrid Grid { get; }

    /// <summary>Gets the edited column.</summary>
    public DataGridColumn Column { get; }

    /// <summary>Gets the edited item.</summary>
    public object Item { get; }

    /// <summary>Gets the index of the item in <see cref="DataGrid.View"/> when the edit began.</summary>
    public int Index { get; }

    /// <summary>Gets the value the edit started with (<see cref="DataGridColumn.GetEditValue"/>).</summary>
    public object? OriginalValue { get; }

    /// <summary>Gets or sets the edited value; the editor keeps it current.</summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets the text typed to start the edit (with cell selection, typing on a cell edits it), or <c>null</c> when the
    /// edit started otherwise (F2, a click, code). A text editor starts with it; others may interpret it, e.g. a digit.
    /// </summary>
    public string? InitialText { get; internal init; }

    /// <summary>Gets the message of the last rejected commit, or <c>null</c>.</summary>
    public string? Error { get; internal set; }

    /// <summary>Gets the editor element.</summary>
    public UIElement? Editor { get; internal set; }

    /// <summary>Stores <see cref="Value"/> and ends the edit; returns <c>false</c> (and stays in edit mode) if the value is rejected.</summary>
    public bool Commit() => Grid.EditContext == this && Grid.CommitEdit();

    /// <summary>Ends the edit without storing the value.</summary>
    public void Cancel()
    {
        if (Grid.EditContext == this) Grid.CancelEdit();
    }
}

/// <summary>Arguments of <see cref="DataGrid.CellEditEnding"/>.</summary>
public sealed class DataGridCellEditEndingEventArgs(DataGridEditContext context, object? value) : EventArgs
{
    /// <summary>Gets the edit.</summary>
    public DataGridEditContext Context { get; } = context;

    /// <summary>Gets the edited item.</summary>
    public object Item => Context.Item;

    /// <summary>Gets the edited column.</summary>
    public DataGridColumn Column => Context.Column;

    /// <summary>Gets or sets the value that will be stored (already converted by the column).</summary>
    public object? Value { get; set; } = value;

    /// <summary>Gets or sets a message that rejects the value: the editor stays open and shows it.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets whether to end the edit without storing the value.</summary>
    public bool Cancel { get; set; }
}
