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
/// Dragging with the middle pointer button pans the view, and the wheel zooms around the pointer.
/// </para>
/// </remarks>
public class NodeEditor : Control
{
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

    /// <summary>Identifies the <see cref="InputEditorFactory"/> property.</summary>
    public static readonly BindableProperty<Func<InputSocketViewModel, UIElement?>?> InputEditorFactoryProperty =
        BindableProperty.Register<NodeEditor, Func<InputSocketViewModel, UIElement?>?>(nameof(InputEditorFactory), null,
            (s, o, n) => ((NodeEditor)s).RecreateNodeViews());

    private const float WheelZoomStep = 1.1f;
    private static readonly Size DefaultSize = new(400, 300);

    private readonly LinkLayer _links;
    private readonly NodeLayer _nodes;
    private readonly Dictionary<NodeViewModel, NodeView> _views = [];
    private bool _isPanning;
    private Point _lastPanPosition;

    static NodeEditor()
    {
        NodeEditorTheme.Register();
        IsFocusableProperty.OverrideDefaultValue<NodeEditor>(true);
        ClipToBoundsProperty.OverrideDefaultValue<NodeEditor>(true);
    }

    /// <summary>Initializes an editor with a <see cref="GridLayer"/> background.</summary>
    public NodeEditor()
    {
        _links = new LinkLayer(this);
        _nodes = new NodeLayer(this);
        AddChild(_links);
        AddChild(_nodes);
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

    /// <inheritdoc/>
    /// <remarks>Dragging with the middle button pans the view.</remarks>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled) return;

        if (e.Button == PointerButtons.Middle)
        {
            CapturePointer();
            _isPanning = true;
            _lastPanPosition = e.ScreenPosition;
            e.Handled = true;
        }
        Focus();
    }

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isPanning) return;

        // Screen pixels are view pixels as long as the editor itself isn't scaled.
        var delta = e.ScreenPosition - _lastPanPosition;
        _lastPanPosition = e.ScreenPosition;
        PanBy(delta.X, delta.Y);
        e.Handled = true;
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isPanning || e.Button != PointerButtons.Middle) return;

        e.Handled = true;
        if (IsPointerCaptured) ReleasePointerCapture();
        _isPanning = false;
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        _isPanning = false;
    }

    /// <inheritdoc/>
    /// <remarks>Zooms around the pointer, by 10% per wheel notch.</remarks>
    public override void OnPointerWheel(PointerWheelEventArgs e)
    {
        base.OnPointerWheel(e);
        if (e.Handled || e.DeltaY == 0) return;

        ZoomAt(e.Position, Zoom * MathF.Pow(WheelZoomStep, e.DeltaY));
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            if (child is UIElement element) element.Measure(availableSize);
        }
        return new Size(
            float.IsInfinity(availableSize.Width) ? DefaultSize.Width : availableSize.Width,
            float.IsInfinity(availableSize.Height) ? DefaultSize.Height : availableSize.Height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (child is UIElement element) element.Arrange(new Rect(Point.Zero, finalSize));
        }
        return finalSize;
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
        foreach (var old in Children.OfType<NodeEditorLayer>().ToList())
        {
            if (!BackgroundLayers.Contains(old))
            {
                old.Editor = null;
                RemoveChild(old);
            }
        }
        for (int i = 0; i < BackgroundLayers.Count; i++)
        {
            var layer = BackgroundLayers[i];
            if (Children.Count > i && Children[i] == layer) continue;
            if (layer.Parent == this) RemoveChild(layer);
            else if (layer.Editor != null) throw new InvalidOperationException("A layer can only be in one node editor.");
            layer.Editor = this;
            InsertChild(i, layer);
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
