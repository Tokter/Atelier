using System.Linq;
using Atelier.Audio;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Nodes;

namespace Atelier.Gallery.Views;

public class WorkspacesView : GalleryPage
{
    private readonly WorkspacesViewModel _vm;
    private readonly WorkspaceView _workspaces;

    public WorkspacesView(WorkspacesViewModel viewModel)
        : base(MaterialIconKind.SpaceDashboard, "Workspaces",
            "Areas and workspaces like Blender's: the window is divided into areas, each showing an editor picked from a " +
            "registry. Split, join, swap, resize and maximize areas, and keep several arrangements as workspaces in tabs.")
    {
        _vm = viewModel;
        _workspaces = new WorkspaceView(CreateEditors())
            .Templates(WorkspacesViewModel.Templates)
            .OnSelectionChanged(w => { if (w != null) _vm.Report($"Switched to the {w.Name} workspace"); })
            .OnLayoutChanged(_ => ReportLayout());
        _workspaces.Height = 640;
        _workspaces.Load(WorkspacesViewModel.DefaultWorkspaces);

        _vm.ResetAction = () => _workspaces.Load(WorkspacesViewModel.DefaultWorkspaces);
        _vm.SaveAction = () => _workspaces.ToDefinition().ToJson();
        _vm.RestoreAction = json => _workspaces.Load(WorkspacesDefinition.FromJson(json));

        Settings(
            new Button().Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand),
            new Button().Variant(ButtonVariant.Outlined).Command(_vm.SaveLayoutCommand),
            new Button().Variant(ButtonVariant.Outlined).Command(_vm.RestoreLayoutCommand));

