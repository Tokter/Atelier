using System;
using System.IO;
using System.Threading.Tasks;
using Atelier.Audio;
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

    /// <summary>Gets the file the graph is saved to when no dialog asks: <c>%APPDATA%\Atelier\Gallery\node-graph.json</c> on Windows.</summary>
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
    private string _status = "Right-click or press Shift+A to add nodes. Select a Remap node and press Tab to edit its group.";

    public NodeEditorViewModel()
    {
        PageIcon = MaterialIconKind.AccountTree;
        PageTitle = "Node Editor";
        CommandGroup = Group;
        Keywords = "node editor graph blender socket link reroute knob dataflow evaluation group subgraph";
        InputEditors.Register(InputEditor.Knob, CreateKnob);
        DemoSockets.RegisterNodes(Graph.Catalog);
        BuildDemo();
        _evaluator = new GraphEvaluator(Graph);
    }

    // The node editor has no knob of its own; Atelier.Audio's is 28 px and resets to the value it started with.
    private static Knob CreateKnob(InputSocketViewModel input)
    {
        var range = InputEditors.GetNumberRange(input);
        var knob = new Knob
        {
            Minimum = range.Minimum,
            Maximum = range.Maximum,
            Width = 28,
            Height = 28,
            ValueFormat = range.ValueFormat,
        };
        knob.SmallChange = range.SmallChange;
        knob.LargeChange = range.LargeChange;
        knob.DefaultValue = (float)InputEditors.ToDouble(input.Value);
        InputEditors.BindNumber(knob, input, v => knob.Value = v, h => knob.ValueChanged += h);
        return knob;
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

    /// <summary>
    /// Gets or sets how the page asks where to save the graph (the view shows a Save dialog); it returns the chosen path,
    /// or <c>null</c> when canceled. Without it, the graph is saved to <see cref="SavePath"/>.
    /// </summary>
    public Func<Task<string?>>? ChooseSaveFile { get; set; }

    /// <summary>
    /// Gets or sets how the page asks which graph to open (the view shows an Open dialog); it returns the chosen path, or
    /// <c>null</c> when canceled. Without it, the graph is loaded from <see cref="SavePath"/>.
    /// </summary>
    public Func<Task<string?>>? ChooseOpenFile { get; set; }

    [RelayCommand]
    [property: Command("SaveGraph", Group, Label = "Save…", Icon = MaterialIcons.Save, Description = "Save the graph as a JSON file")]
    private async Task SaveGraph()
    {
        string? path = ChooseSaveFile != null ? await ChooseSaveFile() : SavePath;
        if (path == null) return;
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } folder) Directory.CreateDirectory(folder);
            File.WriteAllText(path, NodeGraphSerializer.Save(Graph, out var problems));
            Status = problems.Count == 0 ? $"Saved to {path}" : $"Saved to {path}. {string.Join(" ", problems)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Couldn't save: {ex.Message}";
        }
    }

    [RelayCommand]
    [property: Command("LoadGraph", Group, Label = "Open…", Icon = MaterialIcons.FileOpen, Description = "Open a graph saved as a JSON file")]
    private async Task LoadGraph()
    {
        string? path = ChooseOpenFile != null ? await ChooseOpenFile() : SavePath;
        if (path == null) return;
        try
        {
            var problems = NodeGraphSerializer.Load(Graph, File.ReadAllText(path));
            _evaluator.InvalidateAll();
            Status = problems.Count == 0 ? $"Opened {path}" : $"Opened {path}. {string.Join(" ", problems)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            Status = $"Couldn't open {path}: {ex.Message}";
        }
    }

    // A value remapped twice by the same group (Value × Factor, clamped): once for the viewer's number, once to mix a
    // color, which the viewer shows too.
    private void BuildDemo()
    {
        var remap = BuildRemapGroup();
        var value = (ValueNode)Graph.AddNode("value", new Point(20, 150));
        var number = Graph.AddNode(new GroupNodeViewModel(remap) { Position = new Point(210, 20) });
        var amount = Graph.AddNode(new GroupNodeViewModel(remap) { Position = new Point(210, 200) });
        amount.Inputs[1].Value = 1.6;
        var rgb = Graph.AddNode("rgb", new Point(210, 380));
        var mix = Graph.AddNode("mix", new Point(460, 290));
        var viewer = Graph.AddNode("viewer", new Point(700, 120));

        Graph.Connect(value.Outputs[0], number.Inputs[0]);
        Graph.Connect(value.Outputs[0], amount.Inputs[0]);
        Graph.Connect(number.Outputs[0], viewer.Inputs[0]);
        Graph.Connect(amount.Outputs[0], mix.Inputs[0]);
        Graph.Connect(rgb.Outputs[0], mix.Inputs[1]);
        Graph.Connect(mix.Outputs[0], viewer.Inputs[1]);
        Graph.Undo.Clear();
    }

    // The "Remap" group: Value × Factor, clamped between 0 and 1.
    private NodeGroupDefinition BuildRemapGroup()
    {
        var remap = Graph.Groups.Add("Remap");
        var value = remap.AddInput("Value", DemoSockets.Float, defaultValue: 0.5);
        var factor = remap.AddInput("Factor", DemoSockets.Float, defaultValue: 0.8, minimum: 0, maximum: 2);
        var result = remap.AddOutput("Result", DemoSockets.Float);
        var math = (MathNode)remap.Graph.AddNode("math", new Point(220, 30));
        math.Operation = MathOperation.Multiply;
        var clamp = remap.Graph.AddNode("clamp", new Point(440, 20));
        remap.InputNode.Position = new Point(20, 60);
        remap.OutputNode.Position = new Point(650, 60);
        remap.Graph.Connect(remap.InputNode.OutputFor(value)!, math.Inputs[0]);
        remap.Graph.Connect(remap.InputNode.OutputFor(factor)!, math.Inputs[1]);
        remap.Graph.Connect(math.Outputs[0], clamp.Inputs[0]);
        remap.Graph.Connect(clamp.Outputs[0], remap.OutputNode.InputFor(result)!);
        return remap;
    }
}
