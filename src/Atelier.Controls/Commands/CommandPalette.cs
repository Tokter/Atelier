using System;
using System.Collections.Generic;
using System.Linq;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// A search box for running commands: it lists the commands whose shortcuts work where the focus was when it opened
/// (see <see cref="KeybindingHandler.GetActiveCommands"/>), narrows them as the user types, and runs the chosen one on
/// its target, with the focus back where it was.
/// </summary>
/// <remarks>
/// <para>
/// Open it with <see cref="Show"/>, typically from a global command with the shortcut Ctrl+Shift+P. Up and Down move
/// the highlight, Enter runs the highlighted command, Escape or a click outside closes the palette; a click on a row
/// runs it.
/// </para>
/// <para>
/// Rows show the command's icon, label, group, description and shortcut. Commands that can't run now are shown
/// disabled; a shortcut an inner command also uses says which command it runs here. With an empty search, the recently
/// run commands come first; typing ranks label prefixes, word starts and initials ("tt" finds "Toggle theme") above
/// other matches, then matches in the name, group or description.
/// </para>
/// </remarks>
public class CommandPalette : ContentControl
{
    /// <summary>The style key of the popup the palette opens in (see <see cref="Show"/>).</summary>
    public const string PopupStyleKey = "CommandPalettePopup";

    /// <summary>The style key of the command rows (<see cref="Button"/>s).</summary>
    public const string RowStyleKey = "CommandPaletteRow";

    /// <summary>The style key of the highlighted row.</summary>
    public const string HighlightedRowStyleKey = "CommandPaletteHighlightedRow";

    /// <summary>The most rows listed at once; typing narrows the rest.</summary>
    public const int MaxRows = 60;

    private const float PaletteWidth = 640;
    private const float PaletteHeight = 460;
    private const int MaxRecent = 8;

    // Recently run commands, most recent first (for all palettes of the application).
    private static readonly List<(string Group, string Name)> s_recent = [];

    private readonly IReadOnlyList<ActiveCommand> _commands;
    private readonly Dictionary<ActiveCommand, int> _baseOrder = [];
    private readonly TextBox _search;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly ScrollViewer _scroller;
    private readonly TextBlock _empty = new("No matching commands") { Muted = true, Margin = new Thickness(16, 8), Visibility = Visibility.Collapsed };
    private readonly List<(ActiveCommand Command, Button Row)> _rows = [];
    private int _highlight = -1;
    private Popup? _popup;

    /// <summary>Initializes a palette of <paramref name="commands"/> (usually from <see cref="KeybindingHandler.GetActiveCommands"/>).</summary>
    public CommandPalette(IReadOnlyList<ActiveCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _commands = commands;

        // Without a search: groups in the order found (innermost first), each sorted by label.
        var groupOrder = new List<string>();
        foreach (var command in commands)
        {
            if (!groupOrder.Contains(command.Descriptor.Group)) groupOrder.Add(command.Descriptor.Group);
        }
        int index = 0;
        foreach (var command in commands.OrderBy(c => groupOrder.IndexOf(c.Descriptor.Group)).ThenBy(c => LabelOf(c), StringComparer.CurrentCultureIgnoreCase))
        {
            _baseOrder[command] = index++;
        }

        _search = new TextBox { Placeholder = "Type a command", LeadingIconKind = MaterialIconKind.Search, Margin = new Thickness(12, 12, 12, 4) };
        _search.TextChanged += (_, _) => UpdateResults();

        var content = new StackPanel();
        content.Add(_list);
        content.Add(_empty);
        _scroller = new ScrollViewer { Content = content, Margin = new Thickness(8, 0, 8, 0) };

        var hint = new TextBlock("Up and Down to choose · Enter to run · Esc to close") { Muted = true, FontSize = 12, Margin = new Thickness(16, 6, 16, 10) };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.Add(_search);
        Grid.SetRow(_scroller, 1);
        layout.Add(_scroller);
        Grid.SetRow(hint, 2);
        layout.Add(hint);

        Content = new Border
        {
            Width = PaletteWidth,
            Height = PaletteHeight,
            Child = layout,
        };
        UpdateResults();
    }

    /// <summary>Gets the commands the palette offers.</summary>
    public IReadOnlyList<ActiveCommand> Commands => _commands;

    /// <summary>Gets the commands listed for the current search, best match first.</summary>
    public IReadOnlyList<ActiveCommand> Results => _rows.Select(r => r.Command).ToList();

