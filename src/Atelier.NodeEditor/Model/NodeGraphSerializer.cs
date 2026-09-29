using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Atelier.Core.Primitives;

namespace Atelier.Nodes;

/// <summary>
/// Saves node graphs as JSON and loads them again, and copies and pastes parts of graphs (such as the selection) as
/// JSON text for the clipboard.
/// </summary>
/// <remarks>
/// <para>
/// Nodes are saved by their catalog type (<see cref="NodeViewModel.TypeId"/>), with their title, position, width,
/// color, collapsed and muted flags, the values of their inputs (see <see cref="SocketType.WriteValue"/>) and their own
/// settings (<see cref="NodeViewModel.WriteState"/>); reroute points by their socket type. Loading creates the nodes
/// from the graph's <see cref="NodeGraphViewModel.Catalog"/>.
/// </para>
/// <para>
/// Nodes without a type id can't be saved, and nodes of types the catalog doesn't know can't be loaded; both are left
/// out and reported in the result, as are links to missing sockets.
/// </para>
/// </remarks>
public static class NodeGraphSerializer
{
    /// <summary>The value of the <c>"format"</c> property that marks the JSON as a node graph.</summary>
    public const string Format = "atelier-node-graph";

    private const int Version = 1;
    private static readonly JsonSerializerOptions s_indented = new() { WriteIndented = true };

    /// <summary>Saves the whole graph.</summary>
    public static string Save(NodeGraphViewModel graph) => Save(graph, out _);

