using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// Describes an arrangement of areas without creating it: an <see cref="EditorAreaDefinition"/> (one area and its
/// editor) or a <see cref="SplitAreaDefinition"/> (areas side by side or stacked). Used to build an
/// <see cref="AreaLayout"/>, to duplicate one (<see cref="AreaLayout.ToDefinition"/>) and to save and load layouts as
/// JSON.
/// </summary>
/// <remarks>
/// <see cref="Weight"/> is a node's share of its split, like a star size: two areas weighted 3 and 1 get 75% and 25%.
/// </remarks>
/// <example>
/// <code>
/// // A viewport over a timeline on the left, an outliner over properties on the right (a quarter of the width).
/// var layout = AreaDefinition.Row(
///     AreaDefinition.Column(AreaDefinition.Editor("viewport", 3), AreaDefinition.Editor("timeline")).WithWeight(3),
///     AreaDefinition.Column(AreaDefinition.Editor("outliner"), AreaDefinition.Editor("properties", 2)));
/// </code>
/// </example>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(EditorAreaDefinition), "area")]
[JsonDerivedType(typeof(SplitAreaDefinition), "split")]
public abstract record AreaDefinition
{
    /// <summary>Gets the node's share of its split (relative to its siblings' weights). The default is 1.</summary>
    public float Weight { get; init; } = 1f;

    /// <summary>Returns a copy of this definition with <paramref name="weight"/>.</summary>
    public AreaDefinition WithWeight(float weight) => this with { Weight = weight };

    /// <summary>Creates an area that shows the editor with <paramref name="editorId"/>.</summary>
    public static EditorAreaDefinition Editor(string editorId, float weight = 1f) => new(editorId) { Weight = weight };

    /// <summary>Creates areas side by side, left to right (a horizontal split).</summary>
    public static SplitAreaDefinition Row(params AreaDefinition[] children) => new(Orientation.Horizontal, children);

    /// <summary>Creates areas stacked top to bottom (a vertical split).</summary>
    public static SplitAreaDefinition Column(params AreaDefinition[] children) => new(Orientation.Vertical, children);

    /// <summary>Gets the ids of the editors of all areas, left to right and top to bottom.</summary>
    public IEnumerable<string> GetEditorIds() => this switch
    {
        EditorAreaDefinition area => [area.EditorId],
        SplitAreaDefinition split => split.Children.SelectMany(c => c.GetEditorIds()),
        _ => [],
    };
}

/// <summary>Describes one area and the editor it shows.</summary>
/// <param name="EditorId">The id of the editor type (see <see cref="AreaEditorType.Id"/>).</param>
public sealed record EditorAreaDefinition(string EditorId) : AreaDefinition;

/// <summary>Describes areas (or further splits) side by side or stacked.</summary>
/// <param name="Orientation">
/// <see cref="Orientation.Horizontal"/> puts the children side by side, <see cref="Orientation.Vertical"/> stacks them.
/// </param>
/// <param name="Children">The children, at least one.</param>
public sealed record SplitAreaDefinition(Orientation Orientation, IReadOnlyList<AreaDefinition> Children) : AreaDefinition;

/// <summary>Describes a workspace: its name and its arrangement of areas.</summary>
/// <param name="Name">The name shown on the workspace's tab.</param>
/// <param name="Root">The arrangement of its areas.</param>
public sealed record WorkspaceDefinition(string Name, AreaDefinition Root);

/// <summary>
/// Describes the workspaces of a <see cref="WorkspaceView"/> and which one is shown, for saving and restoring them
/// (see <see cref="WorkspaceView.ToDefinition"/> and <see cref="WorkspaceView.Load(WorkspacesDefinition)"/>).
/// </summary>
/// <param name="Workspaces">The workspaces in tab order.</param>
/// <param name="SelectedIndex">The index of the shown workspace.</param>
public sealed record WorkspacesDefinition(IReadOnlyList<WorkspaceDefinition> Workspaces, int SelectedIndex = 0)
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Writes the workspaces as JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, s_options);

    /// <summary>Reads workspaces written by <see cref="ToJson"/>.</summary>
    /// <exception cref="JsonException">The text is not a valid workspaces definition.</exception>
    public static WorkspacesDefinition FromJson(string json) =>
        JsonSerializer.Deserialize<WorkspacesDefinition>(json, s_options) ?? throw new JsonException("The JSON holds no workspaces.");
}
