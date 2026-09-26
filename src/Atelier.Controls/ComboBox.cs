using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Rendering;

namespace Atelier.Controls;

/// <summary>
/// A selection box that shows the selected item and opens a drop-down list to choose another one.
/// </summary>
/// <remarks>
/// <para>
/// The drop-down is an internal <see cref="ListBox"/> in a <see cref="Popup"/>. While it is open, arrow keys, Home/End,
/// PageUp/PageDown and typing move a highlight without changing <see cref="SelectedItem"/>; clicking an item or pressing
/// Enter commits the highlighted item and closes the drop-down, Escape closes it and keeps the previous selection. Focus
/// stays on (and returns to) the combo box.
/// </para>
/// <para>
/// While closed, Up/Down/Home/End change the selection directly (clamped, no wrap-around), typing selects by prefix, and
/// Space, Enter, F4 or Alt+Down open the drop-down.
/// </para>
/// <para>
/// Selection follows changes to <see cref="Items"/> like <see cref="ListBox"/>: the selected index shifts with inserts and
/// moves, removing or replacing the selected item clears the selection, and values set while there are no items are kept
/// as a pending selection until items arrive.
/// </para>
/// <para>Editable (text entry) combo boxes and multi-selection are not supported.</para>
/// </remarks>
public class ComboBox : Control
{
    /// <summary>Identifies the <see cref="ItemsSource"/> bindable property.</summary>
    public static readonly BindableProperty<IEnumerable?> ItemsSourceProperty =
        BindableProperty.Register<ComboBox, IEnumerable?>(
            nameof(ItemsSource),
            null,
            (s, o, n) => ((ComboBox)s)._listBox.ItemsSource = n
        );

    /// <summary>Identifies the <see cref="SelectedItem"/> bindable property.</summary>
    public static readonly BindableProperty<object?> SelectedItemProperty =
        BindableProperty.Register<ComboBox, object?>(
            nameof(SelectedItem),
            null,
            (s, o, n) => ((ComboBox)s).OnSelectedItemChanged(o, n),
            coerceValue: (s, v) => ((ComboBox)s).CoerceSelectedItem(v)
        );

    /// <summary>Identifies the <see cref="SelectedIndex"/> bindable property.</summary>
    public static readonly BindableProperty<int> SelectedIndexProperty =
        BindableProperty.Register<ComboBox, int>(
            nameof(SelectedIndex),
            -1,
            (s, o, n) => ((ComboBox)s).OnSelectedIndexChanged(o, n),
            coerceValue: (s, v) => ((ComboBox)s).CoerceSelectedIndex(v)
        );

