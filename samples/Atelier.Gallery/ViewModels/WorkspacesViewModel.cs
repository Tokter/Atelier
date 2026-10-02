using System;
using System.Collections.ObjectModel;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class WorkspacesViewModel : PageViewModel
{
    /// <summary>The keybinding group of this page's commands.</summary>
    public const string Group = "Workspaces";

    /// <summary>The workspaces the page starts with (and Reset brings back), like Blender's default workspaces.</summary>
    public static WorkspacesDefinition DefaultWorkspaces { get; } = new(
    [
        new WorkspaceDefinition("Layout", AreaDefinition.Column(
            AreaDefinition.Row(
                AreaDefinition.Editor("viewport", 3),
                AreaDefinition.Column(AreaDefinition.Editor("outliner"), AreaDefinition.Editor("properties", 1.6f))),
            AreaDefinition.Editor("timeline").WithWeight(0.45f))),
        new WorkspaceDefinition("Shading", AreaDefinition.Row(
            AreaDefinition.Column(AreaDefinition.Editor("viewport"), AreaDefinition.Editor("nodes", 1.3f)).WithWeight(3),
            AreaDefinition.Editor("properties"))),
        new WorkspaceDefinition("Scripting", AreaDefinition.Row(
            AreaDefinition.Editor("info"),
            AreaDefinition.Column(AreaDefinition.Editor("viewport"), AreaDefinition.Editor("outliner")))),
    ]);

    /// <summary>The templates the "+" button offers.</summary>
    public static WorkspaceDefinition[] Templates { get; } =
    [
        new("General", AreaDefinition.Editor("viewport")),
        new("Compositing", AreaDefinition.Column(AreaDefinition.Editor("viewport"), AreaDefinition.Editor("nodes"))),
        new("Animation", AreaDefinition.Column(
            AreaDefinition.Row(AreaDefinition.Editor("outliner"), AreaDefinition.Editor("viewport", 3)),
            AreaDefinition.Editor("timeline"))),
    ];

    [ObservableProperty]
    private string _lastEvent = "Drag from an area's corner, right-click a border, or switch the editor in a header";

    [ObservableProperty]
    private string? _savedLayout;

    /// <summary>What happened in the workspaces, newest first; the Info editor lists it.</summary>
    public ObservableCollection<string> Log { get; } = [];

    /// <summary>Set by the view: puts the workspaces back to <see cref="DefaultWorkspaces"/>.</summary>
    public Action? ResetAction { get; set; }

    /// <summary>Set by the view: describes the workspaces as JSON.</summary>
    public Func<string>? SaveAction { get; set; }

    /// <summary>Set by the view: restores workspaces from JSON.</summary>
    public Action<string>? RestoreAction { get; set; }

    public WorkspacesViewModel()
    {
        PageIcon = MaterialIconKind.SpaceDashboard;
        PageTitle = "Workspaces";
        CommandGroup = Group;
        Keywords = "workspace workspaces area areas editor split join swap maximize blender layout docking panes tabs";
    }

    public void Report(string text)
    {
        LastEvent = text;
        Log.Insert(0, $"{DateTime.Now:HH:mm:ss}  {text}");
        while (Log.Count > 200) Log.RemoveAt(Log.Count - 1);
    }

    [RelayCommand]
    [property: Command("Reset", Group, Icon = MaterialIcons.RestartAlt, Description = "Put the workspaces of this page back as they were")]
    private void Reset()
    {
        ResetAction?.Invoke();
        Report("Reset the workspaces");
    }

    [RelayCommand]
    [property: Command("SaveLayout", Group, Label = "Save layout", Icon = MaterialIcons.Save, Description = "Remember the workspaces and their areas as JSON")]
    private void SaveLayout()
    {
        SavedLayout = SaveAction?.Invoke();
        RestoreLayoutCommand.NotifyCanExecuteChanged();
        Report("Saved the layout");
    }

    private bool CanRestoreLayout() => SavedLayout != null;

    [RelayCommand(CanExecute = nameof(CanRestoreLayout))]
    [property: Command("RestoreLayout", Group, Label = "Restore layout", Icon = MaterialIcons.Restore, Description = "Restore the workspaces saved with Save layout")]
    private void RestoreLayout()
    {
        if (SavedLayout == null) return;
        RestoreAction?.Invoke(SavedLayout);
        Report("Restored the saved layout");
    }
}
