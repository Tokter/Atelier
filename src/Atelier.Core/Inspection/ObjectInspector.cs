using System;
using System.Collections.Generic;

namespace Atelier.Core.Inspection;

/// <summary>
/// Static utility methods for inspecting objects without reflection.
/// </summary>
public static class ObjectInspector
{
    private static readonly IPropertyDescriptor[] s_emptyProperties = Array.Empty<IPropertyDescriptor>();

    /// <summary>
    /// Gets all inspectable property descriptors for the specified target.
    /// If the target implements <see cref="IInspectableObject"/>, its descriptor table is returned;
    /// otherwise, returns an empty list.
    /// </summary>
    public static IReadOnlyList<IPropertyDescriptor> GetProperties(object? target)
    {
        if (target is IInspectableObject inspectable)
        {
            return inspectable.GetProperties();
        }

        return s_emptyProperties;
    }

    /// <summary>
    /// Finds a specific property descriptor by name on the specified target object.
    /// </summary>
    public static IPropertyDescriptor? GetProperty(object? target, string propertyName)
    {
        if (target is null || string.IsNullOrEmpty(propertyName))
            return null;

        var properties = GetProperties(target);
        for (int i = 0; i < properties.Count; i++)
        {
            if (string.Equals(properties[i].Name, propertyName, StringComparison.Ordinal))
            {
                return properties[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to read the value of a named property on the target without reflection.
    /// </summary>
    public static bool TryGetValue(object? target, string propertyName, out object? value)
    {
        var prop = GetProperty(target, propertyName);
        if (prop != null && target != null)
        {
            value = prop.GetValue(target);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Attempts to write the value of a named property on the target without reflection.
    /// </summary>
    public static bool TrySetValue(object? target, string propertyName, object? value)
    {
        var prop = GetProperty(target, propertyName);
        if (prop != null && target != null && !prop.IsReadOnly)
        {
            prop.SetValue(target, value);
            return true;
        }

        return false;
    }
}
