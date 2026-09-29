using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Numerics;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Nodes;

/// <summary>
/// Shows a <see cref="NodeGraphViewModel"/> in a Blender-style editor: background layers (a grid by default), the links
/// as curves, and a <see cref="NodeView"/> for each node, in a viewport that can be panned and zoomed.
/// </summary>
/// <remarks>
/// <para>
/// Nodes are placed at their <see cref="NodeViewModel.Position"/> in graph coordinates; a point is shown at
/// <c>position × <see cref="Zoom"/> + <see cref="Offset"/></c> in the editor (see <see cref="GraphToView"/>). Zooming
/// scales the node views without laying them out again.
/// </para>
/// <para>
/// The editor is a <see cref="KeybindingHandler"/> that runs the <see cref="CommandGroup"/> commands (see
/// <see cref="NodeEditorCommands"/>) on itself: by default dragging with the middle button pans, the wheel zooms around
/// the pointer and Home frames all nodes. Users can rebind them like any shortcut, to keys or pointer gestures. Its
/// <see cref="ContentControl.Content"/> is its drawing surface; don't replace it. <see cref="KeybindingHandler.Group"/>
/// can name a further group whose commands run on the view models, as for any keybinding handler.
/// </para>
/// </remarks>
public class NodeEditor : KeybindingHandler
{
    /// <summary>The keybinding group of the editor's own commands.</summary>
    public const string CommandGroup = "NodeEditor";

    /// <summary>Identifies the <see cref="Graph"/> property.</summary>
    public static readonly BindableProperty<NodeGraphViewModel?> GraphProperty =
        BindableProperty.Register<NodeEditor, NodeGraphViewModel?>(nameof(Graph), null, (s, o, n) => ((NodeEditor)s).OnGraphChanged(o, n));

    /// <summary>Identifies the <see cref="Zoom"/> property.</summary>
    public static readonly BindableProperty<float> ZoomProperty =
        BindableProperty.Register<NodeEditor, float>(
            nameof(Zoom), 1f, (s, o, n) => ((NodeEditor)s).OnViewportChanged(),
            coerceValue: (s, v) => Math.Clamp(v, ((NodeEditor)s).MinZoom, Math.Max(((NodeEditor)s).MinZoom, ((NodeEditor)s).MaxZoom)),
            validateValue: v => float.IsFinite(v) && v > 0);

    /// <summary>Identifies the <see cref="Offset"/> property.</summary>
    public static readonly BindableProperty<Point> OffsetProperty =
        BindableProperty.Register<NodeEditor, Point>(nameof(Offset), Point.Zero, (s, o, n) => ((NodeEditor)s).OnViewportChanged(),
            validateValue: p => float.IsFinite(p.X) && float.IsFinite(p.Y));

    /// <summary>Identifies the <see cref="MinZoom"/> property.</summary>
    public static readonly BindableProperty<float> MinZoomProperty =
        BindableProperty.Register<NodeEditor, float>(nameof(MinZoom), 0.2f, (s, o, n) => ((NodeEditor)s).CoerceValue(ZoomProperty),
            validateValue: v => float.IsFinite(v) && v > 0);

    /// <summary>Identifies the <see cref="MaxZoom"/> property.</summary>
    public static readonly BindableProperty<float> MaxZoomProperty =
        BindableProperty.Register<NodeEditor, float>(nameof(MaxZoom), 3f, (s, o, n) => ((NodeEditor)s).CoerceValue(ZoomProperty),
            validateValue: v => float.IsFinite(v) && v > 0);

