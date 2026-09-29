using Atelier.Core.Primitives;

namespace Atelier.NodeEditor;

/// <summary>A kind of node users can add: its name, where it's listed, and how to create it.</summary>
public sealed class NodeType
{
    private NodeViewModel? _prototype;

    /// <summary>Initializes a node type.</summary>
    /// <param name="id">A stable identifier, used when saving graphs.</param>
    /// <param name="title">The name shown in menus and, by default, in the node's title bar.</param>
    /// <param name="category">The menu the type is listed in, such as <c>"Input"</c> or <c>"Math"</c>.</param>
    /// <param name="factory">Creates a node of this type.</param>
    public NodeType(string id, string title, string category, Func<NodeViewModel> factory)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(factory);
        Id = id;
        Title = title ?? id;
        Category = category ?? "";
        Factory = factory;
    }

    /// <summary>Gets the stable identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the name shown in menus.</summary>
    public string Title { get; }

    /// <summary>Gets the menu the type is listed in.</summary>
    public string Category { get; }

    /// <summary>Gets the function that creates a node of this type.</summary>
    public Func<NodeViewModel> Factory { get; }

    /// <summary>Gets or sets the title bar color of new nodes (unless the factory sets one).</summary>
    public Color? HeaderColor { get; init; }

    /// <summary>Gets or sets what the node does, shown as the menu item's tooltip.</summary>
    public string? Description { get; init; }

    /// <summary>Gets or sets more words search finds the type by.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Gets the types of the inputs of this type's nodes, from a node created once for it.</summary>
    public IReadOnlyList<SocketType> InputTypes => Prototype.Inputs.Select(i => i.Type).ToList();

    /// <summary>Gets the types of the outputs of this type's nodes, from a node created once for it.</summary>
    public IReadOnlyList<SocketType> OutputTypes => Prototype.Outputs.Select(o => o.Type).ToList();

    /// <summary>Gets whether a node of this type has an input that accepts an output of <paramref name="type"/>.</summary>
    public bool HasInputAccepting(SocketType type) => Prototype.Inputs.Any(i => i.Type.CanConnectFrom(type));

    /// <summary>Gets whether a node of this type has an output an input of <paramref name="type"/> accepts.</summary>
    public bool HasOutputFor(SocketType type) => Prototype.Outputs.Any(o => type.CanConnectFrom(o.Type));

    /// <summary>Creates a node of this type.</summary>
    public NodeViewModel CreateNode()
    {
        var node = Factory() ?? throw new InvalidOperationException($"The factory of node type '{Id}' returned null.");
        node.TypeId = Id;
        node.HeaderColor ??= HeaderColor;
        return node;
    }

    private NodeViewModel Prototype => _prototype ??= CreateNode();

    /// <inheritdoc/>
    public override string ToString() => Id;
}

/// <summary>The kinds of nodes that can be added to a graph, for the editor's add menu and search.</summary>
public sealed class NodeCatalog
{
    private readonly List<NodeType> _types = [];
    private readonly Dictionary<string, NodeType> _byId = [];

    /// <summary>Gets the registered types, in registration order.</summary>
    public IReadOnlyList<NodeType> Types => _types;

    /// <summary>Gets the categories, in the order their first types were registered.</summary>
    public IReadOnlyList<string> Categories => _types.Select(t => t.Category).Distinct().ToList();

    /// <summary>Registers <paramref name="type"/>.</summary>
    /// <exception cref="ArgumentException">A type with the same id is registered already.</exception>
    public NodeType Register(NodeType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!_byId.TryAdd(type.Id, type)) throw new ArgumentException($"A node type '{type.Id}' is registered already.", nameof(type));
        _types.Add(type);
        return type;
    }

    /// <summary>Registers a type whose nodes <paramref name="factory"/> creates.</summary>
    public NodeType Register(string id, string title, string category, Func<NodeViewModel> factory) =>
        Register(new NodeType(id, title, category, factory));

    /// <summary>Gets the type with <paramref name="id"/>, or <c>null</c>.</summary>
    public NodeType? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Creates a node of type <paramref name="typeId"/> at <paramref name="position"/>.</summary>
    /// <exception cref="KeyNotFoundException">No such type is registered.</exception>
    public NodeViewModel CreateNode(string typeId, Point position)
    {
        var type = Find(typeId) ?? throw new KeyNotFoundException($"No node type '{typeId}' is registered.");
        var node = type.CreateNode();
        node.Position = position;
        return node;
    }

    /// <summary>
    /// Finds the types matching <paramref name="query"/> (in their title, category or keywords, best matches first;
    /// all for an empty query). When a link is dragged into empty space, <paramref name="acceptingOutput"/> or
    /// <paramref name="feedingInput"/> limit the results to types that can connect to the dragged socket.
    /// </summary>
    /// <param name="query">The text typed.</param>
    /// <param name="acceptingOutput">Only types with an input that accepts an output of this type.</param>
    /// <param name="feedingInput">Only types with an output that an input of this type accepts.</param>
    public IReadOnlyList<NodeType> Search(string? query, SocketType? acceptingOutput = null, SocketType? feedingInput = null)
    {
        var words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = new List<(NodeType Type, int Score, int Index)>();
        for (int i = 0; i < _types.Count; i++)
        {
            var type = _types[i];
            if (acceptingOutput != null && !type.HasInputAccepting(acceptingOutput)) continue;
            if (feedingInput != null && !type.HasOutputFor(feedingInput)) continue;
            int score = 0;
            foreach (var word in words)
            {
                int wordScore = Score(type, word);
                if (wordScore == 0)
                {
                    score = -1;
                    break;
                }
                score += wordScore;
            }
            if (score >= 0) results.Add((type, score, i));
        }
        return results.OrderByDescending(r => r.Score).ThenBy(r => r.Index).Select(r => r.Type).ToList();
    }

    // How well a word matches: the title's start beats a word start in it, which beats anywhere in it, then the
    // keywords, then the category; 0 is no match.
    private static int Score(NodeType type, string word)
    {
        const StringComparison ignoreCase = StringComparison.OrdinalIgnoreCase;
        if (type.Title.StartsWith(word, ignoreCase)) return 100;
        int index = type.Title.IndexOf(word, ignoreCase);
        if (index > 0 && type.Title[index - 1] == ' ') return 80;
        if (index > 0) return 60;
        if (type.Keywords.Any(k => k.StartsWith(word, ignoreCase))) return 40;
        if (type.Keywords.Any(k => k.Contains(word, ignoreCase))) return 30;
        if (type.Category.Contains(word, ignoreCase)) return 20;
        return 0;
    }
}
