using System;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>Specifies whether a <see cref="GridSplitter"/> resizes columns or rows.</summary>
public enum GridResizeDirection
{
    /// <summary>
    /// Decided from the splitter's layout: columns when it is aligned left or right, has only an explicit width, sits in
    /// an Auto column (and a non-Auto row), or is at least as tall as wide; rows otherwise.
    /// </summary>
    Auto,
    /// <summary>Resizes columns; the splitter is dragged left and right.</summary>
    Columns,
    /// <summary>Resizes rows; the splitter is dragged up and down.</summary>
    Rows,
}

/// <summary>Specifies which two columns (or rows) a <see cref="GridSplitter"/> resizes.</summary>
public enum GridResizeBehavior
{
    /// <summary>
    /// Decided by the alignment: a splitter aligned to the left (top) edge of its cell resizes the previous and current
    /// column (row), one aligned to the right (bottom) edge the current and next, otherwise the previous and next.
    /// </summary>
    BasedOnAlignment,
    /// <summary>The splitter's column (row) and the next one; for a splitter at the far edge of a cell.</summary>
    CurrentAndNext,
    /// <summary>The previous column (row) and the splitter's; for a splitter at the near edge of a cell.</summary>
    PreviousAndCurrent,
    /// <summary>The columns (rows) before and after the splitter's own; for a splitter in a column (row) of its own.</summary>
    PreviousAndNext,
}

/// <summary>Provides data for <see cref="GridSplitter.DragCompleted"/>.</summary>
public sealed class GridSplitterDragCompletedEventArgs(float change, bool canceled) : EventArgs
{
    /// <summary>Gets how far the first of the two columns (rows) grew, in pixels; negative when it shrank.</summary>
    public float Change { get; } = change;

    /// <summary>Gets whether the drag was canceled (Escape, or losing the pointer); the sizes were restored.</summary>
    public bool Canceled { get; } = canceled;
}

/// <summary>
/// Resizes the columns or rows of its parent <see cref="Grid"/> by dragging. Place it in a column (row) of its own,
/// e.g. <c>Columns("*,Auto,*")</c>, or at the edge of a cell, e.g. <c>.HorizontalAlignment(HorizontalAlignment.Right)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Dragging changes the two columns (see <see cref="ResizeBehavior"/>) and keeps their combined size. Star columns keep
/// being star columns with new weights, so their proportions survive window resizes; next to a star column, only the
/// other column is changed (to a pixel size), so a sidebar keeps its width when the window grows. Auto columns become
/// pixel sizes. The columns' <c>MinWidth</c> and <c>MaxWidth</c> limit the drag.
/// </para>
/// <para>
/// The splitter is focusable: the arrow keys resize by <see cref="KeyboardIncrement"/>. Escape cancels a drag, and a
/// double click restores the sizes the columns had before the splitter first changed them.
/// </para>
/// </remarks>
public class GridSplitter : Control
{
    /// <summary>The thickness of a splitter without an explicit width (columns) or height (rows).</summary>
    public const float DefaultThickness = 8f;

