using Atelier.Core.Primitives;

namespace Atelier.Graphics3D;

/// <summary>
/// How a <see cref="Mesh3D"/> looks: a base color or texture, an optional normal map, roughness and metalness for the
/// highlights, and translucency for thin fabric.
/// </summary>
/// <remarks>
/// Surfaces are lit by the scene's sun and a sky and ground ambient (see <see cref="Scene3D"/>).
/// <see cref="Transmission"/> lets sunlight through to the side facing away from the sun, so a canopy glows when seen
/// against the light. <see cref="Unlit"/> draws the plain color, for helpers such as wireframes and points.
/// </remarks>
public sealed class Material3D
{
    private Color _baseColor = Color.White;
    private Texture3D? _baseColorTexture;
    private Texture3D? _normalTexture;
    private float _normalScale = 1f;
    private float _roughness = 0.7f;
    private float _metallic;
    private float _transmission;
    private bool _doubleSided = true;
    private bool _unlit;
    private float _pointSize = 6f;

    /// <summary>Gets or sets the base color (sRGB), multiplied with the texture and vertex colors; its alpha makes the surface see-through.</summary>
    public Color BaseColor { get => _baseColor; set => Set(ref _baseColor, value); }

    /// <summary>Gets or sets the color texture (sRGB), or <c>null</c>.</summary>
    public Texture3D? BaseColorTexture
    {
        get => _baseColorTexture;
        set => SetTexture(ref _baseColorTexture, value);
    }

    /// <summary>
    /// Gets or sets a tangent-space normal map (OpenGL convention, as in glTF: green points up the image),
    /// or <c>null</c>. No tangents are needed: the frame comes from the texture coordinates' screen derivatives.
    /// </summary>
    public Texture3D? NormalTexture
    {
        get => _normalTexture;
        set => SetTexture(ref _normalTexture, value);
    }

    /// <summary>Gets or sets how strongly the normal map bends the normals. The default is 1.</summary>
    public float NormalScale { get => _normalScale; set => Set(ref _normalScale, value); }

    /// <summary>Gets or sets the roughness, from 0 (mirror-like highlights) to 1 (matte). The default is 0.7.</summary>
    public float Roughness { get => _roughness; set => Set(ref _roughness, Math.Clamp(value, 0.02f, 1f)); }

    /// <summary>Gets or sets the metalness, from 0 (dielectric) to 1 (metal). The default is 0.</summary>
    public float Metallic { get => _metallic; set => Set(ref _metallic, Math.Clamp(value, 0f, 1f)); }

    /// <summary>
    /// Gets or sets how much sunlight passes through the surface, from 0 (opaque) to 1: the side facing away from the
    /// sun shows the light filtered by the base color, like backlit fabric. The default is 0.
    /// </summary>
    public float Transmission { get => _transmission; set => Set(ref _transmission, Math.Clamp(value, 0f, 1f)); }

    /// <summary>Gets or sets whether back faces are drawn (with flipped normals). The default is <c>true</c>.</summary>
    public bool DoubleSided { get => _doubleSided; set => Set(ref _doubleSided, value); }

    /// <summary>Gets or sets whether lighting is skipped and the plain color drawn. The default is <c>false</c>.</summary>
    public bool Unlit { get => _unlit; set => Set(ref _unlit, value); }

    /// <summary>Gets or sets the diameter of points (for <see cref="PrimitiveTopology.Points"/>), in pixels. The default is 6.</summary>
    public float PointSize { get => _pointSize; set => Set(ref _pointSize, Math.Max(1f, value)); }

    /// <summary>Occurs after a property or a texture's pixels changed.</summary>
    public event EventHandler? Changed;

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SetTexture(ref Texture3D? field, Texture3D? value)
    {
        if (ReferenceEquals(field, value)) return;
        if (field != null) field.Changed -= OnTextureChanged;
        field = value;
        if (value != null) value.Changed += OnTextureChanged;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnTextureChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
