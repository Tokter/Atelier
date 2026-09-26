using System;

namespace Atelier.Core.Inspection;

/// <summary>
/// Marks a class or struct for automatic compile-time generation of an <see cref="IInspectableObject"/> implementation.
/// The annotated type must be declared as <c>partial</c>.
/// </summary>
/// <remarks>
/// Properties of structs are generated as read-only: descriptors receive the target as <see cref="object"/>, so a setter
/// would only modify a boxed copy and the edit would be lost.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class InspectableAttribute : Attribute
{
}
