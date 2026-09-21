using System;
using Atelier.Core.Inspection;

namespace Atelier.Controls;

/// <summary>
/// Provides data for the <see cref="PropertyGrid.PropertyValueChanged"/> event.
/// </summary>
public sealed class PropertyValueChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the target object that was modified.
    /// </summary>
    public object Target { get; }

    /// <summary>
    /// Gets the property descriptor that was modified.
    /// </summary>
    public IPropertyDescriptor Property { get; }

    /// <summary>
    /// Gets the previous value of the property before the change.
    /// </summary>
    public object? OldValue { get; }

    /// <summary>
    /// Gets the new value of the property after the change.
    /// </summary>
    public object? NewValue { get; }

    public PropertyValueChangedEventArgs(object target, IPropertyDescriptor property, object? oldValue, object? newValue)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Property = property ?? throw new ArgumentNullException(nameof(property));
        OldValue = oldValue;
        NewValue = newValue;
    }
}
