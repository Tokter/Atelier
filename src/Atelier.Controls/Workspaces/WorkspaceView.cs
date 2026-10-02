using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// Shows one of several <see cref="Workspace"/>s under a strip of workspace tabs, like Blender's workspaces: each
/// workspace is an arrangement of areas (an <see cref="AreaLayout"/>), and the tabs switch between them.
/// </summary>
/// <remarks>
/// <para>
/// The "+" button after the tabs adds a workspace: with <see cref="Templates"/>, it opens a menu of them (and
/// "Duplicate Current"); without, it adds a workspace with one area. Double-click a tab to rename its workspace; drag
/// tabs to reorder them. A right-click on a tab opens its menu: Rename, Duplicate, Delete, Delete Other Workspaces,
/// Reorder to Front and Reorder to Back. The last workspace can't be deleted. Duplicates and new workspaces get unique
/// names in Blender's style ("Layout.001").
/// </para>
/// <para>
/// Ctrl+Page Up and Ctrl+Page Down switch workspaces from anywhere in the view. <see cref="LeadingContent"/> and
/// <see cref="TrailingContent"/> share the strip, e.g. for a menu bar before the tabs.
/// </para>
/// <para>
/// <see cref="LayoutChanged"/> reports every change worth saving (workspaces added, removed, renamed, reordered or
/// switched, and changes to their areas); <see cref="ToDefinition"/> and <see cref="Load(WorkspacesDefinition)"/> save
/// and restore everything, e.g. as JSON with <see cref="WorkspacesDefinition.ToJson"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var view = new WorkspaceView(editors);
/// view.AddWorkspace("Layout", AreaDefinition.Row(AreaDefinition.Editor("viewport", 3), AreaDefinition.Editor("outliner")));
/// view.AddWorkspace("Shading", AreaDefinition.Column(AreaDefinition.Editor("viewport"), AreaDefinition.Editor("nodes")));
/// </code>
/// </example>
public class WorkspaceView : Control
{
    /// <summary>Identifies the <see cref="SelectedIndex"/> property.</summary>
    public static readonly BindableProperty<int> SelectedIndexProperty =
        BindableProperty.Register<WorkspaceView, int>(nameof(SelectedIndex), -1, (s, o, n) => ((WorkspaceView)s).OnSelectedIndexChanged(n));

    /// <summary>Identifies the <see cref="LeadingContent"/> property.</summary>
    public static readonly BindableProperty<object?> LeadingContentProperty =
        BindableProperty.Register<WorkspaceView, object?>(nameof(LeadingContent), null, (s, o, n) => ((WorkspaceView)s)._leading.Content = n);

    /// <summary>Identifies the <see cref="TrailingContent"/> property.</summary>
    public static readonly BindableProperty<object?> TrailingContentProperty =
        BindableProperty.Register<WorkspaceView, object?>(nameof(TrailingContent), null, (s, o, n) => ((WorkspaceView)s)._trailing.Content = n);

    private readonly WorkspaceTabStrip _tabs;
    private readonly ContentControl _leading = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentControl _trailing = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Dictionary<Workspace, WorkspaceTabHeader> _headers = [];
    private AreaLayout? _shown;

    /// <summary>Initializes an empty workspace view; add workspaces with <see cref="AddWorkspace(string, AreaDefinition?)"/>.</summary>
    /// <param name="editors">The editor types the areas of all workspaces can show.</param>
    public WorkspaceView(AreaEditorRegistry editors)
    {
        Editors = editors ?? throw new ArgumentNullException(nameof(editors));
        _tabs = new WorkspaceTabStrip(this)
        {
            TabStyle = TabStyle.Browser,
            ShowAddButton = true,
            ItemsSource = Workspaces,
            HeaderTemplate = item => CreateHeader((Workspace)item),
            ContentTemplate = _ => new Border(),
        };
        _tabs.ContentPresenter.Visibility = Visibility.Collapsed;
        ToolTipService.SetToolTip(_tabs.AddButton, "Add workspace");
        _tabs.SelectionChanged += (_, item) => OnTabSelected(item as Workspace);
        _tabs.AddTabRequested += (_, _) => OnAddRequested();
        Workspaces.CollectionChanged += OnWorkspacesChanged;

        AddChild(_leading);
        AddChild(_tabs);
        AddChild(_trailing);
    }

