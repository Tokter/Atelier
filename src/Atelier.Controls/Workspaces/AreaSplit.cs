using System;
using System.Collections.Generic;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// Areas (or further splits) of an <see cref="AreaLayout"/> side by side or stacked, each getting a share of the space
/// by its weight, with an <see cref="AreaBorder"/> in each gap to resize the neighbors.
/// </summary>
/// <remarks>
/// Splits are created and removed by the layout as areas are split, joined and closed; a split always has at least two
/// nodes, and never a child split of its own orientation (those nodes join the parent).
/// </remarks>
public class AreaSplit : Control
{
    private readonly List<UIElement> _nodes = [];
    private readonly List<float> _weights = [];
    private readonly List<AreaBorder> _borders = [];
    private float[] _sizes = [];
    private float[] _measureSizes = [];

    /// <summary>Initializes an empty split.</summary>
    /// <param name="orientation"><see cref="Orientation.Horizontal"/> puts the nodes side by side, <see cref="Orientation.Vertical"/> stacks them.</param>
    public AreaSplit(Orientation orientation)
    {
        Orientation = orientation;
    }

    /// <summary>Gets whether the nodes are side by side (<see cref="Orientation.Horizontal"/>) or stacked.</summary>
    public Orientation Orientation { get; }

    /// <summary>Gets the nodes, <see cref="Area"/>s and <see cref="AreaSplit"/>s, left to right or top to bottom.</summary>
    public IReadOnlyList<UIElement> Nodes => _nodes;

    /// <summary>Gets the borders between the nodes; border <c>i</c> is between node <c>i</c> and <c>i + 1</c>.</summary>
    public IReadOnlyList<AreaBorder> Borders => _borders;

    /// <summary>Gets the layout the split belongs to, or <c>null</c>.</summary>
    public AreaLayout? Layout { get; internal set; }

    /// <summary>Gets the split this split is in, or <c>null</c> for the root.</summary>
    public AreaSplit? ParentSplit => Parent as AreaSplit;

    /// <summary>Gets the weight of the node at <paramref name="index"/> (its share relative to the others).</summary>
    public float GetWeight(int index) => _weights[index];

    /// <summary>Gets the size of the node at <paramref name="index"/> along the split, from the last layout.</summary>
    public float GetSize(int index) => index < _sizes.Length ? _sizes[index] : 0;

    /// <summary>Gets the index of <paramref name="node"/>, or -1.</summary>
    public int IndexOf(UIElement node) => _nodes.IndexOf(node);

    internal void SetWeight(int index, float weight)
    {
        _weights[index] = Math.Max(weight, 1e-4f);
        InvalidateMeasure();
    }

    internal void InsertNode(int index, UIElement node, float weight)
    {
        _nodes.Insert(index, node);
        _weights.Insert(index, Math.Max(weight, 1e-4f));
        AddChild(node);
        SyncBorders();
    }

    internal void RemoveNodeAt(int index)
    {
        var node = _nodes[index];
        _nodes.RemoveAt(index);
        _weights.RemoveAt(index);
        RemoveChild(node);
        SyncBorders();
    }

    // Puts `node` where the node at `index` was, with its weight.
    internal void ReplaceNodeAt(int index, UIElement node)
    {
        var old = _nodes[index];
        _nodes[index] = node;
        RemoveChild(old);
        AddChild(node);
        InvalidateMeasure();
    }

    internal void SwapNodes(int first, int second)
    {
        (_nodes[first], _nodes[second]) = (_nodes[second], _nodes[first]);
        InvalidateMeasure();
    }

    private void SyncBorders()
    {
        int count = Math.Max(0, _nodes.Count - 1);
        while (_borders.Count > count)
        {
            var border = _borders[^1];
            _borders.RemoveAt(_borders.Count - 1);
            RemoveChild(border);
        }
        while (_borders.Count < count)
        {
            var border = new AreaBorder(this, _borders.Count);
            _borders.Add(border);
            AddChild(border);
        }
        InvalidateMeasure();
    }

    private float Spacing => Layout?.Spacing ?? AreaLayout.DefaultSpacing;

