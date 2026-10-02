using System;
using System.Collections.Generic;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// A kind of editor an <see cref="Area"/> can show, like Blender's editor types (3D Viewport, Outliner, Properties...):
/// its id, title and icon for the area's editor menu, and the factory of its content.
/// </summary>
/// <remarks>
/// Register editor types in an <see cref="AreaEditorRegistry"/>; areas refer to them by <see cref="Id"/>, so saved
/// layouts (see <see cref="AreaDefinition"/>) stay valid while the editor's code changes.
/// </remarks>
/// <param name="id">The id areas refer to the editor by, e.g. <c>"outliner"</c>.</param>
/// <param name="title">The title shown in the editor menu, e.g. <c>"Outliner"</c>.</param>
/// <param name="createContent">Creates the editor's content for an area; called once per area and editor type.</param>
public sealed class AreaEditorType(string id, string title, Func<Area, UIElement> createContent)
{
    /// <summary>Gets the id areas refer to the editor by.</summary>
    public string Id { get; } = !string.IsNullOrWhiteSpace(id) ? id : throw new ArgumentException("An editor needs an id.", nameof(id));

    /// <summary>Gets the title shown in the editor menu and the area's tooltip.</summary>
    public string Title { get; } = title ?? throw new ArgumentNullException(nameof(title));

    /// <summary>Gets the factory of the editor's content, called once per area the editor is shown in.</summary>
    public Func<Area, UIElement> CreateContent { get; } = createContent ?? throw new ArgumentNullException(nameof(createContent));

    /// <summary>Gets or sets the icon of the editor, shown on the area's editor button and in the editor menu.</summary>
    public MaterialIconKind Icon { get; init; } = MaterialIconKind.Dashboard;

    /// <summary>
    /// Gets or sets the group the editor is listed under in the editor menu (e.g. "General", "Animation"); editors
    /// without a category come first.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>Gets or sets a short description, shown as the tooltip of the editor's menu item.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the factory of the content shown in the area's header after the editor button, such as the editor's
    /// menus and tools; <c>null</c> (the default) for none. It runs after <see cref="CreateContent"/>, so
    /// <see cref="Area.EditorContent"/> is the new content (and its DataContext can be shared).
    /// </summary>
    public Func<Area, UIElement?>? CreateHeader { get; init; }

    /// <inheritdoc/>
    public override string ToString() => Title;
}

/// <summary>
/// The editor types areas can switch between (see <see cref="AreaEditorType"/>), in the order the editor menu lists
/// them.
/// </summary>
/// <example>
/// <code>
/// var editors = new AreaEditorRegistry()
///     .Register("viewport", "Viewport", MaterialIconKind.ViewInAr, area => new ViewportView())
///     .Register("outliner", "Outliner", MaterialIconKind.AccountTree, area => new OutlinerView(), category: "Data");
/// </code>
/// </example>
public class AreaEditorRegistry
{
    private readonly List<AreaEditorType> _editors = [];
    private readonly Dictionary<string, AreaEditorType> _byId = new(StringComparer.Ordinal);

    /// <summary>Gets the registered editor types in registration order.</summary>
    public IReadOnlyList<AreaEditorType> Editors => _editors;

    /// <summary>Gets the number of registered editor types.</summary>
    public int Count => _editors.Count;

    /// <summary>Occurs after an editor type was registered or removed.</summary>
    public event EventHandler? Changed;

    /// <summary>Registers <paramref name="editor"/>, replacing a registered editor type with the same id.</summary>
    /// <returns>This registry, for chaining.</returns>
    public AreaEditorRegistry Register(AreaEditorType editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (_byId.TryGetValue(editor.Id, out var existing))
        {
            _editors[_editors.IndexOf(existing)] = editor;
        }
        else
        {
            _editors.Add(editor);
        }
        _byId[editor.Id] = editor;
        Changed?.Invoke(this, EventArgs.Empty);
        return this;
    }

    /// <summary>Registers an editor type (see <see cref="AreaEditorType"/>).</summary>
    /// <param name="id">The id areas refer to the editor by.</param>
    /// <param name="title">The title shown in the editor menu.</param>
    /// <param name="icon">The editor's icon.</param>
    /// <param name="createContent">Creates the editor's content for an area.</param>
    /// <param name="category">The group the editor menu lists it under, or <c>null</c>.</param>
    /// <param name="createHeader">Creates the content of the area's header after the editor button, or <c>null</c>.</param>
    /// <returns>This registry, for chaining.</returns>
    public AreaEditorRegistry Register(string id, string title, MaterialIconKind icon, Func<Area, UIElement> createContent,
        string? category = null, Func<Area, UIElement?>? createHeader = null) =>
        Register(new AreaEditorType(id, title, createContent) { Icon = icon, Category = category, CreateHeader = createHeader });

    /// <summary>Removes the editor type with <paramref name="id"/>; areas showing it keep their content.</summary>
    /// <returns><c>true</c> if it was registered.</returns>
    public bool Unregister(string id)
    {
        if (!_byId.Remove(id, out var editor)) return false;
        _editors.Remove(editor);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Gets the editor type with <paramref name="id"/>, or <c>null</c>.</summary>
    public AreaEditorType? Find(string? id) => id != null && _byId.TryGetValue(id, out var editor) ? editor : null;

    /// <summary>
    /// Gets the editor types grouped by <see cref="AreaEditorType.Category"/>, the groups in the order their first
    /// editor was registered (editors without a category first).
    /// </summary>
    public IReadOnlyList<(string? Category, IReadOnlyList<AreaEditorType> Editors)> GetGroups()
    {
        var groups = new List<(string? Category, List<AreaEditorType> Editors)>();
        foreach (var editor in _editors)
        {
            int index = groups.FindIndex(g => g.Category == editor.Category);
            if (index < 0)
            {
                // Uncategorized editors are listed first.
                index = editor.Category == null ? 0 : groups.Count;
                groups.Insert(index, (editor.Category, []));
            }
            groups[index].Editors.Add(editor);
        }
        return groups.ConvertAll(g => (g.Category, (IReadOnlyList<AreaEditorType>)g.Editors));
    }
}
