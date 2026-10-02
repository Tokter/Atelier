using System;
using Atelier.Core.Properties;

namespace Atelier.Controls;

/// <summary>
/// A named arrangement of areas (an <see cref="AreaLayout"/>), like a Blender workspace; a <see cref="WorkspaceView"/>
/// shows one at a time and switches between them with tabs.
/// </summary>
/// <remarks>
/// The layout lives as long as the workspace, so switching to another workspace and back finds every area and editor as
/// it was.
/// </remarks>
public class Workspace : BindableObject
{
    /// <summary>Identifies the <see cref="Name"/> property.</summary>
    public static readonly BindableProperty<string> NameProperty =
        BindableProperty.Register<Workspace, string>(nameof(Name), "Workspace");

    /// <summary>Initializes a workspace that shows <paramref name="layout"/>.</summary>
    /// <param name="name">The name shown on the workspace's tab.</param>
    /// <param name="layout">The areas of the workspace.</param>
    public Workspace(string name, AreaLayout layout)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        layout.Workspace = this;
    }

    /// <summary>Initializes a workspace with the areas of <paramref name="root"/>.</summary>
    /// <param name="name">The name shown on the workspace's tab.</param>
    /// <param name="editors">The editor types the areas can show.</param>
    /// <param name="root">The arrangement of the areas; <c>null</c> for one area with the first registered editor type.</param>
    public Workspace(string name, AreaEditorRegistry editors, AreaDefinition? root = null) : this(name, new AreaLayout(editors, root))
    {
    }

    /// <summary>Gets or sets the name shown on the workspace's tab.</summary>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <summary>Gets the areas of the workspace.</summary>
    public AreaLayout Layout { get; }

    /// <summary>Describes the workspace (its name and arrangement), for saving or duplicating it.</summary>
    public WorkspaceDefinition ToDefinition() => new(Name, Layout.ToDefinition());

    /// <inheritdoc/>
    public override string ToString() => Name;
}
