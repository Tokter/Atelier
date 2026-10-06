using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Numerics;
using Atelier.Core.Primitives;

namespace Atelier.Graphics3D;

/// <summary>
/// What a <see cref="Viewport3D"/> shows: mesh instances and the lighting (a sun, and a sky and ground ambient).
/// </summary>
/// <remarks>
/// <see cref="Changed"/> is raised for any change, of the instances, their meshes, materials or textures, or the
/// lighting, so viewports showing the scene redraw. A scene can be shown in several viewports and windows.
/// </remarks>
/// <example>
/// <code>
/// var scene = new Scene3D();
/// scene.Instances.Add(new MeshInstance3D(mesh, new Material3D { BaseColor = Color.FromHex("#E53935"), Roughness = 0.5f }));
/// var viewport = new Viewport3D().Scene(scene);
/// </code>
/// </example>
public sealed class Scene3D
{
    private Vector3 _sunDirection = Vector3.Normalize(new Vector3(0.4f, 0.8f, 0.45f));
    private Color _sunColor = Color.FromRgb(0xFF, 0xF4, 0xE5);
    private float _sunIntensity = 3f;
    private Color _skyColor = Color.FromRgb(0xA8, 0xC4, 0xE8);
    private Color _groundColor = Color.FromRgb(0x5A, 0x52, 0x48);
    private float _exposure = 1f;
    private readonly List<MeshInstance3D> _observed = [];

    /// <summary>Initializes an empty scene.</summary>
    public Scene3D()
    {
        Instances.CollectionChanged += OnInstancesChanged;
    }

    /// <summary>Gets the mesh instances, drawn in order (transparent ones after opaque ones).</summary>
    public ObservableCollection<MeshInstance3D> Instances { get; } = [];

    /// <summary>Gets or sets the direction towards the sun (normalized when set).</summary>
    public Vector3 SunDirection
    {
        get => _sunDirection;
        set
        {
            var direction = value.LengthSquared() > 1e-12f ? Vector3.Normalize(value) : Vector3.UnitY;
            if (_sunDirection == direction) return;
            _sunDirection = direction;
            RaiseChanged();
        }
    }

    /// <summary>Gets or sets the sun's color (sRGB).</summary>
    public Color SunColor { get => _sunColor; set => Set(ref _sunColor, value); }

    /// <summary>Gets or sets the sun's brightness. The default is 3.</summary>
    public float SunIntensity { get => _sunIntensity; set => Set(ref _sunIntensity, Math.Max(0, value)); }

    /// <summary>Gets or sets the ambient light from above (sRGB).</summary>
    public Color SkyColor { get => _skyColor; set => Set(ref _skyColor, value); }

    /// <summary>Gets or sets the ambient light from below (sRGB).</summary>
    public Color GroundColor { get => _groundColor; set => Set(ref _groundColor, value); }

    /// <summary>Gets or sets the exposure the image is scaled by before tone mapping. The default is 1.</summary>
    public float Exposure { get => _exposure; set => Set(ref _exposure, Math.Max(0, value)); }

    /// <summary>Occurs after anything in the scene changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the box around the visible instances, or <c>null</c> when nothing is visible.</summary>
    public (Vector3 Min, Vector3 Max)? GetBounds()
    {
        (Vector3 Min, Vector3 Max)? result = null;
        foreach (var instance in Instances)
        {
            if (!instance.IsVisible || instance.GetBounds() is not { } bounds) continue;
            result = result is { } r ? (Vector3.Min(r.Min, bounds.Min), Vector3.Max(r.Max, bounds.Max)) : bounds;
        }
        return result;
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private void OnInstancesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Resubscribe from scratch: handles adds, removes, replaces, moves and resets alike (and duplicates).
        foreach (var instance in _observed) instance.Changed -= OnInstanceChanged;
        _observed.Clear();
        foreach (var instance in Instances)
        {
            instance.Changed += OnInstanceChanged;
            _observed.Add(instance);
        }
        RaiseChanged();
    }

    private void OnInstanceChanged(object? sender, EventArgs e) => RaiseChanged();
}
