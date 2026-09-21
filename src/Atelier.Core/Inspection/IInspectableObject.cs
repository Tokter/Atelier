using System.Collections.Generic;

namespace Atelier.Core.Inspection;

/// <summary>
/// Defines an object whose properties can be statically inspected without reflection.
/// Typically implemented automatically at compile-time by the source generator.
/// </summary>
public interface IInspectableObject
{
    /// <summary>
    /// Gets the list of property descriptors for this object.
    /// </summary>
    IReadOnlyList<IPropertyDescriptor> GetProperties();
}
