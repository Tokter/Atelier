using System;
using System.Collections.Generic;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Threading;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>What an <see cref="AreaLayout"/> previews while areas are being split, joined or swapped.</summary>
public enum AreaPreviewKind
{
    /// <summary>Nothing would change.</summary>
    None,

    /// <summary><see cref="AreaLayout.PreviewArea"/> would be split at <see cref="AreaLayout.PreviewSplitLine"/>.</summary>
    Split,

    /// <summary><see cref="AreaLayout.PreviewTarget"/> would be joined into <see cref="AreaLayout.PreviewArea"/>.</summary>
    Join,

    /// <summary><see cref="AreaLayout.PreviewArea"/> and <see cref="AreaLayout.PreviewTarget"/> would swap places.</summary>
    Swap,
}

/// <summary>
/// Divides its space into <see cref="Area"/>s, each showing an editor chosen from <see cref="Editors"/>, which the user
/// can resize, split, join, swap, maximize and close as in Blender's window system. A <see cref="Workspace"/> is a
/// named area layout.
/// </summary>
/// <remarks>
/// <para>
/// The areas form a tree: the <see cref="Root"/> is an area or an <see cref="AreaSplit"/> of areas side by side or
/// stacked, which can contain further splits. Areas are separated by <see cref="Spacing"/> pixels; the gaps are
/// <see cref="AreaBorder"/>s.
/// </para>
/// <para>Interaction, as in Blender:</para>
/// <list type="bullet">
/// <item>Drag a border to resize the areas on both sides (Escape cancels).</item>
/// <item>
/// Drag from an area's corner (an <see cref="AreaCorner"/>) into the area to split it: the first movement decides the
/// direction (sideways splits it side by side, up or down stacks it) and the split line follows the pointer. Drag the
/// corner into a neighbor instead to join the neighbor into the area (only areas that share a whole edge can be
/// joined). Hold Ctrl and drop on another area to swap the two. Escape or a right-click cancels.
/// </item>
/// <item>
/// Right-click a border for the Area Options: Vertical Split and Horizontal Split (then click where the line goes),
/// Join Areas (then click the area to keep) and Swap Areas.
/// </item>
/// <item>
/// Right-click an area's header for the area menu: split, Maximize Area (Ctrl+Space toggles it for the area under
/// the pointer) and Close Area (its neighbor takes its space).
/// </item>
/// </list>
/// <para>
/// A new area from a split shows the same editor type as the area it came from, with new content. Each area keeps the
/// content of the editors it showed (see <see cref="Area"/>). <see cref="LayoutChanged"/> reports every change the
/// user makes; <see cref="ToDefinition"/> describes the arrangement for saving or duplicating it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var layout = new AreaLayout(editors, AreaDefinition.Row(
///     AreaDefinition.Editor("viewport", 3),
///     AreaDefinition.Column(AreaDefinition.Editor("outliner"), AreaDefinition.Editor("properties", 2))));
/// </code>
/// </example>
public class AreaLayout : Control
{
    /// <summary>Identifies the <see cref="Spacing"/> property.</summary>
    public static readonly BindableProperty<float> SpacingProperty =
        BindableProperty.Register<AreaLayout, float>(nameof(Spacing), DefaultSpacing, (s, o, n) => ((AreaLayout)s).OnSpacingChanged(),
            validateValue: static v => float.IsFinite(v) && v >= 0);

    /// <summary>The default gap between areas (and around them), in pixels.</summary>
    public const float DefaultSpacing = 4f;

    /// <summary>The smallest width of an area, in pixels.</summary>
    public const float MinAreaWidth = 64f;

    /// <summary>The smallest height of an area, in pixels: its header.</summary>
    public const float MinAreaHeight = Area.HeaderHeight;

    /// <summary>How far the pointer must move from an area's corner before a split starts.</summary>
    public const float DragThreshold = 6f;

    private enum Interaction { None, Corner, PickSplit, PickJoin }

    private readonly List<Area> _areaBuffer = [];
    private UIElement? _root;
    private Area? _maximized;
    private Border? _maximizedPlaceholder;

    private Interaction _interaction;
    private AreaCorner? _corner;
    private Point _pressPoint;
    private Orientation? _cornerOrientation;
    private Orientation _pickOrientation;
    private Area? _joinFirst, _joinSecond;
    private bool _swallowRightRelease;
    private Point? _lastPointer;
    private AreaBorder? _activeBorder;

    /// <summary>Initializes a layout with one area showing the first registered editor type (if any).</summary>
    /// <param name="editors">The editor types the areas can show.</param>
    public AreaLayout(AreaEditorRegistry editors) : this(editors, null)
    {
    }

