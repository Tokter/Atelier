using System;

namespace Atelier.Core.Inspection;

/// <summary>
/// Configures inspection metadata for a property on an <see cref="InspectableAttribute"/> marked type.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class InspectablePropertyAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the custom display name for this property in UI inspectors.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the logical category for grouping this property in UI inspectors.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the sort order priority within its category (ascending).
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets whether this property should be treated as read-only in UI inspectors.
    /// </summary>
    public bool IsReadOnly { get; set; }

    public InspectablePropertyAttribute()
    {
    }

    public InspectablePropertyAttribute(string displayName, string category = "General")
    {
        DisplayName = displayName;
        Category = category;
    }
}
