using System.Collections.Specialized;
using System.ComponentModel;
using Atelier.Core.Threading;

namespace Atelier.NodeEditor;

/// <summary>Provides data for <see cref="GraphEvaluator.Evaluated"/>.</summary>
public sealed class GraphEvaluatedEventArgs(IReadOnlyList<NodeViewModel> computedNodes) : EventArgs
{
    /// <summary>Gets the nodes that were computed (or passed through, when muted), in the order they were.</summary>
    public IReadOnlyList<NodeViewModel> ComputedNodes { get; } = computedNodes;
}

/// <summary>
/// Keeps the outputs of a graph's <see cref="ComputingNodeViewModel"/>s up to date. It notices what changes (input
/// values, links, nodes, muting) and, once per frame, computes the affected nodes in dependency order. A node
/// whose outputs come out unchanged stops the update from going further downstream.
/// </summary>
/// <remarks>
/// Muted nodes pass each output the value of the first input of a type it accepts (the default otherwise), like
/// Blender. Nodes that don't compute are sources: their downstream nodes update when their outputs are set.
/// </remarks>
public sealed class GraphEvaluator : IDisposable
{
    private readonly IDispatcher _dispatcher;
    private readonly HashSet<NodeViewModel> _dirty = [];
    private readonly HashSet<NodeViewModel> _attached = [];
    private bool _scheduled;
    private bool _evaluating;
    private bool _disposed;

    /// <summary>
    /// Starts evaluating <paramref name="graph"/>: all its nodes are computed in the next pass, which runs through
    /// <paramref name="dispatcher"/> (by default the UI thread's, at the start of the next frame).
    /// </summary>
    public GraphEvaluator(NodeGraphViewModel graph, IDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        Graph = graph;
        _dispatcher = dispatcher ?? Dispatcher.UIThread;
        ((INotifyCollectionChanged)graph.Nodes).CollectionChanged += OnNodesChanged;
        ((INotifyCollectionChanged)graph.Links).CollectionChanged += OnLinksChanged;
        foreach (var node in graph.Nodes) Attach(node);
        InvalidateAll();
    }

    /// <summary>Occurs after each pass that computed at least one node.</summary>
    public event EventHandler<GraphEvaluatedEventArgs>? Evaluated;

    /// <summary>Gets the evaluated graph.</summary>
    public NodeGraphViewModel Graph { get; }

    /// <summary>Gets whether nodes wait to be computed.</summary>
    public bool IsPending => _dirty.Count > 0;

