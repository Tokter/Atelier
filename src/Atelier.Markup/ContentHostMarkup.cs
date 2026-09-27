using System;
using Atelier.Controls;
using Atelier.Core.Animation;
using Atelier.Core.Primitives;

namespace Atelier.Markup;

/// <summary>Fluent methods for <see cref="ScrollViewer"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ScrollViewerMarkup
{
    /// <summary>
    /// Sets horizontal scrolling. The default, <see cref="ScrollBarVisibility.Disabled"/>, doesn't scroll and fits the
    /// content to the width; <see cref="ScrollBarVisibility.Auto"/> scrolls and shows a bar when needed.
    /// </summary>
    public static T HorizontalScrollBarVisibility<T>(this T scrollViewer, ScrollBarVisibility visibility) where T : ScrollViewer =>
        scrollViewer.Set(ScrollViewer.HorizontalScrollBarVisibilityProperty, visibility);

    /// <summary>
    /// Sets vertical scrolling. The default, <see cref="ScrollBarVisibility.Auto"/>, scrolls and shows a bar when the
    /// content is taller than the viewport.
    /// </summary>
    public static T VerticalScrollBarVisibility<T>(this T scrollViewer, ScrollBarVisibility visibility) where T : ScrollViewer =>
        scrollViewer.Set(ScrollViewer.VerticalScrollBarVisibilityProperty, visibility);

    /// <summary>Sets horizontal and vertical scrolling together.</summary>
    public static T ScrollBarVisibility<T>(this T scrollViewer, ScrollBarVisibility horizontal, ScrollBarVisibility vertical) where T : ScrollViewer =>
        scrollViewer.HorizontalScrollBarVisibility(horizontal).VerticalScrollBarVisibility(vertical);

    /// <summary>
    /// Sets whether scrolling an element into view (e.g. the keyboard focus) animates smoothly (the default) or jumps.
    /// </summary>
    public static T IsScrollAnimationEnabled<T>(this T scrollViewer, bool isEnabled = true) where T : ScrollViewer =>
        scrollViewer.Set(ScrollViewer.IsScrollAnimationEnabledProperty, isEnabled);

    /// <summary>Handles <see cref="ScrollViewer.ScrollChanged"/>, raised when the scroll offset, viewport or content size changes.</summary>
    public static T OnScrollChanged<T>(this T scrollViewer, EventHandler<ScrollChangedEventArgs> handler) where T : ScrollViewer
    {
        scrollViewer.ScrollChanged += handler;
        return scrollViewer;
    }
}

