using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

public class ComboBox : Control
{
    public static readonly BindableProperty<IEnumerable?> ItemsSourceProperty =
        BindableProperty.Register<ComboBox, IEnumerable?>(
            nameof(ItemsSource),
            null,
            (s, o, n) => ((ComboBox)s).OnItemsSourceChanged(o, n)
        );

    public static readonly BindableProperty<object?> SelectedItemProperty =
        BindableProperty.Register<ComboBox, object?>(
            nameof(SelectedItem),
            null,
            (s, o, n) => ((ComboBox)s).OnSelectedItemChanged(o, n)
        );

    public static readonly BindableProperty<int> SelectedIndexProperty =
        BindableProperty.Register<ComboBox, int>(
            nameof(SelectedIndex),
            -1,
            (s, o, n) => ((ComboBox)s).OnSelectedIndexChanged(o, n)
        );

    public static readonly BindableProperty<string> PlaceholderProperty =
        BindableProperty.Register<ComboBox, string>(
            nameof(Placeholder),
            "Select an option...",
            (s, o, n) => ((ComboBox)s).InvalidateVisual()
        );

    public static readonly BindableProperty<bool> IsDropDownOpenProperty =
        BindableProperty.Register<ComboBox, bool>(
            nameof(IsDropDownOpen),
            false,
            (s, o, n) => ((ComboBox)s).OnIsDropDownOpenChanged(o, n)
        );

