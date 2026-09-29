using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Nodes;

public partial class NodeEditor
{
    /// <summary>The style key of the panels shown over the graph (the group path and the group's interface), <see cref="Border"/>s.</summary>
    public const string PanelStyleKey = "NodeEditorPanel";

    /// <summary>Identifies the <see cref="ShowGroupInterface"/> property.</summary>
    public static readonly BindableProperty<bool> ShowGroupInterfaceProperty =
        BindableProperty.Register<NodeEditor, bool>(nameof(ShowGroupInterface), true, (s, o, n) => ((NodeEditor)s).UpdateGroupBar());

    private static readonly Color GroupColor = Color.FromRgb(0x2B, 0x65, 0x2B);

    // The room the group path and the interface panel take over the graph, for framing.
    private const float GroupBarHeight = 52;
    private const float InterfacePanelWidth = 290;

    private EditorPanel? _groupBar;
    private EditorPanel? _interfacePanel;

    /// <summary>
    /// Gets or sets whether the panel with the inputs and outputs of the group the editor is in shows (see
    /// <see cref="GroupInterfacePanel"/>). The default is <c>true</c>.
    /// </summary>
    public bool ShowGroupInterface { get => GetValue(ShowGroupInterfaceProperty); set => SetValue(ShowGroupInterfaceProperty, value); }

    /// <summary>
    /// Creates the catalog the add menus offer: the graph's node types, and its groups in a "Groups" category (but not
    /// the groups that would end up inside themselves in <see cref="CurrentGraph"/>).
    /// </summary>
    public NodeCatalog CreateMenuCatalog()
    {
        var catalog = new NodeCatalog();
        if (CurrentGraph is not { } graph) return catalog;
        foreach (var type in graph.Catalog.Types) catalog.Register(type);
        foreach (var definition in graph.Groups.Definitions)
        {
            if (graph.Owner != null && definition.Contains(graph.Owner)) continue;
            catalog.Register(new NodeType("group:" + definition.Id, definition.Name, "Groups", () => new GroupNodeViewModel(definition))
            {
                HeaderColor = GroupColor,
                Description = $"The group {definition.Name}",
                Keywords = ["group"],
            });
        }
        return catalog;
    }

    // The group path over the top-left of the graph, and the group's interface over its top right.
    private void UpdateGroupBar()
    {
        if (_groupBar == null)
        {
            _groupBar = new EditorPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8) };
            _interfacePanel = new EditorPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8) };
            _surface.AddChild(_groupBar);
            _surface.AddChild(_interfacePanel);
        }

        _groupBar.Visibility = _path.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _interfacePanel!.Visibility = _path.Count > 0 && ShowGroupInterface ? Visibility.Visible : Visibility.Collapsed;
        if (_path.Count == 0)
        {
            _groupBar.Child = null;
            _interfacePanel.Child = null;
            return;
        }

        var crumbs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        crumbs.Add(Crumb("Graph", 0));
        for (int i = 0; i < _path.Count; i++)
        {
            crumbs.Add(new TextBlock("›") { Muted = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0) });
            string name = _path[i].Node.Definition.Name;
            crumbs.Add(i < _path.Count - 1
                ? Crumb(name, i + 1)
                : new TextBlock(name) { Bold = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) });
        }
        crumbs.Add(new TextBlock("Tab to leave") { Muted = true, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) });
        _groupBar.Child = crumbs;

        var definition = _path[^1].Node.Definition;
        if ((_interfacePanel.Child as GroupInterfacePanel)?.Definition != definition)
        {
            _interfacePanel.Child = new GroupInterfacePanel(definition);
        }

        Button Crumb(string text, int depth)
        {
            var button = new Button
            {
                Content = text,
                Variant = ButtonVariant.Text,
                MinHeight = 28,
                MinWidth = 0,
                Padding = new Thickness(8, 2),
                IsFocusable = false,
            };
            button.Click += (_, _) => ExitGroups(depth);
            return button;
        }
    }

    /// <summary>A panel over the graph: it keeps presses and wheel turns on it from reaching the graph's tools.</summary>
    private sealed class EditorPanel : Border
    {
        public EditorPanel()
        {
            StyleKey = PanelStyleKey;
        }

        public override void OnPointerPressed(PointerEventArgs e)
        {
            base.OnPointerPressed(e);
            e.Handled = true;
        }

        public override void OnPointerWheel(PointerWheelEventArgs e)
        {
            base.OnPointerWheel(e);
            e.Handled = true;
        }
    }
}

