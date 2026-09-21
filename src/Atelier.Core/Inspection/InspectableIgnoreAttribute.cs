using System;

namespace Atelier.Core.Inspection;

/// <summary>
/// Specifies that a property on an <see cref="InspectableAttribute"/> marked class should be ignored
/// during compile-time metadata generation.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public sealed class InspectableIgnoreAttribute : Attribute
{
}