    /// <summary>Identifies the <see cref="Placeholder"/> bindable property.</summary>
    public static readonly BindableProperty<string> PlaceholderProperty =
        BindableProperty.Register<ComboBox, string>(
            nameof(Placeholder),
            "Select an option...",
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="IsDropDownOpen"/> bindable property.</summary>
    public static readonly BindableProperty<bool> IsDropDownOpenProperty =
        BindableProperty.Register<ComboBox, bool>(
            nameof(IsDropDownOpen),
            false,
            (s, o, n) => ((ComboBox)s).OnIsDropDownOpenChanged(o, n)
        );

    /// <summary>Identifies the <see cref="MaxDropDownHeight"/> bindable property.</summary>
    public static readonly BindableProperty<float> MaxDropDownHeightProperty =
        BindableProperty.Register<ComboBox, float>(
            nameof(MaxDropDownHeight),
            240f,
            (s, o, n) => ((ComboBox)s).ApplyMaxDropDownHeight(n)
        );

    /// <summary>Identifies the <see cref="IsTextSearchEnabled"/> bindable property.</summary>
    public static readonly BindableProperty<bool> IsTextSearchEnabledProperty =
        BindableProperty.Register<ComboBox, bool>(nameof(IsTextSearchEnabled), true);

    /// <summary>
    /// Gets or sets the collection the items are taken from. Default <c>null</c>. Changes of an
    /// <see cref="INotifyCollectionChanged"/> source are mirrored into <see cref="Items"/> (weakly subscribed).
    /// </summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Gets or sets the committed selected item, or <c>null</c>. Default <c>null</c>.</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Gets or sets the index of the committed selection in <see cref="Items"/>, or -1. Default -1.</summary>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>Gets or sets the text shown when nothing is selected. Default "Select an option...".</summary>
    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Gets or sets whether the drop-down list is open. Default <c>false</c>.</summary>
    public bool IsDropDownOpen
    {
        get => GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    /// <summary>Gets or sets the maximum height of the drop-down list; longer lists scroll. Default 240.</summary>
    public float MaxDropDownHeight
    {
        get => GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets whether typing selects (closed) or highlights (open) the next item whose text starts with the typed
    /// prefix. Default <c>true</c>.
    /// </summary>
    public bool IsTextSearchEnabled
    {
        get => GetValue(IsTextSearchEnabledProperty);
        set => SetValue(IsTextSearchEnabledProperty, value);
    }

    /// <summary>Gets the items shown in the drop-down; filled from <see cref="ItemsSource"/> or edited directly.</summary>
    public ObservableCollection<object> Items => _listBox.Items;

    private Func<object, UIElement>? _itemTemplate;

    /// <summary>
    /// Gets or sets the factory for item visuals, used both in the drop-down and for the selection box. Default
    /// <c>null</c>: items are shown as text.
    /// </summary>
    public Func<object, UIElement>? ItemTemplate
    {
        get => _itemTemplate;
        set
        {
            _itemTemplate = value;
            _listBox.ItemTemplate = value;
            _itemsWidthDirty = true;
            UpdateSelectionDisplay();
        }
    }

    /// <summary>
    /// Occurs when the committed selection changes; the argument is the new <see cref="SelectedItem"/>. Raised once per
    /// change and not while moving the highlight in the open drop-down.
    /// </summary>
    public event EventHandler<object?>? SelectionChanged;

    /// <summary>Occurs after the drop-down opened.</summary>
    public event EventHandler? DropDownOpened;

    /// <summary>Occurs after the drop-down closed.</summary>
    public event EventHandler? DropDownClosed;

    private const float ChevronWidth = 28f;

    private readonly Popup _popup;
    private readonly ListBox _listBox;
    private readonly Border _dropdownBorder;
    private UIElement? _selectionDisplayElement;
    private bool _isSyncingSelection;
    private bool _isSelectionResolved;
    private TextSearchState? _textSearch;

    private bool _itemsWidthDirty = true;
    private float _itemsWidth;
    private float _itemsWidthFontSize;
    private string? _itemsWidthFontFamily;

    static ComboBox()
    {
        PaddingProperty.OverrideDefaultValue<ComboBox>(new Thickness(14, 10));
        CornerRadiusProperty.OverrideDefaultValue<ComboBox>(new CornerRadius(4));
    }

    /// <summary>Initializes a new, empty <see cref="ComboBox"/>.</summary>
    public ComboBox()
    {
        IsFocusable = true;

        // The drop-down list never takes focus: keys are forwarded from the combo box, and clicks commit through
        // OnDropDownItemClicked.
        _listBox = new ListBox
        {
            OwnerComboBox = this,
            IsFocusable = false
        };

        _dropdownBorder = new Border
        {
            CornerRadius = new CornerRadius(4),
            Child = _listBox
        };

        _popup = new Popup
        {
            PlacementTarget = this,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            MatchTargetWidth = true,
            Elevation = 6f,
            Child = _dropdownBorder
        };

        _popup.Closed += OnPopupClosed;

        AddChild(_popup);
        ApplyMaxDropDownHeight(MaxDropDownHeight);
    }

    /// <summary>Gets the element created by <see cref="ItemTemplate"/> for the selected item, or <c>null</c>.</summary>
    public UIElement? SelectionDisplayElement => _selectionDisplayElement;

    /// <summary>
    /// Gets the text of the selected item (its <see cref="object.ToString"/>), or <c>null</c> if nothing is selected.
    /// Cached when the selection changes; used by renderers when no <see cref="ItemTemplate"/> is set.
    /// </summary>
    public string? SelectionBoxText { get; private set; }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        // Light dismiss (click outside): close without committing.
        if (IsDropDownOpen)
        {
            IsDropDownOpen = false;
        }
    }

    internal void OnDropDownItemClicked(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            SelectedIndex = index;
        }
        IsDropDownOpen = false;
        Focus();
    }

    internal void OnDropDownItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        _itemsWidthDirty = true;
        InvalidateMeasure();

        object? oldItem = SelectedItem;
        int newIndex = SelectionSync.ResyncIndex(Items, SelectedIndex, oldItem, _isSelectionResolved, e);

        _isSyncingSelection = true;
        try
        {
            if (newIndex >= 0)
            {
                SelectedIndex = newIndex;
                SelectedItem = Items[newIndex];
            }
            else if (newIndex == -1)
            {
                SelectedIndex = -1;
                SelectedItem = null;
            }
        }
        finally
        {
            _isSyncingSelection = false;
        }

        _isSelectionResolved = SelectedIndex >= 0 && SelectedIndex < Items.Count;
        if (!IsDropDownOpen)
        {
            _listBox.SelectedIndex = _isSelectionResolved ? SelectedIndex : -1;
        }

        if (!Equals(oldItem, SelectedItem))
        {
            UpdateSelectionDisplay();
            SelectionChanged?.Invoke(this, SelectedItem);
        }
    }

