using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// Supplies the items of a <see cref="VirtualizingStackPanel"/>: how many there are, and containers that show them.
/// </summary>
/// <remarks>
/// The panel creates containers only for the items in view and reuses them: a container that scrolls out of view is
/// cleared and later prepared for another item.
/// </remarks>
public interface IVirtualItemsGenerator
{
    /// <summary>Gets the number of items.</summary>
    int ItemCount { get; }

    /// <summary>Creates an empty container; the panel prepares it for an item before showing it.</summary>
    UIElement CreateContainer();

    /// <summary>Makes <paramref name="container"/> show the item at <paramref name="index"/>.</summary>
    void PrepareContainer(UIElement container, int index);

    /// <summary>Releases <paramref name="container"/> from its item before it is reused or dropped.</summary>
    void ClearContainer(UIElement container);
}

/// <summary>
/// A vertical stack that creates elements only for the items in view (UI virtualization): placed in a
/// <see cref="ScrollViewer"/>, it reports the height of all items, so the scroll bar is right, but realizes just the
/// visible ones (and <see cref="OverscanCount"/> more on each side), reusing containers while scrolling.
/// </summary>
/// <remarks>
/// <para>
/// With a fixed <see cref="ItemHeight"/> positions are computed directly, which suits lists of any size. Without it,
/// item heights are measured as items come into view; items not yet seen count with the average of the measured ones,
/// so the extent is an estimate that settles while scrolling.
/// </para>
/// <para>
/// Items come from <see cref="Generator"/>; call <see cref="Reset"/> after the items changed.
/// </para>
/// </remarks>
public class VirtualizingStackPanel : Panel
{
    private readonly Dictionary<int, UIElement> _realized = [];
    private readonly Stack<UIElement> _pool = new();
    private readonly List<int> _scratch = [];
    private readonly HeightCache _heights = new();
    private IVirtualItemsGenerator? _generator;
    private ScrollViewer? _scrollViewer;
    private float _itemHeight = float.NaN;
    private float _maxWidth;
    private float _lastOffset = float.NaN;
    private float _lastViewport = float.NaN;

    /// <summary>Gets or sets the source of the items and their containers. Setting it resets the panel.</summary>
    public IVirtualItemsGenerator? Generator
    {
        get => _generator;
        set
        {
            _generator = value;
            Reset();
        }
    }

    /// <summary>
    /// Gets or sets the height of every item, or <see cref="float.NaN"/> (the default) to measure each item. A fixed height
    /// is faster and keeps the scroll extent exact.
    /// </summary>
    public float ItemHeight
    {
        get => _itemHeight;
        set
        {
            _itemHeight = value;
            InvalidateMeasure();
        }
    }

    /// <summary>Gets or sets the height assumed for items before any was measured. The default is 32.</summary>
    public float EstimatedItemHeight { get; set; } = 32f;

    /// <summary>Gets or sets how many items beyond the viewport are realized on each side. The default is 3.</summary>
    public int OverscanCount { get; set; } = 3;

    /// <summary>Gets the index of the first realized item in view, or -1.</summary>
    public int FirstVisibleIndex { get; private set; } = -1;

    /// <summary>Gets the index of the last realized item in view, or -1.</summary>
    public int LastVisibleIndex { get; private set; } = -1;

    /// <summary>Gets the realized containers by item index.</summary>
    public IReadOnlyDictionary<int, UIElement> RealizedContainers => _realized;

    private int ItemCount => _generator?.ItemCount ?? 0;

    private bool IsFixed => _itemHeight > 0 && float.IsFinite(_itemHeight);

    /// <summary>Gets the container of the item at <paramref name="index"/> if it is realized, otherwise <c>null</c>.</summary>
    public UIElement? ContainerFromIndex(int index) => _realized.TryGetValue(index, out var container) ? container : null;

    /// <summary>Gets the index of the item <paramref name="container"/> shows, or -1 if it isn't realized.</summary>
    public int IndexFromContainer(UIElement container)
    {
        foreach (var (index, realized) in _realized)
        {
            if (realized == container) return index;
        }
        return -1;
    }

    /// <summary>
    /// Clears all containers (they are reused) and the measured heights, e.g. after the items changed; the visible
    /// items are realized again at the next layout.
    /// </summary>
    public void Reset()
    {
        foreach (var container in _realized.Values)
        {
            Recycle(container);
        }
        _realized.Clear();
        _heights.Clear();
        _maxWidth = 0;
        InvalidateMeasure();
    }

