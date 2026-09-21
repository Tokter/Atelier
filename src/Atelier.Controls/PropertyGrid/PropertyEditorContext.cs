using System;
using Atelier.Core.Inspection;

namespace Atelier.Controls;

/// <summary>
/// Context provided to built-in and custom property editors to inspect and mutate property values.
/// Fully compatible with Native AOT without reflection.
/// </summary>
public sealed class PropertyEditorContext
{
    /// <summary>
    /// Gets the parent <see cref="PropertyGrid"/> hosting the editor.
    /// </summary>
    public PropertyGrid Grid { get; }

    /// <summary>
    /// Gets the target object instance whose property is being edited.
    /// </summary>
    public object Target { get; }

    /// <summary>
    /// Gets the property descriptor containing metadata and strongly typed accessors.
    /// </summary>
    public IPropertyDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the current property value on the target object.
    /// </summary>
    public object? Value => Descriptor.GetValue(Target);

    /// <summary>
    /// Gets whether the property is read-only.
    /// </summary>
    public bool IsReadOnly => Descriptor.IsReadOnly;

    /// <summary>
    /// Event raised when the value is successfully updated through this context.
    /// </summary>
    public event Action<object?>? ValueChanged;

    public PropertyEditorContext(PropertyGrid grid, object target, IPropertyDescriptor descriptor)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    /// <summary>
    /// Gets the strongly typed current value of the property, or default if null/mismatched.
    /// </summary>
    public T? GetValue<T>()
    {
        object? val = Value;
        return val is T typed ? typed : default;
    }

    /// <summary>
    /// Sets a new property value on the target object and notifies the <see cref="PropertyGrid"/>.
    /// </summary>
    public void UpdateValue(object? newValue)
    {
        if (Descriptor.IsReadOnly)
            return;

        object? oldValue = Descriptor.GetValue(Target);
        if (Equals(oldValue, newValue))
            return;

        Descriptor.SetValue(Target, newValue);
        ValueChanged?.Invoke(newValue);
        Grid.NotifyPropertyValueChanged(new PropertyValueChangedEventArgs(Target, Descriptor, oldValue, newValue));
    }
}
