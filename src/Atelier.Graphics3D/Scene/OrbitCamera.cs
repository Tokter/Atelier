using System.Numerics;

namespace Atelier.Graphics3D;

/// <summary>
/// A camera that orbits a target point, like Blender's viewport: <see cref="Yaw"/> turns around the vertical axis,
/// <see cref="Pitch"/> looks up or down, and <see cref="Distance"/> is how far the camera is from the
/// <see cref="Target"/>.
/// </summary>
/// <remarks>
/// Coordinates are right-handed with Y up. Yaw 0 and pitch 0 look along −Z from the +Z side (the "front" view); a
/// positive yaw moves the camera towards +X, a positive pitch above the target. Projections map depth to −1..1 (OpenGL).
/// </remarks>
public sealed class OrbitCamera
{
    /// <summary>The largest pitch, just short of straight up or down so the view keeps its up direction.</summary>
    public const float MaxPitch = MathF.PI / 2 - 0.0005f;

    private Vector3 _target;
    private float _distance = 10f;
    private float _yaw;
    private float _pitch;
    private float _fieldOfView = 0.8f;
    private bool _orthographic;
    private float _near = 0.05f;
    private float _far = 2000f;

    /// <summary>Gets or sets the point the camera looks at and orbits.</summary>
    public Vector3 Target { get => _target; set => Set(ref _target, value); }

    /// <summary>Gets or sets the distance from the target (at least 0.001). In orthographic mode it sets the zoom.</summary>
    public float Distance { get => _distance; set => Set(ref _distance, Math.Max(0.001f, value)); }

    /// <summary>Gets or sets the turn around the vertical axis, in radians.</summary>
    public float Yaw { get => _yaw; set => Set(ref _yaw, value); }

    /// <summary>Gets or sets the look up or down, in radians (positive is above the target), within ±<see cref="MaxPitch"/>.</summary>
    public float Pitch { get => _pitch; set => Set(ref _pitch, Math.Clamp(value, -MaxPitch, MaxPitch)); }

    /// <summary>Gets or sets the vertical field of view, in radians. The default is 0.8 (about 46°).</summary>
    public float FieldOfView { get => _fieldOfView; set => Set(ref _fieldOfView, Math.Clamp(value, 0.01f, 3f)); }

    /// <summary>Gets or sets whether the projection is orthographic (parallel) instead of perspective.</summary>
    public bool Orthographic { get => _orthographic; set => Set(ref _orthographic, value); }

    /// <summary>Gets or sets the near clipping distance. The default is 0.05.</summary>
    public float Near { get => _near; set => Set(ref _near, Math.Max(1e-5f, value)); }

    /// <summary>Gets or sets the far clipping distance. The default is 2000.</summary>
    public float Far { get => _far; set => Set(ref _far, Math.Max(_near * 2, value)); }

    /// <summary>Gets the direction from the target towards the camera (unit length).</summary>
    public Vector3 Backward => new(MathF.Sin(_yaw) * MathF.Cos(_pitch), MathF.Sin(_pitch), MathF.Cos(_yaw) * MathF.Cos(_pitch));

    /// <summary>Gets the camera's position.</summary>
    public Vector3 Position => _target + Backward * _distance;

    /// <summary>Gets the camera's right direction (unit length, horizontal).</summary>
    public Vector3 Right => new(MathF.Cos(_yaw), 0, -MathF.Sin(_yaw));

    /// <summary>Gets the camera's up direction (unit length).</summary>
    public Vector3 Up => Vector3.Cross(Backward, Right);

    /// <summary>Occurs after a property changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the view matrix (scene to camera coordinates).</summary>
    public Matrix4x4 GetView() => Matrix4x4.CreateLookAt(Position, _target, Up);

    /// <summary>Gets the projection matrix for a viewport of <paramref name="aspect"/> (width / height).</summary>
    public Matrix4x4 GetProjection(float aspect)
    {
        aspect = aspect > 0 && float.IsFinite(aspect) ? aspect : 1;
        // In orthographic mode the view is as tall as the perspective view's at the target, so switching keeps the size.
        // The clipping range is centered on the target, so orbiting never clips what is in front of the camera.
        if (_orthographic)
        {
            float height = 2 * _distance * MathF.Tan(_fieldOfView / 2);
            float depth = Math.Max(_far, _distance * 4);
            return Matrix4x4.CreateOrthographic(height * aspect, height, _distance - depth / 2, _distance + depth / 2);
        }
        return Matrix4x4.CreatePerspectiveFieldOfView(_fieldOfView, aspect, _near, _far);
    }

    /// <summary>Gets the view and projection combined.</summary>
    public Matrix4x4 GetViewProjection(float aspect) => GetView() * GetProjection(aspect);

    /// <summary>
    /// Points the camera at the center of the box and moves it back until the box fits the view (with a small margin),
    /// keeping the direction it looks from. Clipping distances follow the box's size.
    /// </summary>
    public void Frame(Vector3 min, Vector3 max)
    {
        var center = (min + max) / 2;
        float radius = Math.Max((max - min).Length() / 2, 0.01f);
        // The bounding sphere fits the narrower (vertical) field of view, with 10% to spare.
        float distance = radius / MathF.Sin(_fieldOfView / 2) * 1.1f;
        _target = center;
        _distance = distance;
        _near = Math.Max(distance / 1000, 1e-4f);
        _far = Math.Max(distance * 100, _near * 10);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sets <see cref="Yaw"/> and <see cref="Pitch"/> at once, raising <see cref="Changed"/> once.</summary>
    public void SetAngles(float yaw, float pitch)
    {
        _yaw = yaw;
        _pitch = Math.Clamp(pitch, -MaxPitch, MaxPitch);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves the target (and with it the camera) by <paramref name="right"/> and <paramref name="up"/> in the view plane,
    /// in scene units.
    /// </summary>
    public void Pan(float right, float up)
    {
        _target += Right * right + Up * up;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the scene units per pixel at the target's distance for a viewport <paramref name="viewportHeight"/> pixels tall,
    /// for panning that follows the pointer.
    /// </summary>
    public float UnitsPerPixel(float viewportHeight) =>
        viewportHeight > 0 ? 2 * _distance * MathF.Tan(_fieldOfView / 2) / viewportHeight : 0;

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