    /// <summary>Makes the next pass compute <paramref name="node"/> (and what its changed outputs feed).</summary>
    public void Invalidate(NodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_disposed || _evaluating || node.Graph != Graph) return;
        _dirty.Add(node);
        Schedule();
    }

    /// <summary>Makes the next pass compute every node.</summary>
    public void InvalidateAll()
    {
        foreach (var node in Graph.Nodes) Invalidate(node);
    }

    /// <summary>Computes the nodes that wait for it now, instead of in the next frame.</summary>
    /// <returns>The nodes computed.</returns>
    public IReadOnlyList<NodeViewModel> EvaluateNow()
    {
        if (_evaluating || _disposed || _dirty.Count == 0) return [];
        _evaluating = true;
        var computed = new List<NodeViewModel>();
        try
        {
            foreach (var node in Graph.GetTopologicalOrder())
            {
                if (!_dirty.Contains(node)) continue;
                computed.Add(node);
                foreach (var output in Compute(node))
                {
                    foreach (var link in output.Links) _dirty.Add(link.To.Node!);
                }
            }
        }
        finally
        {
            _dirty.Clear();
            _evaluating = false;
        }
        if (computed.Count > 0) Evaluated?.Invoke(this, new GraphEvaluatedEventArgs(computed));
        return computed;
    }

    /// <summary>Stops following the graph's changes.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ((INotifyCollectionChanged)Graph.Nodes).CollectionChanged -= OnNodesChanged;
        ((INotifyCollectionChanged)Graph.Links).CollectionChanged -= OnLinksChanged;
        foreach (var node in _attached.ToList()) Detach(node);
        _dirty.Clear();
    }

    // Computes one node; returns the outputs whose values changed.
    private static List<OutputSocketViewModel> Compute(NodeViewModel node)
    {
        var before = node.Outputs.Select(o => o.Value).ToList();
        if (node.IsMuted)
        {
            node.Error = null;
            PassThrough(node);
        }
        else if (node is ComputingNodeViewModel computing)
        {
            try
            {
                computing.Compute(new ComputeContext(node));
                node.Error = null;
            }
            catch (Exception ex)
            {
                node.Error = ex.Message;
                foreach (var output in node.Outputs) output.Value = output.Type.DefaultValue;
            }
        }

        var changed = new List<OutputSocketViewModel>();
        for (int i = 0; i < node.Outputs.Count; i++)
        {
            if (i >= before.Count || !Equals(before[i], node.Outputs[i].Value)) changed.Add(node.Outputs[i]);
        }
        return changed;
    }

    private static void PassThrough(NodeViewModel node)
    {
        foreach (var output in node.Outputs)
        {
            var input = node.Inputs.FirstOrDefault(i => i.Type == output.Type)
                ?? node.Inputs.FirstOrDefault(i => output.Type.CanConnectFrom(i.Type));
            try
            {
                output.Value = input is null ? output.Type.DefaultValue : output.Type.ConvertFrom(input.EffectiveValue, input.Type);
            }
            catch (Exception ex)
            {
                node.Error = ex.Message;
                output.Value = output.Type.DefaultValue;
            }
        }
    }

    private void Schedule()
    {
        if (_scheduled) return;
        _scheduled = true;
        _dispatcher.Post(() =>
        {
            _scheduled = false;
            EvaluateNow();
        }, DispatcherPriority.Normal);
    }

    private void Attach(NodeViewModel node)
    {
        if (!_attached.Add(node)) return;
        node.PropertyChanged += OnNodePropertyChanged;
        ((INotifyCollectionChanged)node.Inputs).CollectionChanged += OnSocketsChanged;
        ((INotifyCollectionChanged)node.Outputs).CollectionChanged += OnSocketsChanged;
        foreach (var input in node.Inputs) input.PropertyChanged += OnInputPropertyChanged;
        foreach (var output in node.Outputs) output.PropertyChanged += OnOutputPropertyChanged;
        if (node is ComputingNodeViewModel computing) computing.Invalidated += OnNodeInvalidated;
    }

    private void Detach(NodeViewModel node)
    {
        if (!_attached.Remove(node)) return;
        node.PropertyChanged -= OnNodePropertyChanged;
        ((INotifyCollectionChanged)node.Inputs).CollectionChanged -= OnSocketsChanged;
        ((INotifyCollectionChanged)node.Outputs).CollectionChanged -= OnSocketsChanged;
        foreach (var input in node.Inputs) input.PropertyChanged -= OnInputPropertyChanged;
        foreach (var output in node.Outputs) output.PropertyChanged -= OnOutputPropertyChanged;
        if (node is ComputingNodeViewModel computing) computing.Invalidated -= OnNodeInvalidated;
        _dirty.Remove(node);
    }

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var node in _attached.Where(n => n.Graph != Graph).ToList()) Detach(node);
        foreach (var node in Graph.Nodes)
        {
            if (_attached.Contains(node)) continue;
            Attach(node);
            Invalidate(node);
        }
    }

    private void OnLinksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var link in (e.NewItems ?? Array.Empty<LinkViewModel>()).Cast<LinkViewModel>()
            .Concat((e.OldItems ?? Array.Empty<LinkViewModel>()).Cast<LinkViewModel>()))
        {
            if (link.To.Node is { } node) Invalidate(node);
        }
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeViewModel.IsMuted)) Invalidate((NodeViewModel)sender!);
    }

    private void OnNodeInvalidated(object? sender, EventArgs e) => Invalidate((NodeViewModel)sender!);

    private void OnSocketsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var socket in (e.NewItems ?? Array.Empty<SocketViewModel>()).Cast<SocketViewModel>())
        {
            if (socket is InputSocketViewModel input) input.PropertyChanged += OnInputPropertyChanged;
            else socket.PropertyChanged += OnOutputPropertyChanged;
            if (socket.Node is { } node) Invalidate(node);
        }
    }

    private void OnInputPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A connected input's own value isn't used; its link's changes arrive through the links and outputs.
        if (e.PropertyName == nameof(InputSocketViewModel.Value) && sender is InputSocketViewModel { IsConnected: false, Node: { } node })
        {
            Invalidate(node);
        }
    }

    private void OnOutputPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Outputs set outside a pass (by source nodes, or by hand) update what they feed.
        if (e.PropertyName != nameof(OutputSocketViewModel.Value) || _evaluating) return;
        foreach (var link in ((OutputSocketViewModel)sender!).Links)
        {
            if (link.To.Node is { } node) Invalidate(node);
        }
    }
}
