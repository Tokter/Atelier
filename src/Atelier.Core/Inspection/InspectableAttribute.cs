using System;

namespace Atelier.Core.Inspection;

/// <summary>
/// Marks a class or struct for automatic compile-time generation of an <see cref="IInspectableObject"/> implementation.
/// The annotated type must be declared as <c>partial</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class InspectableAttribute : Attribute
{
}