    /// <summary>Identifies the <see cref="LinkThickness"/> property.</summary>
    public static readonly BindableProperty<float> LinkThicknessProperty =
        BindableProperty.Register<NodeEditor, float>(nameof(LinkThickness), 2f, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="SelectionColor"/> property.</summary>
    public static readonly BindableProperty<Color> SelectionColorProperty =
        BindableProperty.Register<NodeEditor, Color>(nameof(SelectionColor), Color.FromRgb(0xFF, 0xA7, 0x33), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="ErrorColor"/> property.</summary>
    public static readonly BindableProperty<Color> ErrorColorProperty =
        BindableProperty.Register<NodeEditor, Color>(nameof(ErrorColor), Color.FromRgb(0xE5, 0x39, 0x35), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="SnapToGrid"/> property.</summary>
    public static readonly BindableProperty<bool> SnapToGridProperty =
        BindableProperty.Register<NodeEditor, bool>(nameof(SnapToGrid), false);

    /// <summary>Identifies the <see cref="SnapSpacing"/> property.</summary>
    public static readonly BindableProperty<float> SnapSpacingProperty =
        BindableProperty.Register<NodeEditor, float>(nameof(SnapSpacing), 20f, validateValue: v => float.IsFinite(v) && v > 0);

    /// <summary>Identifies the <see cref="AutoInsert"/> property.</summary>
    public static readonly BindableProperty<bool> AutoInsertProperty =
        BindableProperty.Register<NodeEditor, bool>(nameof(AutoInsert), true);

    /// <summary>Identifies the <see cref="SearchOnLinkDrop"/> property.</summary>
    public static readonly BindableProperty<bool> SearchOnLinkDropProperty =
        BindableProperty.Register<NodeEditor, bool>(nameof(SearchOnLinkDrop), true);

    /// <summary>Identifies the <see cref="InputEditorFactory"/> property.</summary>
    public static readonly BindableProperty<Func<InputSocketViewModel, UIElement?>?> InputEditorFactoryProperty =
        BindableProperty.Register<NodeEditor, Func<InputSocketViewModel, UIElement?>?>(nameof(InputEditorFactory), null,
            (s, o, n) => ((NodeEditor)s).RecreateNodeViews());

    private static readonly Size DefaultSize = new(400, 300);

    private readonly Surface _surface = new();
    private readonly LinkLayer _links;
    private readonly NodeLayer _nodes;
    private readonly OverlayLayer _overlay;
    private Rect? _selectionBox;
    private LinkViewModel? _hiddenLink;
    private LinkViewModel? _insertTarget;
    private IReadOnlyList<Point>? _stroke;
    private readonly Dictionary<NodeViewModel, NodeView> _views = [];

    static NodeEditor()
    {
        NodeEditorTheme.Register();
        IsFocusableProperty.OverrideDefaultValue<NodeEditor>(true);
        ClipToBoundsProperty.OverrideDefaultValue<NodeEditor>(true);
    }

    /// <summary>Initializes an editor with a <see cref="GridLayer"/> background.</summary>
    public NodeEditor()
    {
        NodeEditorCommands.Register();
        AdditionalScopes.Add(new KeybindingScope(CommandGroup, this));
        _links = new LinkLayer(this);
        _nodes = new NodeLayer(this);
        _surface.AddChild(_links);
        _surface.AddChild(_nodes);
        _overlay = new OverlayLayer(this);
        _surface.AddChild(_overlay);
        Content = _surface;
        BackgroundLayers = [new GridLayer()];
        BackgroundLayers.CollectionChanged += (_, _) => SyncLayers();
        SyncLayers();
    }

    /// <summary>Gets or sets the graph shown.</summary>
    public NodeGraphViewModel? Graph { get => GetValue(GraphProperty); set => SetValue(GraphProperty, value); }

    /// <summary>Gets or sets the scale of the view, kept within <see cref="MinZoom"/>..<see cref="MaxZoom"/>. The default is 1.</summary>
    public float Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

    /// <summary>Gets or sets where the graph's origin is shown, in the editor's coordinates. The default is (0, 0), the top left.</summary>
    public Point Offset { get => GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }

    /// <summary>Gets or sets the smallest <see cref="Zoom"/>. The default is 0.2.</summary>
    public float MinZoom { get => GetValue(MinZoomProperty); set => SetValue(MinZoomProperty, value); }

    /// <summary>Gets or sets the largest <see cref="Zoom"/>. The default is 3.</summary>
    public float MaxZoom { get => GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }

    /// <summary>Gets or sets the width of links at zoom 1; they get thinner when zoomed out, down to 1 px. The default is 2.</summary>
    public float LinkThickness { get => GetValue(LinkThicknessProperty); set => SetValue(LinkThicknessProperty, value); }

    /// <summary>Gets or sets the color that outlines selected nodes and highlights selected links.</summary>
    public Color SelectionColor { get => GetValue(SelectionColorProperty); set => SetValue(SelectionColorProperty, value); }

    /// <summary>Gets or sets the color of nodes with an <see cref="NodeViewModel.Error"/> and of links whose types don't match.</summary>
    public Color ErrorColor { get => GetValue(ErrorColorProperty); set => SetValue(ErrorColorProperty, value); }

    /// <summary>
    /// Gets or sets what creates the control that edits an unconnected input's value next to its socket (or <c>null</c>
    /// for none); <c>null</c> (the default) uses <see cref="InputEditors.Create"/>.
    /// </summary>
    public Func<InputSocketViewModel, UIElement?>? InputEditorFactory { get => GetValue(InputEditorFactoryProperty); set => SetValue(InputEditorFactoryProperty, value); }

    /// <summary>Gets the layers drawn behind the links and nodes, back to front; a <see cref="GridLayer"/> by default.</summary>
    public ObservableCollection<NodeEditorLayer> BackgroundLayers { get; }

    /// <summary>Gets the views of the nodes, back to front.</summary>
    public IEnumerable<NodeView> NodeViews => _nodes.Children.OfType<NodeView>();

    /// <summary>Occurs when <see cref="Zoom"/> or <see cref="Offset"/> changed.</summary>
    public event EventHandler? ViewportChanged;

    /// <summary>Gets the view of <paramref name="node"/>, or <c>null</c>.</summary>
    public NodeView? GetNodeView(NodeViewModel node) => _views.GetValueOrDefault(node);

    /// <summary>Gets or sets whether moving nodes snaps them to multiples of <see cref="SnapSpacing"/>; holding Ctrl while moving does the opposite. The default is <c>false</c>.</summary>
    public bool SnapToGrid { get => GetValue(SnapToGridProperty); set => SetValue(SnapToGridProperty, value); }

    /// <summary>Gets or sets the grid moved nodes snap to, in graph units. The default is 20, the default grid's spacing.</summary>
    public float SnapSpacing { get => GetValue(SnapSpacingProperty); set => SetValue(SnapSpacingProperty, value); }

    /// <summary>Gets the element at <paramref name="viewPoint"/> (in the editor's coordinates), or <c>null</c> over the background.</summary>
    public UIElement? ElementAt(Point viewPoint)
    {
        var hit = _surface.HitTest(viewPoint);
        return hit == _surface || hit == _nodes ? null : hit;
    }

    /// <summary>Gets the node whose view is at <paramref name="viewPoint"/>, or <c>null</c>.</summary>
    public NodeViewModel? NodeAt(Point viewPoint) => NodeViewOf(ElementAt(viewPoint))?.Node;

    /// <summary>Gets the view <paramref name="element"/> is part of, or <c>null</c>.</summary>
    public static NodeView? NodeViewOf(UIElement? element)
    {
        for (VisualNode? node = element; node != null; node = node.Parent)
        {
            if (node is NodeView view) return view;
            if (node is NodeEditor) return null;
        }
        return null;
    }

    /// <summary>
    /// Gets the socket nearest to <paramref name="viewPoint"/> within <paramref name="radius"/> pixels of its anchor (and
    /// accepted by <paramref name="filter"/>), or <c>null</c>.
    /// </summary>
    public SocketViewModel? SocketAt(Point viewPoint, float radius = 10, Func<SocketViewModel, bool>? filter = null)
    {
        SocketViewModel? best = null;
        float bestDistance = radius * radius;
        foreach (var node in Graph?.Nodes ?? (IReadOnlyList<NodeViewModel>)[])
        {
            foreach (var socket in node.Inputs.Cast<SocketViewModel>().Concat(node.Outputs))
            {
                if (filter != null && !filter(socket)) continue;
                if (node.IsCollapsed && !socket.IsConnected) continue;
                var offset = GraphToView(socket.Anchor) - viewPoint;
                float distance = offset.X * offset.X + offset.Y * offset.Y;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = socket;
                }
            }
        }
        return best;
    }

    /// <summary>Gets the link nearest to <paramref name="viewPoint"/> within <paramref name="tolerance"/> pixels, or <c>null</c>.</summary>
    public LinkViewModel? LinkAt(Point viewPoint, float tolerance = 6)
    {
        LinkViewModel? best = null;
        float bestDistance = tolerance;
        foreach (var link in Graph?.Links ?? (IReadOnlyList<LinkViewModel>)[])
        {
            float distance = LinkGeometry.DistanceTo(viewPoint, GraphToView(link.From.Anchor), GraphToView(link.To.Anchor), Zoom);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = link;
            }
        }
        return best;
    }

    /// <summary>Gets the nodes whose area overlaps <paramref name="viewRect"/> (in the editor's coordinates).</summary>
    public IReadOnlyList<NodeViewModel> NodesIn(Rect viewRect)
    {
        var graphRect = new Rect(ViewToGraph(viewRect.Location), new Size(viewRect.Width / Zoom, viewRect.Height / Zoom));
        return (Graph?.Nodes ?? (IReadOnlyList<NodeViewModel>)[]).Where(n => GetNodeArea(n).IntersectsWith(graphRect)).ToList();
    }

    /// <summary>Selects only <paramref name="node"/> (none when <c>null</c>) and brings it to the front.</summary>
    public void SelectOnly(NodeViewModel? node)
    {
        Graph?.ClearSelection();
        if (node == null) return;
        node.IsSelected = true;
        Graph?.BringToFront(node);
    }

    /// <summary>
    /// Adds a node of <paramref name="type"/> with its top-left corner at <paramref name="viewPoint"/> (the view's center
    /// when <c>null</c>) and selects only it.
    /// </summary>
    /// <returns>The new node, or <c>null</c> without a graph.</returns>
    public NodeViewModel? AddNode(NodeType type, Point? viewPoint = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (Graph is not { } graph) return null;
        var node = type.CreateNode();
        node.Position = Snap(ViewToGraph(viewPoint ?? new Point(Bounds.Width / 2, Bounds.Height / 2)), SnapToGrid);
        graph.AddNode(node);
        SelectOnly(node);
        return node;
    }

    /// <summary>
    /// Opens the <see cref="AddNodeMenu"/> of the graph's catalog at the pointer; the picked node is added at
    /// <paramref name="viewPoint"/> (the view's center when <c>null</c>).
    /// </summary>
    /// <returns>The menu, or <c>null</c> without a graph.</returns>
    public AddNodeMenu? ShowAddNodeMenu(Point? viewPoint = null) => ShowAddNodeMenu(viewPoint, null);

    /// <summary>
    /// Opens the <see cref="AddNodeMenu"/> at the pointer, listing only the node types that can connect to
    /// <paramref name="connectTo"/> (when set): the picked node is added at <paramref name="viewPoint"/> and connected to
    /// it, as one undo step (see <see cref="AddConnectedNode"/>).
    /// </summary>
    /// <returns>The menu, or <c>null</c> without a graph.</returns>
    public AddNodeMenu? ShowAddNodeMenu(Point? viewPoint, SocketViewModel? connectTo)
    {
        if (Graph is not { } graph) return null;
        var at = viewPoint ?? new Point(Bounds.Width / 2, Bounds.Height / 2);
        var menu = new AddNodeMenu(graph.Catalog, (connectTo as OutputSocketViewModel)?.Type, (connectTo as InputSocketViewModel)?.Type);
        menu.TypePicked += (_, type) =>
        {
            if (connectTo != null) AddConnectedNode(type, at, connectTo);
            else AddNode(type, at);
        };
        menu.Show(this);
        return menu;
    }

    /// <summary>
    /// Adds a node of <paramref name="type"/> next to <paramref name="viewPoint"/> and connects it to
    /// <paramref name="socket"/>, as one undo step: an output feeds the node's first input that accepts it (the node goes
    /// to the right of the point), an input is fed by the node's first output it accepts (the node goes to the left).
    /// </summary>
    /// <returns>The new node, or <c>null</c> without a graph.</returns>
    public NodeViewModel? AddConnectedNode(NodeType type, Point viewPoint, SocketViewModel socket)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(socket);
        if (Graph is not { } graph) return null;
        using (graph.Undo.Group($"Add {type.Title}"))
        {
            if (AddNode(type, viewPoint) is not { } node) return null;
            var point = ViewToGraph(viewPoint);
            node.Position = new Point(socket is OutputSocketViewModel ? point.X : point.X - node.Width, point.Y - NodeView.DefaultHeaderHeight);
            switch (socket)
            {
                case OutputSocketViewModel output when node.Inputs.FirstOrDefault(i => graph.CanConnect(output, i) == ConnectResult.Ok) is { } input:
                    graph.Connect(output, input);
                    break;
                case InputSocketViewModel input when node.Outputs.FirstOrDefault(o => graph.CanConnect(o, input) == ConnectResult.Ok) is { } output:
                    graph.Connect(output, input);
                    break;
            }
            return node;
        }
    }

