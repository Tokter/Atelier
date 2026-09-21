using System;

namespace Atelier.Core.Inspection;

/// <summary>
/// Represents a statically registered or compile-time generated descriptor for an object property.
/// Compatible with Native AOT without requiring runtime reflection.
/// </summary>
public interface IPropertyDescriptor
{
    /// <summary>
    /// Gets the property name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the user-friendly display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the logical category group.
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Gets the property type.
    /// </summary>
    Type PropertyType { get; }

    /// <summary>
    /// Gets whether this property is read-only.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Gets the current property value from the target object.
    /// </summary>
    object? GetValue(object target);

    /// <summary>
    /// Sets the property value on the target object.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the property is read-only.</exception>
    void SetValue(object target, object? value);
}
