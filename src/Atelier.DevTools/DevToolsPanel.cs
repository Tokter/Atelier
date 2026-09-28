using System.ComponentModel;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Threading;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.DevTools;

/// <summary>
/// The developer tools panel: a toolbar (pick an element, box model, all bounds, frame rate, parent, focused element,
/// refresh, close), the element tree with a search, and tabs for the selected element's properties (editable), all its
/// values with their sources, its layout (box model and sizes) and a pixel zoom.
/// </summary>
public sealed class DevToolsPanel : Border
{
    private readonly DevToolsSession _session;
    private readonly List<ElementNode> _roots = [];
    private readonly ToggleButton _pickButton;
    private readonly ToggleButton _boxModelButton;
    private readonly ToggleButton _boundsButton;
    private readonly ToggleButton _fpsButton;
    private readonly TextBlock _selectionText = new() { Muted = true, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(12, 0, 12, 6) };
    private readonly DataGrid _values = new() { ShowFilterMenus = true, RowHeight = 32, EmptyContent = "Select an element." };
    private readonly StackPanel _layoutContent = new() { Spacing = 16, Margin = new Thickness(16) };
    private readonly TextBlock _pixelText = new() { Muted = true, Margin = new Thickness(12, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer _layoutTimer;
    private ElementInspection? _inspection;
    private List<PropertyValueRow> _rows = [];
    private bool _isSyncingTree;
    private string _layoutSignature = string.Empty;

    internal DevToolsPanel(DevToolsSession session)
    {
        _session = session;
        ClipToBounds = true;

        // Header and toolbar.
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 10, 4, 4) };
        title.Add(new Icon(MaterialIconKind.BugReport, 20) { VerticalAlignment = VerticalAlignment.Center });
        title.Add(new TextBlock { Text = "DevTools", FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
        var close = IconButton(MaterialIconKind.Close, "Close (F12)", () => DevToolsManager.Close(_session.Host));
        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        titleRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Place(titleRow, title, 0, 0);
        Place(titleRow, close, 0, 1);

        _pickButton = Toggle(MaterialIconKind.AdsClick, "Select an element in the window (Ctrl+Shift+C); Esc stops", on => _session.IsPicking = on);
        _boxModelButton = Toggle(MaterialIconKind.Padding, "Show the selected element's margin, border, padding and spacing", on => _session.ShowBoxModel = on);
        _boundsButton = Toggle(MaterialIconKind.BorderAll, "Outline every element", on => _session.ShowAllBounds = on);
        _fpsButton = Toggle(MaterialIconKind.Speed, "Show the frame rate", on => _session.Host.ShowFpsOverlay = on);
        _boxModelButton.IsChecked = _session.ShowBoxModel;
        _fpsButton.IsChecked = _session.Host.ShowFpsOverlay;
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(8, 0, 8, 4) };
        AddAll(toolbar, 
            _pickButton, _boxModelButton, _boundsButton, _fpsButton,
            IconButton(MaterialIconKind.ArrowUpward, "Select the parent", SelectParent),
            IconButton(MaterialIconKind.CenterFocusStrong, "Select the focused element", () => SelectFocused()),
            IconButton(MaterialIconKind.Refresh, "Read the element tree again", RebuildTree));

        SearchBox = new TextBox { Placeholder = "Find element (Enter for the next)", LeadingIconKind = MaterialIconKind.Search, FieldHeight = 36, Margin = new Thickness(12, 4, 12, 8) };
        SearchBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                FindNext(SearchBox.Text);
                e.Handled = true;
            }
        };

        var header = new StackPanel();
        AddAll(header, titleRow, toolbar, SearchBox, _selectionText);

        // Element tree.
        Tree = new TreeView
        {
            ChildrenSelector = item => ((ElementNode)item).Children,
            ItemTemplate = item => NodeRow((ElementNode)item),
            IndentSize = 12,
            Margin = new Thickness(4, 0, 4, 4),
        };
        Tree.SelectionChanged += (_, item) =>
        {
            if (!_isSyncingTree && item is ElementNode node) _session.Select(node.Element);
        };