    /// <summary>Initializes a layout with the areas of <paramref name="definition"/>.</summary>
    /// <param name="editors">The editor types the areas can show.</param>
    /// <param name="definition">The arrangement; <c>null</c> for one area with the first registered editor type.</param>
    public AreaLayout(AreaEditorRegistry editors, AreaDefinition? definition)
    {
        Editors = editors ?? throw new ArgumentNullException(nameof(editors));
        Load(definition ?? DefaultDefinition(editors));
    }

    /// <summary>Gets a definition of one area showing the first editor type of <paramref name="editors"/>.</summary>
    public static AreaDefinition DefaultDefinition(AreaEditorRegistry editors) =>
        AreaDefinition.Editor(editors.Editors.Count > 0 ? editors.Editors[0].Id : string.Empty);

    #region Properties

    /// <summary>Gets the editor types the areas can show.</summary>
    public AreaEditorRegistry Editors { get; }

    /// <summary>Gets or sets the gap between areas and around them, in pixels. The default is <see cref="DefaultSpacing"/>.</summary>
    public float Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Gets the root of the area tree: the only <see cref="Area"/>, or an <see cref="AreaSplit"/>.</summary>
    public UIElement? Root => _root;

    /// <summary>Gets the area that fills the layout (see <see cref="Maximize"/>), or <c>null</c>.</summary>
    public Area? MaximizedArea => _maximized;

    /// <summary>Gets the workspace the layout belongs to, or <c>null</c>.</summary>
    public Workspace? Workspace { get; internal set; }

    /// <summary>Gets the area under the pointer (the area Ctrl+Space maximizes), or <c>null</c>.</summary>
    public Area? HoveredArea { get; private set; }

    /// <summary>Gets the areas, left to right and top to bottom through the tree.</summary>
    public IReadOnlyList<Area> Areas
    {
        get
        {
            var areas = new List<Area>();
            CollectAreas(_root, areas);
            return areas;
        }
    }

    /// <summary>Gets whether a corner is being dragged or an interactive split or join from a menu is waiting for a click.</summary>
    public bool IsInteracting => _interaction != Interaction.None;

    /// <summary>Gets what the current interaction would do when it ends.</summary>
    public AreaPreviewKind PreviewKind { get; private set; }

    /// <summary>Gets the area that would be split, that would grow by a join, or that would be swapped; or <c>null</c>.</summary>
    public Area? PreviewArea { get; private set; }

    /// <summary>Gets the area that would be joined into <see cref="PreviewArea"/> or swapped with it, or <c>null</c>.</summary>
    public Area? PreviewTarget { get; private set; }

    /// <summary>Gets how a previewed split would arrange the two areas.</summary>
    public Orientation PreviewOrientation { get; private set; }

    /// <summary>Gets the share of the first (left or top) of the two areas of a previewed split.</summary>
    public float PreviewRatio { get; private set; }

    /// <summary>Gets whether the new area of a previewed split would be the first (left or top) of the two.</summary>
    public bool PreviewNewAreaFirst { get; private set; }

    /// <summary>Gets the bounds of <see cref="PreviewArea"/> in the layout's coordinates.</summary>
    public Rect PreviewAreaBounds { get; private set; }

    /// <summary>Gets the bounds of <see cref="PreviewTarget"/> in the layout's coordinates.</summary>
    public Rect PreviewTargetBounds { get; private set; }

    /// <summary>Gets the line of a previewed split, 2 px wide, in the layout's coordinates.</summary>
    public Rect PreviewSplitLine { get; private set; }

    /// <summary>Gets the direction from <see cref="PreviewArea"/> to the area a previewed join removes.</summary>
    public Dock PreviewDirection { get; private set; }

    /// <summary>Occurs after the user (or code) changed the arrangement, a size or an area's editor.</summary>
    public event EventHandler? LayoutChanged;

    #endregion

    #region Tree

    /// <summary>Replaces the areas with those of <paramref name="definition"/>; the content of the old areas is released.</summary>
    public void Load(AreaDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        CancelInteraction();
        RestoreMaximized();
        ReleaseAll();
        SetRoot(Build(definition));
    }

    /// <summary>Describes the current arrangement, sizes and editors, for saving or duplicating the layout.</summary>
    public AreaDefinition ToDefinition() => _root == null ? DefaultDefinition(Editors) : Define(_root, 1f);

    private AreaDefinition Define(UIElement node, float weight)
    {
        if (node == _maximizedPlaceholder && _maximized != null) node = _maximized;
        if (node is AreaSplit split)
        {
            var children = new List<AreaDefinition>(split.Nodes.Count);
            for (int i = 0; i < split.Nodes.Count; i++)
            {
                children.Add(Define(split.Nodes[i], split.GetWeight(i)));
            }
            return new SplitAreaDefinition(split.Orientation, children) { Weight = weight };
        }
        return new EditorAreaDefinition(((Area)node).EditorId ?? string.Empty) { Weight = weight };
    }