    /// <summary>Gets the highlighted command, which Enter runs; <c>null</c> if nothing is listed that can run.</summary>
    public ActiveCommand? HighlightedCommand => _highlight >= 0 && _highlight < _rows.Count ? _rows[_highlight].Command : null;

    /// <summary>Gets or sets the search text.</summary>
    public string SearchText { get => _search.Text; set => _search.Text = value; }

    /// <summary>Gets the search field.</summary>
    public TextBox SearchBox => _search;

    /// <summary>Occurs after a command was run from the palette (the palette has closed by then).</summary>
    public event EventHandler<ActiveCommand>? CommandExecuted;

    /// <summary>
    /// Opens a palette of the commands that work where the focus is in the window of <paramref name="element"/>: in a
    /// popup near the top of the window, with the focus in its search field. Drag commands (<see cref="IDragCommand"/>)
    /// are left out, as they only run from the pointer.
    /// </summary>
    /// <param name="element">Any element of the window.</param>
    /// <param name="filter">Leaves out commands it returns <c>false</c> for, e.g. the command that opens the palette.</param>
    /// <returns>The palette.</returns>
    public static CommandPalette Show(UIElement element, Func<ActiveCommand, bool>? filter = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        var root = WindowRoot(element);
        var focused = FocusManager.GetFocusedElement(root) ?? root;
        // Drag commands only run from the pointer.
        var commands = KeybindingHandler.GetActiveCommands(focused).Where(c => c.Descriptor.Command is not IDragCommand).ToList();
        if (filter != null) commands = commands.Where(filter).ToList();

        var palette = new CommandPalette(commands);
        var popup = new Popup
        {
            StyleKey = PopupStyleKey,
            Child = palette,
            PlacementTarget = root,
            Placement = PlacementMode.Center,
            // Near the top of the window rather than in its middle.
            VerticalOffset = Math.Min(0, PaletteHeight / 2 + 56 - root.Bounds.Height / 2),
        };
        palette._popup = popup;
        popup.IsOpen = true;
        palette._search.Focus();
        return palette;
    }

    /// <summary>Closes the palette; the focus returns to where it was.</summary>
    public void Close()
    {
        if (_popup != null) _popup.IsOpen = false;
    }

    /// <summary>Moves the highlight by <paramref name="delta"/> rows, skipping commands that can't run.</summary>
    public void MoveHighlight(int delta)
    {
        if (_rows.Count == 0 || delta == 0) return;
        int step = Math.Sign(delta);
        int index = _highlight;
        int remaining = Math.Abs(delta);
        int best = _highlight;
        for (int i = 0; i < _rows.Count * 2 && remaining > 0; i++)
        {
            index = Math.Clamp(index + step, 0, _rows.Count - 1);
            if (_rows[index].Command.CanExecute)
            {
                best = index;
                remaining--;
            }
            if (index == 0 || index == _rows.Count - 1) break;
        }
        SetHighlight(best);
    }

    /// <summary>Runs the highlighted command (closing the palette first); <c>false</c> if there is none that can run.</summary>
    public bool ExecuteHighlighted() => HighlightedCommand is { } command && Execute(command);