        // Tabs.
        Properties = new PropertyGrid { Margin = new Thickness(4) };
        ElementInspection.RegisterEditors(Properties);
        _values.Columns.Add(new DataGridTextColumn<PropertyValueRow>("Property", r => r.Name) { Key = "name", Width = GridLength.Pixels(150) });
        _values.Columns.Add(new DataGridTextColumn<PropertyValueRow>("Value", r => r.Value) { Key = "value", Width = GridLength.Stars(1), MinWidth = 100 });
        _values.Columns.Add(new DataGridTextColumn<PropertyValueRow>("Source", r => r.Source) { Key = "source", Width = GridLength.Pixels(90) });
        _values.Columns.Add(new DataGridTextColumn<PropertyValueRow>("Declared by", r => r.DeclaredBy) { Key = "owner", Width = GridLength.Pixels(120), IsVisible = false });
        _values.Columns.Add(new DataGridTextColumn<PropertyValueRow>("Type", r => r.TypeName) { Key = "type", Width = GridLength.Pixels(110), IsVisible = false });
        _values.SortBy(_values.Columns[0], DataGridSortDirection.Ascending);

        Zoom = new PixelZoomView(session);
        Zoom.PixelHovered += (_, pixel) => _pixelText.Text = pixel is { } p ? $"x {p.Pixel.X:0}  y {p.Pixel.Y:0}   {p.Color}" : "Point at a pixel";
        var zoomSlider = new Slider { Minimum = 1, Maximum = 32, Value = Zoom.Zoom, Width = 160, VerticalAlignment = VerticalAlignment.Center };
        zoomSlider.ValueChanged += (_, v) => Zoom.Zoom = v;
        zoomSlider.Margin = new Thickness(4, 0, 12, 0);
        Zoom.ZoomChanged += (_, _) => zoomSlider.Value = Zoom.Zoom;
        var gridSwitch = new CheckBox("Pixel grid") { IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        gridSwitch.CheckedChanged += (_, on) => Zoom.ShowPixelGrid = on == true;
        var boxSwitch = new CheckBox("Box model") { IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        boxSwitch.CheckedChanged += (_, on) => Zoom.ShowBoxModel = on == true;
        var zoomBar = new WrapPanel { Margin = new Thickness(8, 4) };
        AddAll(zoomBar, new Icon(MaterialIconKind.ZoomIn, 20) { VerticalAlignment = VerticalAlignment.Center }, zoomSlider, gridSwitch, boxSwitch, _pixelText);
        var zoomPage = new Grid();
        zoomPage.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        zoomPage.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Place(zoomPage, Zoom, 0, 0);
        Place(zoomPage, zoomBar, 1, 0);

        Tabs = new TabControl { TabStyle = TabStyle.Secondary };
        Tabs.Items.Add(new TabItem("Properties", Properties));
        Tabs.Items.Add(new TabItem("Values", _values));
        Tabs.Items.Add(new TabItem("Layout", new ScrollViewer { Content = _layoutContent }));
        Tabs.Items.Add(new TabItem("Zoom", zoomPage));

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Stars(2)));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Stars(3)));
        Place(root, header, 0, 0);
        Place(root, Tree, 1, 0);
        Place(root, new GridSplitter(), 2, 0);
        Place(root, Tabs, 3, 0);
        Child = root;

        _session.SelectionChanged += (_, _) => OnSelectionChanged();
        _session.ModesChanged += (_, _) => SyncToggles();
        ThemeManager.ThemeChanged += OnThemeChanged;
        ApplyThemeColors();

        // The layout facts change without notifications (sizes): read them twice a second while that tab is shown.
        _layoutTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), (_, _) =>
        {
            if (Tabs.SelectedIndex == 2) UpdateLayoutTab(force: false);
        });
        _layoutTimer.Start();
        OnSelectionChanged();
    }

    #region Parts

    /// <summary>Gets the element tree.</summary>
    public TreeView Tree { get; }

    /// <summary>Gets the search box of the tree.</summary>
    public TextBox SearchBox { get; }

    /// <summary>Gets the property grid of the selected element.</summary>
    public PropertyGrid Properties { get; }

    /// <summary>Gets the table of all values of the selected element.</summary>
    public DataGrid Values => _values;

    /// <summary>Gets the pixel zoom.</summary>
    public PixelZoomView Zoom { get; }

    /// <summary>Gets the tabs (Properties, Values, Layout, Zoom).</summary>
    public TabControl Tabs { get; }

    /// <summary>Gets the tree's root nodes: the window's content, then the open popups.</summary>
    public IReadOnlyList<ElementNode> Roots => _roots;

    private static void AddAll(Panel panel, params UIElement[] children)
    {
        foreach (var child in children) panel.Add(child);
    }

    private static void Place(Grid grid, UIElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        grid.Add(element);
    }

    private static Button IconButton(MaterialIconKind icon, string toolTip, Action action)
    {
        var button = new Button
        {
            Variant = ButtonVariant.Text,
            Content = new Icon(icon, 20),
            Width = 36,
            Height = 36,
            MinWidth = 0,
            MinHeight = 0,
            Padding = Thickness.Zero,
            CornerRadius = new CornerRadius(18),
        };
        ToolTipService.SetToolTip(button, toolTip);
        button.Click += (_, _) => action();
        return button;
    }

    private static ToggleButton Toggle(MaterialIconKind icon, string toolTip, Action<bool> changed)
    {
        var button = new ToggleButton
        {
            Content = new Icon(icon, 20),
            Width = 36,
            Height = 36,
            MinWidth = 0,
            MinHeight = 0,
            Padding = Thickness.Zero,
            CornerRadius = new CornerRadius(18),
        };
        ToolTipService.SetToolTip(button, toolTip);
        button.CheckedChanged += (_, on) => changed(on == true);
        return button;
    }

    private void SyncToggles()
    {
        _pickButton.IsChecked = _session.IsPicking;
        _boxModelButton.IsChecked = _session.ShowBoxModel;
        _boundsButton.IsChecked = _session.ShowAllBounds;
    }

    private void OnThemeChanged(Theme theme) => ApplyThemeColors();

    private void ApplyThemeColors()
    {
        if (ThemeManager.HasTheme && ThemeManager.Current is MaterialTheme material) Background = material.Colors.SurfaceContainerLow;
    }

    #endregion

    #region Tree

    /// <summary>Reads the element tree again (the window's content and its open popups).</summary>
    public void RebuildTree()
    {
        _roots.Clear();
        _roots.Add(new ElementNode(_session.InspectedRoot));
        foreach (var popup in PopupManager.ActivePopups)
        {
            if (popup.IsOpen && popup.Child is { } child) _roots.Add(new ElementNode(child, "Popup: "));
        }
        _isSyncingTree = true;
        try
        {
            Tree.ItemsSource = null;
            Tree.ItemsSource = _roots;
        }
        finally
        {
            _isSyncingTree = false;
        }
        if (_session.SelectedElement is { } selected) Reveal(selected, allowRebuild: false);
    }

    private UIElement NodeRow(ElementNode node)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        bool hidden = node.Element.Visibility != Visibility.Visible;
        row.Add(new TextBlock { Text = node.Prefix + ElementInspection.Describe(node.Element), Muted = hidden, Italic = hidden, VerticalAlignment = VerticalAlignment.Center });
        var size = node.Element.Bounds.Size;
        row.Add(new TextBlock { Text = $"{size.Width:0.#}×{size.Height:0.#}", Muted = true, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });

        // Hovering a row highlights its element in the window.
        row.PointerEntered += (_, _) => _session.Overlay.HoveredElement = node.Element;
        row.PointerExited += (_, _) =>
        {
            if (_session.Overlay.HoveredElement == node.Element) _session.Overlay.HoveredElement = null;
        };
        return row;
    }

    // Expands the tree down to the element and selects its node; reads the tree again if the element is new.
    private void Reveal(UIElement element, bool allowRebuild = true)
    {
        var chain = new List<UIElement>();
        ElementNode? root = null;
        for (VisualNode? node = element; node != null; node = node.Parent)
        {
            if (node is not UIElement ui) continue;
            chain.Add(ui);
            root = _roots.FirstOrDefault(r => r.Element == ui);
            if (root != null) break;
        }
        if (root == null)
        {
            if (allowRebuild)
            {
                RebuildTree();
                Reveal(element, allowRebuild: false);
            }
            return;
        }
        chain.Reverse();

        _isSyncingTree = true;
        try
        {
            var current = root;
            for (int i = 1; i < chain.Count; i++)
            {
                if (FindItem(current) is { } item) item.IsExpanded = true;
                var next = current.Children.FirstOrDefault(c => c.Element == chain[i]);
                if (next == null)
                {
                    current.Reset(); // children changed since they were read
                    next = current.Children.FirstOrDefault(c => c.Element == chain[i]);
                    if (next == null) break;
                    if (FindItem(current) is { } refreshed) refreshed.RebuildChildren();
                }
                current = next;
            }
            Tree.SelectedItem = current;
            if (Tree.SelectedNode is { } selected)
            {
                Tree.ScrollIntoView(selected);
                // Deep nodes are far to the right: show the selected one's start.
                float indent = selected.Level * Tree.IndentSize;
                Tree.ScrollViewer.ScrollOffsetX = Math.Max(0, indent - 24);
            }
        }
        finally
        {
            _isSyncingTree = false;
        }
    }

    private TreeViewItem? FindItem(ElementNode node) => Tree.GetAllNodes().FirstOrDefault(n => ReferenceEquals(n.ItemValue, node));

    private void SelectParent()
    {
        for (VisualNode? node = _session.SelectedElement?.Parent; node != null; node = node.Parent)
        {
            if (node is UIElement parent && _session.IsInspectable(parent))
            {
                _session.Select(parent);
                return;
            }
        }
    }

    /// <summary>Selects the content's focused element (see <see cref="DevToolsSession.FocusedElement"/>); <c>false</c> if there is none.</summary>
    public bool SelectFocused()
    {
        if (_session.FocusedElement is not { } focused) return false;
        _session.Select(focused);
        return true;
    }

    /// <summary>Selects the next element (after the selected one, in tree order) whose description contains <paramref name="text"/>.</summary>
    public bool FindNext(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return false;
        var all = new List<UIElement>();
        foreach (var root in _roots) Collect(root.Element, all);
        int start = _session.SelectedElement is { } selected ? all.IndexOf(selected) + 1 : 0;
        for (int i = 0; i < all.Count; i++)
        {
            var candidate = all[(start + i) % all.Count];
            if (ElementInspection.Describe(candidate).Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                _session.Select(candidate);
                return true;
            }
        }
        return false;

        static void Collect(UIElement element, List<UIElement> list)
        {
            list.Add(element);
            foreach (var child in element.Children)
            {
                if (child is UIElement ui) Collect(ui, list);
            }
        }
    }

    #endregion

    #region Selection

    private void OnSelectionChanged()
    {
        var element = _session.SelectedElement;
        if (_inspection != null) _inspection.PropertyChanged -= OnInspectedPropertyChanged;
        _inspection?.Dispose();
        _inspection = element == null ? null : new ElementInspection(element);
        if (_inspection != null) _inspection.PropertyChanged += OnInspectedPropertyChanged;
        Properties.SelectedObject = _inspection;

        _rows = element == null ? [] : PropertyValueRow.For(element);
        _values.ItemsSource = _rows;

        _selectionText.Text = element == null ? "Nothing selected: pick an element or choose one in the tree." : PathOf(element);
        UpdateLayoutTab(force: true);
        Zoom.InvalidateVisual();
        if (element != null) Reveal(element);
    }

    // Keeps the values table current.
    private void OnInspectedPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        bool changed = false;
        foreach (var row in _rows)
        {
            if (row.Property.Name == e.PropertyName)
            {
                row.Refresh();
                changed = true;
            }
        }
        if (changed) _values.Refresh();
        UpdateLayoutTab(force: false);
    }

    private static string PathOf(UIElement element)
    {
        var names = new List<string>();
        for (VisualNode? node = element; node != null && names.Count < 8; node = node.Parent) names.Add(node.GetType().Name);
        names.Reverse();
        return string.Join(" › ", names);
    }

    #endregion

    #region Layout tab

    // Rebuilds the layout tab when what it shows changed (or when forced).
    private void UpdateLayoutTab(bool force)
    {
        var element = _session.SelectedElement;
        if (element == null)
        {
            if (force || _layoutSignature.Length > 0)
            {
                _layoutSignature = string.Empty;
                _layoutContent.Clear();
                _layoutContent.Add(new TextBlock { Text = "Select an element.", Muted = true });
            }
            return;
        }

        var box = BoxModel.Compute(element, _session.RectOf);
        var facts = LayoutFacts(element, box);
        string signature = string.Join("|", facts.Select(f => f.Value)) + box.MarginThickness + box.BorderThickness + box.PaddingThickness;
        if (!force && signature == _layoutSignature) return;
        _layoutSignature = signature;

        _layoutContent.Clear();
        _layoutContent.Add(BoxDiagram(box));
        var table = new Grid { ColumnSpacing = 12, RowSpacing = 4 };
        table.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        table.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int i = 0; i < facts.Count; i++)
        {
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Place(table, new TextBlock { Text = facts[i].Name, Muted = true }, i, 0);
            Place(table, new TextBlock { Text = facts[i].Value, TextWrapping = TextWrapping.Wrap }, i, 1);
        }
        _layoutContent.Add(table);
    }

    private List<(string Name, string Value)> LayoutFacts(UIElement element, BoxModel box)
    {
        var desired = element.DesiredSize;
        return
        [
            ("Type", element.GetType().FullName ?? element.GetType().Name),
            ("Position", $"{box.Border.X:0.##}, {box.Border.Y:0.##}"),
            ("Size", $"{box.Border.Width:0.##} × {box.Border.Height:0.##}"),
            ("Desired size", $"{desired.Width:0.##} × {desired.Height:0.##}"),
            ("Width / Height", $"{Explicit(element.Width)} / {Explicit(element.Height)}"),
            ("Min / Max", $"{element.MinWidth:0.##} × {element.MinHeight:0.##} / {Explicit(element.MaxWidth)} × {Explicit(element.MaxHeight)}"),
            ("Alignment", $"{element.HorizontalAlignment}, {element.VerticalAlignment}"),
            ("Spacing", box.Spacing ?? "–"),
            ("Visibility", $"{element.Visibility}, opacity {element.Opacity:0.##}"),
            ("State", $"{(element.IsEnabled ? "enabled" : "disabled")}{(element.IsFocused ? ", focused" : "")}{(element.IsFocusable ? ", focusable" : "")}{(element.IsHitTestVisible ? "" : ", not hit-testable")}"),
            ("Clip", element.ClipToBounds ? "clips its children" : "–"),
            ("Layout", $"measure {(element.IsMeasureValid ? "valid" : "pending")}, arrange {(element.IsArrangeValid ? "valid" : "pending")}"),
            ("Children", element.Children.Count.ToString()),
        ];
    }

    private static string Explicit(float value) => float.IsNaN(value) ? "auto" : float.IsPositiveInfinity(value) ? "∞" : $"{value:0.##}";

    // Nested boxes like browser developer tools: margin, border, padding, content, each with its four sides.
    private static UIElement BoxDiagram(BoxModel box)
    {
        var content = new Border
        {
            Background = DevToolsOverlayRenderer.ContentColor,
            Padding = new Thickness(12, 8),
            MinWidth = 90,
            Child = new TextBlock { Text = $"{box.Content.Width:0.##} × {box.Content.Height:0.##}", HorizontalAlignment = HorizontalAlignment.Center },
        };
        var padding = Layer("padding", box.PaddingThickness, DevToolsOverlayRenderer.PaddingColor, content);
        var border = Layer("border", box.BorderThickness, DevToolsOverlayRenderer.BorderColor, padding);
        var margin = Layer("margin", box.MarginThickness, DevToolsOverlayRenderer.MarginColor, border);
        margin.HorizontalAlignment = HorizontalAlignment.Center;
        return margin;
    }

    private static Border Layer(string name, Thickness sides, Color color, UIElement inner)
    {
        static TextBlock Side(float value) => new() { Text = value == 0 ? "–" : $"{value:0.##}", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var grid = new Grid { ColumnSpacing = 6, RowSpacing = 2 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Place(grid, new TextBlock { Text = name, FontSize = 11, Muted = true }, 0, 0);
        Place(grid, Side(sides.Top), 0, 1);
        Place(grid, Side(sides.Left), 1, 0);
        Place(grid, inner, 1, 1);
        Place(grid, Side(sides.Right), 1, 2);
        Place(grid, Side(sides.Bottom), 2, 1);
        return new Border { Background = color, Padding = new Thickness(6, 4), Child = grid };
    }

    #endregion

    internal void OnDetaching()
    {
        _layoutTimer.Stop();
        ThemeManager.ThemeChanged -= OnThemeChanged;
        if (_inspection != null) _inspection.PropertyChanged -= OnInspectedPropertyChanged;
        _inspection?.Dispose();
        _inspection = null;
        Properties.SelectedObject = null;
        _session.Overlay.HoveredElement = null;
    }
}

/// <summary>A node of the tools' element tree: an element and, read when first needed, its child elements.</summary>
public sealed class ElementNode
{
    private List<ElementNode>? _children;

    internal ElementNode(UIElement element, string prefix = "")
    {
        Element = element;
        Prefix = prefix;
    }

    /// <summary>Gets the element.</summary>
    public UIElement Element { get; }

    /// <summary>Gets the text before the description (e.g. "Popup: ").</summary>
    public string Prefix { get; }

    /// <summary>Gets the child elements' nodes.</summary>
    public List<ElementNode> Children => _children ??= Element.Children.OfType<UIElement>().Select(e => new ElementNode(e)).ToList();

    /// <summary>Forgets the children, to read them again.</summary>
    public void Reset() => _children = null;

    /// <inheritdoc/>
    public override string ToString() => Prefix + ElementInspection.Describe(Element);
}
