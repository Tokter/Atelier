using System;
using System.Collections.Generic;
using System.Linq;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Threading;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// Lets users see and change the application's commands: a list of the command groups (and of all and of the changed
/// commands) on the left, the commands of the chosen one with a search, and the label, icon and keyboard shortcut of the
/// selected command (see <see cref="CommandAttribute"/>) on the right. Changes are applied with
/// <see cref="KeybindingManager.SetCustomization"/>, so buttons, menus, tooltips and the shortcuts themselves follow
/// immediately; save them with <see cref="KeybindingManager.ExportCustomizations"/>.
/// </summary>
/// <remarks>
/// <para>
/// Shortcuts are recorded with a <see cref="ShortcutRecorder"/>. A shortcut another command of the same group already
/// uses (or that starts one of its chords) is shown as a conflict; it is still applied, so the user can resolve it.
/// </para>
/// <para>
/// The parts are styled with the style keys defined here (group headers, command names, conflicts, the changed mark),
/// by the theme.
/// </para>
/// </remarks>
public class KeybindingEditor : ContentControl
{
    /// <summary>The style key of the group headers (<see cref="TextBlock"/>s).</summary>
    public const string GroupHeaderStyleKey = "KeybindingEditorGroupHeader";

    /// <summary>The style key of the selected command's title (a <see cref="TextBlock"/>).</summary>
    public const string TitleStyleKey = "KeybindingEditorTitle";

    /// <summary>The style key of conflict and error messages (<see cref="TextBlock"/>s).</summary>
    public const string ErrorTextStyleKey = "KeybindingEditorError";

    /// <summary>The style key of the dot marking changed commands (a <see cref="Border"/>).</summary>
    public const string ChangedMarkStyleKey = "KeybindingEditorChangedMark";

    /// <summary>The style key of the command rows (<see cref="Button"/>s) when not selected.</summary>
    public const string RowStyleKey = "KeybindingEditorRow";

    /// <summary>The style key of the selected command row.</summary>
    public const string SelectedRowStyleKey = "KeybindingEditorSelectedRow";

    /// <summary>Identifies the <see cref="SearchText"/> property.</summary>
    public static readonly BindableProperty<string> SearchTextProperty =
        BindableProperty.Register<KeybindingEditor, string>(nameof(SearchText), string.Empty, (s, o, n) => ((KeybindingEditor)s).OnSearchTextChanged(n));

    private readonly TextBox _search;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly Dictionary<(string Group, string Name), Row> _rows = [];
    private readonly List<(TextBlock Header, List<Row> Rows)> _groups = [];

    // The group list on the left: all commands, the changed ones, then each group.
    private readonly StackPanel _groupList = new() { Spacing = 2 };
    private readonly List<GroupEntry> _groupEntries = [];
    private string? _groupFilter;
    private bool _changedOnly;

