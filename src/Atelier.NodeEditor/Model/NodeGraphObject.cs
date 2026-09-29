using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Atelier.NodeEditor;

/// <summary>The base of the node graph's view models: property change notification.</summary>
public abstract class NodeGraphObject : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises <see cref="PropertyChanged"/> for <paramref name="propertyName"/>.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Sets <paramref name="field"/> and raises <see cref="PropertyChanged"/> if the value changed.</summary>
    /// <returns><c>true</c> if the value changed.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Sets <paramref name="field"/> like <see cref="SetProperty{T}"/> and records the change on <paramref name="undo"/>
    /// as a step named <paramref name="actionName"/>; <paramref name="apply"/> sets the property when it's undone or
    /// redone. Changes of the same property of the same object merge (see <see cref="UndoStack.RecordChange{T}"/>).
    /// </summary>
    protected bool SetUndoableProperty<T>(ref T field, T value, UndoStack? undo, string actionName, Action<T> apply, [CallerMemberName] string? propertyName = null)
    {
        var oldValue = field;
        if (!SetProperty(ref field, value, propertyName)) return false;
        undo?.RecordChange(actionName, (this, propertyName), oldValue, value, apply);
        return true;
    }
}