/// <summary>Fluent methods for <see cref="TransitioningContentControl"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class TransitioningContentControlMarkup
{
    /// <summary>Sets the animation played when the content changes, such as a fade or slide. <c>null</c> switches instantly.</summary>
    public static T Transition<T>(this T control, ITransition? transition) where T : TransitioningContentControl =>
        control.Set(TransitioningContentControl.TransitionProperty, transition);

    /// <summary>Overrides the duration of the transition. <c>null</c> uses the transition's own duration.</summary>
    public static T Duration<T>(this T control, TimeSpan? duration) where T : TransitioningContentControl =>
        control.Set(TransitioningContentControl.DurationProperty, duration);

    /// <summary>
    /// Overrides the easing curve of the transition, e.g. <see cref="Core.Animation.Easing.Emphasized"/>. <c>null</c>
    /// uses the transition's easing.
    /// </summary>
    public static T WithEasing<T>(this T control, Func<float, float>? easing) where T : TransitioningContentControl =>
        control.Set(TransitioningContentControl.EasingProperty, easing);

    /// <summary>Handles <see cref="TransitioningContentControl.TransitionStarted"/>, raised when a transition starts.</summary>
    public static T OnTransitionStarted<T>(this T control, EventHandler handler) where T : TransitioningContentControl
    {
        control.TransitionStarted += handler;
        return control;
    }

    /// <summary>Runs <paramref name="action"/> when a transition starts (<see cref="TransitioningContentControl.TransitionStarted"/>).</summary>
    public static T OnTransitionStarted<T>(this T control, Action action) where T : TransitioningContentControl =>
        control.OnTransitionStarted(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="TransitioningContentControl.TransitionCompleted"/>, raised when a transition has finished.</summary>
    public static T OnTransitionCompleted<T>(this T control, EventHandler handler) where T : TransitioningContentControl
    {
        control.TransitionCompleted += handler;
        return control;
    }

    /// <summary>Runs <paramref name="action"/> when a transition has finished (<see cref="TransitioningContentControl.TransitionCompleted"/>).</summary>
    public static T OnTransitionCompleted<T>(this T control, Action action) where T : TransitioningContentControl =>
        control.OnTransitionCompleted(MarkupExtensions.ToHandler(action));
}

/// <summary>Fluent methods for <see cref="KeybindingHandler"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class KeybindingHandlerMarkup
{
    /// <summary>Sets the keybinding group whose bindings this handler runs, e.g. <c>"Global"</c> or <c>"Editor"</c>.</summary>
    public static T Group<T>(this T handler, string group) where T : KeybindingHandler => handler.Set(KeybindingHandler.GroupProperty, group);
}

/// <summary>Fluent methods for <see cref="Toolbar"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ToolbarMarkup
{
    /// <summary>Sets the elevation, drawn as a drop shadow. The default is 2; 0 draws no shadow.</summary>
    public static T Elevation<T>(this T toolbar, float elevation) where T : Toolbar => toolbar.Set(Toolbar.ElevationProperty, elevation);

    /// <summary>Sets the color of the toolbar's outline. It is drawn only where <c>BorderThickness</c> is non-zero.</summary>
    public static T BorderBrush<T>(this T toolbar, Color color) where T : Toolbar => toolbar.Set(Toolbar.BorderBrushProperty, color);

    /// <summary>Sets the same outline width on all sides of the toolbar.</summary>
    public static T BorderThickness<T>(this T toolbar, float uniform) where T : Toolbar => toolbar.Set(Toolbar.BorderThicknessProperty, new Thickness(uniform));

    /// <summary>Sets the outline width of each side, e.g. only a bottom line: <c>new Thickness(0, 0, 0, 1)</c>.</summary>
    public static T BorderThickness<T>(this T toolbar, Thickness thickness) where T : Toolbar => toolbar.Set(Toolbar.BorderThicknessProperty, thickness);
}

/// <summary>
/// Fluent methods for <see cref="TitleBar"/>, the caption of windows with custom chrome. See
/// <see cref="MarkupExtensions"/> for the conventions.
/// </summary>
/// <remarks>
/// The actions that replace the window commands (<see cref="TitleBar.OnMinimize"/>, ...) are delegate properties, so
/// they're set with an object initializer rather than a method.
/// </remarks>
public static class TitleBarMarkup
{
    /// <summary>Sets the window title shown next to the icon. Empty (the default) hides it.</summary>
    public static T Title<T>(this T titleBar, string? title) where T : TitleBar => titleBar.Set(TitleBar.TitleProperty, title ?? string.Empty);

    /// <summary>
    /// Sets the icon at the left: an element, a path to a .png, .jpg or .ico image (shown 20×20), or other content.
    /// <c>null</c> (the default) hides it.
    /// </summary>
    public static T Icon<T>(this T titleBar, object? icon) where T : TitleBar => titleBar.Set(TitleBar.IconProperty, icon);

    /// <summary>Sets the content filling the space between the title and the caption buttons, such as a search box or tabs.</summary>
    public static T Content<T>(this T titleBar, object? content) where T : TitleBar => titleBar.Set(TitleBar.ContentProperty, content);

    /// <summary>Shows or hides the minimize button (shown by default).</summary>
    public static T ShowMinimizeButton<T>(this T titleBar, bool show = true) where T : TitleBar => titleBar.Set(TitleBar.ShowMinimizeButtonProperty, show);

    /// <summary>Shows or hides the maximize/restore button (shown by default).</summary>
    public static T ShowMaximizeButton<T>(this T titleBar, bool show = true) where T : TitleBar => titleBar.Set(TitleBar.ShowMaximizeButtonProperty, show);

    /// <summary>Shows or hides the close button (shown by default).</summary>
    public static T ShowCloseButton<T>(this T titleBar, bool show = true) where T : TitleBar => titleBar.Set(TitleBar.ShowCloseButtonProperty, show);
}