/// <summary>
/// Edits a group's interface: its name, and its inputs and outputs (rename, move up or down, remove). New sockets are
/// added by dragging a link onto the empty socket of the group's Group Input or Group Output node.
/// </summary>
/// <remarks>The <see cref="NodeEditor"/> shows it while it's inside a group (see <see cref="NodeEditor.ShowGroupInterface"/>).</remarks>
public class GroupInterfacePanel : ContentControl
{
    private const float PanelWidth = 260;

    /// <summary>Initializes a panel for <paramref name="definition"/>.</summary>
    public GroupInterfacePanel(NodeGroupDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        Rebuild();
    }

    /// <summary>Gets the group edited.</summary>
    public NodeGroupDefinition Definition { get; }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        Definition.InterfaceChanged += OnInterfaceChanged;
        Rebuild();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree()
    {
        base.OnDetachedFromVisualTree();
        Definition.InterfaceChanged -= OnInterfaceChanged;
    }

    private void OnInterfaceChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        var stack = new StackPanel { Spacing = 4, Width = PanelWidth };
        var name = new TextBox { Label = "Group", Text = Definition.Name, FieldHeight = 36, FontSize = 13, MinWidth = 0 };
        OnCommit(name, text =>
        {
            if (text.Trim().Length > 0 && text != Definition.Name) Definition.Name = text.Trim();
        });
        stack.Add(name);
        AddSockets(stack, "Inputs", Definition.Inputs);
        AddSockets(stack, "Outputs", Definition.Outputs);
        stack.Add(new TextBlock("Drag a link onto the empty socket of the Group Input or Group Output node to add one.")
        {
            Muted = true,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(4, 4, 4, 0),
        });
        Content = stack;
    }

    private void AddSockets(StackPanel stack, string title, IReadOnlyList<GroupSocket> sockets)
    {
        stack.Add(new TextBlock(title) { StyleKey = AddNodeMenu.CategoryStyleKey, Margin = new Thickness(4, 8, 4, 0) });
        if (sockets.Count == 0)
        {
            stack.Add(new TextBlock("None") { Muted = true, FontSize = 12, Margin = new Thickness(4, 0) });
            return;
        }
        for (int i = 0; i < sockets.Count; i++) stack.Add(Row(sockets[i], i, sockets.Count));
    }

    private Grid Row(GroupSocket socket, int index, int count)
    {
        var dot = new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(5),
            Background = socket.Type.Color,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
        };
        var name = new TextBox { Text = socket.Name, FieldHeight = 36, FontSize = 13, MinWidth = 0 };
        OnCommit(name, text =>
        {
            if (text.Trim().Length > 0 && text != socket.Name) socket.Name = text.Trim();
        });

        var row = new Grid { ColumnSpacing = 2 };
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int i = 0; i < 3; i++) row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.Add(dot);
        Grid.SetColumn(name, 1);
        row.Add(name);
        Add(2, MaterialIconKind.ArrowUpward, "Move up", index > 0, () => Definition.MoveSocket(socket, index - 1));
        Add(3, MaterialIconKind.ArrowDownward, "Move down", index < count - 1, () => Definition.MoveSocket(socket, index + 1));
        Add(4, MaterialIconKind.Close, "Remove", true, () => Definition.RemoveSocket(socket));
        return row;

        void Add(int column, MaterialIconKind icon, string toolTip, bool enabled, Action action)
        {
            var button = new Button
            {
                Content = new Icon(icon, 18),
                Variant = ButtonVariant.Text,
                Width = 28,
                Height = 28,
                MinWidth = 0,
                MinHeight = 0,
                Padding = Thickness.Zero,
                IsEnabled = enabled,
                IsFocusable = false,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTipService.SetToolTip(button, toolTip);
            button.Click += (_, _) => action();
            Grid.SetColumn(button, column);
            row.Add(button);
        }
    }

    // Commits a text box's text when it loses the focus or Enter is pressed.
    private static void OnCommit(TextBox textBox, Action<string> commit)
    {
        textBox.LostFocus += (_, _) => commit(textBox.Text);
        textBox.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            commit(textBox.Text);
            e.Handled = true;
        };
    }
}
