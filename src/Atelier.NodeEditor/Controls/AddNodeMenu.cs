using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Nodes;

/// <summary>
/// A searchable menu of node types (like Blender's Shift+A search): with an empty search it lists the catalog by
/// category; typing lists the best matches (see <see cref="NodeCatalog.Search"/>). Up and Down move the highlight,
/// Enter or a click picks a type, Escape or a click outside closes the menu.
/// </summary>
/// <remarks>
/// <see cref="NodeEditor.ShowAddNodeMenu"/> opens it at the pointer and adds the picked node there. It can be limited to
/// types that connect to a dragged socket (see <see cref="NodeCatalog.Search"/>).
/// </remarks>
public class AddNodeMenu : ContentControl
{
    /// <summary>The style key of the popup the menu opens in.</summary>
    public const string PopupStyleKey = "AddNodeMenuPopup";

    /// <summary>The style key of the category headers (<see cref="TextBlock"/>s).</summary>
    public const string CategoryStyleKey = "AddNodeMenuCategory";

    private const float MenuWidth = 280;
    private const float MenuHeight = 380;

    private readonly NodeCatalog _catalog;
    private readonly SocketType? _acceptingOutput;
    private readonly SocketType? _feedingInput;
    private readonly TextBox _search;
    private readonly StackPanel _list = new() { Spacing = 1 };
    private readonly TextBlock _empty = new("No matching nodes") { Muted = true, Margin = new Thickness(12, 8), Visibility = Visibility.Collapsed };
    private readonly List<(NodeType Type, Button Row)> _rows = [];
    private int _highlight = -1;
    private Popup? _popup;

    /// <summary>Initializes a menu of <paramref name="catalog"/>'s types, optionally only those that connect to a dragged socket's type.</summary>
    /// <param name="catalog">The node types.</param>
    /// <param name="acceptingOutput">Only types with an input that accepts an output of this type.</param>
    /// <param name="feedingInput">Only types with an output that an input of this type accepts.</param>
    public AddNodeMenu(NodeCatalog catalog, SocketType? acceptingOutput = null, SocketType? feedingInput = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        _acceptingOutput = acceptingOutput;
        _feedingInput = feedingInput;

        _search = new TextBox { Placeholder = "Search nodes", LeadingIconKind = MaterialIconKind.Search, Margin = new Thickness(8, 8, 8, 4), MinWidth = 0 };
        _search.TextChanged += (_, _) => UpdateResults();
        var content = new StackPanel();
        content.Add(_list);
        content.Add(_empty);
        var scroller = new ScrollViewer { Content = content, Margin = new Thickness(4, 0, 4, 6) };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        layout.Add(_search);
        Grid.SetRow(scroller, 1);
        layout.Add(scroller);
        Content = new Border { Width = MenuWidth, Height = MenuHeight, Child = layout };
        UpdateResults();
    }

    /// <summary>Gets the types listed for the current search, in order.</summary>
    public IReadOnlyList<NodeType> Results => _rows.Select(r => r.Type).ToList();

    /// <summary>Gets the highlighted type, which Enter picks; <c>null</c> if nothing is listed.</summary>
    public NodeType? HighlightedType => _highlight >= 0 && _highlight < _rows.Count ? _rows[_highlight].Type : null;

    /// <summary>Gets or sets the search text.</summary>
    public string SearchText { get => _search.Text; set => _search.Text = value; }

    /// <summary>Gets the search field.</summary>
    public TextBox SearchBox => _search;

    /// <summary>Occurs when a type was picked (the menu has closed by then).</summary>
    public event EventHandler<NodeType>? TypePicked;

    /// <summary>Occurs when the menu closed, picked or not.</summary>
    public event EventHandler? Closed;

    /// <summary>Opens the menu in a popup at the pointer over <paramref name="target"/>, with the focus in its search field.</summary>
    public void Show(UIElement target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var popup = new Popup
        {
            StyleKey = PopupStyleKey,
            Child = this,
            PlacementTarget = target,
            Placement = PlacementMode.Pointer,
        };
        popup.Closed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
        _popup = popup;
        popup.IsOpen = true;
        _search.Focus();
    }

    /// <summary>Closes the menu.</summary>
    public void Close()
    {
        if (_popup != null) _popup.IsOpen = false;
    }

    /// <summary>Picks <paramref name="type"/>: closes the menu and raises <see cref="TypePicked"/>.</summary>
    public void Pick(NodeType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Close();
        TypePicked?.Invoke(this, type);
    }

    /// <summary>Moves the highlight by <paramref name="delta"/> rows.</summary>
    public void MoveHighlight(int delta)
    {
        if (_rows.Count == 0) return;
        SetHighlight(Math.Clamp(_highlight + delta, 0, _rows.Count - 1));
    }

    /// <inheritdoc/>
    /// <remarks>Up, Down, Page Up, Page Down, Enter and Escape work while typing in the search field.</remarks>
    public override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || e.Modifiers != ModifierKeys.None) return;
        switch (e.Key)
        {
            case Key.Down: MoveHighlight(1); break;
            case Key.Up: MoveHighlight(-1); break;
            case Key.PageDown: MoveHighlight(8); break;
            case Key.PageUp: MoveHighlight(-8); break;
            case Key.Enter:
                if (HighlightedType is { } type) Pick(type);
                break;
            case Key.Escape: Close(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void UpdateResults()
    {
        string query = (_search.Text ?? string.Empty).Trim();
        var types = _catalog.Search(query, _acceptingOutput, _feedingInput);
        _list.Clear();
        _rows.Clear();
        if (query.Length == 0)
        {
            // By category, in the catalog's order.
            foreach (var category in types.Select(t => t.Category).Distinct())
            {
                if (category.Length > 0) _list.Add(new TextBlock(category) { StyleKey = CategoryStyleKey, Margin = new Thickness(12, 8, 12, 2) });
                foreach (var type in types.Where(t => t.Category == category)) AddRow(type);
            }
        }
        else
        {
            foreach (var type in types) AddRow(type);
        }
        _empty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _highlight = -1;
        SetHighlight(_rows.Count > 0 ? 0 : -1);
    }

    private void AddRow(NodeType type)
    {
        var swatch = new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(3),
            Background = type.HeaderColor ?? Color.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        content.Add(swatch);
        content.Add(new TextBlock(type.Title) { VerticalAlignment = VerticalAlignment.Center });
        var row = new Button
        {
            Variant = ButtonVariant.Text,
            Content = content,
            Padding = new Thickness(12, 4),
            MinHeight = 32,
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            IsFocusable = false, // the focus stays in the search field
        };
        if (type.Description is { Length: > 0 } description) ToolTipService.SetToolTip(row, description);
        row.Click += (_, _) => Pick(type);
        _rows.Add((type, row));
        _list.Add(row);
    }

    private void SetHighlight(int index)
    {
        if (_highlight >= 0 && _highlight < _rows.Count) _rows[_highlight].Row.Variant = ButtonVariant.Text;
        _highlight = index;
        if (_highlight >= 0 && _highlight < _rows.Count)
        {
            _rows[_highlight].Row.Variant = ButtonVariant.Tonal;
            _rows[_highlight].Row.BringIntoView();
        }
    }
}
