using System;
using System.Collections.Generic;
using System.Linq;
using Atelier.Core.Inspection;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// A high-performance, Native AOT-compatible property inspector and editor control.
/// Operates with zero runtime reflection by leveraging <see cref="IInspectableObject"/> and <see cref="IPropertyDescriptor"/>.
/// </summary>
public class PropertyGrid : Control
{
    #region Bindable Properties

    public static readonly BindableProperty<object?> SelectedObjectProperty =
        BindableProperty.Register<PropertyGrid, object?>(
            nameof(SelectedObject),
            null,
            (s, o, n) => ((PropertyGrid)s).OnSelectedObjectChanged(o, n)
        );

    public static readonly BindableProperty<PropertySortMode> SortModeProperty =
        BindableProperty.Register<PropertyGrid, PropertySortMode>(
            nameof(SortMode),
            PropertySortMode.Categorized,
            (s, o, n) => ((PropertyGrid)s).OnSortModeChanged(o, n)
        );

    public static readonly BindableProperty<string> FilterTextProperty =
        BindableProperty.Register<PropertyGrid, string>(
            nameof(FilterText),
            string.Empty,
            (s, o, n) => ((PropertyGrid)s).OnFilterTextChanged(o, n)
        );

    public static readonly BindableProperty<bool> IsToolbarVisibleProperty =
        BindableProperty.Register<PropertyGrid, bool>(
            nameof(IsToolbarVisible),
            true,
            (s, o, n) => ((PropertyGrid)s).OnIsToolbarVisibleChanged(o, n)
        );

    public static readonly BindableProperty<float> ToolbarElevationProperty =
        BindableProperty.Register<PropertyGrid, float>(
            nameof(ToolbarElevation),
            2f,
            (s, o, n) => ((PropertyGrid)s).OnToolbarElevationChanged(o, n)
        );

    public static readonly BindableProperty<float> LabelWidthProperty =
        BindableProperty.Register<PropertyGrid, float>(
            nameof(LabelWidth),
            160f,
            (s, o, n) => ((PropertyGrid)s).RebuildProperties()
        );

    #endregion

    #region Properties

    public object? SelectedObject
    {
        get => GetValue(SelectedObjectProperty);
        set => SetValue(SelectedObjectProperty, value);
    }

    public PropertySortMode SortMode
    {
        get => GetValue(SortModeProperty);
        set => SetValue(SortModeProperty, value);
    }

    public string FilterText
    {
        get => GetValue(FilterTextProperty);
        set => SetValue(FilterTextProperty, value);
    }

    public bool IsToolbarVisible
    {
        get => GetValue(IsToolbarVisibleProperty);
        set => SetValue(IsToolbarVisibleProperty, value);
    }

    public float ToolbarElevation
    {
        get => GetValue(ToolbarElevationProperty);
        set => SetValue(ToolbarElevationProperty, value);
    }

    public float LabelWidth
    {
        get => GetValue(LabelWidthProperty);
        set => SetValue(LabelWidthProperty, value);
    }

    /// <summary>
    /// Gets the internal toolbar control for customization or direct inspection.
    /// </summary>
    public Toolbar Toolbar => _toolbar;

    /// <summary>
    /// Gets the per-instance editor registry for registering custom or overriding built-in editors.
    /// </summary>
    public PropertyEditorRegistry EditorRegistry { get; } = new();

    #endregion

    #region Events

    /// <summary>
    /// Raised when a property value is modified through an editor within this PropertyGrid.
    /// </summary>
    public event EventHandler<PropertyValueChangedEventArgs>? PropertyValueChanged;

    /// <summary>
    /// Raised when the <see cref="SelectedObject"/> changes.
    /// </summary>
    public event EventHandler<object?>? SelectedObjectChanged;

    /// <summary>
    /// Raised when the <see cref="SortMode"/> changes.
    /// </summary>
    public event EventHandler<PropertySortMode>? SortModeChanged;

    /// <summary>
    /// Raised when the <see cref="FilterText"/> changes.
    /// </summary>
    public event EventHandler<string>? FilterTextChanged;

    #endregion

    #region Internal Visual Tree Components

    private readonly Border _rootBorder;
    private readonly Grid _rootLayout;
    private readonly Toolbar _toolbar;
    private readonly Button _categorizedBtn;
    private readonly Button _alphabeticalBtn;
    private readonly TextBox _filterTextBox;
    private readonly ScrollViewer _scrollViewer;
    private readonly StackPanel _contentPanel;

    private readonly Dictionary<string, bool> _categoryExpansionState = new(StringComparer.OrdinalIgnoreCase);
    private bool _isUpdatingFilterText = false;

