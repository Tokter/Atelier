using System.Numerics;

namespace Atelier.Graphics3D;

/// <summary>How the indices of a <see cref="Mesh3D"/> are put together into primitives.</summary>
public enum PrimitiveTopology
{
    /// <summary>Every three indices form a triangle (counter-clockwise is the front).</summary>
    Triangles,

    /// <summary>Every two indices form a line segment.</summary>
    Lines,

    /// <summary>Every index is a point, drawn as a round dot <see cref="Material3D.PointSize"/> pixels wide.</summary>
    Points,
}

/// <summary>
/// Geometry for a <see cref="Viewport3D"/>: vertex positions with optional normals, texture coordinates and colors, and
/// indices that form triangles, lines or points.
/// </summary>
/// <remarks>
/// <para>
/// A mesh can be shown by several <see cref="MeshInstance3D"/>s and in several viewports; the GPU copy is made per
/// window on the first draw and updated when the mesh changes. <see cref="SetGeometry"/> replaces everything;
/// <see cref="UpdatePositions"/> only moves the vertices, which is cheap enough to animate every frame (cloth, a
/// simulated canopy).
/// </para>
/// <para>Coordinates are right-handed with Y up (the glTF convention).</para>
/// </remarks>
/// <example>
/// <code>
/// var quad = new Mesh3D();
/// quad.SetGeometry(
///     [new(-1, 0, -1), new(1, 0, -1), new(1, 0, 1), new(-1, 0, 1)],
///     [0, 2, 1, 0, 3, 2],
///     texCoords: [new(0, 0), new(1, 0), new(1, 1), new(0, 1)]);
/// </code>
/// </example>
public sealed class Mesh3D
{
    private Vector3[] _positions = [];
    private Vector3[]? _normals;
    private Vector2[]? _texCoords;
    private Vector4[]? _colors;
    private uint[] _indices = [];
    private (Vector3 Min, Vector3 Max) _bounds;

    /// <summary>Initializes an empty mesh of <paramref name="topology"/>.</summary>
    public Mesh3D(PrimitiveTopology topology = PrimitiveTopology.Triangles)
    {
        Topology = topology;
    }

    /// <summary>Gets how the indices form primitives.</summary>
    public PrimitiveTopology Topology { get; }

    /// <summary>Gets the vertex positions.</summary>
    public Vector3[] Positions => _positions;

    /// <summary>Gets the vertex normals, or <c>null</c> (lines and points without normals are drawn unlit).</summary>
    public Vector3[]? Normals => _normals;

    /// <summary>Gets the texture coordinates (0,0 is the top left of the texture), or <c>null</c>.</summary>
    public Vector2[]? TexCoords => _texCoords;

    /// <summary>Gets the per-vertex colors (linear RGBA, 0 to 1, multiplied with the material's color), or <c>null</c>.</summary>
    public Vector4[]? Colors => _colors;

    /// <summary>Gets the indices into the vertex arrays.</summary>
    public uint[] Indices => _indices;

    /// <summary>Gets the box around the positions (both corners zero for an empty mesh).</summary>
    public (Vector3 Min, Vector3 Max) Bounds => _bounds;

    /// <summary>Gets whether the mesh has no primitives to draw.</summary>
    public bool IsEmpty => _indices.Length == 0 || _positions.Length == 0;

    /// <summary>Gets a number that changes whenever <see cref="SetGeometry"/> replaces the data (GPU buffers are rebuilt).</summary>
    public int GeometryVersion { get; private set; }

    /// <summary>Gets a number that changes whenever the positions (or normals) change, by either update method.</summary>
    public int PositionsVersion { get; private set; }

