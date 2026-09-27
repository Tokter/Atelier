using System;
using Atelier.Controls;

namespace Atelier.Markup;

/// <summary>Fluent methods for <see cref="GridSplitter"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class GridSplitterMarkup
{
    /// <summary>Sets whether the splitter resizes columns or rows. <see cref="GridResizeDirection.Auto"/> (the default) decides from its layout.</summary>
    public static T ResizeDirection<T>(this T splitter, GridResizeDirection direction) where T : GridSplitter =>
        splitter.Set(GridSplitter.ResizeDirectionProperty, direction);

    /// <summary>Sets which two columns (rows) are resized. The default decides by the splitter's alignment.</summary>
    public static T ResizeBehavior<T>(this T splitter, GridResizeBehavior behavior) where T : GridSplitter =>
        splitter.Set(GridSplitter.ResizeBehaviorProperty, behavior);

    /// <summary>Sets the step, in pixels, that dragging snaps to. The default is 1.</summary>
    public static T DragIncrement<T>(this T splitter, float increment) where T : GridSplitter =>
        splitter.Set(GridSplitter.DragIncrementProperty, increment);

    /// <summary>Sets how far, in pixels, one arrow key press resizes. The default is 10.</summary>
    public static T KeyboardIncrement<T>(this T splitter, float increment) where T : GridSplitter =>
        splitter.Set(GridSplitter.KeyboardIncrementProperty, increment);

    /// <summary>Resizes only when the mouse is released, showing the splitter at its future place while dragging.</summary>
    public static T ShowsPreview<T>(this T splitter, bool showsPreview = true) where T : GridSplitter =>
        splitter.Set(GridSplitter.ShowsPreviewProperty, showsPreview);

    /// <summary>Handles <see cref="GridSplitter.DragStarted"/>, raised when a drag starts.</summary>
    public static T OnDragStarted<T>(this T splitter, EventHandler handler) where T : GridSplitter
    {
        splitter.DragStarted += handler;
        return splitter;
    }

    /// <summary>Runs <paramref name="action"/> when a drag starts (<see cref="GridSplitter.DragStarted"/>).</summary>
    public static T OnDragStarted<T>(this T splitter, Action action) where T : GridSplitter => splitter.OnDragStarted(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="GridSplitter.DragDelta"/>, raised while dragging with how far the first column (row) has grown.</summary>
    public static T OnDragDelta<T>(this T splitter, EventHandler<float> handler) where T : GridSplitter
    {
        splitter.DragDelta += handler;
        return splitter;
    }

    /// <summary>Runs <paramref name="action"/> while dragging with how far the first column (row) has grown (<see cref="GridSplitter.DragDelta"/>).</summary>
    public static T OnDragDelta<T>(this T splitter, Action<float> action) where T : GridSplitter => splitter.OnDragDelta(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="GridSplitter.Resized"/>, raised whenever the splitter changed the sizes (drag, keyboard, restore).</summary>
    public static T OnResized<T>(this T splitter, EventHandler handler) where T : GridSplitter
    {
        splitter.Resized += handler;
        return splitter;
    }

    /// <summary>Runs <paramref name="action"/> whenever the splitter changed the sizes (<see cref="GridSplitter.Resized"/>).</summary>
    public static T OnResized<T>(this T splitter, Action action) where T : GridSplitter => splitter.OnResized(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="GridSplitter.DragCompleted"/>, raised when a drag ends or is canceled.</summary>
    public static T OnDragCompleted<T>(this T splitter, EventHandler<GridSplitterDragCompletedEventArgs> handler) where T : GridSplitter
    {
        splitter.DragCompleted += handler;
        return splitter;
    }
}