    /// <summary>Opens the editor's context menu (see <see cref="CreateContextMenu"/>) at the pointer.</summary>
    /// <returns>The menu, or <c>null</c> if it didn't open.</returns>
    public ContextMenu? ShowContextMenu(Point? viewPoint = null)
    {
        if (Graph == null) return null;
        var menu = CreateContextMenu(viewPoint ?? new Point(Bounds.Width / 2, Bounds.Height / 2));
        return menu.Open(this) ? menu : null;
    }

    /// <summary>
    /// Creates the context menu for <paramref name="viewPoint"/>: an "Add" submenu of the catalog's types by category
    /// (added there), then the commands for the selection, the view and undo. Override it to change the menu.
    /// </summary>
    public virtual ContextMenu CreateContextMenu(Point viewPoint)
    {
        var menu = new ContextMenu();
        if (Graph is { Catalog.Types.Count: > 0 } graph)
        {
            var add = new MenuItem("_Add") { Icon = new Icon(MaterialIconKind.AddCircle, 18) };
            foreach (var category in graph.Catalog.Categories)
            {
                var parent = add;
                if (category.Length > 0)
                {
                    parent = new MenuItem(category);
                    add.Items.Add(parent);
                }
                foreach (var type in graph.Catalog.Types.Where(t => t.Category == category))
                {
                    var item = new MenuItem(type.Title);
                    item.Click += (_, _) => AddNode(type, viewPoint);
                    parent.Items.Add(item);
                }
            }
            menu.Items.Add(add);
            menu.Items.Add(new Separator());
        }
        AddCommands(NodeEditorCommands.Copy, NodeEditorCommands.Paste);
        menu.Items.Add(new Separator());
        AddCommands(NodeEditorCommands.Delete, NodeEditorCommands.DeleteReconnect, NodeEditorCommands.Duplicate, NodeEditorCommands.ToggleCollapse, NodeEditorCommands.ToggleMute);
        menu.Items.Add(new Separator());
        AddCommands(NodeEditorCommands.SelectAll, NodeEditorCommands.FrameAll, NodeEditorCommands.FrameSelected);
        menu.Items.Add(new Separator());
        AddCommands(NodeEditorCommands.Undo, NodeEditorCommands.Redo);
        return menu;

        void AddCommands(params System.Windows.Input.ICommand[] commands)
        {
            foreach (var command in commands) menu.Items.Add(new MenuItem(null, command) { CommandParameter = this });
        }
    }