    #region Properties

    /// <summary>Gets the editor types the areas of all workspaces can show.</summary>
    public AreaEditorRegistry Editors { get; }

    /// <summary>Gets the workspaces in tab order.</summary>
    public ObservableCollection<Workspace> Workspaces { get; } = [];

    /// <summary>
    /// Gets the workspace templates the "+" button offers (with "Duplicate Current"); without templates, it adds a
    /// workspace with one area.
    /// </summary>
    public ObservableCollection<WorkspaceDefinition> Templates { get; } = [];

    /// <summary>Gets or sets the index of the shown workspace; -1 only without workspaces.</summary>
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <summary>Gets or sets the shown workspace.</summary>
    public Workspace? SelectedWorkspace
    {
        get => _tabs.SelectedItem as Workspace;
        set => _tabs.SelectedItem = value;
    }

    /// <summary>Gets the area layout of the shown workspace, or <c>null</c>.</summary>
    public AreaLayout? CurrentLayout => _shown;

    /// <summary>Gets or sets content before the tabs in the strip, such as a menu bar.</summary>
    public object? LeadingContent { get => GetValue(LeadingContentProperty); set => SetValue(LeadingContentProperty, value); }

    /// <summary>Gets or sets content after the tabs in the strip, at the right edge.</summary>
    public object? TrailingContent { get => GetValue(TrailingContentProperty); set => SetValue(TrailingContentProperty, value); }

    /// <summary>Gets the tab strip (browser-style tabs with a "+" button).</summary>
    public TabControl TabStrip => _tabs;

    /// <summary>Gets the bounds of the strip (leading content, tabs and trailing content) in the view's coordinates.</summary>
    public Rect StripBounds => new(0, 0, Bounds.Width, StripHeight);

    private float StripHeight => Math.Max(_tabs.DesiredSize.Height, Math.Max(_leading.DesiredSize.Height, _trailing.DesiredSize.Height));

    /// <summary>Occurs after the shown workspace changed, with the new one.</summary>
    public event EventHandler<Workspace?>? SelectionChanged;

    /// <summary>
    /// Occurs after a change worth saving: a workspace was added, removed, renamed, reordered or shown, or the areas of one
    /// changed.
    /// </summary>
    public event EventHandler? LayoutChanged;

    #endregion

    #region Workspaces

    /// <summary>Adds a workspace with the areas of <paramref name="root"/> (one area when <c>null</c>) at the end.</summary>
    /// <param name="name">The name; made unique (see <see cref="GetUniqueName"/>).</param>
    /// <param name="root">The arrangement of the areas.</param>
    /// <param name="select">Whether to show the new workspace.</param>
    /// <returns>The new workspace.</returns>
    public Workspace AddWorkspace(string name, AreaDefinition? root = null, bool select = false) =>
        InsertWorkspace(Workspaces.Count, name, root, select);