    // Divides `length` (without the gaps) between the nodes by weight, on whole pixels, into `sizes`.
    private float[] ComputeSizes(float length, ref float[] sizes)
    {
        if (sizes.Length != _nodes.Count) sizes = new float[_nodes.Count];
        float total = 0;
        foreach (var weight in _weights) total += weight;
        float available = Math.Max(0, length - Spacing * Math.Max(0, _nodes.Count - 1));
        float position = 0, rounded = 0;
        for (int i = 0; i < sizes.Length; i++)
        {
            position += total > 0 ? available * _weights[i] / total : 0;
            float end = i == sizes.Length - 1 ? available : MathF.Round(position);
            sizes[i] = Math.Max(0, end - rounded);
            rounded = end;
        }
        return sizes;
    }

    /// <summary>
    /// Gets the smallest size <paramref name="node"/> can have along <paramref name="orientation"/>: an area's
    /// <see cref="AreaLayout.MinAreaWidth"/> or <see cref="AreaLayout.MinAreaHeight"/>, a split's from its nodes.
    /// </summary>
    public static float GetMinSize(UIElement node, Orientation orientation)
    {
        if (node is AreaSplit split)
        {
            float result = 0;
            foreach (var child in split._nodes)
            {
                float min = GetMinSize(child, orientation);
                result = split.Orientation == orientation ? result + min : Math.Max(result, min);
            }
            if (split.Orientation == orientation) result += split.Spacing * Math.Max(0, split._nodes.Count - 1);
            return result;
        }
        return orientation == Orientation.Horizontal ? AreaLayout.MinAreaWidth : AreaLayout.MinAreaHeight;
    }

    /// <summary>
    /// Moves border <paramref name="index"/> by <paramref name="delta"/> pixels from where it was when the nodes had
    /// <paramref name="startSizes"/>, keeping both neighbors at least their minimum size.
    /// </summary>
    internal void ResizeAt(int index, float[] startSizes, float startWeightSum, float delta)
    {
        if (index < 0 || index + 1 >= _nodes.Count || index + 1 >= startSizes.Length) return;
        float first = startSizes[index], second = startSizes[index + 1], both = first + second;
        float minFirst = GetMinSize(_nodes[index], Orientation), minSecond = GetMinSize(_nodes[index + 1], Orientation);
        if (both <= 0 || both < minFirst + minSecond) return;

        float size = Math.Clamp(first + delta, minFirst, both - minSecond);
        _weights[index] = Math.Max(startWeightSum * size / both, 1e-4f);
        _weights[index + 1] = Math.Max(startWeightSum - _weights[index], 1e-4f);
        InvalidateMeasure();
    }

    internal float[] SizesSnapshot() => (float[])_sizes.Clone();

    internal float WeightSum(int index) => _weights[index] + _weights[index + 1];

    internal void RestoreWeights(int index, float first, float second)
    {
        _weights[index] = first;
        _weights[index + 1] = second;
        InvalidateMeasure();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        bool horizontal = Orientation == Orientation.Horizontal;
        float along = horizontal ? availableSize.Width : availableSize.Height;
        float across = horizontal ? availableSize.Height : availableSize.Width;
        if (float.IsInfinity(along)) along = 0;
        if (float.IsInfinity(across)) across = 0;

        var sizes = ComputeSizes(along, ref _measureSizes);
        for (int i = 0; i < _nodes.Count; i++)
        {
            _nodes[i].Measure(horizontal ? new Size(sizes[i], across) : new Size(across, sizes[i]));
        }
        foreach (var border in _borders)
        {
            border.Measure(Size.Zero);
        }
        return horizontal ? new Size(along, across) : new Size(across, along);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        bool horizontal = Orientation == Orientation.Horizontal;
        float along = horizontal ? finalSize.Width : finalSize.Height;
        float across = horizontal ? finalSize.Height : finalSize.Width;
        ComputeSizes(along, ref _sizes);
        float spacing = Spacing;
        float hit = Math.Max(AreaBorder.MinHitThickness, spacing + 2 * AreaBorder.HitOverlap);

        float position = 0;
        for (int i = 0; i < _nodes.Count; i++)
        {
            float size = _sizes[i];
            _nodes[i].Arrange(horizontal ? new Rect(position, 0, size, across) : new Rect(0, position, across, size));
            position += size;
            if (i < _borders.Count)
            {
                // The border covers the gap and reaches a little over the edges of its neighbors.
                float start = position + (spacing - hit) * 0.5f;
                _borders[i].Arrange(horizontal ? new Rect(start, 0, hit, across) : new Rect(0, start, across, hit));
                position += spacing;
            }
        }
        return finalSize;
    }
}