    /// <summary>Snaps <paramref name="graphPoint"/> to <see cref="SnapSpacing"/> when <paramref name="snap"/>.</summary>
    public Point Snap(Point graphPoint, bool snap)
    {
        if (!snap) return graphPoint;
        float spacing = SnapSpacing;
        return new Point(MathF.Round(graphPoint.X / spacing) * spacing, MathF.Round(graphPoint.Y / spacing) * spacing);
    }

    /// <summary>Gets the box being drawn by box selection, in the editor's coordinates, or <c>null</c>.</summary>
    public Rect? SelectionBox { get => _selectionBox; internal set { _selectionBox = value; _overlay.InvalidateVisual(); } }

    /// <summary>Gets the link a node being moved would be inserted into if dropped now (drawn highlighted), or <c>null</c>.</summary>
    public LinkViewModel? InsertTarget { get => _insertTarget; internal set { _insertTarget = value; _links.InvalidateVisual(); } }

    /// <summary>Gets the stroke being drawn by the cut or reroute tool, in the editor's coordinates, or <c>null</c>.</summary>
    public IReadOnlyList<Point>? Stroke { get => _stroke; internal set { _stroke = value; _overlay.InvalidateVisual(); } }

    // Whether the stroke cuts links (drawn in the error color) rather than adding reroute points.
    internal bool StrokeCuts { get; set; }