    private static UIElement Build(AreaDefinition definition)
    {
        switch (definition)
        {
            case EditorAreaDefinition area:
                return new Area(area.EditorId);
            case SplitAreaDefinition { Children.Count: 0 }:
                throw new ArgumentException("A split needs at least one child.", nameof(definition));
            case SplitAreaDefinition { Children.Count: 1 } single:
                return Build(single.Children[0]);
            case SplitAreaDefinition splitDefinition:
                var split = new AreaSplit(splitDefinition.Orientation);
                foreach (var child in splitDefinition.Children)
                {
                    var node = Build(child);
                    float weight = float.IsFinite(child.Weight) && child.Weight > 0 ? child.Weight : 1f;
                    if (node is AreaSplit inner && inner.Orientation == split.Orientation)
                    {
                        MoveNodesInto(inner, split, split.Nodes.Count, weight);
                    }
                    else
                    {
                        split.InsertNode(split.Nodes.Count, node, weight);
                    }
                }
                return split;
            default:
                throw new ArgumentException($"Unknown area definition {definition.GetType().Name}.", nameof(definition));
        }
    }

    // Moves the nodes of `from` into `to` at `index`, sharing `weight` by their weights.
    private static void MoveNodesInto(AreaSplit from, AreaSplit to, int index, float weight)
    {
        float total = 0;
        for (int i = 0; i < from.Nodes.Count; i++) total += from.GetWeight(i);
        while (from.Nodes.Count > 0)
        {
            var node = from.Nodes[0];
            float share = total > 0 ? weight * from.GetWeight(0) / total : weight / Math.Max(1, from.Nodes.Count);
            from.RemoveNodeAt(0);
            to.InsertNode(index++, node, share);
        }
    }

    private void SetRoot(UIElement? root)
    {
        if (_root != null && _root.Parent == this) RemoveChild(_root);
        _root = root;
        if (root != null)
        {
            if (root.Parent is VisualNode parent) parent.RemoveChild(root);
            AddChild(root);
            Adopt(root);
        }
        InvalidateMeasure();
    }

    private void Adopt(UIElement node)
    {
        switch (node)
        {
            case Area area:
                area.SetLayout(this);
                break;
            case AreaSplit split:
                split.Layout = this;
                foreach (var child in split.Nodes) Adopt(child);
                break;
        }
    }

    private void CollectAreas(UIElement? node, List<Area> areas)
    {
        if (node == _maximizedPlaceholder && _maximized != null) node = _maximized;
        switch (node)
        {
            case Area area:
                areas.Add(area);
                break;
            case AreaSplit split:
                foreach (var child in split.Nodes) CollectAreas(child, areas);
                break;
        }
    }

    // Puts `replacement` where `node` is in the tree (with its weight), detaching `node`.
    private void ReplaceNode(UIElement node, UIElement replacement)
    {
        if (node.Parent is AreaSplit parent)
        {
            parent.ReplaceNodeAt(parent.IndexOf(node), replacement);
            Adopt(replacement);
        }
        else
        {
            SetRoot(replacement);
        }
    }

    // After a node left `split`: a split with one node left is replaced by that node, and a split that ends up in a
    // split of the same orientation gives its nodes to it.
    private void Collapse(AreaSplit split)
    {
        if (split.Nodes.Count != 1) return;
        var only = split.Nodes[0];
        split.RemoveNodeAt(0);
        ReplaceNode(split, only);
        if (only is AreaSplit inner && inner.Parent is AreaSplit outer && outer.Orientation == inner.Orientation)
        {
            int index = outer.IndexOf(inner);
            float weight = outer.GetWeight(index);
            outer.RemoveNodeAt(index);
            MoveNodesInto(inner, outer, index, weight);
            Adopt(outer);
        }
    }

    private void ReleaseAll()
    {
        _areaBuffer.Clear();
        CollectAreas(_root, _areaBuffer);
        foreach (var area in _areaBuffer)
        {
            area.ReleaseEditors();
            area.SetLayout(null);
        }
        _areaBuffer.Clear();
    }

    /// <summary>Releases the content of all areas (disposing what can be disposed) and removes them; used when a workspace is deleted.</summary>
    public void Clear()
    {
        CancelInteraction();
        RestoreMaximized();
        ReleaseAll();
        SetRoot(null);
    }

    internal void OnAreaChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

    private void OnSpacingChanged()
    {
        InvalidateSplits(_root);
        InvalidateMeasure();
    }

    private static void InvalidateSplits(UIElement? node)
    {
        if (node is not AreaSplit split) return;
        split.InvalidateMeasure();
        foreach (var child in split.Nodes) InvalidateSplits(child);
    }