    /// <summary>Occurs after the geometry or the positions changed.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Replaces all data. The arrays are kept (not copied): don't change them afterwards except through
    /// <see cref="UpdatePositions"/>.
    /// </summary>
    /// <param name="positions">The vertex positions.</param>
    /// <param name="indices">The indices: three per triangle, two per line, one per point.</param>
    /// <param name="normals">The normals, one per vertex; <c>null</c> computes smooth normals for triangles.</param>
    /// <param name="texCoords">The texture coordinates, one per vertex, or <c>null</c>.</param>
    /// <param name="colors">The colors (linear RGBA), one per vertex, or <c>null</c>.</param>
    /// <exception cref="ArgumentException">An array's length doesn't match the positions, or an index is out of range.</exception>
    public void SetGeometry(Vector3[] positions, uint[] indices, Vector3[]? normals = null, Vector2[]? texCoords = null, Vector4[]? colors = null)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(indices);
        if (normals != null && normals.Length != positions.Length) throw new ArgumentException("There must be one normal per position.", nameof(normals));
        if (texCoords != null && texCoords.Length != positions.Length) throw new ArgumentException("There must be one texture coordinate per position.", nameof(texCoords));
        if (colors != null && colors.Length != positions.Length) throw new ArgumentException("There must be one color per position.", nameof(colors));
        uint count = (uint)positions.Length;
        foreach (uint index in indices)
        {
            if (index >= count) throw new ArgumentException($"The index {index} is out of range (there are {count} positions).", nameof(indices));
        }

        _positions = positions;
        _indices = indices;
        _normals = normals ?? (Topology == PrimitiveTopology.Triangles ? ComputeNormals(positions, indices) : null);
        _texCoords = texCoords;
        _colors = colors;
        _bounds = ComputeBounds(positions);
        GeometryVersion++;
        PositionsVersion++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves the vertices without changing the topology, for animation: only the positions and normals are sent to the
    /// GPU again.
    /// </summary>
    /// <param name="positions">The new positions, as many as there are.</param>
    /// <param name="normals">The new normals, or empty to recompute smooth normals (for triangles; meshes without normals keep none).</param>
    /// <exception cref="ArgumentException">The number of positions or normals changed.</exception>
    public void UpdatePositions(ReadOnlySpan<Vector3> positions, ReadOnlySpan<Vector3> normals = default)
    {
        if (positions.Length != _positions.Length) throw new ArgumentException("The number of positions can't change; use SetGeometry.", nameof(positions));
        if (!normals.IsEmpty && normals.Length != _positions.Length) throw new ArgumentException("There must be one normal per position.", nameof(normals));

        positions.CopyTo(_positions);
        if (!normals.IsEmpty)
        {
            _normals ??= new Vector3[_positions.Length];
            normals.CopyTo(_normals);
        }
        else if (_normals != null && Topology == PrimitiveTopology.Triangles)
        {
            ComputeNormals(_positions, _indices, _normals);
        }
        _bounds = ComputeBounds(_positions);
        PositionsVersion++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Computes smooth vertex normals for triangles: each vertex gets the sum of its triangles' face normals weighted by
    /// their area, normalized.
    /// </summary>
    public static Vector3[] ComputeNormals(ReadOnlySpan<Vector3> positions, ReadOnlySpan<uint> indices)
    {
        var normals = new Vector3[positions.Length];
        ComputeNormals(positions, indices, normals);
        return normals;
    }

    /// <summary>Computes smooth vertex normals (see <see cref="ComputeNormals(ReadOnlySpan{Vector3}, ReadOnlySpan{uint})"/>) into <paramref name="normals"/>.</summary>
    public static void ComputeNormals(ReadOnlySpan<Vector3> positions, ReadOnlySpan<uint> indices, Span<Vector3> normals)
    {
        normals.Clear();
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            uint a = indices[i], b = indices[i + 1], c = indices[i + 2];
            // The cross product's length is twice the triangle's area: summing it weights by area.
            var face = Vector3.Cross(positions[(int)b] - positions[(int)a], positions[(int)c] - positions[(int)a]);
            normals[(int)a] += face;
            normals[(int)b] += face;
            normals[(int)c] += face;
        }
        for (int i = 0; i < normals.Length; i++)
        {
            float length = normals[i].Length();
            normals[i] = length > 1e-20f ? normals[i] / length : Vector3.UnitY;
        }
    }

    private static (Vector3, Vector3) ComputeBounds(ReadOnlySpan<Vector3> positions)
    {
        if (positions.IsEmpty) return (Vector3.Zero, Vector3.Zero);
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var p in positions)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return (min, max);
    }
}