    #endregion

    public PropertyGrid()
    {
        IsFocusable = true;

        // Toolbar buttons
        var catContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        catContent.Add(new Icon { Kind = MaterialIconKind.Category, Size = 16, VerticalAlignment = VerticalAlignment.Center });
        catContent.Add(new TextBlock("Categorized") { FontSize = 12, VerticalAlignment = VerticalAlignment.Center });

        _categorizedBtn = new Button
        {
            Variant = ButtonVariant.Tonal,
            Padding = new Thickness(10, 6),
            CornerRadius = new CornerRadius(16),
            VerticalAlignment = VerticalAlignment.Center,
            Content = catContent
        };
        _categorizedBtn.Click += (s, e) => SortMode = PropertySortMode.Categorized;

        var alphaContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        alphaContent.Add(new Icon { Kind = MaterialIconKind.SortByAlpha, Size = 16, VerticalAlignment = VerticalAlignment.Center });
        alphaContent.Add(new TextBlock("Alphabetical") { FontSize = 12, VerticalAlignment = VerticalAlignment.Center });

        _alphabeticalBtn = new Button
        {
            Variant = ButtonVariant.Text,
            Padding = new Thickness(10, 6),
            CornerRadius = new CornerRadius(16),
            VerticalAlignment = VerticalAlignment.Center,
            Content = alphaContent
        };
        _alphabeticalBtn.Click += (s, e) => SortMode = PropertySortMode.Alphabetical;

        var buttonGroup = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        buttonGroup.Add(_categorizedBtn);
        buttonGroup.Add(_alphabeticalBtn);

        // Filter text box
        _filterTextBox = new TextBox
        {
            Placeholder = "Filter properties...",
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12, 4),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _filterTextBox.TextChanged += (s, text) =>
        {
            if (_isUpdatingFilterText) return;
            _isUpdatingFilterText = true;
            try
            {
                FilterText = text;
            }
            finally
            {
                _isUpdatingFilterText = false;
            }
        };

        // Toolbar Grid: Col 0 = Toggle Buttons (Auto), Col 1 = Filter (Star)
        var toolbarGrid = new Grid();
        toolbarGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        Grid.SetColumn(buttonGroup, 0);
        Grid.SetColumn(_filterTextBox, 1);

        toolbarGrid.Add(buttonGroup);
        toolbarGrid.Add(_filterTextBox);

        _toolbar = new Toolbar
        {
            Elevation = ToolbarElevation,
            Padding = new Thickness(10, 8),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.08f),
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Content = toolbarGrid
        };

