using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Markup;

/// <summary>Fluent methods for <see cref="AreaEditorRegistry"/>.</summary>
public static class AreaEditorRegistryMarkup
{
    /// <summary>
    /// Registers an editor type whose content is a <typeparamref name="TView"/> made by <paramref name="create"/>
    /// (see <see cref="AreaEditorRegistry.Register(string, string, MaterialIconKind, Func{Area, UIElement}, string?, Func{Area, UIElement?}?)"/>).
    /// </summary>
    public static AreaEditorRegistry Editor<TView>(this AreaEditorRegistry registry, string id, string title, MaterialIconKind icon,
        Func<TView> create, string? category = null) where TView : UIElement
    {
        ArgumentNullException.ThrowIfNull(create);
        return registry.Register(id, title, icon, _ => create(), category);
    }
}

/// <summary>Fluent methods for <see cref="Area"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class AreaMarkup
{
    /// <summary>Sets the id of the editor type the area shows.</summary>
    public static T EditorId<T>(this T area, string? editorId) where T : Area => area.Set(Area.EditorIdProperty, editorId);

    /// <summary>Sets whether the header (with the editor button) is shown.</summary>
    public static T ShowHeader<T>(this T area, bool show = true) where T : Area => area.Set(Area.ShowHeaderProperty, show);
}

/// <summary>Fluent methods for <see cref="AreaLayout"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class AreaLayoutMarkup
{
    /// <summary>Sets the gap between areas and around them, in pixels (4 by default).</summary>
    public static T Spacing<T>(this T layout, float spacing) where T : AreaLayout => layout.Set(AreaLayout.SpacingProperty, spacing);

    /// <summary>Handles <see cref="AreaLayout.LayoutChanged"/>, raised after the arrangement, a size or an editor changed.</summary>
    public static T OnLayoutChanged<T>(this T layout, Action<T> handler) where T : AreaLayout
    {
        ArgumentNullException.ThrowIfNull(handler);
        layout.LayoutChanged += (_, _) => handler(layout);
        return layout;
    }
}

/// <summary>Fluent methods for <see cref="WorkspaceView"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class WorkspaceViewMarkup
{
    /// <summary>Adds workspaces from definitions (names are made unique).</summary>
    public static T Workspaces<T>(this T view, params WorkspaceDefinition[] workspaces) where T : WorkspaceView
    {
        foreach (var workspace in workspaces)
        {
            view.AddWorkspace(workspace);
        }
        return view;
    }

    /// <summary>Adds a workspace with the areas of <paramref name="root"/>.</summary>
    public static T Workspace<T>(this T view, string name, AreaDefinition root) where T : WorkspaceView
    {
        view.AddWorkspace(name, root);
        return view;
    }

    /// <summary>Sets the templates the "+" button offers (see <see cref="WorkspaceView.Templates"/>).</summary>
    public static T Templates<T>(this T view, params WorkspaceDefinition[] templates) where T : WorkspaceView
    {
        view.Templates.Clear();
        foreach (var template in templates)
        {
            view.Templates.Add(template);
        }
        return view;
    }

    /// <summary>Shows the workspace at <paramref name="index"/>.</summary>
    public static T SelectedIndex<T>(this T view, int index) where T : WorkspaceView => view.Set(WorkspaceView.SelectedIndexProperty, index);

    /// <summary>Binds the index of the shown workspace to a source value. With a <paramref name="setter"/>, switching tabs is written back.</summary>
    public static T BindSelectedIndex<T, TSource>(this T view, TSource source, Func<TSource, int> getter, Action<TSource, int>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : WorkspaceView where TSource : class =>
        view.BindToSource(WorkspaceView.SelectedIndexProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Sets content before the tabs in the strip, such as a menu bar.</summary>
    public static T LeadingContent<T>(this T view, object? content) where T : WorkspaceView => view.Set(WorkspaceView.LeadingContentProperty, content);

    /// <summary>Sets content after the tabs, at the right edge of the strip.</summary>
    public static T TrailingContent<T>(this T view, object? content) where T : WorkspaceView => view.Set(WorkspaceView.TrailingContentProperty, content);

    /// <summary>Handles <see cref="WorkspaceView.SelectionChanged"/>, raised with the newly shown workspace.</summary>
    public static T OnSelectionChanged<T>(this T view, Action<Workspace?> handler) where T : WorkspaceView
    {
        ArgumentNullException.ThrowIfNull(handler);
        view.SelectionChanged += (_, workspace) => handler(workspace);
        return view;
    }

    /// <summary>
    /// Handles <see cref="WorkspaceView.LayoutChanged"/>, raised after any change worth saving (workspaces added, removed,
    /// renamed, reordered or switched, and changes to their areas).
    /// </summary>
    public static T OnLayoutChanged<T>(this T view, Action<T> handler) where T : WorkspaceView
    {
        ArgumentNullException.ThrowIfNull(handler);
        view.LayoutChanged += (_, _) => handler(view);
        return view;
    }
}
