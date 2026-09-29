using System;
using System.Text.Json.Nodes;
using Atelier.Core.Primitives;
using Atelier.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Atelier.Gallery.ViewModels;

/// <summary>The socket types of the node editor demo: numbers, and colors that also accept numbers (as grays).</summary>
public static class DemoSockets
{
    /// <summary>A number.</summary>
    public static readonly SocketType Float = new("float", "Float", Color.FromRgb(0xA1, 0xA1, 0xA1), typeof(double), 0.0);

    /// <summary>A color; a number connected to it becomes a gray (an opt-in conversion).</summary>
    public static readonly SocketType Rgba = new SocketType("color", "Color", Color.FromRgb(0xC7, 0xC7, 0x29), typeof(Color), Color.Black)
        .SetSerialization(value => ((Color)value!).ToString(), json => Color.FromHex((string)json!))
        .AddConversionFrom(Float, value => Gray(value));

    private static Color Gray(object? value)
    {
        float level = (float)Math.Clamp(value is double d ? d : 0, 0, 1);
        return Color.FromRgba(level, level, level);
    }

    /// <summary>Registers the demo's node types in <paramref name="catalog"/>.</summary>
    public static void RegisterNodes(NodeCatalog catalog)
    {
        var input = Color.FromRgb(0x83, 0x31, 0x4A);
        var converter = Color.FromRgb(0x24, 0x6F, 0x83);
        var colorNodes = Color.FromRgb(0x6C, 0x69, 0x1C);
        var output = Color.FromRgb(0x3C, 0x3C, 0x83);
        catalog.Register(new NodeType("value", "Value", "Input", () => new ValueNode()) { HeaderColor = input, Description = "A number set with a knob", Keywords = ["number", "constant"] });
        catalog.Register(new NodeType("rgb", "RGB", "Input", () => new RgbNode()) { HeaderColor = input, Description = "A color from red, green and blue", Keywords = ["color"] });
        catalog.Register(new NodeType("math", "Math", "Converter", () => new MathNode()) { HeaderColor = converter, Description = "Adds, subtracts, multiplies or divides two numbers", Keywords = ["add", "subtract", "multiply", "divide", "power"] });
        catalog.Register(new NodeType("clamp", "Clamp", "Converter", () => new ClampNode()) { HeaderColor = converter, Description = "Keeps a number within a range", Keywords = ["limit", "range"] });
        catalog.Register(new NodeType("mix", "Mix", "Color", () => new MixNode()) { HeaderColor = colorNodes, Description = "Blends two colors", Keywords = ["blend", "lerp"] });
        catalog.Register(new NodeType("viewer", "Viewer", "Output", () => new ViewerNode()) { HeaderColor = output, Description = "Shows a number and a color", Keywords = ["output", "result", "preview"] });
    }
}

/// <summary>A number set with a knob.</summary>
public sealed class ValueNode : ComputingNodeViewModel
{
    public ValueNode() : base("Value")
    {
        var value = AddInput("Value", DemoSockets.Float, 0.5);
        value.Editor = InputEditor.Knob;
        value.Minimum = 0;
        value.Maximum = 1;
        AddOutput("Value", DemoSockets.Float);
        Width = 120;
    }

    protected override void Compute(ComputeContext context) => context.Set("Value", context.Get<double>("Value"));
}

/// <summary>The operations of a <see cref="MathNode"/>.</summary>
public enum MathOperation
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Power,
    Minimum,
    Maximum,
}

/// <summary>Combines two numbers; the operation is picked in the node's content and saved with the graph.</summary>
public sealed class MathNode : ComputingNodeViewModel
{
    private MathOperation _operation;

    public MathNode() : base("Math")
    {
        AddOutput("Value", DemoSockets.Float);
        AddInput("A", DemoSockets.Float, 0.5);
        AddInput("B", DemoSockets.Float, 0.5);
        Content = new MathSettings(this);
        Width = 160;
        UpdateTitle();
    }

    /// <summary>Gets or sets the operation; changes are undoable.</summary>
    public MathOperation Operation
    {
        get => _operation;
        set
        {
            if (!SetUndoableProperty(ref _operation, value, Graph?.Undo, "Change operation", v => Operation = v)) return;
            UpdateTitle();
            Invalidate();
        }
    }

    public override void WriteState(JsonObject state) => state["operation"] = Operation.ToString();