    /// <summary>
    /// Updates the panel after the items changed as <paramref name="e"/> describes (in item indexes): containers of
    /// removed or replaced items are recycled, the others keep their items and are prepared again where their index
    /// moved, and measured heights move with their items. A reset (or a change without indexes) calls <see cref="Reset"/>.
    /// </summary>
    public void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        Func<int, int>? map = e.Action switch
        {
            NotifyCollectionChangedAction.Add when e.NewItems != null && e.NewStartingIndex >= 0 =>
                i => i >= e.NewStartingIndex ? i + e.NewItems.Count : i,
            NotifyCollectionChangedAction.Remove when e.OldItems != null && e.OldStartingIndex >= 0 =>
                i => i < e.OldStartingIndex ? i : i < e.OldStartingIndex + e.OldItems.Count ? -1 : i - e.OldItems.Count,
            NotifyCollectionChangedAction.Replace when e.NewItems != null && e.NewStartingIndex >= 0 =>
                i => i >= e.NewStartingIndex && i < e.NewStartingIndex + e.NewItems.Count ? -1 : i,
            NotifyCollectionChangedAction.Move when e.OldItems is { Count: 1 } && e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0 =>
                i => MoveIndex(i, e.OldStartingIndex, e.NewStartingIndex),
            _ => null,
        };
        if (map == null)
        {
            Reset();
            return;
        }