    // The details of the selected command.
    private readonly StackPanel _details = new() { Spacing = 16 };
    private readonly TextBlock _noSelection = new("Select a command to change its label, icon or shortcut.") { Muted = true, TextWrapping = TextWrapping.Wrap };
    private readonly Icon _titleIcon = new() { Size = 32, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _title = new() { StyleKey = TitleStyleKey, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _identity = new() { Muted = true };
    private readonly TextBlock _description = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _labelBox = new() { Label = "Label", SupportingText = "An underscore marks the access key in menus, e.g. \"_Save\"." };
    private readonly TextBox _iconBox = new() { Label = "Icon", SupportingText = "An icon name such as \"DarkMode\", or SVG." };
    private readonly Icon _iconPreview = new() { Size = 24, VerticalAlignment = VerticalAlignment.Center };
    private readonly ShortcutRecorder _recorder = new();
    private readonly Button _removeShortcut = new() { Content = "Remove", Variant = ButtonVariant.Text, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _defaultShortcut = new() { Muted = true, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _conflicts = new() { StyleKey = ErrorTextStyleKey, TextWrapping = TextWrapping.Wrap };
    private readonly Button _resetCommand = new() { Content = "Reset to default", Variant = ButtonVariant.Outlined, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button _resetAll = new() { Content = "Reset all", Variant = ButtonVariant.Text, VerticalAlignment = VerticalAlignment.Center };

    private (string Group, string Name)? _selected;
    private bool _isApplying;
    private bool _scrollToSelection;
    private bool _isObserving;
    private Action? _refresh;

    /// <summary>Initializes an editor of the registered commands.</summary>
    public KeybindingEditor()
    {
        _search = new TextBox { Placeholder = "Search commands", LeadingIconKind = MaterialIconKind.Search, VerticalAlignment = VerticalAlignment.Center };
        _search.TextChanged += (_, text) => SearchText = text;
        _resetAll.Click += (_, _) => KeybindingManager.ClearCustomizations();

        var toolbar = new Grid { ColumnSpacing = 12 };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        toolbar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        toolbar.Add(_search);
        Grid.SetColumn(_resetAll, 1);
        toolbar.Add(_resetAll);

        BuildDetails();
        var groupScroller = new ScrollViewer { Content = _groupList, Margin = new Thickness(0, 0, 4, 0) };
        var listScroller = new ScrollViewer { Content = _list, Margin = new Thickness(0, 0, 4, 0) };
        var detailsScroller = new ScrollViewer { Content = _details };

        var body = new Grid { ColumnSpacing = 16 };
        body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Pixels(180)));
        body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Pixels(320)));
        body.Add(groupScroller);
        Grid.SetColumn(listScroller, 1);
        body.Add(listScroller);
        Grid.SetColumn(detailsScroller, 2);
        body.Add(detailsScroller);

        var root = new Grid { RowSpacing = 16 };
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        root.Add(toolbar);
        Grid.SetRow(body, 1);
        root.Add(body);
        Content = root;

        Rebuild();
    }

    /// <summary>
    /// Gets or sets the search: only commands whose label, name, group, description or shortcut contain it are listed.
    /// </summary>
    public string SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <summary>Gets the selected command (with the user's changes), or <c>null</c>.</summary>
    public IKeybindingDescriptor? SelectedCommand => _selected is { } key ? KeybindingManager.FindCommand(key.Group, key.Name) : null;

    /// <summary>Gets the group whose commands are listed, or <c>null</c> when all groups (or the changed commands) are listed.</summary>
    public string? SelectedGroup => _groupFilter;

    /// <summary>Gets whether only the commands the user changed are listed (see <see cref="ShowChangedCommands"/>).</summary>
    public bool ShowsChangedCommandsOnly => _changedOnly;

    /// <summary>Lists the commands of <paramref name="group"/> only, or of all groups for <c>null</c>.</summary>
    public void ShowGroup(string? group)
    {
        _groupFilter = group;
        _changedOnly = false;
        ApplyFilter();
    }

    /// <summary>Lists only the commands the user changed (of all groups).</summary>
    public void ShowChangedCommands()
    {
        _groupFilter = null;
        _changedOnly = true;
        ApplyFilter();
    }

    /// <summary>
    /// Selects the command <paramref name="name"/> of <paramref name="group"/>, listing its group if the current list
    /// doesn't include it; <c>false</c> if it isn't registered.
    /// </summary>
    public bool Select(string group, string name)
    {
        if (!_rows.ContainsKey((group, name))) return false;
        if ((_groupFilter != null && _groupFilter != group) || (_changedOnly && KeybindingManager.GetCustomization(group, name) == null))
        {
            ShowGroup(group);
        }
        _selected = (group, name);
        _scrollToSelection = true;
        InvalidateArrange();
        UpdateRows();
        UpdateDetails();
        return true;
    }

    /// <summary>Gets the rows (command buttons) currently listed, in order, for tests and automation.</summary>
    public IEnumerable<Button> VisibleRows => _groups.SelectMany(g => g.Rows).Where(r => r.Button.Visibility == Visibility.Visible).Select(r => r.Button);

    /// <summary>Gets the field that records the selected command's shortcut.</summary>
    public ShortcutRecorder ShortcutRecorder => _recorder;

    /// <summary>Gets the field with the selected command's label.</summary>
    public TextBox LabelBox => _labelBox;

    /// <summary>Gets the field with the selected command's icon.</summary>
    public TextBox IconBox => _iconBox;

    /// <summary>Gets the conflicts of the selected command's shortcut, as shown (empty if there are none).</summary>
    public string ConflictText => _conflicts.Text;

    /// <inheritdoc/>
    /// <remarks>Scrolls a command selected with <see cref="Select"/> into view once its row is laid out.</remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (_scrollToSelection && _selected is { } key && _rows.TryGetValue(key, out var row) && row.Button.Bounds.Height > 0)
        {
            _scrollToSelection = false;
            row.Button.BringIntoView();
        }
        return size;
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        if (!_isObserving)
        {
            KeybindingManager.KeybindingsChanged += OnKeybindingsChanged;
            _isObserving = true;
        }
        Rebuild(); // registrations may have changed while the editor wasn't shown
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree()
    {
        if (_isObserving)
        {
            KeybindingManager.KeybindingsChanged -= OnKeybindingsChanged;
            _isObserving = false;
        }
        base.OnDetachedFromVisualTree();
    }

    private void OnKeybindingsChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess()) Refresh();
        else Dispatcher.Post(_refresh ??= Refresh);
    }

