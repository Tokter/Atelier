namespace Atelier.NodeEditor;

/// <summary>A change that can be undone and redone.</summary>
public interface IUndoAction
{
    /// <summary>Gets what the change was, for "Undo …" menu items.</summary>
    string Name { get; }

    /// <summary>Reverts the change.</summary>
    void Undo();

    /// <summary>Applies the change again.</summary>
    void Redo();
}

/// <summary>An undoable change made of a function pair.</summary>
public sealed class DelegateUndoAction(string name, Action undo, Action redo) : IUndoAction
{
    /// <inheritdoc/>
    public string Name { get; } = name;

    /// <inheritdoc/>
    public void Undo() => undo();

    /// <inheritdoc/>
    public void Redo() => redo();
}

/// <summary>
/// The undo and redo history of a graph. Graph operations and user-editable properties record their changes here
/// themselves; <see cref="Group"/> makes several changes one step.
/// </summary>
/// <remarks>
/// Changes of the same property that follow each other closely (within <see cref="MergeWindow"/>, or inside one group)
/// merge into one step, so dragging a slider or a node is undone at once. <see cref="BreakMerge"/> starts a new step.
/// </remarks>
public sealed class UndoStack
{
    private readonly List<IUndoAction> _undo = [];
    private readonly List<IUndoAction> _redo = [];
    private readonly Stack<CompositeAction> _groups = new();
    private PropertyChange? _lastChange;
    private long _lastChangeTicks;

    /// <summary>Occurs after the history changed (a change was recorded, undone or redone, or it was cleared).</summary>
    public event EventHandler? Changed;

    /// <summary>Gets or sets how many steps are kept. The default is 200.</summary>
    public int MaxDepth { get; set; } = 200;

    /// <summary>Gets or sets how long after a change of a property the next change of it merges into the same step. The default is 1 second.</summary>
    public TimeSpan MergeWindow { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the clock for <see cref="MergeWindow"/> (for tests).</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>Gets whether an undo or redo is being applied; changes made meanwhile aren't recorded.</summary>
    public bool IsApplying { get; private set; }

    /// <summary>Gets whether there is a step to undo.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Gets whether there is a step to redo.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Gets the name of the step <see cref="Undo"/> reverts, or <c>null</c>.</summary>
    public string? UndoName => _undo.Count > 0 ? _undo[^1].Name : null;

    /// <summary>Gets the name of the step <see cref="Redo"/> applies, or <c>null</c>.</summary>
    public string? RedoName => _redo.Count > 0 ? _redo[^1].Name : null;

    /// <summary>
    /// Starts a group: the changes recorded until the returned object is disposed become one step named
    /// <paramref name="name"/>. Groups can nest; an empty group records nothing.
    /// </summary>
    public IDisposable Group(string name)
    {
        var group = new CompositeAction(name);
        _groups.Push(group);
        _lastChange = null;
        return new GroupScope(this, group);
    }

    /// <summary>Records a change that has already been made; ignored while <see cref="IsApplying"/>.</summary>
    public void Push(IUndoAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsApplying) return;
        _lastChange = null;
        Add(action);
    }

    /// <summary>
    /// Records that a property keyed by <paramref name="key"/> changed from <paramref name="oldValue"/> to
    /// <paramref name="newValue"/>; <paramref name="apply"/> sets it. Merges with the previous change of the same key
    /// (see the remarks of <see cref="UndoStack"/>). Ignored while <see cref="IsApplying"/>.
    /// </summary>
    public void RecordChange<T>(string name, object key, T oldValue, T newValue, Action<T> apply)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(apply);
        if (IsApplying || EqualityComparer<T>.Default.Equals(oldValue, newValue)) return;

        long now = TimeProvider.GetTimestamp();
        if (_lastChange != null && Equals(_lastChange.Key, key) && _lastChange.Group == CurrentGroup
            && (CurrentGroup != null || TimeProvider.GetElapsedTime(_lastChangeTicks, now) <= MergeWindow))
        {
            _lastChange.NewValue = newValue;
            _lastChangeTicks = now;
            return;
        }

        var change = new PropertyChange(name, key, oldValue, newValue, value => apply((T)value!), CurrentGroup);
        Add(change);
        _lastChange = change;
        _lastChangeTicks = now;
    }

    /// <summary>Makes the next property change a step of its own, even if it would merge with the previous one.</summary>
    public void BreakMerge() => _lastChange = null;

    /// <summary>Reverts the last step; <c>false</c> if there is none.</summary>
    public bool Undo() => Apply(_undo, _redo, undo: true);

    /// <summary>Applies the last undone step again; <c>false</c> if there is none.</summary>
    public bool Redo() => Apply(_redo, _undo, undo: false);

    /// <summary>Forgets all steps.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _lastChange = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private CompositeAction? CurrentGroup => _groups.Count > 0 ? _groups.Peek() : null;

    private void Add(IUndoAction action)
    {
        if (CurrentGroup is { } group)
        {
            group.Actions.Add(action);
            return;
        }
        _undo.Add(action);
        if (_undo.Count > MaxDepth) _undo.RemoveAt(0);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool Apply(List<IUndoAction> from, List<IUndoAction> to, bool undo)
    {
        if (from.Count == 0 || _groups.Count > 0) return false;
        var action = from[^1];
        from.RemoveAt(from.Count - 1);
        IsApplying = true;
        try
        {
            if (undo) action.Undo();
            else action.Redo();
        }
        finally
        {
            IsApplying = false;
        }
        to.Add(action);
        _lastChange = null;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void EndGroup(CompositeAction group)
    {
        if (_groups.Count == 0 || _groups.Peek() != group)
        {
            throw new InvalidOperationException("Undo groups must be ended in the reverse order they were started.");
        }
        _groups.Pop();
        _lastChange = null;
        if (group.Actions.Count > 0) Add(group.Actions.Count == 1 && _groups.Count == 0 ? Renamed(group.Actions[0], group.Name) : group);
    }

    // A group of one change keeps that change, under the group's name.
    private static IUndoAction Renamed(IUndoAction action, string name) =>
        action.Name == name ? action : new DelegateUndoAction(name, action.Undo, action.Redo);

    private sealed class CompositeAction(string name) : IUndoAction
    {
        public string Name { get; } = name;
        public List<IUndoAction> Actions { get; } = [];

        public void Undo()
        {
            for (int i = Actions.Count - 1; i >= 0; i--) Actions[i].Undo();
        }

        public void Redo()
        {
            foreach (var action in Actions) action.Redo();
        }
    }

    private sealed class PropertyChange(string name, object key, object? oldValue, object? newValue, Action<object?> apply, CompositeAction? group) : IUndoAction
    {
        public string Name { get; } = name;
        public object Key { get; } = key;
        public object? NewValue { get; set; } = newValue;
        public CompositeAction? Group { get; } = group;

        public void Undo() => apply(oldValue);
        public void Redo() => apply(NewValue);
    }

    private sealed class GroupScope(UndoStack stack, CompositeAction group) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            stack.EndGroup(group);
        }
    }
}
