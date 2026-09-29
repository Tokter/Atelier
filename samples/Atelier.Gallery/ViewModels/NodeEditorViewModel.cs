using System;
using System.IO;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

/// <summary>
/// The node editor page: a small graph of numbers and colors that is evaluated as it's edited, with the editor's
/// options, and saving to a file.
/// </summary>
public partial class NodeEditorViewModel : PageViewModel
{
    /// <summary>The keybinding group of this page's commands.</summary>
    public const string Group = "NodeEditorPage";

    /// <summary>Gets the file the graph is saved to: <c>%APPDATA%\Atelier\Gallery\node-graph.json</c> on Windows.</summary>
    public static string SavePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Atelier", "Gallery", "node-graph.json");

    private readonly GraphEvaluator _evaluator;

    [ObservableProperty]
    private bool _useDotGrid;

    [ObservableProperty]
    private bool _snapToGrid;

    [ObservableProperty]
    private bool _autoInsert = true;

    [ObservableProperty]
    private string _status = "Right-click or press Shift+A to add nodes.";

    public NodeEditorViewModel()
    {
        PageIcon = MaterialIconKind.AccountTree;
        PageTitle = "Node Editor";
        CommandGroup = Group;
        Keywords = "node editor graph blender socket link reroute knob dataflow evaluation";
        DemoSockets.RegisterNodes(Graph.Catalog);
        BuildDemo();
        _evaluator = new GraphEvaluator(Graph);
    }

    /// <summary>Gets the graph the page edits.</summary>
    public NodeGraphViewModel Graph { get; } = new();

    [RelayCommand]
    [property: Command("Reset", Group, Icon = MaterialIcons.RestartAlt, Description = "Put the demo graph back as it was")]
    private void Reset()
    {
        Graph.Clear();
        BuildDemo();
        _evaluator.InvalidateAll();
        Status = "The demo graph is back.";
    }

    [RelayCommand]
    [property: Command("SaveGraph", Group, Label = "Save", Icon = MaterialIcons.Save, Description = "Save the graph to node-graph.json in the gallery's settings folder")]
    private void SaveGraph()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        File.WriteAllText(SavePath, NodeGraphSerializer.Save(Graph));
        Status = $"Saved to {SavePath}";
        LoadGraphCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanLoadGraph))]
    [property: Command("LoadGraph", Group, Label = "Load", Icon = MaterialIcons.FileOpen, Description = "Load the graph saved last")]
    private void LoadGraph()
    {
        var problems = NodeGraphSerializer.Load(Graph, File.ReadAllText(SavePath));
        _evaluator.InvalidateAll();
        Status = problems.Count == 0 ? "Loaded the saved graph." : string.Join(" ", problems);
    }

    private static bool CanLoadGraph() => File.Exists(SavePath);

    // Two values multiplied and clamped, and a color mixed by the result, both shown by a viewer.
    private void BuildDemo()
    {
        var value = (ValueNode)Graph.AddNode("value", new Point(20, 30));
        var scale = (ValueNode)Graph.AddNode("value", new Point(20, 180));
        scale.Title = "Scale";
        scale.Inputs[0].Value = 0.8;
        var math = (MathNode)Graph.AddNode("math", new Point(190, 50));
        math.Operation = MathOperation.Multiply;
        var clamp = Graph.AddNode("clamp", new Point(400, 30));
        var rgb = Graph.AddNode("rgb", new Point(190, 320));
        var mix = Graph.AddNode("mix", new Point(590, 290));
        var viewer = Graph.AddNode("viewer", new Point(820, 140));

        Graph.Connect(value.Outputs[0], math.Inputs[0]);
        Graph.Connect(scale.Outputs[0], math.Inputs[1]);
        Graph.Connect(math.Outputs[0], clamp.Inputs[0]);
        Graph.Connect(clamp.Outputs[0], viewer.Inputs[0]);
        Graph.Connect(clamp.Outputs[0], mix.Inputs[0]);
        Graph.Connect(rgb.Outputs[0], mix.Inputs[1]);
        Graph.Connect(mix.Outputs[0], viewer.Inputs[1]);
        Graph.Undo.Clear();
    }
}