    private int CoerceSelectedIndex(int value)
    {
        if (value < -1) return -1;
        if (Items.Count == 0) return value; // pending until items arrive
        return value < Items.Count ? value : -1;
    }

    private object? CoerceSelectedItem(object? value)
    {
        if (value == null || Items.Count == 0) return value;
        return SelectionSync.IndexOf(Items, value, SelectedIndex) >= 0 ? value : null;
    }

    private void OnSelectedItemChanged(object? oldItem, object? newItem)
    {
        if (_isSyncingSelection) return;

        _isSyncingSelection = true;
        try
        {
            SelectedIndex = newItem == null ? -1 : SelectionSync.IndexOf(Items, newItem, SelectedIndex);
        }
        finally
        {
            _isSyncingSelection = false;
        }

        CommitSelectionState();
        SelectionChanged?.Invoke(this, newItem);
    }

    private void OnSelectedIndexChanged(int oldIndex, int newIndex)
    {
        if (_isSyncingSelection) return;

        object? oldItem = SelectedItem;
        _isSyncingSelection = true;
        try
        {
            if (newIndex >= 0 && newIndex < Items.Count)
            {
                SelectedItem = Items[newIndex];
            }
            else if (newIndex < 0 || Items.Count > 0)
            {
                SelectedItem = null;
            }
        }
        finally
        {
            _isSyncingSelection = false;
        }

        CommitSelectionState();
        if (!Equals(oldItem, SelectedItem))
        {
            SelectionChanged?.Invoke(this, SelectedItem);
        }
    }

    private void CommitSelectionState()
    {
        _isSelectionResolved = SelectedIndex >= 0 && SelectedIndex < Items.Count;
        if (!IsDropDownOpen)
        {
            _listBox.SelectedIndex = _isSelectionResolved ? SelectedIndex : -1;
        }
        UpdateSelectionDisplay();
    }