    public override void ReadState(JsonObject state) =>
        Operation = Enum.TryParse<MathOperation>((string?)state["operation"], out var operation) ? operation : MathOperation.Add;

    protected override void Compute(ComputeContext context)
    {
        double a = context.Get<double>("A"), b = context.Get<double>("B");
        context.Set("Value", Operation switch
        {
            MathOperation.Subtract => a - b,
            MathOperation.Multiply => a * b,
            MathOperation.Divide => b == 0 ? throw new DivideByZeroException("Division by zero") : a / b,
            MathOperation.Power => Math.Pow(a, b),
            MathOperation.Minimum => Math.Min(a, b),
            MathOperation.Maximum => Math.Max(a, b),
            _ => a + b,
        });
    }

    private void UpdateTitle() => Title = Operation.ToString();
}

/// <summary>The content of a <see cref="MathNode"/>: its operation (shown as a combo box by the page).</summary>
public sealed class MathSettings(MathNode node)
{
    public MathNode Node { get; } = node;
}

/// <summary>Keeps a number between a minimum and a maximum, set with knobs.</summary>
public sealed class ClampNode : ComputingNodeViewModel
{
    public ClampNode() : base("Clamp")
    {
        AddOutput("Result", DemoSockets.Float);
        AddInput("Value", DemoSockets.Float, 0.5);
        var min = AddInput("Min", DemoSockets.Float, 0.0);
        var max = AddInput("Max", DemoSockets.Float, 1.0);
        foreach (var bound in new[] { min, max })
        {
            bound.Editor = InputEditor.Knob;
            bound.Minimum = 0;
            bound.Maximum = 1;
        }
    }

    protected override void Compute(ComputeContext context) =>
        context.Set("Result", Math.Clamp(context.Get<double>("Value"), context.Get<double>("Min"), Math.Max(context.Get<double>("Min"), context.Get<double>("Max"))));
}

/// <summary>A color from red, green and blue sliders.</summary>
public sealed class RgbNode : ComputingNodeViewModel
{
    public RgbNode() : base("RGB")
    {
        AddOutput("Color", DemoSockets.Rgba);
        foreach (var (name, value) in new[] { ("Red", 0.9), ("Green", 0.5), ("Blue", 0.2) })
        {
            var channel = AddInput(name, DemoSockets.Float, value);
            channel.Minimum = 0;
            channel.Maximum = 1;
        }
        Width = 170;
    }

    protected override void Compute(ComputeContext context) =>
        context.Set("Color", Color.FromRgba((float)context.Get<double>("Red"), (float)context.Get<double>("Green"), (float)context.Get<double>("Blue")));
}

/// <summary>Blends two colors by a factor.</summary>
public sealed class MixNode : ComputingNodeViewModel
{
    public MixNode() : base("Mix")
    {
        AddOutput("Color", DemoSockets.Rgba);
        var factor = AddInput("Factor", DemoSockets.Float, 0.5);
        factor.Minimum = 0;
        factor.Maximum = 1;
        AddInput("A", DemoSockets.Rgba, Color.Black);
        AddInput("B", DemoSockets.Rgba, Color.White);
        Width = 170;
    }

    protected override void Compute(ComputeContext context) =>
        context.Set("Color", Color.Lerp(context.Get<Color>("A"), context.Get<Color>("B"), (float)Math.Clamp(context.Get<double>("Factor"), 0, 1)));
}

/// <summary>Shows the number and the color it's fed, in its content.</summary>
public sealed class ViewerNode : ComputingNodeViewModel
{
    public ViewerNode() : base("Viewer")
    {
        AddInput("Value", DemoSockets.Float);
        AddInput("Color", DemoSockets.Rgba, Color.Black);
        Content = Display;
        Width = 150;
    }

    /// <summary>Gets what the node shows.</summary>
    public ViewerDisplay Display { get; } = new();

    protected override void Compute(ComputeContext context)
    {
        Display.Text = $"{context.Get<double>("Value"):0.###}";
        Display.Color = context.Get<Color>("Color");
    }
}

/// <summary>The content of a <see cref="ViewerNode"/>: a swatch and a number (shown by the page).</summary>
public sealed partial class ViewerDisplay : ObservableObject
{
    [ObservableProperty]
    private string _text = "0";

    [ObservableProperty]
    private Color _color = Color.Black;
}