    /// <summary>Runs <paramref name="command"/>: closes the palette, so the focus is back where it was, then runs it.</summary>
    /// <returns><c>false</c> if the command can't run now.</returns>
    public bool Execute(ActiveCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.CanExecute) return false;
        Close();
        Remember(command);
        command.TryExecute();
        CommandExecuted?.Invoke(this, command);
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>Up, Down, Page Up, Page Down and Enter work while typing in the search field.</remarks>
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
            case Key.Enter: ExecuteHighlighted(); break;
            default: return;
        }
        e.Handled = true;
    }

    /// <summary>
    /// Scores how well <paramref name="command"/> matches <paramref name="query"/> (lower-case, trimmed): 0 or less
    /// for no match; label prefixes score highest, then word starts and initials, substrings, the label's letters in
    /// order, and matches in the name, group or description.
    /// </summary>
    public static int Score(string query, ActiveCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrEmpty(query)) return 1;
        string label = LabelOf(command).ToLowerInvariant();
        if (label.StartsWith(query, StringComparison.Ordinal)) return 1000;

        var words = label.Split([' ', '-', '_', '.', '…'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w.StartsWith(query, StringComparison.Ordinal))) return 800;
        if (new string(words.Select(w => w[0]).ToArray()).StartsWith(query, StringComparison.Ordinal)) return 700;
        if (label.Contains(query, StringComparison.Ordinal)) return 600;

        // The query's letters in order in the label: the closer together, the better.
        int position = -1, start = -1;
        foreach (char c in query)
        {
            position = label.IndexOf(c, position + 1);
            if (position < 0) break;
            if (start < 0) start = position;
        }
        if (position >= 0) return Math.Max(401, 500 - (position - start + 1 - query.Length));

        var descriptor = command.Descriptor;
        if (descriptor.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || descriptor.Group.Contains(query, StringComparison.OrdinalIgnoreCase)
            || KeybindingDescriptor.LabelFromName(descriptor.Group).Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 300;
        }
        return descriptor.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ? 200 : 0;
    }

    private static string LabelOf(ActiveCommand command) => AccessText.Parse(command.Descriptor.Label, out _);

    private static UIElement WindowRoot(UIElement element)
    {
        VisualNode node = element;
        while ((node.Parent ?? FocusManager.GetTreeOwner(node)) is { } up) node = up;
        return node as UIElement ?? element;
    }

    private static void Remember(ActiveCommand command)
    {
        var key = (command.Descriptor.Group, command.Descriptor.Name);
        s_recent.Remove(key);
        s_recent.Insert(0, key);
        if (s_recent.Count > MaxRecent) s_recent.RemoveAt(s_recent.Count - 1);
    }

    private void UpdateResults()
    {
        string query = (_search.Text ?? string.Empty).Trim().ToLowerInvariant();
        IEnumerable<ActiveCommand> results;
        if (query.Length == 0)
        {
            results = _commands
                .OrderBy(c => s_recent.IndexOf((c.Descriptor.Group, c.Descriptor.Name)) is var recent and >= 0 ? recent : MaxRecent)
                .ThenBy(c => _baseOrder[c]);
        }
        else
        {
            results = _commands
                .Select(c => (Command: c, Score: Score(query, c)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => _baseOrder[x.Command])
                .Select(x => x.Command);
        }

        _list.Clear();
        _rows.Clear();
        foreach (var command in results.Take(MaxRows))
        {
            var row = CreateRow(command);
            _rows.Add((command, row));
            _list.Add(row);
        }
        _empty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _highlight = -1;
        SetHighlight(_rows.FindIndex(r => r.Command.CanExecute));
    }

    private void SetHighlight(int index)
    {
        if (_highlight >= 0 && _highlight < _rows.Count) Style(_rows[_highlight].Row, highlighted: false);
        _highlight = index;
        if (_highlight >= 0 && _highlight < _rows.Count)
        {
            Style(_rows[_highlight].Row, highlighted: true);
            _rows[_highlight].Row.BringIntoView();
        }

        static void Style(Button row, bool highlighted)
        {
            row.StyleKey = highlighted ? HighlightedRowStyleKey : RowStyleKey;
            row.Variant = highlighted ? ButtonVariant.Tonal : ButtonVariant.Text;
        }
    }

    private Button CreateRow(ActiveCommand command)
    {
        var descriptor = command.Descriptor;
        var icon = Icon.FromSource(descriptor.Icon, 20);
        icon.VerticalAlignment = VerticalAlignment.Center;

        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        title.Add(new TextBlock(LabelOf(command)) { Bold = true, VerticalAlignment = VerticalAlignment.Center });
        title.Add(new TextBlock(KeybindingDescriptor.LabelFromName(descriptor.Group)) { Muted = true, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        var texts = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        texts.Add(title);
        if (descriptor.Description.Length > 0)
        {
            texts.Add(new TextBlock(descriptor.Description) { Muted = true, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
        }

        UIElement shortcut = command.ShadowedBy is { } other
            ? new TextBlock($"{KeybindingGesture.FormatForDisplay(descriptor.Keybinding)} runs \"{LabelOf(other)}\" here")
            {
                Muted = true,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            }
            : new ShortcutView(descriptor.Keybinding);

        var content = new Grid { ColumnSpacing = 12 };
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        content.Add(icon);
        Grid.SetColumn(texts, 1);
        content.Add(texts);
        Grid.SetColumn(shortcut, 2);
        content.Add(shortcut);

        var row = new Button
        {
            StyleKey = RowStyleKey,
            Variant = ButtonVariant.Text,
            Content = content,
            Padding = new Thickness(12, 6),
            MinHeight = 44,
            CornerRadius = new CornerRadius(12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsFocusable = false, // the focus stays in the search field
            IsEnabled = command.CanExecute,
        };
        row.Click += (_, _) => Execute(command);
        return row;
    }
}