    /// <summary>Adds a workspace from <paramref name="definition"/> at the end (see <see cref="AddWorkspace(string, AreaDefinition?, bool)"/>).</summary>
    public Workspace AddWorkspace(WorkspaceDefinition definition, bool select = false)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return AddWorkspace(definition.Name, definition.Root, select);
    }

    private Workspace InsertWorkspace(int index, string name, AreaDefinition? root, bool select)
    {
        var workspace = new Workspace(GetUniqueName(name), Editors, root);
        Workspaces.Insert(index, workspace);
        if (select) SelectedWorkspace = workspace;
        return workspace;
    }

    /// <summary>
    /// Adds a copy of <paramref name="workspace"/> after it, with the same areas and editor types (and new editor
    /// content), named like "Layout.001", and shows it.
    /// </summary>
    /// <returns>The copy.</returns>
    public Workspace Duplicate(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        int index = Workspaces.IndexOf(workspace);
        return InsertWorkspace(index < 0 ? Workspaces.Count : index + 1, workspace.Name, workspace.Layout.ToDefinition(), select: true);
    }

    /// <summary>Gets whether <paramref name="workspace"/> can be deleted: it isn't the last one.</summary>
    public bool CanDelete(Workspace workspace) => Workspaces.Count > 1 && Workspaces.Contains(workspace);

    /// <summary>Deletes <paramref name="workspace"/> and releases the content of its areas; the last workspace stays.</summary>
    /// <returns><c>true</c> if it was deleted.</returns>
    public bool Delete(Workspace workspace)
    {
        if (!CanDelete(workspace)) return false;
        Workspaces.Remove(workspace);
        workspace.Layout.Clear();
        return true;
    }

    /// <summary>Deletes every workspace but <paramref name="workspace"/>, and shows it.</summary>
    public void DeleteOthers(Workspace workspace)
    {
        if (!Workspaces.Contains(workspace)) return;
        SelectedWorkspace = workspace;
        for (int i = Workspaces.Count - 1; i >= 0; i--)
        {
            if (Workspaces[i] != workspace) Delete(Workspaces[i]);
        }
    }

    /// <summary>Moves <paramref name="workspace"/>'s tab to the front (<paramref name="toFront"/>) or the back.</summary>
    public void Reorder(Workspace workspace, bool toFront)
    {
        int index = Workspaces.IndexOf(workspace);
        if (index < 0) return;
        int target = toFront ? 0 : Workspaces.Count - 1;
        if (index != target) Workspaces.Move(index, target);
    }

    /// <summary>Renames <paramref name="workspace"/> to <paramref name="name"/> (trimmed, made unique); a blank name is ignored.</summary>
    /// <returns><c>true</c> if the name changed.</returns>
    public bool Rename(Workspace workspace, string name)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name == workspace.Name) return false;
        workspace.Name = GetUniqueName(name, workspace);
        RaiseLayoutChanged();
        return true;
    }

    /// <summary>Shows a text box in <paramref name="workspace"/>'s tab to rename it: Enter or leaving it renames, Escape cancels.</summary>
    public void BeginRename(Workspace workspace)
    {
        if (_headers.TryGetValue(workspace, out var header)) header.BeginRename();
    }

    /// <summary>
    /// Returns <paramref name="name"/>, or when another workspace has it, the name with the lowest free Blender-style
    /// number: "Layout.001", "Layout.002"...
    /// </summary>
    /// <param name="name">The wanted name; a number suffix such as ".003" is replaced.</param>
    /// <param name="except">A workspace whose own name doesn't count (the one being renamed).</param>
    public string GetUniqueName(string name, Workspace? except = null)
    {
        bool Taken(string candidate)
        {
            foreach (var workspace in Workspaces)
            {
                if (workspace != except && string.Equals(workspace.Name, candidate, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        if (!Taken(name)) return name;
        string stem = name;
        int dot = name.LastIndexOf('.');
        if (dot > 0 && dot == name.Length - 4 && int.TryParse(name.AsSpan(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            stem = name[..dot];
        }
        for (int i = 1; ; i++)
        {
            string candidate = string.Create(CultureInfo.InvariantCulture, $"{stem}.{i:000}");
            if (!Taken(candidate)) return candidate;
        }
    }

    /// <summary>Describes all workspaces and the shown one, for saving them (see <see cref="WorkspacesDefinition.ToJson"/>).</summary>
    public WorkspacesDefinition ToDefinition()
    {
        var workspaces = new List<WorkspaceDefinition>(Workspaces.Count);
        foreach (var workspace in Workspaces) workspaces.Add(workspace.ToDefinition());
        return new WorkspacesDefinition(workspaces, Math.Max(0, SelectedIndex));
    }

    /// <summary>Replaces all workspaces with those of <paramref name="definition"/>, releasing the content of the old ones.</summary>
    public void Load(WorkspacesDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var old = new List<Workspace>(Workspaces);
        Workspaces.Clear(); // a reset: no OldItems, so the old workspaces are let go of here
        foreach (var workspace in old)
        {
            workspace.Layout.LayoutChanged -= OnWorkspaceLayoutChanged;
            _headers.Remove(workspace);
            workspace.Layout.Clear();
        }
        foreach (var workspace in definition.Workspaces) AddWorkspace(workspace);
        if (Workspaces.Count > 0) SelectedIndex = Math.Clamp(definition.SelectedIndex, 0, Workspaces.Count - 1);
    }

    private void OnWorkspacesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (Workspace workspace in e.OldItems)
            {
                if (Workspaces.Contains(workspace)) continue;
                workspace.Layout.LayoutChanged -= OnWorkspaceLayoutChanged;
                _headers.Remove(workspace);
            }
        }
        if (e.NewItems != null)
        {
            foreach (Workspace workspace in e.NewItems)
            {
                if (workspace.Layout.Editors != Editors)
                {
                    throw new InvalidOperationException("A workspace of a workspace view must use the view's editor registry.");
                }
                workspace.Layout.LayoutChanged -= OnWorkspaceLayoutChanged;
                workspace.Layout.LayoutChanged += OnWorkspaceLayoutChanged;
            }
        }
        RaiseLayoutChanged();
    }

    private void OnWorkspaceLayoutChanged(object? sender, EventArgs e) => RaiseLayoutChanged();

    private void RaiseLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

    #endregion

    #region Selection

    private void OnSelectedIndexChanged(int index)
    {
        if (_tabs.SelectedIndex != index) _tabs.SelectedIndex = index;
    }

    private void OnTabSelected(Workspace? workspace)
    {
        SelectedIndex = _tabs.SelectedIndex;
        var layout = workspace?.Layout;
        if (layout == _shown) return;

        if (_shown != null)
        {
            _shown.CancelInteraction();
            RemoveChild(_shown);
        }
        _shown = layout;
        if (layout != null) AddChild(layout);
        InvalidateMeasure();
        SelectionChanged?.Invoke(this, workspace);
        RaiseLayoutChanged();
    }

    /// <inheritdoc/>
    /// <remarks>Ctrl+Page Up and Ctrl+Page Down show the previous and next workspace (wrapping around).</remarks>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Workspaces.Count == 0 || e.Modifiers != ModifierKeys.Control || e.Key is not (Key.PageUp or Key.PageDown)) return;
        int count = Workspaces.Count;
        SelectedIndex = (Math.Max(0, SelectedIndex) + (e.Key == Key.PageDown ? 1 : count - 1)) % count;
        e.Handled = true;
    }

    #endregion

    #region Menus

    private void OnAddRequested()
    {
        if (Templates.Count == 0)
        {
            AddWorkspace("Workspace", select: true);
            return;
        }
        ShowAddMenu();
    }

    /// <summary>Opens the menu of the "+" button: the <see cref="Templates"/> and "Duplicate Current". Returns it, or <c>null</c>.</summary>
    public ContextMenu? ShowAddMenu()
    {
        var menu = CreateAddMenu();
        if (!menu.Open(_tabs.AddButton)) return null;
        menu.Placement = PlacementMode.Bottom;
        return menu;
    }

    /// <summary>Creates the menu of the "+" button (see <see cref="ShowAddMenu"/>). Override it to change the menu.</summary>
    public virtual ContextMenu CreateAddMenu()
    {
        var menu = new ContextMenu();
        foreach (var template in Templates)
        {
            var item = new MenuItem(template.Name) { Icon = MaterialIconKind.SpaceDashboard };
            item.Click += (_, _) => AddWorkspace(template, select: true);
            menu.Items.Add(item);
        }
        if (SelectedWorkspace is { } current)
        {
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());
            var duplicate = new MenuItem("Duplicate Current") { Icon = MaterialIconKind.ContentCopy };
            duplicate.Click += (_, _) => Duplicate(current);
            menu.Items.Add(duplicate);
        }
        return menu;
    }

    /// <summary>
    /// Creates the items of a workspace tab's menu: Rename…, Duplicate, Delete, Delete Other Workspaces, Reorder to Front
    /// and Reorder to Back. Override it to change the menu.
    /// </summary>
    public virtual IReadOnlyList<UIElement> CreateTabMenuItems(Workspace workspace)
    {
        var rename = new MenuItem("Rename…") { Icon = MaterialIconKind.Edit };
        rename.Click += (_, _) => BeginRename(workspace);
        var duplicate = new MenuItem("Duplicate") { Icon = MaterialIconKind.ContentCopy };
        duplicate.Click += (_, _) => Duplicate(workspace);
        var delete = new MenuItem("Delete") { Icon = MaterialIconKind.Delete, IsEnabled = CanDelete(workspace) };
        delete.Click += (_, _) => Delete(workspace);
        var deleteOthers = new MenuItem("Delete Other Workspaces") { IsEnabled = CanDelete(workspace) };
        deleteOthers.Click += (_, _) => DeleteOthers(workspace);

        int index = Workspaces.IndexOf(workspace);
        var front = new MenuItem("Reorder to Front") { Icon = MaterialIconKind.FirstPage, IsEnabled = index > 0 };
        front.Click += (_, _) => Reorder(workspace, toFront: true);
        var back = new MenuItem("Reorder to Back") { Icon = MaterialIconKind.LastPage, IsEnabled = index >= 0 && index < Workspaces.Count - 1 };
        back.Click += (_, _) => Reorder(workspace, toFront: false);
        return [rename, duplicate, new Separator(), delete, deleteOthers, new Separator(), front, back];
    }

    /// <summary>Opens the menu of <paramref name="workspace"/>'s tab (see <see cref="CreateTabMenuItems"/>). Returns it, or <c>null</c>.</summary>
    public ContextMenu? ShowTabMenu(Workspace workspace)
    {
        int index = Workspaces.IndexOf(workspace);
        if (index < 0 || _tabs.GetTab(index) is not { } tab) return null;
        return ContextMenuService.GetContextMenu(tab) is { } menu && menu.Open(tab) ? menu : null;
    }

    private UIElement CreateHeader(Workspace workspace)
    {
        var header = new WorkspaceTabHeader(this, workspace);
        _headers[workspace] = header;
        return header;
    }

    internal void SetUpTab(TabItem tab, Workspace workspace)
    {
        var menu = new ContextMenu();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            foreach (var item in CreateTabMenuItems(workspace)) menu.Items.Add(item);
        };
        ContextMenuService.SetContextMenu(tab, menu);
        tab.PointerPressed += (_, e) =>
        {
            if (e.Handled || e.Button != PointerButtons.Left || e.ClickCount != 2) return;
            e.Handled = true;
            BeginRename(workspace);
        };
    }

    #endregion

    #region Layout

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        _leading.Measure(new Size(availableSize.Width, availableSize.Height));
        _trailing.Measure(new Size(Math.Max(0, availableSize.Width - _leading.DesiredSize.Width), availableSize.Height));
        float tabsWidth = Math.Max(0, availableSize.Width - _leading.DesiredSize.Width - _trailing.DesiredSize.Width);
        _tabs.Measure(new Size(tabsWidth, availableSize.Height));
        float strip = StripHeight;

        _shown?.Measure(new Size(availableSize.Width, Math.Max(0, availableSize.Height - strip)));
        float width = float.IsInfinity(availableSize.Width)
            ? _leading.DesiredSize.Width + _tabs.DesiredSize.Width + _trailing.DesiredSize.Width
            : availableSize.Width;
        float height = float.IsInfinity(availableSize.Height) ? strip + (_shown?.DesiredSize.Height ?? 0) : availableSize.Height;
        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        float strip = StripHeight;
        float leading = _leading.DesiredSize.Width, trailing = _trailing.DesiredSize.Width;
        _leading.Arrange(new Rect(0, (strip - _leading.DesiredSize.Height) * 0.5f, leading, _leading.DesiredSize.Height));
        float tabsWidth = Math.Max(0, finalSize.Width - leading - trailing);
        _tabs.Arrange(new Rect(leading, 0, tabsWidth, strip));
        _trailing.Arrange(new Rect(finalSize.Width - trailing, (strip - _trailing.DesiredSize.Height) * 0.5f, trailing, _trailing.DesiredSize.Height));
        _shown?.Arrange(new Rect(0, strip, finalSize.Width, Math.Max(0, finalSize.Height - strip)));
        return finalSize;
    }

    #endregion

    // The tab strip gives each tab its menu and double-click rename.
    private sealed class WorkspaceTabStrip(WorkspaceView view) : TabControl
    {
        protected override UIElement CreateContainerForItem(object item)
        {
            var container = base.CreateContainerForItem(item);
            if (container is TabItem tab && item is Workspace workspace) view.SetUpTab(tab, workspace);
            return container;
        }
    }
}