    #endregion

    #region Operations

    /// <summary>
    /// Splits <paramref name="area"/> in two: side by side (<see cref="Orientation.Horizontal"/>, Blender's vertical
    /// split) or stacked (<see cref="Orientation.Vertical"/>). The new area shows <paramref name="editorId"/>, or the
    /// same editor type as <paramref name="area"/>.
    /// </summary>
    /// <param name="area">The area to split.</param>
    /// <param name="orientation">How the two areas are arranged.</param>
    /// <param name="ratio">The share of the first (left or top) of the two, between 0 and 1.</param>
    /// <param name="newAreaFirst">Whether the new area is the first (left or top) of the two.</param>
    /// <param name="editorId">The editor type of the new area; <c>null</c> for the same as <paramref name="area"/>.</param>
    /// <returns>The new area.</returns>
    public Area Split(Area area, Orientation orientation, float ratio = 0.5f, bool newAreaFirst = false, string? editorId = null)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (area.Layout != this) throw new ArgumentException("The area isn't in this layout.", nameof(area));
        RestoreMaximized();
        ratio = Math.Clamp(float.IsFinite(ratio) ? ratio : 0.5f, 0.01f, 0.99f);

        var created = new Area(editorId ?? area.EditorId);
        float firstShare = ratio, secondShare = 1 - ratio;
        if (area.Parent is AreaSplit parent && parent.Orientation == orientation)
        {
            int index = parent.IndexOf(area);
            float weight = parent.GetWeight(index);
            parent.SetWeight(index, weight * (newAreaFirst ? secondShare : firstShare));
            parent.InsertNode(newAreaFirst ? index : index + 1, created, weight * (newAreaFirst ? firstShare : secondShare));
            Adopt(created);
        }
        else
        {
            var split = new AreaSplit(orientation);
            ReplaceNode(area, split);
            split.InsertNode(0, newAreaFirst ? created : area, firstShare);
            split.InsertNode(1, newAreaFirst ? area : created, secondShare);
            Adopt(split);
        }
        OnAreaChanged();
        return created;
    }

    /// <summary>
    /// Gets whether <paramref name="remove"/> can be joined into <paramref name="keep"/>: they are neighbors in the same
    /// split, so they share a whole edge.
    /// </summary>
    public bool CanJoin(Area keep, Area remove) =>
        keep != remove && keep.Parent is AreaSplit parent && remove.Parent == parent &&
        Math.Abs(parent.IndexOf(keep) - parent.IndexOf(remove)) == 1 && keep.Layout == this;

    /// <summary>Joins <paramref name="remove"/> into <paramref name="keep"/>: <paramref name="keep"/> takes its space and it closes.</summary>
    /// <returns><c>true</c> if the areas were joined (see <see cref="CanJoin"/>).</returns>
    public bool Join(Area keep, Area remove)
    {
        ArgumentNullException.ThrowIfNull(keep);
        ArgumentNullException.ThrowIfNull(remove);
        RestoreMaximized();
        if (!CanJoin(keep, remove)) return false;

        var parent = (AreaSplit)keep.Parent!;
        int keepIndex = parent.IndexOf(keep), removeIndex = parent.IndexOf(remove);
        parent.SetWeight(keepIndex, parent.GetWeight(keepIndex) + parent.GetWeight(removeIndex));
        parent.RemoveNodeAt(removeIndex);
        remove.ReleaseEditors();
        remove.SetLayout(null);
        Collapse(parent);
        OnAreaChanged();
        return true;
    }

    /// <summary>Swaps the places (and sizes) of two areas.</summary>
    /// <returns><c>true</c> if they were swapped.</returns>
    public bool Swap(Area first, Area second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        RestoreMaximized();
        if (first == second || first.Layout != this || second.Layout != this ||
            first.Parent is not AreaSplit firstParent || second.Parent is not AreaSplit secondParent)
        {
            return false;
        }

        int firstIndex = firstParent.IndexOf(first), secondIndex = secondParent.IndexOf(second);
        if (firstParent == secondParent)
        {
            firstParent.SwapNodes(firstIndex, secondIndex);
        }
        else
        {
            var placeholder = new Border();
            firstParent.ReplaceNodeAt(firstIndex, placeholder);
            secondParent.ReplaceNodeAt(secondIndex, first);
            firstParent.ReplaceNodeAt(firstIndex, second);
        }
        OnAreaChanged();
        return true;
    }

    /// <summary>Gets whether <paramref name="area"/> can close: it isn't the only area.</summary>
    public bool CanClose(Area area) => area.Layout == this && area.Parent is AreaSplit;

    /// <summary>Closes <paramref name="area"/>: its neighbor before it (or after it, for the first) takes its space.</summary>
    /// <returns><c>true</c> if the area closed (see <see cref="CanClose"/>).</returns>
    public bool Close(Area area)
    {
        ArgumentNullException.ThrowIfNull(area);
        RestoreMaximized();
        if (!CanClose(area)) return false;

        var parent = (AreaSplit)area.Parent!;
        int index = parent.IndexOf(area);
        int neighbor = index > 0 ? index - 1 : index + 1;
        parent.SetWeight(neighbor, parent.GetWeight(neighbor) + parent.GetWeight(index));
        parent.RemoveNodeAt(index);
        area.ReleaseEditors();
        area.SetLayout(null);
        Collapse(parent);
        OnAreaChanged();
        return true;
    }

    /// <summary>
    /// Makes <paramref name="area"/> fill the layout, hiding the others, like Blender's Maximize Area (Ctrl+Space);
    /// <see cref="RestoreMaximized"/> brings them back. Splitting, joining, swapping and closing restore it first.
    /// </summary>
    public void Maximize(Area area)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (area.Layout != this || _maximized == area) return;
        CancelInteraction();
        RestoreMaximized();

        _maximized = area;
        if (area.Parent is AreaSplit parent)
        {
            _maximizedPlaceholder = new Border();
            parent.ReplaceNodeAt(parent.IndexOf(area), _maximizedPlaceholder);
            if (_root != null) _root.Visibility = Visibility.Collapsed;
            AddChild(area);
        }
        SetCornersEnabled(false);
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Shows all areas again after <see cref="Maximize"/>.</summary>
    public void RestoreMaximized()
    {
        if (_maximized is not { } area) return;
        SetCornersEnabled(true);
        if (_maximizedPlaceholder?.Parent is AreaSplit parent)
        {
            RemoveChild(area);
            parent.ReplaceNodeAt(parent.IndexOf(_maximizedPlaceholder), area);
            if (_root != null) _root.Visibility = Visibility.Visible;
        }
        _maximized = null;
        _maximizedPlaceholder = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Maximizes <paramref name="area"/>, or restores the layout while it is maximized.</summary>
    public void ToggleMaximize(Area area)
    {
        if (_maximized != null) RestoreMaximized();
        else Maximize(area);
    }

    private void SetCornersEnabled(bool enabled)
    {
        _areaBuffer.Clear();
        CollectAreas(_root, _areaBuffer);
        foreach (var area in _areaBuffer)
        {
            foreach (var corner in area.Corners) corner.IsHitTestVisible = enabled;
        }
        _areaBuffer.Clear();
    }

    #endregion

    #region Geometry

    /// <summary>Gets the bounds of <paramref name="element"/> (in this layout) in the layout's coordinates.</summary>
    public Rect GetBoundsInLayout(UIElement element)
    {
        var topLeft = PointToClient(element.PointToScreen(Point.Zero));
        return new Rect(topLeft.X, topLeft.Y, element.Bounds.Width, element.Bounds.Height);
    }

    /// <summary>Gets the area at <paramref name="point"/> (in the layout's coordinates), or <c>null</c> in a gap.</summary>
    public Area? AreaAt(Point point)
    {
        _areaBuffer.Clear();
        if (_maximized != null) _areaBuffer.Add(_maximized);
        else CollectAreas(_root, _areaBuffer);
        Area? found = null;
        foreach (var area in _areaBuffer)
        {
            if (GetBoundsInLayout(area).Contains(point))
            {
                found = area;
                break;
            }
        }
        _areaBuffer.Clear();
        return found;
    }

    #endregion

    #region Interaction

    internal bool BeginCornerDrag(AreaCorner corner, Point screenPosition)
    {
        if (_interaction != Interaction.None || _maximized != null) return false;
        _interaction = Interaction.Corner;
        _corner = corner;
        corner.IsDragging = true;
        _pressPoint = PointToClient(screenPosition);
        _cornerOrientation = null;
        ClearPreview();
        return true;
    }

    internal void UpdateCornerDrag(Point screenPosition, ModifierKeys modifiers)
    {
        if (_interaction != Interaction.Corner || _corner == null) return;
        var source = _corner.Area;
        var point = PointToClient(screenPosition);

        if ((modifiers & ModifierKeys.Control) != 0)
        {
            // Ctrl: swap with the area under the pointer.
            if (AreaAt(point) is { } target && target != source) SetSwapPreview(source, target);
            else ClearPreview();
            return;
        }

        var bounds = GetBoundsInLayout(source);
        if (bounds.Contains(point))
        {
            if (_cornerOrientation == null)
            {
                float dx = point.X - _pressPoint.X, dy = point.Y - _pressPoint.Y;
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < DragThreshold)
                {
                    ClearPreview();
                    return;
                }
                _cornerOrientation = Math.Abs(dx) >= Math.Abs(dy) ? Orientation.Horizontal : Orientation.Vertical;
            }

            // The new area is on the corner's side.
            var position = _corner.Position;
            bool newFirst = _cornerOrientation == Orientation.Horizontal
                ? position is AreaCornerPosition.TopLeft or AreaCornerPosition.BottomLeft
                : position is AreaCornerPosition.TopLeft or AreaCornerPosition.TopRight;
            if (!SetSplitPreview(source, _cornerOrientation.Value, point, newFirst)) ClearPreview();
            return;
        }

        // Outside the area: join the neighbor under the pointer, if it shares a whole edge. Coming back decides the
        // split direction anew.
        _cornerOrientation = null;
        if (AreaAt(point) is { } neighbor && CanJoin(source, neighbor)) SetJoinPreview(source, neighbor);
        else ClearPreview();
    }

    internal void EndCornerDrag(Point screenPosition, ModifierKeys modifiers, bool commit)
    {
        if (_interaction != Interaction.Corner) return;
        if (commit) UpdateCornerDrag(screenPosition, modifiers);
        var preview = CapturePreview();
        EndInteraction();
        if (commit) Apply(preview);
    }

    /// <summary>
    /// Starts an interactive split, as Blender's Vertical Split and Horizontal Split from a menu do: the split line
    /// follows the pointer over the area under it, a click splits there, and Escape or a right-click cancels.
    /// </summary>
    /// <param name="orientation">
    /// <see cref="Orientation.Horizontal"/> for areas side by side (a vertical split line), <see cref="Orientation.Vertical"/>
    /// for stacked areas.
    /// </param>
    public void BeginInteractiveSplit(Orientation orientation)
    {
        CancelInteraction();
        RestoreMaximized();
        _interaction = Interaction.PickSplit;
        _pickOrientation = orientation;
        if (_lastPointer is { } point) UpdatePick(point);
        TakeKeyboardFocus();
    }

    /// <summary>
    /// Starts an interactive join of two neighbors, as Blender's Join Areas from the Area Options does: pointing at one of
    /// them previews it taking the other's space, a click joins, and Escape or a right-click cancels.
    /// </summary>
    /// <returns><c>false</c> if the areas can't be joined.</returns>
    public bool BeginInteractiveJoin(Area first, Area second)
    {
        if (!CanJoin(first, second)) return false;
        CancelInteraction();
        _interaction = Interaction.PickJoin;
        _joinFirst = first;
        _joinSecond = second;
        if (_lastPointer is { } point) UpdatePick(point);
        TakeKeyboardFocus();
        return true;
    }

    /// <summary>Cancels a corner drag, a border drag, or an interactive split or join.</summary>
    public void CancelInteraction()
    {
        _activeBorder?.CancelDrag();
        if (_interaction == Interaction.None) return;
        var corner = _corner;
        EndInteraction();
        if (corner?.IsPointerCaptured == true) corner.ReleasePointerCapture();
    }

    private void EndInteraction()
    {
        if (_corner != null) _corner.IsDragging = false;
        _interaction = Interaction.None;
        _corner = null;
        _joinFirst = _joinSecond = null;
        _cornerOrientation = null;
        ClearPreview();
        IsFocusable = false;
    }

    // Esc must reach the layout while it waits for a click; the menu that started it gives the focus back first.
    private void TakeKeyboardFocus()
    {
        IsFocusable = true;
        Dispatcher.Post(() =>
        {
            if (_interaction != Interaction.None) Focus();
        });
    }

    internal void SetActiveBorder(AreaBorder? border) => _activeBorder = border;

    private void UpdatePick(Point point)
    {
        switch (_interaction)
        {
            case Interaction.PickSplit:
                if (AreaAt(point) is not { } area || !SetSplitPreview(area, _pickOrientation, point, newAreaFirst: false)) ClearPreview();
                break;
            case Interaction.PickJoin:
                var over = AreaAt(point);
                if (over == _joinFirst && _joinSecond != null) SetJoinPreview(_joinFirst!, _joinSecond);
                else if (over == _joinSecond && _joinFirst != null) SetJoinPreview(_joinSecond!, _joinFirst);
                else ClearPreview();
                break;
        }
    }

    /// <inheritdoc/>
    public override void OnPreviewPointerMoved(PointerEventArgs e)
    {
        base.OnPreviewPointerMoved(e);
        var point = PointToClient(e.ScreenPosition);
        _lastPointer = point;
        HoveredArea = AreaAt(point);
        if (_interaction is Interaction.PickSplit or Interaction.PickJoin) UpdatePick(point);
    }

    /// <inheritdoc/>
    public override void OnPreviewPointerPressed(PointerEventArgs e)
    {
        base.OnPreviewPointerPressed(e);
        if (_interaction == Interaction.None || e.Handled) return;

        if (e.Button == PointerButtons.Right)
        {
            CancelInteraction();
            _swallowRightRelease = true;
            e.Handled = true;
            return;
        }
        if (e.Button == PointerButtons.Left && _interaction is Interaction.PickSplit or Interaction.PickJoin)
        {
            UpdatePick(PointToClient(e.ScreenPosition));
            var preview = CapturePreview();
            EndInteraction();
            Apply(preview);
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    public override void OnPreviewPointerReleased(PointerEventArgs e)
    {
        base.OnPreviewPointerReleased(e);
        if (_swallowRightRelease && e.Button == PointerButtons.Right)
        {
            _swallowRightRelease = false;
            e.Handled = true; // the right-click canceled; it doesn't open a menu
        }
    }

    /// <inheritdoc/>
    public override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape && (_interaction != Interaction.None || _activeBorder != null))
        {
            CancelInteraction();
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    /// <remarks>Ctrl+Space maximizes the area under the pointer (or with the focus), or restores the layout.</remarks>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Space || (e.Modifiers & ModifierKeys.Control) == 0 || (e.Modifiers & ~ModifierKeys.Control) != 0) return;
        if (_maximized != null)
        {
            RestoreMaximized();
            e.Handled = true;
        }
        else if ((HoveredArea ?? FocusedArea()) is { } area)
        {
            Maximize(area);
            e.Handled = true;
        }
    }

    private Area? FocusedArea()
    {
        for (VisualNode? node = FocusManager.GetFocusedElement(this); node != null && node != this; node = node.Parent)
        {
            if (node is Area area && area.Layout == this) return area;
        }
        return null;
    }

    #endregion

    #region Preview

    private readonly record struct PreviewState(AreaPreviewKind Kind, Area? Area, Area? Target, Orientation Orientation, float Ratio, bool NewFirst);

    private PreviewState CapturePreview() =>
        new(PreviewKind, PreviewArea, PreviewTarget, PreviewOrientation, PreviewRatio, PreviewNewAreaFirst);

    private void Apply(PreviewState preview)
    {
        switch (preview.Kind)
        {
            case AreaPreviewKind.Split when preview.Area != null:
                Split(preview.Area, preview.Orientation, preview.Ratio, preview.NewFirst);
                break;
            case AreaPreviewKind.Join when preview.Area != null && preview.Target != null:
                Join(preview.Area, preview.Target);
                break;
            case AreaPreviewKind.Swap when preview.Area != null && preview.Target != null:
                Swap(preview.Area, preview.Target);
                break;
        }
    }

    private void ClearPreview()
    {
        if (PreviewKind == AreaPreviewKind.None && PreviewArea == null) return;
        PreviewKind = AreaPreviewKind.None;
        PreviewArea = PreviewTarget = null;
        PreviewAreaBounds = PreviewTargetBounds = PreviewSplitLine = Rect.Zero;
        InvalidateVisual();
    }

    // Previews splitting `area` at `point`, keeping both parts at least their minimum size; false if it is too small.
    private bool SetSplitPreview(Area area, Orientation orientation, Point point, bool newAreaFirst)
    {
        var bounds = GetBoundsInLayout(area);
        bool horizontal = orientation == Orientation.Horizontal;
        float length = horizontal ? bounds.Width : bounds.Height;
        float min = horizontal ? MinAreaWidth : MinAreaHeight;
        float spacing = Spacing;
        if (length < 2 * min + spacing) return false;

        float offset = horizontal ? point.X - bounds.X : point.Y - bounds.Y;
        offset = Math.Clamp(offset, min + spacing * 0.5f, length - min - spacing * 0.5f);

        PreviewKind = AreaPreviewKind.Split;
        PreviewArea = area;
        PreviewTarget = null;
        PreviewOrientation = orientation;
        PreviewRatio = (offset - spacing * 0.5f) / Math.Max(1, length - spacing);
        PreviewNewAreaFirst = newAreaFirst;
        PreviewAreaBounds = bounds;
        PreviewTargetBounds = Rect.Zero;
        PreviewSplitLine = horizontal
            ? new Rect(MathF.Round(bounds.X + offset) - 1, bounds.Y, 2, bounds.Height)
            : new Rect(bounds.X, MathF.Round(bounds.Y + offset) - 1, bounds.Width, 2);
        InvalidateVisual();
        return true;
    }

    private void SetJoinPreview(Area keep, Area remove)
    {
        var parent = (AreaSplit)keep.Parent!;
        bool after = parent.IndexOf(remove) > parent.IndexOf(keep);
        PreviewKind = AreaPreviewKind.Join;
        PreviewArea = keep;
        PreviewTarget = remove;
        PreviewDirection = parent.Orientation == Orientation.Horizontal
            ? after ? Dock.Right : Dock.Left
            : after ? Dock.Bottom : Dock.Top;
        PreviewAreaBounds = GetBoundsInLayout(keep);
        PreviewTargetBounds = GetBoundsInLayout(remove);
        PreviewSplitLine = Rect.Zero;
        InvalidateVisual();
    }

    private void SetSwapPreview(Area source, Area target)
    {
        PreviewKind = AreaPreviewKind.Swap;
        PreviewArea = source;
        PreviewTarget = target;
        PreviewAreaBounds = GetBoundsInLayout(source);
        PreviewTargetBounds = GetBoundsInLayout(target);
        PreviewSplitLine = Rect.Zero;
        InvalidateVisual();
    }

    #endregion

    #region Menus

    /// <summary>
    /// Opens the Area Options menu for <paramref name="border"/> at the pointer (see <see cref="CreateAreaOptionsMenu"/>).
    /// Returns the menu, or <c>null</c> when it couldn't open.
    /// </summary>
    public ContextMenu? ShowAreaOptions(AreaBorder border)
    {
        ArgumentNullException.ThrowIfNull(border);
        var menu = CreateAreaOptionsMenu(border);
        return menu.Open(border) ? menu : null;
    }

    /// <summary>
    /// Creates the Area Options menu of a border, as in Blender: Vertical Split, Horizontal Split, Join Areas (when both
    /// neighbors are areas) and Swap Areas. Override it to change the menu.
    /// </summary>
    public virtual ContextMenu CreateAreaOptionsMenu(AreaBorder border)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateSplitItem(Orientation.Horizontal));
        menu.Items.Add(CreateSplitItem(Orientation.Vertical));
        menu.Items.Add(new Separator());

        var first = border.First as Area;
        var second = border.Second as Area;
        bool areas = first != null && second != null;
        bool horizontal = border.Split.Orientation == Orientation.Horizontal;
        var join = new MenuItem("Join Areas") { Icon = MaterialIconKind.Merge, IsEnabled = areas };
        join.Click += (_, _) => BeginInteractiveJoin(first!, second!);
        var swap = new MenuItem("Swap Areas") { Icon = horizontal ? MaterialIconKind.SwapHoriz : MaterialIconKind.SwapVert, IsEnabled = areas };
        swap.Click += (_, _) => Swap(first!, second!);
        menu.Items.Add(join);
        menu.Items.Add(swap);
        return menu;
    }

    /// <summary>
    /// Creates the items of the area menu an area's header shows on a right-click: Vertical Split, Horizontal Split,
    /// Maximize Area (Ctrl+Space) and Close Area. Override it to change the menu.
    /// </summary>
    public virtual IReadOnlyList<UIElement> CreateAreaMenuItems(Area area)
    {
        bool maximized = _maximized != null;
        var vertical = CreateSplitItem(Orientation.Horizontal);
        var horizontal = CreateSplitItem(Orientation.Vertical);
        vertical.IsEnabled = horizontal.IsEnabled = !maximized;

        var maximize = new MenuItem(maximized ? "Back to Previous" : "Maximize Area")
        {
            Icon = maximized ? MaterialIconKind.CloseFullscreen : MaterialIconKind.OpenInFull,
            InputGestureText = "Ctrl+Space",
        };
        maximize.Click += (_, _) => ToggleMaximize(area);
        var close = new MenuItem("Close Area") { Icon = MaterialIconKind.Close, IsEnabled = !maximized && CanClose(area) };
        close.Click += (_, _) => Close(area);
        return [vertical, horizontal, new Separator(), maximize, close];
    }

    private MenuItem CreateSplitItem(Orientation orientation)
    {
        bool sideBySide = orientation == Orientation.Horizontal;
        var item = new MenuItem(sideBySide ? "Vertical Split" : "Horizontal Split")
        {
            Icon = sideBySide ? MaterialIconKind.VerticalSplit : MaterialIconKind.HorizontalSplit,
        };
        item.Click += (_, _) => BeginInteractiveSplit(orientation);
        return item;
    }

    #endregion

    #region Layout

    private Rect InnerRect(Size size)
    {
        float spacing = Spacing;
        return new Rect(spacing, spacing, Math.Max(0, size.Width - 2 * spacing), Math.Max(0, size.Height - 2 * spacing));
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        float width = float.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        float height = float.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
        var inner = InnerRect(new Size(width, height));
        var shown = _maximized?.Parent == this ? _maximized : _root;
        shown?.Measure(new Size(inner.Width, inner.Height));
        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var inner = InnerRect(finalSize);
        if (_maximized?.Parent == this) _maximized.Arrange(inner);
        else _root?.Arrange(inner);
        return finalSize;
    }

    #endregion
}