    /// <summary>
    /// Gets or sets whether dropping a node that has no links onto a link inserts it there (and makes room for it, moving
    /// the nodes after it to the right). The default is <c>true</c>.
    /// </summary>
    public bool AutoInsert { get => GetValue(AutoInsertProperty); set => SetValue(AutoInsertProperty, value); }

    /// <summary>
    /// Gets or sets whether dropping a new link on empty space opens the add-node menu with the node types it can
    /// connect to; the picked node is added there and connected. The default is <c>true</c>.
    /// </summary>
    public bool SearchOnLinkDrop { get => GetValue(SearchOnLinkDropProperty); set => SetValue(SearchOnLinkDropProperty, value); }

    /// <summary>Gets the socket a link is being dragged from, or <c>null</c>.</summary>
    public SocketViewModel? DraggedFrom { get; private set; }

    /// <summary>Gets where the dragged link's loose end is, in the editor's coordinates.</summary>
    public Point DraggedTo { get; private set; }

    /// <summary>Gets the socket the dragged link would connect to if dropped now, or <c>null</c>.</summary>
    public SocketViewModel? DropTarget { get; private set; }

    /// <summary>Gets the link a connect drag picked up from its input (not drawn until the drag ends), or <c>null</c>.</summary>
    public LinkViewModel? HiddenLink { get => _hiddenLink; internal set { _hiddenLink = value; _links.InvalidateVisual(); } }