/// <summary>
/// The header of a workspace's tab in a <see cref="WorkspaceView"/>: the workspace's name, or a text box while it is
/// being renamed.
/// </summary>
public class WorkspaceTabHeader : Control
{
    private readonly WorkspaceView _view;
    private readonly TextBlock _label = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ShowsToolTipWhenTrimmed = true };
    private TextBox? _editor;

    internal WorkspaceTabHeader(WorkspaceView view, Workspace workspace)
    {
        _view = view;
        Workspace = workspace;
        VerticalAlignment = VerticalAlignment.Center;
        _label.SetBinding(TextBlock.TextProperty, workspace, w => w.Name);
        AddChild(_label);
    }

    /// <summary>Gets the workspace of the tab.</summary>
    public Workspace Workspace { get; }

    /// <summary>Gets the text box while the workspace is being renamed, or <c>null</c>.</summary>
    public TextBox? RenameBox => _editor;

    /// <summary>Gets whether the workspace is being renamed.</summary>
    public bool IsRenaming => _editor != null;

    /// <summary>Replaces the name with a text box: Enter or leaving it renames the workspace, Escape cancels.</summary>
    public void BeginRename()
    {
        if (_editor != null) return;
        var box = new TextBox
        {
            Text = Workspace.Name,
            FieldHeight = 28,
            FontSize = 13,
            MinWidth = 96,
            VerticalAlignment = VerticalAlignment.Center,
        };
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { EndRename(keep: true); e.Handled = true; }
            else if (e.Key == Key.Escape) { EndRename(keep: false); e.Handled = true; }
        };
        box.LostFocus += (_, _) => EndRename(keep: true);
        _editor = box;
        _label.Visibility = Visibility.Collapsed;
        AddChild(box);
        InvalidateMeasure();
        box.Focus();
        box.SelectAll();
    }

    /// <summary>Ends renaming; <paramref name="keep"/> renames the workspace to the text box's text.</summary>
    public void EndRename(bool keep)
    {
        if (_editor is not { } box) return;
        _editor = null;
        if (keep) _view.Rename(Workspace, box.Text);
        RemoveChild(box);
        _label.Visibility = Visibility.Visible;
        InvalidateMeasure();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        UIElement shown = _editor ?? (UIElement)_label;
        shown.Measure(availableSize);
        return shown.DesiredSize;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        UIElement shown = _editor ?? (UIElement)_label;
        shown.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        return finalSize;
    }
}