    // Registrations added or removed rebuild the list; otherwise the rows and details are updated in place, so a field
    // being edited keeps its focus and caret.
    private void Refresh()
    {
        var keys = KeybindingManager.GetKeybindings().Select(d => (d.Group, d.Name)).ToHashSet();
        if (!keys.SetEquals(_rows.Keys))
        {
            Rebuild();
            return;
        }
        UpdateRows();
        UpdateDetails();
    }

    #region List

    private sealed class Row
    {
        public required (string Group, string Name) Key { get; init; }
        public required Button Button { get; init; }
        public required Icon Icon { get; init; }
        public required TextBlock Label { get; init; }
        public required ShortcutView Shortcut { get; init; }
        public required Border ChangedMark { get; init; }
        public string SearchText { get; set; } = string.Empty;
    }

    private void Rebuild()
    {
        _list.Clear();
        _rows.Clear();
        _groups.Clear();

        var commands = KeybindingManager.GetKeybindings()
            .OrderBy(d => d.Group == "Global" ? 0 : 1)
            .ThenBy(d => d.Group, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(d => AccessText.Parse(d.Label, out _), StringComparer.CurrentCultureIgnoreCase);

        foreach (var group in commands.GroupBy(d => d.Group))
        {
            var header = new TextBlock(GroupTitle(group.Key)) { StyleKey = GroupHeaderStyleKey, Margin = new Thickness(12, _groups.Count == 0 ? 0 : 16, 0, 4) };
            _list.Add(header);
            var rows = new List<Row>();
            foreach (var command in group)
            {
                var row = CreateRow(command);
                _rows[row.Key] = row;
                rows.Add(row);
                _list.Add(row.Button);
            }
            _groups.Add((header, rows));
        }

        if (_selected is { } selected && !_rows.ContainsKey(selected)) _selected = null;
        if (_groupFilter != null && !_groups.Any(g => g.Rows[0].Key.Group == _groupFilter)) _groupFilter = null;
        RebuildGroupList();
        UpdateRows();
        ApplyFilter();
        UpdateDetails();
    }

    // A group name for people: "SelectionControls" becomes "Selection controls".
    private static string GroupTitle(string group) => KeybindingDescriptor.LabelFromName(group);

    private sealed class GroupEntry
    {
        public required string? Group { get; init; }
        public required bool ChangedOnly { get; init; }
        public required Button Button { get; init; }
        public required TextBlock Count { get; init; }
    }

    private void RebuildGroupList()
    {
        _groupList.Clear();
        _groupEntries.Clear();
        AddGroupEntry("All commands", null, changedOnly: false);
        AddGroupEntry("Changed", null, changedOnly: true);
        _groupList.Add(new Border { Height = 12 });
        foreach (var (_, rows) in _groups) AddGroupEntry(GroupTitle(rows[0].Key.Group), rows[0].Key.Group, changedOnly: false);
    }

    private void AddGroupEntry(string title, string? group, bool changedOnly)
    {
        var label = new TextBlock(title) { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var count = new TextBlock { Muted = true, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var content = new Grid { ColumnSpacing = 8 };
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        content.Add(label);
        Grid.SetColumn(count, 1);
        content.Add(count);

        var button = new Button
        {
            StyleKey = RowStyleKey,
            Variant = ButtonVariant.Text,
            Content = content,
            Padding = new Thickness(12, 4),
            MinHeight = 36,
            CornerRadius = new CornerRadius(18),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        button.Click += (_, _) =>
        {
            if (changedOnly) ShowChangedCommands();
            else ShowGroup(group);
        };
        _groupEntries.Add(new GroupEntry { Group = group, ChangedOnly = changedOnly, Button = button, Count = count });
        _groupList.Add(button);
    }

    private Row CreateRow(IKeybindingDescriptor command)
    {
        var icon = new Icon { Size = 20, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var name = new TextBlock(command.Name) { Muted = true, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Add(label);
        texts.Add(name);
        var changed = new Border { StyleKey = ChangedMarkStyleKey, Width = 8, Height = 8, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center };
        var shortcut = new ShortcutView();

        var content = new Grid { ColumnSpacing = 12 };
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        content.Add(icon);
        Grid.SetColumn(texts, 1);
        content.Add(texts);
        Grid.SetColumn(changed, 2);
        content.Add(changed);
        Grid.SetColumn(shortcut, 3);
        content.Add(shortcut);

        var key = (command.Group, command.Name);
        var button = new Button
        {
            StyleKey = RowStyleKey,
            Variant = ButtonVariant.Text,
            Content = content,
            Padding = new Thickness(12, 6),
            MinHeight = 48,
            CornerRadius = new CornerRadius(12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        button.Click += (_, _) => Select(key.Group, key.Name);
        return new Row { Key = key, Button = button, Icon = icon, Label = label, Shortcut = shortcut, ChangedMark = changed };
    }

    private void UpdateRows()
    {
        foreach (var row in _rows.Values)
        {
            if (KeybindingManager.FindCommand(row.Key.Group, row.Key.Name) is not { } command) continue;
            string label = AccessText.Parse(command.Label, out _);
            if (row.Label.Text != label) row.Label.Text = label;
            if (row.Icon.Source != command.Icon) row.Icon.Source = command.Icon;
            if (row.Shortcut.Shortcut != command.Keybinding) row.Shortcut.Shortcut = command.Keybinding;
            row.ChangedMark.Visibility = KeybindingManager.GetCustomization(row.Key.Group, row.Key.Name) != null ? Visibility.Visible : Visibility.Hidden;
            bool selected = _selected == row.Key;
            row.Button.StyleKey = selected ? SelectedRowStyleKey : RowStyleKey;
            row.Button.Variant = selected ? ButtonVariant.Tonal : ButtonVariant.Text;
            row.SearchText = string.Join("\n", label, command.Name, command.Group, command.Description,
                KeybindingGesture.FormatForDisplay(command.Keybinding));
        }
        ApplyFilter();
    }

    private void OnSearchTextChanged(string text)
    {
        if (_search != null && _search.Text != text) _search.Text = text;
        ApplyFilter();
    }

    // Lists the commands of the chosen group (or all, or the changed ones) that match the search; the group list shows
    // how many commands of each entry match.
    private void ApplyFilter()
    {
        string search = SearchText?.Trim() ?? string.Empty;
        int all = 0, changed = 0;
        var perGroup = new Dictionary<string, int>();
        foreach (var (header, rows) in _groups)
        {
            bool any = false;
            int matches = 0;
            foreach (var row in rows)
            {
                bool match = search.Length == 0 || row.SearchText.Contains(search, StringComparison.CurrentCultureIgnoreCase);
                bool isChanged = KeybindingManager.GetCustomization(row.Key.Group, row.Key.Name) != null;
                if (match)
                {
                    matches++;
                    if (isChanged) changed++;
                }
                bool listed = match && (!_changedOnly || isChanged) && (_groupFilter == null || _groupFilter == row.Key.Group);
                row.Button.Visibility = listed ? Visibility.Visible : Visibility.Collapsed;
                any |= listed;
            }
            all += matches;
            perGroup[rows[0].Key.Group] = matches;
            // One group needs no header; the group list shows which one it is.
            header.Visibility = any && _groupFilter == null ? Visibility.Visible : Visibility.Collapsed;
        }

        foreach (var entry in _groupEntries)
        {
            int count = entry.ChangedOnly ? changed : entry.Group == null ? all : perGroup.GetValueOrDefault(entry.Group);
            entry.Count.Text = count.ToString();
            bool selected = entry.ChangedOnly ? _changedOnly : !_changedOnly && entry.Group == _groupFilter;
            entry.Button.StyleKey = selected ? SelectedRowStyleKey : RowStyleKey;
            entry.Button.Variant = selected ? ButtonVariant.Tonal : ButtonVariant.Text;
        }
    }

    #endregion

    #region Details

    private void BuildDetails()
    {
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        titleRow.Add(_titleIcon);
        var titles = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        titles.Add(_title);
        titles.Add(_identity);
        titleRow.Add(titles);

        _labelBox.TextChanged += (_, text) => Change(c => c with { Label = text.Length == 0 || text == Default?.Label ? null : text });
        _iconBox.TextChanged += (_, text) =>
        {
            _iconPreview.Source = text.Length > 0 ? text : Default?.Icon;
            bool valid = text.Length == 0 || IconSource.IsValid(text);
            Validation.SetErrors(_iconBox, this, valid ? null : ["Not a Material icon name, SVG path data or an SVG document."]);
            if (valid) Change(c => c with { Icon = text.Length == 0 || text == Default?.Icon ? null : text });
        };
        var iconRow = new Grid { ColumnSpacing = 12 };
        iconRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        iconRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        iconRow.Add(_iconBox);
        Grid.SetColumn(_iconPreview, 1);
        _iconPreview.VerticalAlignment = VerticalAlignment.Top;
        _iconPreview.Margin = new Thickness(0, 12, 0, 0);
        iconRow.Add(_iconPreview);

        _recorder.ShortcutRecorded += (_, shortcut) =>
            Change(c => c with { Keybinding = KeybindingGesture.Matches(shortcut, Default?.Keybinding) ? null : shortcut });
        _removeShortcut.Click += (_, _) =>
            Change(c => c with { Keybinding = string.IsNullOrEmpty(Default?.Keybinding) ? null : string.Empty });
        var shortcutRow = new Grid { ColumnSpacing = 8 };
        shortcutRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        shortcutRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        shortcutRow.Add(_recorder);
        Grid.SetColumn(_removeShortcut, 1);
        shortcutRow.Add(_removeShortcut);
        var shortcut = new StackPanel { Spacing = 6 };
        shortcut.Add(new TextBlock("Shortcut") { Muted = true });
        shortcut.Add(shortcutRow);
        shortcut.Add(_defaultShortcut);
        shortcut.Add(_conflicts);

        _resetCommand.Click += (_, _) =>
        {
            if (_selected is { } key) KeybindingManager.ResetCustomization(key.Group, key.Name);
        };

        _details.Add(_noSelection);
        _details.Add(titleRow);
        _details.Add(_description);
        _details.Add(_labelBox);
        _details.Add(iconRow);
        _details.Add(shortcut);
        _details.Add(_resetCommand);
    }

    // The selected command as registered, without the user's changes.
    private IKeybindingDescriptor? Default => SelectedCommand is { } command ? KeybindingManager.GetDefault(command) : null;

    // Changes the selected command's customization; the registrations then notify, and Refresh updates the display.
    private void Change(Func<CommandCustomization, CommandCustomization> change)
    {
        if (_isApplying || _selected is not { } key) return;
        var current = KeybindingManager.GetCustomization(key.Group, key.Name) ?? new CommandCustomization();
        _isApplying = true;
        try
        {
            KeybindingManager.SetCustomization(key.Group, key.Name, change(current));
        }
        finally
        {
            _isApplying = false;
        }
        UpdateRows();
        UpdateDetails(keepFields: true);
    }

    private void UpdateDetails(bool keepFields = false)
    {
        var command = SelectedCommand;
        bool hasSelection = command != null;
        foreach (var child in _details.Children.OfType<UIElement>())
        {
            child.Visibility = (child == _noSelection) != hasSelection ? Visibility.Visible : Visibility.Collapsed;
        }
        if (command == null) return;

        var original = KeybindingManager.GetDefault(command);
        var customization = KeybindingManager.GetCustomization(command.Group, command.Name);
        _titleIcon.Source = command.Icon;
        _title.Text = AccessText.Parse(command.Label, out _);
        _identity.Text = $"{command.Group} · {command.Name}";
        _description.Text = command.Description;
        _description.Visibility = command.Description.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // The fields show what the user typed; they're only set from the command when it changed elsewhere.
        if (!keepFields)
        {
            _isApplying = true;
            try
            {
                _labelBox.Placeholder = original.Label;
                _labelBox.Text = command.Label;
                _iconBox.Placeholder = original.Icon;
                _iconBox.Text = command.Icon;
                Validation.SetErrors(_iconBox, this, null);
                _iconPreview.Source = command.Icon;
            }
            finally
            {
                _isApplying = false;
            }
        }

        _recorder.Shortcut = command.Keybinding;
        _removeShortcut.IsEnabled = command.Keybinding.Length > 0;
        _defaultShortcut.Text = customization?.Keybinding != null
            ? $"Default: {(original.Keybinding.Length > 0 ? KeybindingGesture.FormatForDisplay(original.Keybinding) : "none")}"
            : string.Empty;
        _defaultShortcut.Visibility = _defaultShortcut.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        _conflicts.Text = string.Join("\n", KeybindingManager.GetConflicts()
            .Where(c => IsSelected(c.First) || IsSelected(c.Second))
            .Select(c =>
            {
                var other = IsSelected(c.First) ? c.Second : c.First;
                string otherLabel = AccessText.Parse(other.Label, out _);
                return !c.IsPrefix
                    ? $"\"{otherLabel}\" uses the same shortcut; only one of them runs."
                    : IsSelected(c.First)
                        ? $"It starts the chord of \"{otherLabel}\" ({KeybindingGesture.FormatForDisplay(other.Keybinding)}), which can't be completed."
                        : $"\"{otherLabel}\" ({KeybindingGesture.FormatForDisplay(other.Keybinding)}) runs before this chord can be completed.";
            }));
        _conflicts.Visibility = _conflicts.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _resetCommand.IsEnabled = customization != null;

        bool IsSelected(IKeybindingDescriptor d) => d.Group == command.Group && d.Name == command.Name;
    }

    #endregion
}