    /// <summary>Saves the whole graph; <paramref name="problems"/> lists what couldn't be saved.</summary>
    public static string Save(NodeGraphViewModel graph, out IReadOnlyList<string> problems)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return Write(graph.Nodes, graph.Links, out problems).ToJsonString(s_indented);
    }

    /// <summary>Saves <paramref name="nodes"/> and the links between them, as copied to the clipboard.</summary>
    public static string Copy(IEnumerable<NodeViewModel> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var set = nodes.ToHashSet();
        var links = set.SelectMany(n => n.Inputs).Where(i => i.Link is { } link && set.Contains(link.From.Node!)).Select(i => i.Link!);
        return Write(set, links, out _).ToJsonString();
    }

    /// <summary>Gets whether <paramref name="text"/> looks like a saved node graph (e.g. to enable pasting).</summary>
    public static bool IsGraph(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('{')) return false;
        try
        {
            return JsonNode.Parse(text) is JsonObject root && (string?)root["format"] == Format;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Replaces the contents of <paramref name="graph"/> with the saved graph <paramref name="json"/>; the undo history
    /// starts afresh.
    /// </summary>
    /// <returns>What couldn't be loaded, if anything.</returns>
    /// <exception cref="FormatException"><paramref name="json"/> isn't a saved node graph.</exception>
    public static IReadOnlyList<string> Load(NodeGraphViewModel graph, string json)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var problems = new List<string>();
        var (nodes, links) = Read(graph.Catalog, Parse(json), keepIds: true, problems);
        graph.Clear();
        foreach (var node in nodes.Values) graph.AddNode(node);
        foreach (var (from, to) in links) graph.Connect(from, to);
        graph.Undo.Clear();
        return problems;
    }

    /// <summary>
    /// Adds the nodes (and the links between them) saved in <paramref name="json"/> to <paramref name="graph"/>, with new
    /// ids, as one undo step, and selects only them. They keep their layout, moved so that their top-left corner is at
    /// <paramref name="at"/> (in graph coordinates) or, without it, 20 units from where they were.
    /// </summary>
    /// <returns>The added nodes.</returns>
    /// <exception cref="FormatException"><paramref name="json"/> isn't a saved node graph.</exception>
    public static IReadOnlyList<NodeViewModel> Paste(NodeGraphViewModel graph, string json, Point? at = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var (nodes, links) = Read(graph.Catalog, Parse(json), keepIds: false, []);
        if (nodes.Count == 0) return [];

        var added = nodes.Values.ToList();
        var offset = at is { } target
            ? target - new Point(added.Min(n => n.Position.X), added.Min(n => n.Position.Y))
            : new Point(20, 20);
        using (graph.Undo.Group(added.Count == 1 ? $"Paste {added[0].Title}" : "Paste"))
        {
            foreach (var node in added)
            {
                node.Position += offset;
                graph.AddNode(node);
            }
            foreach (var (from, to) in links) graph.Connect(from, to);
        }
        graph.ClearSelection();
        foreach (var node in added) node.IsSelected = true;
        return added;
    }

    private static JsonObject Write(IEnumerable<NodeViewModel> nodes, IEnumerable<LinkViewModel> links, out IReadOnlyList<string> problems)
    {
        var list = new List<string>();
        var saved = new HashSet<NodeViewModel>();
        var nodeArray = new JsonArray();
        foreach (var node in nodes)
        {
            if (WriteNode(node) is { } json)
            {
                nodeArray.Add(json);
                saved.Add(node);
            }
            else
            {
                list.Add($"The node '{node.Title}' has no type id and wasn't saved.");
            }
        }

        var linkArray = new JsonArray();
        foreach (var link in links)
        {
            if (!saved.Contains(link.From.Node!) || !saved.Contains(link.To.Node!)) continue;
            linkArray.Add(new JsonObject
            {
                ["from"] = link.From.Node!.Id,
                ["output"] = link.From.Name,
                ["to"] = link.To.Node!.Id,
                ["input"] = link.To.Name,
            });
        }

        problems = list;
        return new JsonObject
        {
            ["format"] = Format,
            ["version"] = Version,
            ["nodes"] = nodeArray,
            ["links"] = linkArray,
        };
    }

    private static JsonObject? WriteNode(NodeViewModel node)
    {
        var json = new JsonObject { ["id"] = node.Id };
        if (node is RerouteNodeViewModel reroute)
        {
            json["reroute"] = reroute.Type.Id;
        }
        else if (node.TypeId is { } typeId)
        {
            json["type"] = typeId;
            json["title"] = node.Title;
        }
        else
        {
            return null;
        }
        json["x"] = node.Position.X;
        json["y"] = node.Position.Y;
        if (node is RerouteNodeViewModel) return json;

        json["width"] = node.Width;
        if (node.HeaderColor is { } color) json["color"] = color.ToString();
        if (node.IsCollapsed) json["collapsed"] = true;
        if (node.IsMuted) json["muted"] = true;

        var inputs = new JsonObject();
        foreach (var input in node.Inputs)
        {
            if (!inputs.ContainsKey(input.Name)) inputs[input.Name] = input.Type.WriteValue(input.Value);
        }
        if (inputs.Count > 0) json["inputs"] = inputs;

        var state = new JsonObject();
        node.WriteState(state);
        if (state.Count > 0) json["state"] = state;
        return json;
    }

    private static JsonObject Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            if (JsonNode.Parse(json) is JsonObject root && (string?)root["format"] == Format) return root;
        }
        catch (JsonException ex)
        {
            throw new FormatException("The text isn't a saved node graph.", ex);
        }
        throw new FormatException("The text isn't a saved node graph.");
    }

    // Creates the nodes (not yet in a graph), keyed by their saved ids, and resolves the links between them.
    private static (Dictionary<string, NodeViewModel> Nodes, List<(OutputSocketViewModel, InputSocketViewModel)> Links) Read(
        NodeCatalog catalog, JsonObject root, bool keepIds, List<string> problems)
    {
        var nodes = new Dictionary<string, NodeViewModel>();
        foreach (var item in root["nodes"] as JsonArray ?? [])
        {
            if (item is not JsonObject json || (string?)json["id"] is not { } id) continue;
            if (ReadNode(catalog, json, problems) is not { } node) continue;
            if (keepIds) node.Id = id;
            nodes[id] = node;
        }

        var links = new List<(OutputSocketViewModel, InputSocketViewModel)>();
        foreach (var item in root["links"] as JsonArray ?? [])
        {
            if (item is not JsonObject json) continue;
            var from = nodes.GetValueOrDefault((string?)json["from"] ?? "")?.FindOutput((string?)json["output"] ?? "");
            var to = nodes.GetValueOrDefault((string?)json["to"] ?? "")?.FindInput((string?)json["input"] ?? "");
            if (from is null || to is null)
            {
                problems.Add($"A link from '{json["output"]}' to '{json["input"]}' was left out: a socket is missing.");
                continue;
            }
            links.Add((from, to));
        }
        return (nodes, links);
    }

    private static NodeViewModel? ReadNode(NodeCatalog catalog, JsonObject json, List<string> problems)
    {
        var position = new Point(ReadFloat(json["x"]), ReadFloat(json["y"]));
        if ((string?)json["reroute"] is { } socketTypeId)
        {
            if (catalog.FindSocketType(socketTypeId) is not { } socketType)
            {
                problems.Add($"A reroute point of the unknown socket type '{socketTypeId}' was left out.");
                return null;
            }
            return new RerouteNodeViewModel(socketType) { Position = position };
        }

        string typeId = (string?)json["type"] ?? "";
        if (catalog.Find(typeId) is not { } type)
        {
            problems.Add($"The node '{json["title"]}' of the unknown type '{typeId}' was left out.");
            return null;
        }
        var node = type.CreateNode();
        node.Position = position;
        if ((string?)json["title"] is { } title) node.Title = title;
        if (json["width"] is { } width) node.Width = ReadFloat(width);
        if ((string?)json["color"] is { } color && Color.TryParseHex(color, out var headerColor)) node.HeaderColor = headerColor;
        node.IsCollapsed = (bool?)json["collapsed"] ?? false;
        node.IsMuted = (bool?)json["muted"] ?? false;

        if (json["inputs"] is JsonObject inputs)
        {
            foreach (var (name, value) in inputs)
            {
                if (node.FindInput(name) is not { } input) continue;
                if (input.Type.TryReadValue(value, out var read)) input.Value = read;
                else problems.Add($"The value of '{node.Title}.{name}' couldn't be read.");
            }
        }
        if (json["state"] is JsonObject state) node.ReadState(state);
        return node;
    }

    private static float ReadFloat(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out double number) ? (float)number
        : node is JsonValue text && text.TryGetValue(out string? s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed
        : 0f;
}
