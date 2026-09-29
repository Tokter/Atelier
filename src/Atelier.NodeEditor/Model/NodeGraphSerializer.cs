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
/// settings (<see cref="NodeViewModel.WriteState"/>); reroute points by their socket type, group nodes by their group.
/// The groups the saved nodes use (see <see cref="NodeGroupLibrary"/>) are saved with them: their inputs, outputs and
/// the graphs inside. Loading creates the nodes from the graph's <see cref="NodeGraphViewModel.Catalog"/>.
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

    /// <summary>Saves the whole graph (its root graph, when it's the graph inside a group) with all its groups.</summary>
    public static string Save(NodeGraphViewModel graph) => Save(graph, out _);

    /// <summary>Saves the whole graph with all its groups; <paramref name="problems"/> lists what couldn't be saved.</summary>
    public static string Save(NodeGraphViewModel graph, out IReadOnlyList<string> problems)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var root = graph.Root;
        var list = new List<string>();
        var json = WriteGraph(root.Nodes, root.Links, list);
        json["groups"] = WriteGroups(root.Groups.Definitions, list);
        problems = list;
        return json.ToJsonString(s_indented);
    }

    /// <summary>Saves <paramref name="nodes"/>, the links between them and the groups they use, as copied to the clipboard.</summary>
    public static string Copy(IEnumerable<NodeViewModel> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var set = nodes.Where(n => n.CanRemove).ToHashSet();
        var links = set.SelectMany(n => n.Inputs).Where(i => i.Link is { } link && set.Contains(link.From.Node!)).Select(i => i.Link!);
        var list = new List<string>();
        var json = WriteGraph(set, links, list);
        json["groups"] = WriteGroups(UsedGroups(set), list);
        return json.ToJsonString();
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
    /// Replaces the contents of <paramref name="graph"/> (its root graph, when it's the graph inside a group) and its
    /// groups with the saved graph <paramref name="json"/>; the undo history starts afresh.
    /// </summary>
    /// <returns>What couldn't be loaded, if anything.</returns>
    /// <exception cref="FormatException"><paramref name="json"/> isn't a saved node graph.</exception>
    public static IReadOnlyList<string> Load(NodeGraphViewModel graph, string json)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var root = graph.Root;
        var problems = new List<string>();
        var saved = Parse(json);
        root.Clear();

        // The groups first (all of them, as they can use each other), then the graphs inside them, then the root graph.
        var groups = new Dictionary<string, NodeGroupDefinition>();
        var groupJson = new List<(NodeGroupDefinition, JsonObject)>();
        foreach (var item in saved["groups"] as JsonArray ?? [])
        {
            if (item is not JsonObject group || (string?)group["id"] is not { } id) continue;
            var definition = root.Groups.AddLoaded(id, (string?)group["name"] ?? "Group");
            ReadInterface(root.Catalog, definition, group, loaded: true, problems);
            groups[id] = definition;
            groupJson.Add((definition, group));
        }
        foreach (var (definition, group) in groupJson) AddNodes(definition.Graph, group, keepIds: true, groups, problems);
        AddNodes(root, saved, keepIds: true, groups, problems);
        root.Undo.Clear();
        return problems;
    }

    /// <summary>
    /// Adds the nodes (and the links between them) saved in <paramref name="json"/> to <paramref name="graph"/>, with new
    /// ids, as one undo step, and selects only them. They keep their layout, moved so that their top-left corner is at
    /// <paramref name="at"/> (in graph coordinates) or, without it, 20 units from where they were. Groups the graph
    /// doesn't have yet are added; groups it has (with the same id) are used.
    /// </summary>
    /// <returns>The added nodes.</returns>
    /// <exception cref="FormatException"><paramref name="json"/> isn't a saved node graph.</exception>
    public static IReadOnlyList<NodeViewModel> Paste(NodeGraphViewModel graph, string json, Point? at = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var saved = Parse(json);
        var problems = new List<string>();
        using var step = graph.Undo.Group("Paste");

        var groups = new Dictionary<string, NodeGroupDefinition>();
        var added = new List<(NodeGroupDefinition, JsonObject)>();
        foreach (var item in saved["groups"] as JsonArray ?? [])
        {
            if (item is not JsonObject group || (string?)group["id"] is not { } id) continue;
            if (graph.Groups.Find(id) is { } existing)
            {
                groups[id] = existing;
                continue;
            }
            var definition = graph.Groups.Add((string?)group["name"] ?? "Group", id);
            ReadInterface(graph.Catalog, definition, group, loaded: false, problems);
            groups[id] = definition;
            added.Add((definition, group));
        }
        foreach (var (definition, group) in added) AddNodes(definition.Graph, group, keepIds: false, groups, problems);

        var (nodes, links) = Read(graph.Catalog, saved, keepIds: false, groups, null, problems);
        if (nodes.Count == 0) return [];
        var pasted = nodes.Values.ToList();
        var offset = at is { } target
            ? target - new Point(pasted.Min(n => n.Position.X), pasted.Min(n => n.Position.Y))
            : new Point(20, 20);
        foreach (var node in pasted)
        {
            node.Position += offset;
            TryAdd(graph, node, problems);
        }
        foreach (var (from, to) in links) graph.Connect(from, to);
        step.Name = pasted.Count == 1 ? $"Paste {pasted[0].Title}" : "Paste";
        graph.ClearSelection();
        foreach (var node in pasted.Where(n => n.Graph == graph)) node.IsSelected = true;
        return pasted.Where(n => n.Graph == graph).ToList();
    }

    // The groups the nodes use, with the groups those use inside, the used ones first.
    private static List<NodeGroupDefinition> UsedGroups(IEnumerable<NodeViewModel> nodes)
    {
        var result = new List<NodeGroupDefinition>();
        void Visit(NodeGroupDefinition definition)
        {
            if (result.Contains(definition)) return;
            foreach (var inner in definition.Graph.Nodes.OfType<GroupNodeViewModel>()) Visit(inner.Definition);
            result.Add(definition);
        }
        foreach (var node in nodes.OfType<GroupNodeViewModel>()) Visit(node.Definition);
        return result;
    }

    private static JsonArray WriteGroups(IEnumerable<NodeGroupDefinition> definitions, List<string> problems)
    {
        var array = new JsonArray();
        foreach (var definition in definitions)
        {
            var json = WriteGraph(definition.Graph.Nodes, definition.Graph.Links, problems);
            json.Remove("format");
            json.Remove("version");
            json["id"] = definition.Id;
            json["name"] = definition.Name;
            json["inputs"] = new JsonArray(definition.Inputs.Select(s => (JsonNode)WriteSocket(s)).ToArray());
            json["outputs"] = new JsonArray(definition.Outputs.Select(s => (JsonNode)WriteSocket(s)).ToArray());
            array.Add(json);
        }
        return array;
    }

    private static JsonObject WriteSocket(GroupSocket socket)
    {
        var json = new JsonObject { ["id"] = socket.Id, ["name"] = socket.Name, ["type"] = socket.Type.Id };
        if (!socket.IsInput) return json;
        if (socket.Type.WriteValue(socket.DefaultValue) is { } value) json["default"] = value;
        if (socket.Minimum is { } min) json["min"] = min;
        if (socket.Maximum is { } max) json["max"] = max;
        if (socket.Editor != InputEditor.Auto) json["editor"] = socket.Editor.ToString();
        return json;
    }

    private static void ReadInterface(NodeCatalog catalog, NodeGroupDefinition definition, JsonObject json, bool loaded, List<string> problems)
    {
        foreach (var (key, isInput) in new[] { ("inputs", true), ("outputs", false) })
        {
            foreach (var item in json[key] as JsonArray ?? [])
            {
                if (item is not JsonObject socket) continue;
                string typeId = (string?)socket["type"] ?? "";
                string name = (string?)socket["name"] ?? "Value";
                if (catalog.FindSocketType(typeId) is not { } type)
                {
                    problems.Add($"The {(isInput ? "input" : "output")} '{name}' of the group '{definition.Name}' has the unknown type '{typeId}' and was left out.");
                    continue;
                }
                var groupSocket = loaded
                    ? definition.AddLoadedSocket((string?)socket["id"] ?? Guid.NewGuid().ToString("N"), name, type, isInput)
                    : isInput ? definition.AddInput(name, type) : definition.AddOutput(name, type);
                if (!isInput) continue;
                if (type.TryReadValue(socket["default"], out var value) && value != null) groupSocket.DefaultValue = value;
                groupSocket.Minimum = (double?)socket["min"];
                groupSocket.Maximum = (double?)socket["max"];
                if (Enum.TryParse<InputEditor>((string?)socket["editor"], out var editor)) groupSocket.Editor = editor;
            }
        }
        definition.OnInterfaceChanged();
    }

    private static JsonObject WriteGraph(IEnumerable<NodeViewModel> nodes, IEnumerable<LinkViewModel> links, List<string> problems)
    {
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
                problems.Add($"The node '{node.Title}' has no type id and wasn't saved.");
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
        switch (node)
        {
            case RerouteNodeViewModel reroute:
                json["reroute"] = reroute.Type.Id;
                break;
            case GroupInputNodeViewModel:
                json["groupInput"] = true;
                break;
            case GroupOutputNodeViewModel:
                json["groupOutput"] = true;
                break;
            case GroupNodeViewModel group:
                json["group"] = group.Definition.Id;
                break;
            default:
                if (node.TypeId is not { } typeId) return null;
                json["type"] = typeId;
                json["title"] = node.Title;
                break;
        }
        json["x"] = node.Position.X;
        json["y"] = node.Position.Y;
        if (node is RerouteNodeViewModel or GroupInputNodeViewModel or GroupOutputNodeViewModel) return json;

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

    // Reads the nodes and links of a saved graph into `graph`.
    private static void AddNodes(NodeGraphViewModel graph, JsonObject json, bool keepIds, Dictionary<string, NodeGroupDefinition> groups, List<string> problems)
    {
        var (nodes, links) = Read(graph.Catalog, json, keepIds, groups, graph.Owner, problems);
        foreach (var node in nodes.Values)
        {
            if (node.Graph == null) TryAdd(graph, node, problems);
        }
        foreach (var (from, to) in links)
        {
            if (from.Node?.Graph == graph && to.Node?.Graph == graph) graph.Connect(from, to);
        }
    }

    private static void TryAdd(NodeGraphViewModel graph, NodeViewModel node, List<string> problems)
    {
        try
        {
            graph.AddNode(node);
        }
        catch (InvalidOperationException ex)
        {
            problems.Add(ex.Message);
        }
    }

    // Creates the nodes (not yet in a graph, except a group's own input and output nodes), keyed by their saved ids, and
    // resolves the links between them.
    private static (Dictionary<string, NodeViewModel> Nodes, List<(OutputSocketViewModel, InputSocketViewModel)> Links) Read(
        NodeCatalog catalog, JsonObject root, bool keepIds, Dictionary<string, NodeGroupDefinition> groups, NodeGroupDefinition? inside,
        List<string> problems)
    {
        var nodes = new Dictionary<string, NodeViewModel>();
        foreach (var item in root["nodes"] as JsonArray ?? [])
        {
            if (item is not JsonObject json || (string?)json["id"] is not { } id) continue;
            if (ReadNode(catalog, json, groups, inside, problems) is not { } node) continue;
            if (keepIds && node.Graph == null) node.Id = id;
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

    private static NodeViewModel? ReadNode(NodeCatalog catalog, JsonObject json, Dictionary<string, NodeGroupDefinition> groups,
        NodeGroupDefinition? inside, List<string> problems)
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
        if ((bool?)json["groupInput"] == true || (bool?)json["groupOutput"] == true)
        {
            if (inside is null) return null;
            NodeViewModel own = (bool?)json["groupInput"] == true ? inside.InputNode : inside.OutputNode;
            own.Position = position;
            return own;
        }

        NodeViewModel node;
        if ((string?)json["group"] is { } groupId)
        {
            if (!groups.TryGetValue(groupId, out var definition))
            {
                problems.Add($"A node of the missing group '{groupId}' was left out.");
                return null;
            }
            node = new GroupNodeViewModel(definition);
        }
        else
        {
            string typeId = (string?)json["type"] ?? "";
            if (catalog.Find(typeId) is not { } type)
            {
                problems.Add($"The node '{json["title"]}' of the unknown type '{typeId}' was left out.");
                return null;
            }
            node = type.CreateNode();
            if ((string?)json["title"] is { } title) node.Title = title;
        }
        node.Position = position;
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