    /// <summary>Identifies the <see cref="ResizeDirection"/> property.</summary>
    public static readonly BindableProperty<GridResizeDirection> ResizeDirectionProperty =
        BindableProperty.Register<GridSplitter, GridResizeDirection>(nameof(ResizeDirection), GridResizeDirection.Auto,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="ResizeBehavior"/> property.</summary>
    public static readonly BindableProperty<GridResizeBehavior> ResizeBehaviorProperty =
        BindableProperty.Register<GridSplitter, GridResizeBehavior>(nameof(ResizeBehavior), GridResizeBehavior.BasedOnAlignment);

    /// <summary>Identifies the <see cref="DragIncrement"/> property.</summary>
    public static readonly BindableProperty<float> DragIncrementProperty =
        BindableProperty.Register<GridSplitter, float>(nameof(DragIncrement), 1f, validateValue: static v => float.IsFinite(v) && v > 0);

    /// <summary>Identifies the <see cref="KeyboardIncrement"/> property.</summary>
    public static readonly BindableProperty<float> KeyboardIncrementProperty =
        BindableProperty.Register<GridSplitter, float>(nameof(KeyboardIncrement), 10f, validateValue: static v => float.IsFinite(v) && v > 0);

    /// <summary>Identifies the <see cref="ShowsPreview"/> property.</summary>
    public static readonly BindableProperty<bool> ShowsPreviewProperty =
        BindableProperty.Register<GridSplitter, bool>(nameof(ShowsPreview), false);

    static GridSplitter()
    {
        IsFocusableProperty.OverrideDefaultValue<GridSplitter>(true);
    }

    /// <summary>Gets or sets whether the splitter resizes columns or rows. The default is <see cref="GridResizeDirection.Auto"/>.</summary>
    public GridResizeDirection ResizeDirection
    {
        get => GetValue(ResizeDirectionProperty);
        set => SetValue(ResizeDirectionProperty, value);
    }

    /// <summary>Gets or sets which two columns (rows) are resized. The default is <see cref="GridResizeBehavior.BasedOnAlignment"/>.</summary>
    public GridResizeBehavior ResizeBehavior
    {
        get => GetValue(ResizeBehaviorProperty);
        set => SetValue(ResizeBehaviorProperty, value);
    }

    /// <summary>Gets or sets the step, in pixels, that dragging snaps to. The default is 1.</summary>
    public float DragIncrement
    {
        get => GetValue(DragIncrementProperty);
        set => SetValue(DragIncrementProperty, value);
    }

    /// <summary>Gets or sets how far, in pixels, one arrow key press resizes. The default is 10.</summary>
    public float KeyboardIncrement
    {
        get => GetValue(KeyboardIncrementProperty);
        set => SetValue(KeyboardIncrementProperty, value);
    }

    /// <summary>
    /// Gets or sets whether dragging only moves the splitter and resizes the columns when the mouse is released,
    /// instead of resizing live (the default).
    /// </summary>
    public bool ShowsPreview
    {
        get => GetValue(ShowsPreviewProperty);
        set => SetValue(ShowsPreviewProperty, value);
    }

    /// <summary>Gets whether the splitter is being dragged.</summary>
    public bool IsDragging => _drag != null;

    /// <summary>Gets the direction the splitter resizes in, with <see cref="GridResizeDirection.Auto"/> resolved.</summary>
    public GridResizeDirection ActualDirection
    {
        get
        {
            var direction = ResizeDirection;
            if (direction != GridResizeDirection.Auto)
            {
                return direction;
            }

            if (HorizontalAlignment != HorizontalAlignment.Stretch) return GridResizeDirection.Columns;
            if (VerticalAlignment != VerticalAlignment.Stretch) return GridResizeDirection.Rows;
            if (!float.IsNaN(Width) && float.IsNaN(Height)) return GridResizeDirection.Columns;
            if (!float.IsNaN(Height) && float.IsNaN(Width)) return GridResizeDirection.Rows;

            // A splitter in an Auto column (row) of its own is sized by itself: that's the direction it resizes.
            if (Parent is Grid grid)
            {
                bool autoColumn = IsAuto(grid.ColumnDefinitions, Grid.GetColumn(this));
                bool autoRow = IsAuto(grid.RowDefinitions, Grid.GetRow(this));
                if (autoColumn != autoRow) return autoColumn ? GridResizeDirection.Columns : GridResizeDirection.Rows;
            }

            return Bounds.Width <= Bounds.Height ? GridResizeDirection.Columns : GridResizeDirection.Rows;
        }
    }

    /// <summary>Occurs when a drag starts.</summary>
    public event EventHandler? DragStarted;

    /// <summary>Occurs while dragging, with how far the first column (row) has grown since the drag started.</summary>
    public event EventHandler<float>? DragDelta;

    /// <summary>Occurs when a drag ends, completed or canceled.</summary>
    public event EventHandler<GridSplitterDragCompletedEventArgs>? DragCompleted;

    /// <summary>
    /// Occurs after the splitter changed the sizes of its columns (rows): while dragging (or when a preview drag ends),
    /// by keyboard, by <see cref="Resize"/>, when a drag is canceled and when the original sizes are restored.
    /// </summary>
    public event EventHandler? Resized;

    /// <inheritdoc/>
    protected override CursorType GetCursor() =>
        Cursor != CursorType.Default ? Cursor
        : ActualDirection == GridResizeDirection.Columns ? CursorType.SizeWestEast : CursorType.SizeNorthSouth;

    /// <inheritdoc/>
    /// <remarks>A splitter is <see cref="DefaultThickness"/> across and stretches along its cell.</remarks>
    protected override Size MeasureOverride(Size availableSize) =>
        ActualDirection == GridResizeDirection.Columns ? new Size(DefaultThickness, 0) : new Size(0, DefaultThickness);

    #region Resizing

    // A resize in progress: the two definitions, their lengths and sizes at the start, and the pointer start.
    private sealed class Drag(DefinitionBase first, DefinitionBase second, GridLength firstLength, GridLength secondLength,
        float firstSize, float secondSize, float startPosition)
    {
        public DefinitionBase First { get; } = first;
        public DefinitionBase Second { get; } = second;
        public GridLength FirstLength { get; } = firstLength;
        public GridLength SecondLength { get; } = secondLength;
        public float FirstSize { get; } = firstSize;
        public float SecondSize { get; } = secondSize;
        public float StartPosition { get; } = startPosition;
        public float Change { get; set; }
    }

    private Drag? _drag;

    // The lengths before this splitter first changed them, restored by a double click.
    private DefinitionBase? _originalFirst, _originalSecond;
    private GridLength _originalFirstLength, _originalSecondLength;

    /// <summary>
    /// Moves the splitter by <paramref name="change"/> pixels, as if dragged: the first of the two columns (rows) grows
    /// by that much (within the limits) and the second shrinks. Returns the change actually applied.
    /// </summary>
    public float Resize(float change)
    {
        var pair = BeginResize(startPosition: 0);
        if (pair == null)
        {
            return 0;
        }

        float applied = Apply(pair, change, commit: true);
        Resized?.Invoke(this, EventArgs.Empty);
        return applied;
    }

    /// <summary>Restores the sizes the columns (rows) had before this splitter first changed them.</summary>
    public void RestoreOriginalSizes()
    {
        if (_originalFirst == null || _originalSecond == null)
        {
            return;
        }

        SetLength(_originalFirst, _originalFirstLength);
        SetLength(_originalSecond, _originalSecondLength);
        Resized?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || e.Button != PointerButtons.Left || !IsEnabled)
        {
            return;
        }

        Focus();
        e.Handled = true;

        if (e.ClickCount == 2)
        {
            RestoreOriginalSizes();
            return;
        }

        if (Parent is not Grid grid)
        {
            return;
        }

        var start = grid.PointToClient(e.ScreenPosition);
        _drag = BeginResize(ActualDirection == GridResizeDirection.Columns ? start.X : start.Y);
        if (_drag != null)
        {
            CapturePointer();
            InvalidateVisual();
            DragStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag == null || Parent is not Grid grid)
        {
            return;
        }

        var point = grid.PointToClient(e.ScreenPosition);
        float position = ActualDirection == GridResizeDirection.Columns ? point.X : point.Y;
        float increment = DragIncrement;
        float change = MathF.Round((position - _drag.StartPosition) / increment) * increment;

        _drag.Change = Apply(_drag, change, commit: !ShowsPreview);
        if (!ShowsPreview)
        {
            Resized?.Invoke(this, EventArgs.Empty);
        }
        if (ShowsPreview)
        {
            // The preview is the splitter itself, drawn at its future place.
            RenderTransform = ActualDirection == GridResizeDirection.Columns
                ? System.Numerics.Matrix3x2.CreateTranslation(_drag.Change, 0)
                : System.Numerics.Matrix3x2.CreateTranslation(0, _drag.Change);
        }

        DragDelta?.Invoke(this, _drag.Change);
        e.Handled = true;
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag != null)
        {
            e.Handled = true;
            EndDrag(canceled: false);
        }
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        if (_drag != null)
        {
            EndDrag(canceled: false);
        }
    }

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        if (e.Key == Key.Escape && _drag != null)
        {
            EndDrag(canceled: true);
            e.Handled = true;
            return;
        }

        bool columns = ActualDirection == GridResizeDirection.Columns;
        float step = e.Key switch
        {
            Key.Left when columns => -KeyboardIncrement,
            Key.Right when columns => KeyboardIncrement,
            Key.Up when !columns => -KeyboardIncrement,
            Key.Down when !columns => KeyboardIncrement,
            _ => 0,
        };

        if (step != 0 && _drag == null)
        {
            Resize(step);
            e.Handled = true;
        }
    }

    private void EndDrag(bool canceled)
    {
        var drag = _drag!;
        _drag = null;

        if (canceled)
        {
            SetLength(drag.First, drag.FirstLength);
            SetLength(drag.Second, drag.SecondLength);
            Resized?.Invoke(this, EventArgs.Empty);
            drag.Change = 0;
        }
        else if (ShowsPreview)
        {
            Apply(drag, drag.Change, commit: true);
            Resized?.Invoke(this, EventArgs.Empty);
        }

        RenderTransform = System.Numerics.Matrix3x2.Identity;
        if (UIElement.CapturedElement == this)
        {
            ReleasePointerCapture();
        }
        InvalidateVisual();
        DragCompleted?.Invoke(this, new GridSplitterDragCompletedEventArgs(drag.Change, canceled));
    }

    // Finds the two definitions to resize and snapshots them; null when the splitter can't resize anything.
    private Drag? BeginResize(float startPosition)
    {
        if (Parent is not Grid grid)
        {
            return null;
        }

        bool columns = ActualDirection == GridResizeDirection.Columns;
        int index = columns ? Grid.GetColumn(this) : Grid.GetRow(this);
        int span = columns ? Grid.GetColumnSpan(this) : Grid.GetRowSpan(this);
        int count = columns ? grid.ColumnDefinitions.Count : grid.RowDefinitions.Count;

        var behavior = ResizeBehavior;
        if (behavior == GridResizeBehavior.BasedOnAlignment)
        {
            behavior = columns
                ? HorizontalAlignment switch
                {
                    HorizontalAlignment.Left => GridResizeBehavior.PreviousAndCurrent,
                    HorizontalAlignment.Right => GridResizeBehavior.CurrentAndNext,
                    _ => GridResizeBehavior.PreviousAndNext,
                }
                : VerticalAlignment switch
                {
                    VerticalAlignment.Top => GridResizeBehavior.PreviousAndCurrent,
                    VerticalAlignment.Bottom => GridResizeBehavior.CurrentAndNext,
                    _ => GridResizeBehavior.PreviousAndNext,
                };
        }

        int first = behavior == GridResizeBehavior.CurrentAndNext ? index : index - 1;
        int second = behavior == GridResizeBehavior.PreviousAndCurrent ? index : index + Math.Max(1, span);
        if (behavior == GridResizeBehavior.CurrentAndNext)
        {
            first = index + Math.Max(1, span) - 1;
        }

        if (first < 0 || second >= count || first >= second)
        {
            return null;
        }

        DefinitionBase a = columns ? grid.ColumnDefinitions[first] : grid.RowDefinitions[first];
        DefinitionBase b = columns ? grid.ColumnDefinitions[second] : grid.RowDefinitions[second];

        if (_originalFirst != a || _originalSecond != b)
        {
            _originalFirst = a;
            _originalSecond = b;
            _originalFirstLength = GetLength(a);
            _originalSecondLength = GetLength(b);
        }

        return new Drag(a, b, GetLength(a), GetLength(b), GetActualSize(a), GetActualSize(b), startPosition);
    }

    // Resizes the pair by change (clamped by both definitions' limits and their combined size) and returns the change
    // applied. Without commit, only computes it (preview).
    private static float Apply(Drag drag, float change, bool commit)
    {
        float total = drag.FirstSize + drag.SecondSize;
        float min = Math.Max(GetMin(drag.First), total - GetMax(drag.Second));
        float max = Math.Min(GetMax(drag.First), total - GetMin(drag.Second));
        float first = max >= min ? Math.Clamp(drag.FirstSize + change, min, max) : drag.FirstSize;
        float second = total - first;

        if (commit)
        {
            var a = drag.FirstLength;
            var b = drag.SecondLength;
            if (a.IsStar && b.IsStar)
            {
                // Keep the combined weight, split in proportion to the new sizes.
                float weight = a.Value + b.Value;
                float firstWeight = total > 0 ? weight * first / total : a.Value;
                SetLength(drag.First, GridLength.Stars(firstWeight));
                SetLength(drag.Second, GridLength.Stars(weight - firstWeight));
            }
            else
            {
                // Next to a star, only the other one changes; the star takes what is left.
                if (!a.IsStar) SetLength(drag.First, GridLength.Pixels(first));
                if (!b.IsStar) SetLength(drag.Second, GridLength.Pixels(second));
            }
        }

        return first - drag.FirstSize;
    }

    private static bool IsAuto<T>(DefinitionCollection<T> definitions, int index) where T : DefinitionBase =>
        index >= 0 && index < definitions.Count && GetLength(definitions[index]).IsAuto;

    private static GridLength GetLength(DefinitionBase definition) =>
        definition is ColumnDefinition column ? column.Width : ((RowDefinition)definition).Height;

    private static void SetLength(DefinitionBase definition, GridLength length)
    {
        if (definition is ColumnDefinition column) column.Width = length;
        else ((RowDefinition)definition).Height = length;
    }

    private static float GetActualSize(DefinitionBase definition) =>
        definition is ColumnDefinition column ? column.ActualWidth : ((RowDefinition)definition).ActualHeight;

    private static float GetMin(DefinitionBase definition) =>
        definition is ColumnDefinition column ? column.MinWidth : ((RowDefinition)definition).MinHeight;

    private static float GetMax(DefinitionBase definition) =>
        definition is ColumnDefinition column ? column.MaxWidth : ((RowDefinition)definition).MaxHeight;

    #endregion
}