    private void OnIsDropDownOpenChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            // Start highlighting at the committed selection.
            _listBox.SelectedIndex = _isSelectionResolved ? SelectedIndex : -1;
            _popup.IsOpen = true;
            if (_isSelectionResolved)
            {
                _listBox.ScrollIntoView(SelectedIndex);
            }
            DropDownOpened?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            // Drop an uncommitted highlight.
            _listBox.SelectedIndex = _isSelectionResolved ? SelectedIndex : -1;
            _popup.IsOpen = false;
            DropDownClosed?.Invoke(this, EventArgs.Empty);
        }

        InvalidateVisual();
    }

    private void ApplyMaxDropDownHeight(float height)
    {
        _listBox.MaxHeight = height;
        _popup.MaxHeight = height + 16f;
    }

    private void UpdateSelectionDisplay()
    {
        var item = SelectedItem;
        SelectionBoxText = item?.ToString();

        if (_selectionDisplayElement != null)
        {
            RemoveChild(_selectionDisplayElement);
            _selectionDisplayElement = null;
        }

        if (item != null && ItemTemplate != null)
        {
            _selectionDisplayElement = ItemTemplate(item);
            _selectionDisplayElement.DataContext = item;
            _selectionDisplayElement.IsHitTestVisible = false;
            AddChild(_selectionDisplayElement);
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    /// <remarks>A left click toggles the drop-down.</remarks>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!IsEnabled) return;

        // Presses inside the drop-down bubble up to here; they must not toggle it.
        if (e.Source is VisualNode source && source.IsDescendantOf(_popup)) return;

        if (e.Button == PointerButtons.Left)
        {
            Focus();
            IsDropDownOpen = !IsDropDownOpen;
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsEnabled || e.Handled) return;

        bool alt = (e.Modifiers & ModifierKeys.Alt) != 0;

        if (!IsDropDownOpen)
        {
            if (e.Key is Key.Space or Key.Enter or Key.F4 || (e.Key == Key.Down && alt))
            {
                IsDropDownOpen = true;
                e.Handled = true;
                return;
            }

            if (Items.Count == 0) return;

            int current = _isSelectionResolved ? SelectedIndex : -1;
            int target = e.Key switch
            {
                Key.Down => current < 0 ? 0 : Math.Min(current + 1, Items.Count - 1),
                Key.Up => current <= 0 ? 0 : current - 1,
                Key.Home => 0,
                Key.End => Items.Count - 1,
                _ => int.MinValue
            };

            if (target != int.MinValue)
            {
                SelectedIndex = target;
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Escape)
        {
            IsDropDownOpen = false; // reverts the highlight
            e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.F4 || (e.Key == Key.Up && alt))
        {
            int highlighted = _listBox.SelectedIndex;
            if (highlighted >= 0 && highlighted < Items.Count)
            {
                SelectedIndex = highlighted;
            }
            IsDropDownOpen = false;
            e.Handled = true;
        }
        else
        {
            // Navigation keys move the highlight in the drop-down list.
            _listBox.OnKeyDown(e);
        }
    }

    /// <inheritdoc/>
    public override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || !IsEnabled || !IsTextSearchEnabled) return;

        if (IsDropDownOpen)
        {
            _listBox.OnTextInput(e);
            return;
        }

        int match = (_textSearch ??= new TextSearchState()).Find(Items, SelectedIndex, e.Text);
        if (match >= 0)
        {
            SelectedIndex = match;
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Without an <see cref="ItemTemplate"/> the width fits the widest item text (and the placeholder), so the box does
    /// not resize when the selection changes; with a template it fits the selected item's visual. At least 160 wide and
    /// 40 high.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        float contentW;
        float contentH;

        if (_selectionDisplayElement != null)
        {
            var innerAvailable = new Size(Math.Max(0, availableSize.Width - padding.Horizontal - ChevronWidth), availableSize.Height);
            _selectionDisplayElement.Measure(innerAvailable);
            contentW = _selectionDisplayElement.DesiredSize.Width;
            contentH = _selectionDisplayElement.DesiredSize.Height;
        }
        else
        {
            contentW = GetItemsTextWidth();
            contentH = TextMeasurer.GetFontSpacing(FontSize, FontFamily);
        }

        float width = Math.Max(160f, contentW + ChevronWidth + padding.Horizontal);
        if (!float.IsInfinity(availableSize.Width))
        {
            width = Math.Min(width, Math.Max(160f, availableSize.Width));
        }

        return new Size(width, Math.Max(40f, contentH + padding.Vertical));
    }

    private float GetItemsTextWidth()
    {
        float fontSize = FontSize;
        string? fontFamily = FontFamily;
        if (_itemsWidthDirty || fontSize != _itemsWidthFontSize || fontFamily != _itemsWidthFontFamily)
        {
            float width = TextMeasurer.Measure(Placeholder ?? string.Empty, fontSize, fontFamily).Width;
            if (ItemTemplate == null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    string? text = Items[i]?.ToString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        width = Math.Max(width, TextMeasurer.Measure(text, fontSize, fontFamily).Width);
                    }
                }
            }

            _itemsWidth = width;
            _itemsWidthFontSize = fontSize;
            _itemsWidthFontFamily = fontFamily;
            _itemsWidthDirty = false;
        }

        return _itemsWidth;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_selectionDisplayElement != null)
        {
            var padding = Padding;
            float contentW = Math.Max(0, finalSize.Width - padding.Horizontal - ChevronWidth);
            float contentH = Math.Max(0, finalSize.Height - padding.Vertical);
            _selectionDisplayElement.Arrange(new Rect(padding.Left, padding.Top, contentW, contentH));
        }

        return finalSize;
    }
}