    internal void ShowDraggedLink(SocketViewModel? from, Point to, SocketViewModel? target)
    {
        DraggedFrom = from;
        DraggedTo = to;
        DropTarget = target;
        _overlay.InvalidateVisual();
    }

    /// <summary>Converts a point in graph coordinates to the editor's coordinates.</summary>
    public Point GraphToView(Point graphPoint) => new(graphPoint.X * Zoom + Offset.X, graphPoint.Y * Zoom + Offset.Y);

    /// <summary>Converts a point in the editor's coordinates to graph coordinates.</summary>
    public Point ViewToGraph(Point viewPoint) => new((viewPoint.X - Offset.X) / Zoom, (viewPoint.Y - Offset.Y) / Zoom);

    /// <summary>Gets the part of the graph that is visible, in graph coordinates.</summary>
    public Rect VisibleGraphArea
    {
        get
        {
            var topLeft = ViewToGraph(Point.Zero);
            return new Rect(topLeft.X, topLeft.Y, Bounds.Width / Zoom, Bounds.Height / Zoom);
        }
    }

    /// <summary>Sets <see cref="Zoom"/> to <paramref name="zoom"/> (within the limits), keeping the graph point under <paramref name="viewPoint"/> in place.</summary>
    public void ZoomAt(Point viewPoint, float zoom)
    {
        var graphPoint = ViewToGraph(viewPoint);
        Zoom = zoom;
        Offset = new Point(viewPoint.X - graphPoint.X * Zoom, viewPoint.Y - graphPoint.Y * Zoom);
    }

