using Atelier.Core.Primitives;

namespace Atelier.NodeEditor;

/// <summary>How a socket is drawn; Blender uses circles for single values and diamonds for per-element fields.</summary>
public enum SocketShape
{
    /// <summary>A circle.</summary>
    Circle,

    /// <summary>A diamond.</summary>
    Diamond,

    /// <summary>A square.</summary>
    Square,
}

/// <summary>
/// The data type of sockets: its name, color and shape, the .NET type of its values, and which other types' outputs
/// its inputs accept. By default an input only accepts outputs of its own type; <see cref="AddConversionFrom"/> opts
/// into more, with the conversion of the value (a float into a vector, an int into a float).
/// </summary>
/// <remarks>Socket types are compared by reference; create each once and share it (e.g. as static fields).</remarks>
public sealed class SocketType
{
    private readonly Dictionary<SocketType, Func<object?, object?>> _conversions = [];

    /// <summary>Initializes a socket type.</summary>
    /// <param name="id">A stable identifier, such as <c>"float"</c>, used when saving graphs.</param>
    /// <param name="name">The name shown to users.</param>
    /// <param name="color">The color of its sockets and links.</param>
    /// <param name="valueType">The .NET type of its values, or <c>null</c> for sockets that carry no value.</param>
    /// <param name="defaultValue">The value of an input without link or value of its own.</param>
    /// <param name="shape">How its sockets are drawn.</param>
    public SocketType(string id, string name, Color color, Type? valueType = null, object? defaultValue = null, SocketShape shape = SocketShape.Circle)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
        Name = name ?? id;
        Color = color;
        ValueType = valueType;
        DefaultValue = defaultValue;
        Shape = shape;
    }

    /// <summary>Gets the stable identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the name shown to users.</summary>
    public string Name { get; }

    /// <summary>Gets the color of its sockets and links.</summary>
    public Color Color { get; }

    /// <summary>Gets the .NET type of its values, or <c>null</c>.</summary>
    public Type? ValueType { get; }

    /// <summary>Gets the value of an input without a link or a value of its own.</summary>
    public object? DefaultValue { get; }

    /// <summary>Gets how its sockets are drawn.</summary>
    public SocketShape Shape { get; }

    /// <summary>
    /// Lets inputs of this type accept outputs of <paramref name="from"/>, converting their values with
    /// <paramref name="convert"/>.
    /// </summary>
    /// <returns>This type, to chain calls.</returns>
    public SocketType AddConversionFrom(SocketType from, Func<object?, object?> convert)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(convert);
        if (from != this) _conversions[from] = convert;
        return this;
    }

    /// <summary>Gets whether inputs of this type accept outputs of <paramref name="from"/>: the same type, or one with a conversion.</summary>
    public bool CanConnectFrom(SocketType from) => from == this || _conversions.ContainsKey(from);

    /// <summary>Converts <paramref name="value"/> from an output of <paramref name="from"/> to this type (unchanged for the same type).</summary>
    /// <exception cref="InvalidOperationException">This type doesn't accept <paramref name="from"/>.</exception>
    public object? ConvertFrom(object? value, SocketType from)
    {
        if (from == this) return value;
        return _conversions.TryGetValue(from, out var convert)
            ? convert(value)
            : throw new InvalidOperationException($"Sockets of type '{Id}' don't accept '{from.Id}'.");
    }

    /// <inheritdoc/>
    public override string ToString() => Id;
}