/// <summary>
/// The gap between two neighbors of an <see cref="AreaSplit"/>: dragging it resizes them (Escape cancels), and a
/// right-click opens the Area Options menu (split, join, swap), as on a Blender area border.
/// </summary>
/// <remarks>
/// The border covers the gap and <see cref="HitOverlap"/> pixels of each neighbor's edge, at least
/// <see cref="MinHitThickness"/> pixels, and shows a resize cursor.
/// </remarks>
public class AreaBorder : Control
{
    /// <summary>How far the border reaches over the edges of its neighbors, in pixels.</summary>
    public const float HitOverlap = 2f;

    /// <summary>The smallest thickness of the border's hit area, in pixels.</summary>
    public const float MinHitThickness = 6f;

    private Point _pressPosition;
    private float[]? _startSizes;
    private float _startWeightSum, _startFirst, _startSecond;

    internal AreaBorder(AreaSplit split, int index)
    {
        Split = split;
        Index = index;
        ZIndex = 1;
    }

    /// <summary>Gets the split the border belongs to.</summary>
    public AreaSplit Split { get; }

    /// <summary>Gets the index of the border: it is between the split's nodes <c>Index</c> and <c>Index + 1</c>.</summary>
    public int Index { get; }

    /// <summary>Gets the node before the border (left or above).</summary>
    public UIElement First => Split.Nodes[Index];

    /// <summary>Gets the node after the border (right or below).</summary>
    public UIElement Second => Split.Nodes[Index + 1];

    /// <summary>Gets whether the border is being dragged.</summary>
    public bool IsDragging => _startSizes != null;

    /// <inheritdoc/>
    protected override CursorType GetCursor()
    {
        var cursor = Cursor;
        if (cursor != CursorType.Default) return cursor;
        return Split.Orientation == Orientation.Horizontal ? CursorType.SizeWestEast : CursorType.SizeNorthSouth;
    }

    /// <inheritdoc/>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !IsEnabled) return;
        if (e.Button == PointerButtons.Right)
        {
            e.Handled = true; // the menu opens on release
            return;
        }
        if (e.Button != PointerButtons.Left || Split.Layout?.IsInteracting == true) return;

        e.Handled = true;
        _pressPosition = e.ScreenPosition;
        _startSizes = Split.SizesSnapshot();
        _startWeightSum = Split.WeightSum(Index);
        _startFirst = Split.GetWeight(Index);
        _startSecond = Split.GetWeight(Index + 1);
        Split.Layout?.SetActiveBorder(this);
        CapturePointer();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!IsPointerCaptured || _startSizes == null) return;
        float delta = Split.Orientation == Orientation.Horizontal
            ? e.ScreenPosition.X - _pressPosition.X
            : e.ScreenPosition.Y - _pressPosition.Y;
        Split.ResizeAt(Index, _startSizes, _startWeightSum, delta);
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Button == PointerButtons.Right && !e.Handled)
        {
            e.Handled = true;
            Split.Layout?.ShowAreaOptions(this);
            return;
        }
        if (!IsPointerCaptured || e.Button != PointerButtons.Left) return;
        e.Handled = true;
        bool moved = _startSizes != null;
        EndDrag();
        ReleasePointerCapture();
        if (moved) Split.Layout?.OnAreaChanged();
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        if (_startSizes != null) CancelDrag();
    }

    /// <summary>Ends a drag and puts the neighbors back to their sizes before it.</summary>
    public void CancelDrag()
    {
        if (_startSizes == null) return;
        Split.RestoreWeights(Index, _startFirst, _startSecond);
        EndDrag();
        if (IsPointerCaptured) ReleasePointerCapture();
    }

    private void EndDrag()
    {
        _startSizes = null;
        Split.Layout?.SetActiveBorder(null);
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => Size.Zero;
}