        // Properties container & scroll viewer
        _contentPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        };

        _scrollViewer = new ScrollViewer
        {
            Content = _contentPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        // Root layout: Row 0 = Toolbar (Auto), Row 1 = Content (Star)
        _rootLayout = new Grid();
        _rootLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _rootLayout.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        Grid.SetRow(_toolbar, 0);
        Grid.SetRow(_scrollViewer, 1);

        // Add ScrollViewer first, then Toolbar so Toolbar renders on top
        // and its elevation drop shadow casts downward over the scrolling properties.
        _rootLayout.Add(_scrollViewer);
        _rootLayout.Add(_toolbar);

        _rootBorder = new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.12f),
            CornerRadius = new CornerRadius(8),
            Background = Color.Transparent,
            ClipToBounds = true,
            Child = _rootLayout
        };

        AddChild(_rootBorder);
        RebuildProperties();
    }

    #region Custom Editor API

    /// <summary>
    /// Registers a custom editor factory for property type <typeparamref name="T"/> on this PropertyGrid.
    /// </summary>
    public void RegisterEditor<T>(Func<PropertyEditorContext, UIElement> factory) =>
        EditorRegistry.Register<T>(factory);

    /// <summary>
    /// Registers a custom editor factory for the specified property type on this PropertyGrid.
    /// </summary>
    public void RegisterEditor(Type type, Func<PropertyEditorContext, UIElement> factory) =>
        EditorRegistry.Register(type, factory);

    /// <summary>
    /// Registers a custom editor factory with a predicate selector on this PropertyGrid.
    /// </summary>
    public void RegisterEditor(Func<PropertyEditorContext, bool> predicate, Func<PropertyEditorContext, UIElement> factory) =>
        EditorRegistry.Register(predicate, factory);

    #endregion

    #region Expand / Collapse Control

    public void ExpandAll()
    {
        var props = ObjectInspector.GetProperties(SelectedObject);
        foreach (var p in props)
        {
            string cat = string.IsNullOrWhiteSpace(p.Category) ? "General" : p.Category;
            _categoryExpansionState[cat] = true;
        }
        RebuildProperties();
    }

    public void CollapseAll()
    {
        var props = ObjectInspector.GetProperties(SelectedObject);
        foreach (var p in props)
        {
            string cat = string.IsNullOrWhiteSpace(p.Category) ? "General" : p.Category;
            _categoryExpansionState[cat] = false;
        }
        RebuildProperties();
    }

    public void SetCategoryExpanded(string category, bool isExpanded)
    {
        _categoryExpansionState[category] = isExpanded;
        RebuildProperties();
    }

    #endregion

    #region Change Notifications & Rebuilding

    private void OnSelectedObjectChanged(object? oldObj, object? newObj)
    {
        SelectedObjectChanged?.Invoke(this, newObj);
        RebuildProperties();
    }

    private void OnSortModeChanged(PropertySortMode oldMode, PropertySortMode newMode)
    {
        _categorizedBtn.Variant = newMode == PropertySortMode.Categorized ? ButtonVariant.Tonal : ButtonVariant.Text;
        _alphabeticalBtn.Variant = newMode == PropertySortMode.Alphabetical ? ButtonVariant.Tonal : ButtonVariant.Text;
        SortModeChanged?.Invoke(this, newMode);
        RebuildProperties();
    }

    private void OnFilterTextChanged(string oldText, string newText)
    {
        if (!_isUpdatingFilterText && _filterTextBox.Text != newText)
        {
            _isUpdatingFilterText = true;
            try
            {
                _filterTextBox.Text = newText ?? string.Empty;
            }
            finally
            {
                _isUpdatingFilterText = false;
            }
        }

        FilterTextChanged?.Invoke(this, newText);
        RebuildProperties();
    }

    private void OnIsToolbarVisibleChanged(bool oldVal, bool newVal)
    {
        _toolbar.Visibility = newVal ? Visibility.Visible : Visibility.Collapsed;
        _toolbar.InvalidateMeasure();
        _rootLayout.InvalidateMeasure();
        _rootBorder.InvalidateMeasure();
        _scrollViewer.InvalidateMeasure();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnToolbarElevationChanged(float oldVal, float newVal)
    {
        _toolbar.Elevation = newVal;
        _toolbar.InvalidateVisual();
        InvalidateVisual();
    }

    internal void NotifyPropertyValueChanged(PropertyValueChangedEventArgs e)
    {
        PropertyValueChanged?.Invoke(this, e);
    }

    public void RebuildProperties()
    {
        _contentPanel.Clear();

        object? target = SelectedObject;
        if (target == null)
        {
            _contentPanel.Add(new Border
            {
                Padding = new Thickness(32),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock("No object selected for inspection")
                {
                    FontSize = 12,
                    Foreground = Color.FromHex("#888888")
                }
            });
            _scrollViewer.ScrollOffsetY = 0;
            _scrollViewer.InvalidateMeasure();
            _contentPanel.InvalidateMeasure();
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        var allProperties = ObjectInspector.GetProperties(target);
        if (allProperties.Count == 0)
        {
            _contentPanel.Add(new Border
            {
                Padding = new Thickness(32),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock("No inspectable properties found on selected object")
                {
                    FontSize = 12,
                    Foreground = Color.FromHex("#888888")
                }
            });
            _scrollViewer.ScrollOffsetY = 0;
            _scrollViewer.InvalidateMeasure();
            _contentPanel.InvalidateMeasure();
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        string filter = (FilterText ?? string.Empty).Trim();

        // Filter matching properties
        var matchingProperties = new List<IPropertyDescriptor>();
        for (int i = 0; i < allProperties.Count; i++)
        {
            var p = allProperties[i];
            if (MatchesFilter(p, filter))
            {
                matchingProperties.Add(p);
            }
        }

        if (matchingProperties.Count == 0)
        {
            _contentPanel.Add(new Border
            {
                Padding = new Thickness(32),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock($"No properties match \"{filter}\"")
                {
                    FontSize = 12,
                    Foreground = Color.FromHex("#888888")
                }
            });
            _scrollViewer.ScrollOffsetY = 0;
            _scrollViewer.InvalidateMeasure();
            _contentPanel.InvalidateMeasure();
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        if (SortMode == PropertySortMode.Categorized)
        {
            BuildCategorizedView(target, matchingProperties);
        }
        else
        {
            BuildAlphabeticalView(target, matchingProperties);
        }

        _scrollViewer.ScrollOffsetY = 0;
        _scrollViewer.InvalidateMeasure();
        _contentPanel.InvalidateMeasure();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private static bool MatchesFilter(IPropertyDescriptor prop, string filter)
    {
        if (string.IsNullOrEmpty(filter))
            return true;

        return prop.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               prop.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               prop.Category.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void BuildCategorizedView(object target, List<IPropertyDescriptor> matchingProps)
    {
        var categoryGroups = matchingProps
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Category) ? "General" : p.Category)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        bool hasFilter = !string.IsNullOrEmpty(FilterText?.Trim());

        foreach (var group in categoryGroups)
        {
            string categoryName = group.Key;
            var propsInGroup = group.ToList();

            bool isExpanded = hasFilter || (!_categoryExpansionState.TryGetValue(categoryName, out bool exp) || exp);

            var chevronIcon = new Icon
            {
                Kind = isExpanded ? MaterialIconKind.ExpandMore : MaterialIconKind.ChevronRight,
                Size = 18,
                VerticalAlignment = VerticalAlignment.Center
            };

            var headerTitle = new TextBlock(categoryName)
            {
                Bold = true,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };

            var countBadge = new TextBlock($"({propsInGroup.Count})")
            {
                FontSize = 11,
                Foreground = Color.FromHex("#888888"),
                VerticalAlignment = VerticalAlignment.Center
            };

            var headerContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerContent.Add(chevronIcon);
            headerContent.Add(headerTitle);
            headerContent.Add(countBadge);

            var categoryHeader = new Border
            {
                Padding = new Thickness(10, 8),
                Background = Color.FromHex("#000000").WithAlpha(0.04f),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = Color.FromHex("#000000").WithAlpha(0.06f),
                Child = headerContent
            };

            var childrenPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 0,
                Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed
            };

            foreach (var prop in propsInGroup)
            {
                var row = CreatePropertyRow(prop, target);
                childrenPanel.Add(row);
            }

            // Click header to toggle category expansion
            categoryHeader.PointerPressed += (s, e) =>
            {
                bool currentExp = !_categoryExpansionState.TryGetValue(categoryName, out bool cur) || cur;
                bool newExp = !currentExp;
                _categoryExpansionState[categoryName] = newExp;
                chevronIcon.Kind = newExp ? MaterialIconKind.ExpandMore : MaterialIconKind.ChevronRight;
                childrenPanel.Visibility = newExp ? Visibility.Visible : Visibility.Collapsed;
                InvalidateMeasure();
            };

            _contentPanel.Add(categoryHeader);
            _contentPanel.Add(childrenPanel);
        }
    }

    private void BuildAlphabeticalView(object target, List<IPropertyDescriptor> matchingProps)
    {
        var sorted = matchingProps
            .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var prop in sorted)
        {
            var row = CreatePropertyRow(prop, target);
            _contentPanel.Add(row);
        }
    }

    private UIElement CreatePropertyRow(IPropertyDescriptor prop, object target)
    {
        var labelPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        var label = new TextBlock(prop.DisplayName)
        {
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        labelPanel.Add(label);

        if (prop.IsReadOnly)
        {
            var readOnlyBadge = new TextBlock("(Read-Only)")
            {
                FontSize = 10,
                Foreground = Color.FromHex("#9E9E9E"),
                VerticalAlignment = VerticalAlignment.Center
            };
            labelPanel.Add(readOnlyBadge);
        }

        var context = new PropertyEditorContext(this, target, prop);
        var editor = EditorRegistry.CreateEditor(context);
        editor.VerticalAlignment = VerticalAlignment.Center;

        var rowGrid = new Grid();
        rowGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Pixels(LabelWidth)));
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        Grid.SetColumn(labelPanel, 0);
        Grid.SetColumn(editor, 1);

        rowGrid.Add(labelPanel);
        rowGrid.Add(editor);

        var rowBorder = new Border
        {
            Padding = new Thickness(12, 6),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Color.FromHex("#000000").WithAlpha(0.04f),
            Child = rowGrid
        };

        // Hover highlight
        rowBorder.PointerEntered += (s, e) => rowBorder.Background = Color.FromHex("#000000").WithAlpha(0.02f);
        rowBorder.PointerExited += (s, e) => rowBorder.Background = Color.Transparent;

        return rowBorder;
    }

    #endregion

    #region Layout Overrides

    protected override Size MeasureOverride(Size availableSize)
    {
        _rootBorder.Measure(availableSize);
        return _rootBorder.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _rootBorder.Arrange(new Rect(Point.Zero, finalSize));
        return finalSize;
    }

    #endregion
}
