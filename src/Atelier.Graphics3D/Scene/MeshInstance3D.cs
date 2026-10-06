using System.Numerics;

namespace Atelier.Graphics3D;

/// <summary>A <see cref="Mesh3D"/> placed in a <see cref="Scene3D"/> with a <see cref="Material3D"/> and a transform.</summary>
public sealed class MeshInstance3D
{
    private Mesh3D _mesh;
    private Material3D _material;
    private Matrix4x4 _transform = Matrix4x4.Identity;
    private bool _isVisible = true;
    private bool _drawOnTop;

    /// <summary>Initializes an instance of <paramref name="mesh"/> drawn with <paramref name="material"/>.</summary>
    public MeshInstance3D(Mesh3D mesh, Material3D material)
    {
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        _material = material ?? throw new ArgumentNullException(nameof(material));
        _mesh.Changed += OnPartChanged;
        _material.Changed += OnPartChanged;
    }

    /// <summary>Gets or sets the geometry.</summary>
    public Mesh3D Mesh
    {
        get => _mesh;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_mesh, value)) return;
            _mesh.Changed -= OnPartChanged;
            _mesh = value;
            _mesh.Changed += OnPartChanged;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the material.</summary>
    public Material3D Material
    {
        get => _material;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_material, value)) return;
            _material.Changed -= OnPartChanged;
            _material = value;
            _material.Changed += OnPartChanged;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the transform from the mesh's coordinates to the scene's (row vectors, as System.Numerics).</summary>
    public Matrix4x4 Transform
    {
        get => _transform;
        set
        {
            if (_transform == value) return;
            _transform = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets whether the instance is drawn. The default is <c>true</c>.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            _isVisible = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets whether the instance is drawn over everything else, ignoring depth (for helpers such as simulation
    /// nodes over a canopy). The default is <c>false</c>.
    /// </summary>
    public bool DrawOnTop
    {
        get => _drawOnTop;
        set
        {
            if (_drawOnTop == value) return;
            _drawOnTop = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets a name, for debugging and tools.</summary>
    public string? Name { get; set; }

    /// <summary>Occurs after a property, the mesh or the material changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the box around the mesh in scene coordinates, or <c>null</c> when the mesh is empty.</summary>
    public (Vector3 Min, Vector3 Max)? GetBounds()
    {
        if (_mesh.Positions.Length == 0) return null;
        var (min, max) = _mesh.Bounds;
        if (_transform.IsIdentity) return (min, max);
        var resultMin = new Vector3(float.MaxValue);
        var resultMax = new Vector3(float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
            var p = Vector3.Transform(corner, _transform);
            resultMin = Vector3.Min(resultMin, p);
            resultMax = Vector3.Max(resultMax, p);
        }
        return (resultMin, resultMax);
    }

    /// <inheritdoc/>
    public override string ToString() => Name ?? $"MeshInstance3D({_mesh.Positions.Length} vertices)";

    private void OnPartChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