    /// <summary>Moves the view by (<paramref name="dx"/>, <paramref name="dy"/>) pixels; the graph moves with the pointer.</summary>
    public void PanBy(float dx, float dy) => Offset = new Point(Offset.X + dx, Offset.Y + dy);

    /// <summary>
    /// Zooms and pans so that all nodes are visible with <paramref name="padding"/> pixels around them, zooming in no
    /// further than 1. Does nothing without nodes or before the editor has a size.
    /// </summary>
    public void FrameAll(float padding = 40) => FrameNodes(Graph?.Nodes ?? Enumerable.Empty<NodeViewModel>(), padding);

    /// <summary>Zooms and pans so that <paramref name="nodes"/> are visible; see <see cref="FrameAll"/>.</summary>
    public void FrameNodes(IEnumerable<NodeViewModel> nodes, float padding = 40)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
        foreach (var node in nodes)
        {
            var area = GetNodeArea(node);
            left = Math.Min(left, area.Left);
            top = Math.Min(top, area.Top);
            right = Math.Max(right, area.Right);
            bottom = Math.Max(bottom, area.Bottom);
        }
        if (left > right || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        float width = Math.Max(1, right - left), height = Math.Max(1, bottom - top);
        float zoom = Math.Min((Bounds.Width - 2 * padding) / width, (Bounds.Height - 2 * padding) / height);
        Zoom = Math.Min(1f, zoom);
        var center = new Point((left + right) * 0.5f, (top + bottom) * 0.5f);
        Offset = new Point(Bounds.Width * 0.5f - center.X * Zoom, Bounds.Height * 0.5f - center.Y * Zoom);
    }

    /// <summary>Gets the area <paramref name="node"/> covers in graph coordinates (its body, once its view was measured).</summary>
    public Rect GetNodeArea(NodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        float height = GetNodeView(node) is { } view && view.DesiredSize.Height > 0 ? view.DesiredSize.Height : NodeView.DefaultHeaderHeight;
        return new Rect(node.Position.X, node.Position.Y, node.Width, height);
    }

    /// <summary>
    /// Gets where the pointer is over the editor, in its coordinates, or <c>null</c> when it isn't over it. Commands such
    /// as zooming use it.
    /// </summary>
    public Point? PointerPosition { get; private set; }

    /// <inheritdoc/>
    /// <remarks>Takes the focus (for the editor's shortcuts) unless an element inside takes it.</remarks>
    public override void OnPreviewPointerPressed(PointerEventArgs e)
    {
        PointerPosition = e.Position;
        base.OnPreviewPointerPressed(e);
        if (!IsFocused) Focus();
    }

    /// <inheritdoc/>
    public override void OnPreviewPointerMoved(PointerEventArgs e)
    {
        PointerPosition = e.Position;
        base.OnPreviewPointerMoved(e);
    }

    /// <inheritdoc/>
    public override void OnPreviewPointerWheel(PointerWheelEventArgs e)
    {
        PointerPosition = e.Position;
        base.OnPreviewPointerWheel(e);
    }