        Sections(WorkspacesSection(), GesturesSection());
    }

    private string? _lastLayout;

    // Reports a change of the shown workspace's areas, once per actual change.
    private void ReportLayout()
    {
        if (_workspaces.SelectedWorkspace is not { } workspace) return;
        var areas = workspace.Layout.Areas;
        string summary = $"{workspace.Name}: {areas.Count} area{(areas.Count == 1 ? "" : "s")} ({string.Join(", ", areas.Select(a => a.EditorType?.Title ?? a.EditorId))})";
        if (summary == _lastLayout) return;
        _lastLayout = summary;
        _vm.Report(summary);
    }

    private UIElement WorkspacesSection() => Ui.Section("Workspaces and areas",
        "Each tab is a workspace with its own arrangement of areas. Drag from any corner of an area into it to split it, or " +
        "into a neighbor to join that neighbor into it; hold Ctrl to swap two areas instead. Right-click a border for the " +
        "Area Options, or a header for the area menu. The button at the top left of every area switches its editor; " +
        "switching back finds the editor as you left it. Double-click a tab to rename its workspace, right-click it to " +
        "duplicate or delete it, and use + to add one from a template.",
        new Border().CornerRadius(12).ClipToBounds(true).Child(_workspaces),
        Ui.Readout(_vm, v => v.LastEvent),
        Ui.Code("var editors = new AreaEditorRegistry()\n" +
                "    .Register(\"viewport\", \"Viewport\", MaterialIconKind.ViewInAr, area => new ViewportView())\n" +
                "    .Register(\"outliner\", \"Outliner\", MaterialIconKind.FormatListBulleted, area => new OutlinerView(), category: \"Data\");\n" +
                "var view = new WorkspaceView(editors);\n" +
                "view.AddWorkspace(\"Layout\", AreaDefinition.Row(\n" +
                "    AreaDefinition.Editor(\"viewport\", 3),\n" +
                "    AreaDefinition.Column(AreaDefinition.Editor(\"outliner\"), AreaDefinition.Editor(\"properties\"))));\n" +
                "string json = view.ToDefinition().ToJson();   // save; view.Load(WorkspacesDefinition.FromJson(json)) restores"));

    private static UIElement GesturesSection()
    {
        static UIElement Row(string gesture, string action) =>
            new Grid().Columns(GridLength.Pixels(220), GridLength.Star).ColumnSpacing(12).Children(
                new TextBlock(gesture).Bold(),
                new TextBlock(action).Muted().TextWrapping().Column(1));

        return Ui.Section("Gestures",
            "The area interactions follow Blender's window system.",
            new StackPanel().Spacing(10).Children(
                Row("Drag a border", "Resize the areas on both sides (Escape cancels)"),
                Row("Drag from a corner inward", "Split the area; the first movement decides side by side or stacked"),
                Row("Drag from a corner outward", "Join the neighbor into the area (they must share a whole edge)"),
                Row("Ctrl+drag from a corner", "Swap the area with the one you drop it on"),
                Row("Right-click a border", "Area Options: Vertical Split, Horizontal Split, Join Areas, Swap Areas"),
                Row("Right-click a header", "Split, Maximize Area and Close Area"),
                Row("Ctrl+Space", "Maximize the area under the pointer, or go back"),
                Row("Ctrl+Page Up / Page Down", "Previous or next workspace"),
                Row("Double-click a tab", "Rename the workspace (right-click for more)")));
    }

    #region Editors

    private AreaEditorRegistry CreateEditors() => new AreaEditorRegistry()
        .Register("viewport", "Viewport", MaterialIconKind.ViewInAr, _ => Viewport(), "General", _ => HeaderMenus("View", "Select", "Add", "Object"))
        .Register("nodes", "Shader Editor", MaterialIconKind.AccountTree, _ => Nodes(), "General", _ => HeaderMenus("View", "Select", "Add", "Node"))
        .Register("timeline", "Timeline", MaterialIconKind.ViewTimeline, _ => Timeline(), "Animation", _ => HeaderMenus("Playback", "Keying", "View", "Marker"))
        .Register("outliner", "Outliner", MaterialIconKind.FormatListBulleted, _ => Outliner(), "Data")
        .Register("properties", "Properties", MaterialIconKind.Tune, _ => Properties(), "Data")
        .Register("info", "Info", MaterialIconKind.Info, _ => Info(), "Scripting", _ => InfoHeader());

    // Blender-style menu names in an editor's header.
    private static UIElement HeaderMenus(params string[] names) =>
        new StackPanel().Orientation(Orientation.Horizontal).Spacing(2).VerticalAlignment(VerticalAlignment.Center).Children(
            names.Select(name => (UIElement)new Button(name).Variant(ButtonVariant.Text).StyleKey(Area.EditorButtonStyleKey).Height(26).MinHeight(0).MinWidth(0).Padding(10, 0).FontSize(13).FontWeight(FontWeight.Normal)).ToArray());

    private static UIElement Viewport() =>
        new Grid().Children(
            new Image().Source(SampleImages.Logo).Stretch(Stretch.Uniform).Width(160).Height(160)
                .HorizontalAlignment(HorizontalAlignment.Center).VerticalAlignment(VerticalAlignment.Center),
            new TextBlock("User Perspective").BodySmall().Muted().Margin(12, 8));

    private static UIElement Nodes() =>
        new NodeEditor { Graph = new NodeEditorViewModel().Graph, Zoom = 0.7f, Offset = new Point(8, 8) };

    private static UIElement Timeline()
    {
        var song = new TimelineContext { TempoMap = TempoMap.Constant(120) };
        var ruler = new TimelineRuler().Timeline(song).ShowMarkers(false);
        DockPanel.SetDock(ruler, Dock.Top);
        return new DockPanel().Children(ruler, new AudioWaveformEditor().Timeline(song).Source(TimelineSounds.Bass));
    }

    private static UIElement Outliner() =>
        new ScrollViewer().Content(new TreeView().Margin(4).RootItems(
            new TreeViewItem("Scene Collection").IsExpanded().ChildrenItems(
                new TreeViewItem("Collection").IsExpanded().ChildrenItems(
                    new TreeViewItem("Camera"),
                    new TreeViewItem("Cube").IsSelected(),
                    new TreeViewItem("Light")),
                new TreeViewItem("Props").ChildrenItems(
                    new TreeViewItem("Chair"),
                    new TreeViewItem("Table")))));

    private static UIElement Properties()
    {
        static UIElement Setting(string label, float value, float maximum) =>
            new StackPanel().Spacing(2).Children(
                new TextBlock(label).BodySmall().Muted(),
                new Slider().Minimum(0).Maximum(maximum).Value(value));

        return new ScrollViewer().Content(new StackPanel().Spacing(12).Margin(16, 12).Children(
            new TextBlock("Transform").TitleSmall(),
            Setting("Location X", 1.2f, 10),
            Setting("Location Y", 4f, 10),
            Setting("Rotation", 45, 360),
            Setting("Scale", 1, 4),
            new TextBlock("Visibility").TitleSmall(),
            new Switch("Show in viewports").IsChecked(true),
            new Switch("Show in renders").IsChecked(true),
            new Switch("Holdout")));
    }

    private UIElement Info() => new ListBox().ItemsSource(_vm.Log).Margin(4);

    private UIElement InfoHeader() =>
        new Button("Clear").Variant(ButtonVariant.Text).StyleKey(Area.EditorButtonStyleKey).Height(26).MinHeight(0).MinWidth(0).Padding(10, 0).FontSize(13)
            .OnClick(() => _vm.Log.Clear());

    #endregion
}