    public static readonly BindableProperty<float> MaxDropDownHeightProperty =
        BindableProperty.Register<ComboBox, float>(
            nameof(MaxDropDownHeight),
            240f,
            (s, o, n) => ((ComboBox)s).OnMaxDropDownHeightChanged(n)
        );

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public float MaxDropDownHeight
    {
        get => GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    public ObservableCollection<object> Items { get; } = [];

    private Func<object, UIElement>? _itemTemplate;
    public Func<object, UIElement>? ItemTemplate
    {
        get => _itemTemplate;
        set
        {
            _itemTemplate = value;
            _listBox.ItemTemplate = value;
            UpdateSelectionDisplay();
        }
    }

    public event EventHandler<object?>? SelectionChanged;
    public event EventHandler? DropDownOpened;
    public event EventHandler? DropDownClosed;

    private readonly Popup _popup;
    private readonly ListBox _listBox;
    private readonly Border _dropdownBorder;
    private UIElement? _selectionDisplayElement;
    private bool _isSynchronizingSelection;

    public ComboBox()
    {
        IsFocusable = true;
        Padding = new Thickness(14, 10);
        CornerRadius = new CornerRadius(4);

        // Internal ListBox for popup dropdown
        _listBox = new ListBox
        {
            ItemsSource = Items,
            MaxHeight = MaxDropDownHeight
        };

        _listBox.SelectionChanged += (s, item) =>
        {
            if (!_isSynchronizingSelection)
            {
                SelectedItem = item;
                IsDropDownOpen = false;
            }
        };

        _dropdownBorder = new Border
        {
            CornerRadius = new CornerRadius(4),
            Child = _listBox
        };

        // Internal Popup
        _popup = new Popup
        {
            PlacementTarget = this,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            MatchTargetWidth = true,
            Elevation = 6f,
            Child = _dropdownBorder
        };

        _popup.Closed += (s, e) =>
        {
            if (IsDropDownOpen)
            {
                IsDropDownOpen = false;
            }
        };

        AddChild(_popup);

        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    private void OnItemsSourceChanged(IEnumerable? oldSource, IEnumerable? newSource)
    {
        if (oldSource is INotifyCollectionChanged oldIncc)
        {
            oldIncc.CollectionChanged -= OnItemsSourceCollectionChanged;
        }

        Items.Clear();

        if (newSource != null)
        {
            foreach (var item in newSource)
            {
                Items.Add(item);
            }

            if (newSource is INotifyCollectionChanged newIncc)
            {
                newIncc.CollectionChanged += OnItemsSourceCollectionChanged;
            }
        }

        UpdateSelectionDisplay();
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    for (int i = 0; i < e.NewItems.Count; i++)
                    {
                        Items.Insert(e.NewStartingIndex + i, e.NewItems[i]!);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    for (int i = 0; i < e.OldItems.Count; i++)
                    {
                        Items.RemoveAt(e.OldStartingIndex);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                Items.Clear();
                if (ItemsSource != null)
                {
                    foreach (var item in ItemsSource) Items.Add(item);
                }
                break;
        }

        UpdateSelectionDisplay();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateSelectionDisplay();
    }

    private void OnSelectedItemChanged(object? oldItem, object? newItem)
    {
        if (_isSynchronizingSelection) return;

        _isSynchronizingSelection = true;
        try
        {
            SelectedIndex = newItem != null ? Items.IndexOf(newItem) : -1;
            _listBox.SelectedItem = newItem;
            UpdateSelectionDisplay();
            SelectionChanged?.Invoke(this, newItem);
        }
        finally
        {
            _isSynchronizingSelection = false;
        }

        InvalidateVisual();
    }

    private void OnSelectedIndexChanged(int oldIndex, int newIndex)
    {
        if (_isSynchronizingSelection) return;

        _isSynchronizingSelection = true;
        try
        {
            if (newIndex >= 0 && newIndex < Items.Count)
            {
                SelectedItem = Items[newIndex];
                _listBox.SelectedIndex = newIndex;
            }
            else
            {
                SelectedItem = null;
                _listBox.SelectedIndex = -1;
            }
            UpdateSelectionDisplay();
            SelectionChanged?.Invoke(this, SelectedItem);
        }
        finally
        {
            _isSynchronizingSelection = false;
        }

        InvalidateVisual();
    }

    private void OnIsDropDownOpenChanged(bool oldVal, bool newVal)
    {
        _popup.IsOpen = newVal;

        if (newVal)
        {
            if (SelectedIndex >= 0)
            {
                _listBox.SelectedIndex = SelectedIndex;
                _listBox.ScrollIntoView(SelectedIndex);
            }
            DropDownOpened?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            DropDownClosed?.Invoke(this, EventArgs.Empty);
        }

        InvalidateVisual();
    }

    private void OnMaxDropDownHeightChanged(float height)
    {
        _listBox.MaxHeight = height;
        _popup.MaxHeight = height + 16f;
    }

    private void UpdateSelectionDisplay()
    {
        if (_selectionDisplayElement != null)
        {
            RemoveChild(_selectionDisplayElement);
            _selectionDisplayElement = null;
        }

        if (SelectedItem != null)
        {
            if (ItemTemplate != null)
            {
                _selectionDisplayElement = ItemTemplate(SelectedItem);
                _selectionDisplayElement.DataContext = SelectedItem;
                _selectionDisplayElement.IsHitTestVisible = false;
                AddChild(_selectionDisplayElement);
            }
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    public UIElement? SelectionDisplayElement => _selectionDisplayElement;

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!IsEnabled) return;

        if (e.Button == PointerButtons.Left)
        {
            IsDropDownOpen = !IsDropDownOpen;
            e.Handled = true;
        }
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsEnabled) return;

        if (!IsDropDownOpen)
        {
            if (e.Key is Key.Space or Key.Enter || (e.Key == Key.Down && (e.Modifiers & ModifierKeys.Alt) != 0))
            {
                IsDropDownOpen = true;
                e.Handled = true;
            }
            else if (e.Key == Key.Down && Items.Count > 0)
            {
                int next = SelectedIndex < Items.Count - 1 ? SelectedIndex + 1 : 0;
                SelectedIndex = next;
                e.Handled = true;
            }
            else if (e.Key == Key.Up && Items.Count > 0)
            {
                int prev = SelectedIndex > 0 ? SelectedIndex - 1 : Items.Count - 1;
                SelectedIndex = prev;
                e.Handled = true;
            }
        }
        else
        {
            if (e.Key == Key.Escape || (e.Key == Key.Up && (e.Modifiers & ModifierKeys.Alt) != 0))
            {
                IsDropDownOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (_listBox.SelectedIndex >= 0)
                {
                    SelectedItem = _listBox.SelectedItem;
                }
                IsDropDownOpen = false;
                e.Handled = true;
            }
            else
            {
                // Forward navigation keys to inner ListBox
                _listBox.OnKeyDown(e);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        float minH = 40f;
        float headerW = 120f;
        float chevronW = 28f;

        if (_selectionDisplayElement != null)
        {
            var innerAvailable = new Size(Math.Max(0, availableSize.Width - Padding.Horizontal - chevronW), availableSize.Height);
            _selectionDisplayElement.Measure(innerAvailable);
            headerW = _selectionDisplayElement.DesiredSize.Width;
            minH = Math.Max(minH, _selectionDisplayElement.DesiredSize.Height + Padding.Vertical);
        }

        float totalW = headerW + chevronW + Padding.Horizontal;
        return new Size(Math.Max(160f, totalW), minH);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        float chevronW = 28f;
        if (_selectionDisplayElement != null)
        {
            float contentW = Math.Max(0, finalSize.Width - Padding.Horizontal - chevronW);
            float contentH = Math.Max(0, finalSize.Height - Padding.Vertical);
            _selectionDisplayElement.Arrange(new Rect(Padding.Left, Padding.Top, contentW, contentH));
        }

        return finalSize;
    }
}