        var moved = new List<(int Index, UIElement Container)>();
        _scratch.Clear();
        _scratch.AddRange(_realized.Keys);
        var old = new Dictionary<int, UIElement>(_realized);
        _realized.Clear();
        foreach (int index in _scratch)
        {
            var container = old[index];
            int target = map(index);
            if (target < 0)
            {
                Recycle(container);
                continue;
            }
            _realized[target] = container;
            if (target != index) moved.Add((target, container));
        }
        foreach (var (index, container) in moved)
        {
            _generator?.PrepareContainer(container, index);
        }
        _heights.Remap(map);
        InvalidateMeasure();
    }

    private static int MoveIndex(int index, int from, int to)
    {
        if (index == from) return to;
        if (index > from) index--;
        if (index >= to) index++;
        return index;
    }

    /// <summary>Prepares the realized containers again for their items, e.g. after the items' state changed.</summary>
    public void RefreshRealized()
    {
        if (_generator == null) return;
        foreach (var (index, container) in _realized)
        {
            _generator.PrepareContainer(container, index);
        }
        InvalidateMeasure();
    }

    #region Geometry

    /// <summary>Gets the top of the item at <paramref name="index"/> (exact for measured or fixed-height items).</summary>
    public float GetItemTop(int index) => IsFixed ? index * _itemHeight : _heights.Top(index, EstimatedItemHeight);

    /// <summary>Gets the height of the item at <paramref name="index"/> (estimated if it hasn't been measured).</summary>
    public float GetItemHeight(int index) => IsFixed ? _itemHeight : _heights.Height(index, EstimatedItemHeight);

    /// <summary>Gets the total height of all items.</summary>
    public float ExtentHeight => GetItemTop(ItemCount);

    /// <summary>Gets the index of the item at <paramref name="y"/> (in the panel's coordinates), clamped to the items.</summary>
    public int GetIndexAt(float y)
    {
        int count = ItemCount;
        if (count == 0) return -1;
        if (y <= 0) return 0;
        if (!float.IsFinite(y)) return count - 1;
        if (IsFixed) return Math.Min(count - 1, (int)(y / _itemHeight));

        int lo = 0, hi = count - 1;
        while (lo < hi)
        {
            int mid = lo + (hi - lo + 1) / 2;
            if (GetItemTop(mid) <= y) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>
    /// Scrolls the enclosing <see cref="ScrollViewer"/> so the item at <paramref name="index"/> is fully visible (if it
    /// isn't already).
    /// </summary>
    public void ScrollIntoView(int index, bool animate = false)
    {
        if (index < 0 || index >= ItemCount || FindScrollViewer() is not { } viewer) return;

        float top = GetItemTop(index);
        float bottom = top + GetItemHeight(index);
        float offset = viewer.ScrollOffsetY;
        float viewport = viewer.Viewport.Height;
        if (top < offset)
        {
            viewer.ScrollTo(viewer.ScrollOffsetX, top, animate);
        }
        else if (bottom > offset + viewport && viewport > 0)
        {
            viewer.ScrollTo(viewer.ScrollOffsetX, Math.Min(top, bottom - viewport), animate);
        }
    }

    /// <summary>Gets the number of whole items that fit into the viewport, at least 1 (for Page Up/Down).</summary>
    public int ItemsPerPage
    {
        get
        {
            float viewport = FindScrollViewer()?.Viewport.Height ?? 0;
            float height = IsFixed ? _itemHeight : _heights.Average(EstimatedItemHeight);
            return Math.Max(1, (int)(viewport / Math.Max(1f, height)));
        }
    }

    #endregion

    #region Realization

    private ScrollViewer? FindScrollViewer()
    {
        if (_scrollViewer != null) return _scrollViewer;
        for (var node = Parent; node != null; node = node.Parent)
        {
            if (node is ScrollViewer viewer)
            {
                _scrollViewer = viewer;
                viewer.ScrollChanged += OnScrollChanged;
                return viewer;
            }
        }
        return null;
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree()
    {
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
            _scrollViewer = null;
        }
        base.OnDetachedFromVisualTree();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // Re-realize only when the visible range can have changed.
        if (e.VerticalOffset != _lastOffset || e.ViewportHeight != _lastViewport)
        {
            InvalidateMeasure();
        }
    }

    private UIElement Realize(int index)
    {
        var container = _pool.Count > 0 ? _pool.Pop() : null;
        if (container == null)
        {
            container = _generator!.CreateContainer();
            AddChild(container);
        }
        else
        {
            container.Visibility = Visibility.Visible;
        }
        _generator!.PrepareContainer(container, index);
        _realized[index] = container;
        return container;
    }

    private void Recycle(UIElement container)
    {
        _generator?.ClearContainer(container);
        container.Visibility = Visibility.Collapsed;
        _pool.Push(container);
    }

    /// <inheritdoc/>
    /// <remarks>Realizes the items in view and returns the size of all items (the widest realized one, the total height).</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        int count = ItemCount;
        var viewer = FindScrollViewer();
        float offset = viewer?.ScrollOffsetY ?? 0;
        float viewport = viewer is { Viewport.Height: > 0 } ? viewer.Viewport.Height
            : float.IsFinite(availableSize.Height) ? availableSize.Height
            : 1000f;
        _lastOffset = offset;
        _lastViewport = viewport;

        int first = -1, last = -1;
        if (count > 0)
        {
            // An unbounded viewport (e.g. in a vertical stack without a height) shows everything.
            first = float.IsFinite(viewport) ? Math.Max(0, GetIndexAt(offset) - OverscanCount) : 0;
            last = float.IsFinite(viewport) ? Math.Min(count - 1, GetIndexAt(offset + viewport) + OverscanCount) : count - 1;
        }

        // Recycle what left the range.
        _scratch.Clear();
        foreach (var index in _realized.Keys)
        {
            if (index < first || index > last) _scratch.Add(index);
        }
        foreach (var index in _scratch)
        {
            Recycle(_realized[index]);
            _realized.Remove(index);
        }

        var childSize = new Size(availableSize.Width, IsFixed ? _itemHeight : float.PositiveInfinity);
        for (int i = first; i >= 0 && i <= last; i++)
        {
            var container = _realized.TryGetValue(i, out var existing) ? existing : Realize(i);
            container.Measure(childSize);
            _maxWidth = Math.Max(_maxWidth, container.DesiredSize.Width);
            if (!IsFixed) _heights.Set(i, container.DesiredSize.Height);
        }

        FirstVisibleIndex = count > 0 ? GetIndexAt(offset) : -1;
        LastVisibleIndex = count > 0 ? GetIndexAt(offset + viewport - 1) : -1;
        return new Size(_maxWidth, ExtentHeight);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (index, container) in _realized)
        {
            container.Arrange(new Rect(0, GetItemTop(index), finalSize.Width, GetItemHeight(index)));
        }
        return finalSize;
    }

    #endregion

    // Measured heights of variable-height items; unmeasured items count with the average.
    private sealed class HeightCache
    {
        private readonly Dictionary<int, float> _heights = [];
        private int[] _keys = [];
        private float[] _deltaPrefix = [0f];
        private float _sum;
        private bool _dirty;

        public void Clear()
        {
            _heights.Clear();
            _sum = 0;
            _dirty = true;
        }

        public void Set(int index, float height)
        {
            if (_heights.TryGetValue(index, out float old))
            {
                if (old == height) return;
                _sum -= old;
            }
            _heights[index] = height;
            _sum += height;
            _dirty = true;
        }

        // Moves the heights to new indexes; -1 drops a height.
        public void Remap(Func<int, int> map)
        {
            if (_heights.Count == 0) return;
            var old = new List<KeyValuePair<int, float>>(_heights);
            _heights.Clear();
            _sum = 0;
            foreach (var (index, height) in old)
            {
                int target = map(index);
                if (target < 0) continue;
                _heights[target] = height;
                _sum += height;
            }
            _dirty = true;
        }

        public float Average(float estimate) => _heights.Count > 0 ? _sum / _heights.Count : estimate;

        public float Height(int index, float estimate) => _heights.TryGetValue(index, out float h) ? h : Average(estimate);

        // i items at the average height, corrected by how much each measured item before i differs from it.
        public float Top(int index, float estimate)
        {
            float average = Average(estimate);
            Rebuild(average);
            int known = LowerBound(_keys, index);
            return index * average + _deltaPrefix[known];
        }

        private void Rebuild(float average)
        {
            if (!_dirty && _deltaPrefix.Length == _keys.Length + 1 && _builtAverage == average) return;
            _keys = [.. _heights.Keys];
            Array.Sort(_keys);
            _deltaPrefix = new float[_keys.Length + 1];
            for (int i = 0; i < _keys.Length; i++)
            {
                _deltaPrefix[i + 1] = _deltaPrefix[i] + (_heights[_keys[i]] - average);
            }
            _builtAverage = average;
            _dirty = false;
        }

        private float _builtAverage = float.NaN;

        private static int LowerBound(int[] keys, int value)
        {
            int lo = 0, hi = keys.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (keys[mid] < value) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }
    }
}
