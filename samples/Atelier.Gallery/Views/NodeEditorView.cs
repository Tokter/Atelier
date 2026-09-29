using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Nodes;

namespace Atelier.Gallery.Views;

public class NodeEditorView : GalleryPage
{
    private readonly NodeEditorViewModel _vm;
    private readonly NodeEditor _editor;

    public NodeEditorView(NodeEditorViewModel viewModel)
        : base(MaterialIconKind.AccountTree, "Node Editor",
            "A Blender-style node editor: nodes with typed sockets, links between them, value controls next to the " +
            "inputs, and a graph that is evaluated as you edit it. Every action is a command you can rebind, to keys or " +
            "to pointer gestures.")
    {
        _vm = viewModel;
        _editor = new NodeEditor { Graph = _vm.Graph, Height = 560, Zoom = 0.9f, Offset = new Point(8, 16) }
            .Bind(NodeEditor.SnapToGridProperty, _vm, v => v.SnapToGrid)
            .Bind(NodeEditor.AutoInsertProperty, _vm, v => v.AutoInsert);
        UpdateBackground();

        // Save and Open ask for the file with the file dialogs.
        const string filter = "Node graph (*.json)|*.json|All files (*.*)|*.*";
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var saveDialog = new SaveFileDialog { Title = "Save the graph", Filter = filter, FileName = "node-graph", DefaultExt = "json", InitialDirectory = documents };
        var openDialog = new OpenFileDialog { Title = "Open a graph", Filter = filter, InitialDirectory = documents };
        _vm.ChooseSaveFile = async () => await saveDialog.ShowAsync(this) ? saveDialog.FileName : null;
        _vm.ChooseOpenFile = async () => await openDialog.ShowAsync(this) ? openDialog.FileName : null;

        Settings(
            new Switch("Dot grid").BindIsChecked(_vm, v => v.UseDotGrid, (v, on) => v.UseDotGrid = on),
            new Switch("Snap to grid").BindIsChecked(_vm, v => v.SnapToGrid, (v, on) => v.SnapToGrid = on),
            new Switch("Insert nodes dropped on links").BindIsChecked(_vm, v => v.AutoInsert, (v, on) => v.AutoInsert = on),
            new Button().Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand),
            new Button().Variant(ButtonVariant.Outlined).Command(_vm.SaveGraphCommand),
            new Button().Variant(ButtonVariant.Outlined).Command(_vm.LoadGraphCommand));

        Sections(GraphSection(), ShortcutsSection());
    }

    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        _vm.PropertyChanged += OnViewModelChanged;
        UpdateBackground();
    }

    protected override void OnDetachedFromVisualTree()
    {
        base.OnDetachedFromVisualTree();
        _vm.PropertyChanged -= OnViewModelChanged;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeEditorViewModel.UseDotGrid)) UpdateBackground();
    }

    private void UpdateBackground() =>
        _editor.BackgroundLayers[0] = _vm.UseDotGrid ? new DotGridLayer() : new GridLayer();

    private UIElement GraphSection()
    {
        // The editor's own commands, run on the editor: their labels, icons and shortcuts come from their registrations.
        Button Tool(ICommand command) => new Button().Variant(ButtonVariant.Text).CommandDisplay(CommandDisplay.Icon).Command(command, _editor);

        var toolbar = new WrapPanel().Spacing(4, 4).Children(
            new Button().Variant(ButtonVariant.Tonal).Command(NodeEditorCommands.AddNode, _editor),
            Tool(NodeEditorCommands.Undo),
            Tool(NodeEditorCommands.Redo),
            Tool(NodeEditorCommands.Duplicate),
            Tool(NodeEditorCommands.Delete),
            Tool(NodeEditorCommands.ToggleCollapse),
            Tool(NodeEditorCommands.ToggleMute),
            Tool(NodeEditorCommands.MakeGroup),
            Tool(NodeEditorCommands.EnterGroup),
            Tool(NodeEditorCommands.FrameAll));

        return Ui.Section("Graph",
            "Drag nodes to move them and sockets to connect them; drop a link on empty space to add a node for it. The " +
            "Viewer shows the result, updated as you turn the knobs. Middle-drag pans, the wheel zooms, Ctrl+right drag " +
            "cuts links and Shift+right drag adds reroute points. The Remap nodes use one group: select one and press Tab " +
            "to edit it (both change), or select nodes and press Ctrl+G to make a group of your own.",
            toolbar,
            new Border().CornerRadius(12).ClipToBounds().Child(_editor),
            Ui.Readout(_vm, v => v.Status));
    }

    private static UIElement ShortcutsSection()
    {
        var rows = KeybindingManager.GetKeybindings(NodeEditor.CommandGroup)
            .Select(d => (UIElement)new Grid()
                .Columns(GridLength.Star, GridLength.Auto)
                .ColumnSpacing(12)
                .Children(
                    new StackPanel().Spacing(2).Children(
                        new TextBlock(AccessText.Parse(d.Label, out _)).Bold(),
                        new TextBlock(d.Description).BodySmall().Muted().TextWrapping()),
                    new ShortcutView(d.Keybinding).Column(1).VerticalAlignment(VerticalAlignment.Center)))
            .ToArray();

        return Ui.Section("Commands",
            "The editor's actions, with their current shortcuts. Change them in the command editor (Ctrl+K, Ctrl+S): " +
            "record a key, a click, a drag or a wheel turn.",
            Ui.Columns(360, rows));
    }

    /// <summary>The view of a math node's settings: a compact combo box of the operations.</summary>
    public static UIElement MathSettingsView(MathSettings settings) =>
        new ComboBox()
            .Items(Enum.GetValues<MathOperation>().Cast<object>().ToArray())
            .IsCompact()
            .Bind(ComboBox.SelectedItemProperty, settings.Node, n => (object?)n.Operation, (n, item) =>
            {
                if (item is MathOperation operation) n.Operation = operation;
            });

    /// <summary>The view of a viewer node's display: the color as a swatch and the number.</summary>
    public static UIElement ViewerDisplayView(ViewerDisplay display) =>
        new StackPanel().Orientation(Orientation.Horizontal).Spacing(10).Children(
            new Border().Width(36).Height(24).CornerRadius(4).BorderThickness(1).BorderBrush(Color.Black.WithAlpha(0.3f))
                .Bind(Border.BackgroundProperty, display, d => d.Color),
            new TextBlock().VerticalAlignment(VerticalAlignment.Center).TitleMedium()
                .Bind(TextBlock.TextProperty, display, d => d.Text));
}