    /// <inheritdoc/>
    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsPointerCaptured) PointerPosition = null;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        return new Size(
            float.IsInfinity(availableSize.Width) ? DefaultSize.Width : availableSize.Width,
            float.IsInfinity(availableSize.Height) ? DefaultSize.Height : availableSize.Height);
    }

    // Called by node views when their sockets' anchors moved.
    internal void InvalidateLinks() => _links.InvalidateVisual();

    // Called by node views when their node moved or was resized.
    internal void InvalidateNodePlacement()
    {
        _nodes.InvalidateArrange();
        _links.InvalidateVisual();
    }

    private void OnViewportChanged()
    {
        _nodes.InvalidateArrange();
        _links.InvalidateVisual();
        foreach (var layer in BackgroundLayers) layer.InvalidateVisual();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncLayers()
    {
        foreach (var old in _surface.Children.OfType<NodeEditorLayer>().ToList())
        {
            if (!BackgroundLayers.Contains(old))
            {
                old.Editor = null;
                _surface.RemoveChild(old);
            }
        }
        for (int i = 0; i < BackgroundLayers.Count; i++)
        {
            var layer = BackgroundLayers[i];
            if (_surface.Children.Count > i && _surface.Children[i] == layer) continue;
            if (layer.Parent == _surface) _surface.RemoveChild(layer);
            else if (layer.Editor != null) throw new InvalidOperationException("A layer can only be in one node editor.");
            layer.Editor = this;
            _surface.InsertChild(i, layer);
        }
    }

    private void OnGraphChanged(NodeGraphViewModel? oldGraph, NodeGraphViewModel? newGraph)
    {
        if (oldGraph != null)
        {
            ((INotifyCollectionChanged)oldGraph.Nodes).CollectionChanged -= OnNodesChanged;
            ((INotifyCollectionChanged)oldGraph.Links).CollectionChanged -= OnLinksChanged;
        }
        if (newGraph != null)
        {
            ((INotifyCollectionChanged)newGraph.Nodes).CollectionChanged += OnNodesChanged;
            ((INotifyCollectionChanged)newGraph.Links).CollectionChanged += OnLinksChanged;
        }
        SyncNodes();
        _links.InvalidateVisual();
    }

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncNodes();

    private void OnLinksChanged(object? sender, NotifyCollectionChangedEventArgs e) => _links.InvalidateVisual();

    private void RecreateNodeViews()
    {
        foreach (var view in _views.Values) _nodes.RemoveChild(view);
        _views.Clear();
        SyncNodes();
    }

    // Keeps one view per node, in the graph's order.
    private void SyncNodes()
    {
        var nodes = Graph?.Nodes ?? (IReadOnlyList<NodeViewModel>)[];
        var wanted = nodes.ToHashSet();
        foreach (var (node, view) in _views.ToList())
        {
            if (wanted.Contains(node)) continue;
            _nodes.RemoveChild(view);
            _views.Remove(node);
        }
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (!_views.TryGetValue(node, out var view))
            {
                view = new NodeView(node, this);
                _views[node] = view;
            }
            if (_nodes.Children.Count > i && _nodes.Children[i] == view) continue;
            if (view.Parent == _nodes) _nodes.RemoveChild(view);
            _nodes.InsertChild(i, view);
        }
        _links.InvalidateVisual();
    }

    // Holds the layers, the links and the nodes, back to front, all filling the editor.
    private sealed class Surface : UIElement
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            base.MeasureOverride(availableSize);
            return Size.Zero;
        }
    }

    // Places the node views at their positions, scaled by the zoom.
    private sealed class NodeLayer(NodeEditor editor) : UIElement
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var child in Children)
            {
                if (child is UIElement element) element.Measure(Size.Infinity);
            }
            return Size.Zero;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            float zoom = editor.Zoom;
            var scale = Matrix3x2.CreateScale(zoom);
            foreach (var child in Children)
            {
                if (child is not NodeView view) continue;
                var position = editor.GraphToView(view.Node.Position);
                if (view.RenderTransform != scale) view.RenderTransform = scale;
                view.Arrange(new Rect(position.X - NodeView.SocketOverhang * zoom, position.Y, view.DesiredSize.Width, view.DesiredSize.Height));
            }
            return finalSize;
        }
    }
}
