using System.Runtime.CompilerServices;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Markup;

namespace Atelier.Graphics3D;

/// <summary>Fluent methods for <see cref="Viewport3D"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class Viewport3DMarkup
{
    /// <summary>Sets the scene shown.</summary>
    public static T Scene<T>(this T viewport, Scene3D? scene) where T : Viewport3D => viewport.Set(Viewport3D.SceneProperty, scene);

    /// <summary>Sets how surfaces are drawn. The default is <see cref="ViewportShading.Shaded"/>.</summary>
    public static T Shading<T>(this T viewport, ViewportShading shading) where T : Viewport3D => viewport.Set(Viewport3D.ShadingProperty, shading);

    /// <summary>Sets whether the ground grid is drawn. The default is <c>true</c>.</summary>
    public static T ShowGrid<T>(this T viewport, bool showGrid = true) where T : Viewport3D => viewport.Set(Viewport3D.ShowGridProperty, showGrid);

    /// <summary>Sets whether the axis gizmo is drawn. The default is <c>true</c>.</summary>
    public static T ShowAxes<T>(this T viewport, bool showAxes = true) where T : Viewport3D => viewport.Set(Viewport3D.ShowAxesProperty, showAxes);

    /// <summary>Sets the height of the ground grid (see <see cref="Viewport3D.GridHeight"/>).</summary>
    public static T GridHeight<T>(this T viewport, float height) where T : Viewport3D => viewport.Set(Viewport3D.GridHeightProperty, height);

    /// <summary>Sets the background gradient, from <paramref name="top"/> to <paramref name="bottom"/> (sRGB).</summary>
    public static T Background<T>(this T viewport, Color top, Color bottom) where T : Viewport3D =>
        viewport.Set(Viewport3D.BackgroundTopProperty, top).Set(Viewport3D.BackgroundBottomProperty, bottom);

    /// <summary>Sets the color of wireframe edges.</summary>
    public static T WireframeColor<T>(this T viewport, Color color) where T : Viewport3D => viewport.Set(Viewport3D.WireframeColorProperty, color);

    /// <summary>Sets the multisamples per pixel (1 to 32). The default is 4.</summary>
    public static T Samples<T>(this T viewport, int samples) where T : Viewport3D => viewport.Set(Viewport3D.SamplesProperty, samples);

    /// <summary>Sets the camera's angles (radians) and distance from its target.</summary>
    public static T CameraView<T>(this T viewport, float yaw, float pitch, float distance) where T : Viewport3D
    {
        viewport.Camera.SetAngles(yaw, pitch);
        viewport.Camera.Distance = distance;
        return viewport;
    }

    /// <summary>Handles <see cref="Viewport3D.Rendered"/>, raised after every frame rendered with the GPU.</summary>
    public static T OnRendered<T>(this T viewport, EventHandler handler) where T : Viewport3D
    {
        viewport.Rendered += handler;
        return viewport;
    }

    /// <summary>Runs <paramref name="action"/> after every frame rendered with the GPU (<see cref="Viewport3D.Rendered"/>).</summary>
    public static T OnRendered<T>(this T viewport, Action action) where T : Viewport3D => viewport.OnRendered(MarkupExtensions.ToHandler(action));

    /// <summary>Binds the scene to <paramref name="source"/>.</summary>
    public static T BindScene<T, TSource>(this T viewport, TSource source, Func<TSource, Scene3D?> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Viewport3D where TSource : class =>
        viewport.BindToSource(Viewport3D.SceneProperty, source, getter, null, UpdateSourceTrigger.PropertyChanged, getterExpression);

    /// <summary>Binds the shading to <paramref name="source"/>. With a <paramref name="setter"/>, Shift+Z writes it back.</summary>
    public static T BindShading<T, TSource>(this T viewport, TSource source, Func<TSource, ViewportShading> getter, Action<TSource, ViewportShading>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Viewport3D where TSource : class =>
        viewport.BindToSource(Viewport3D.ShadingProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds whether the grid is shown to <paramref name="source"/>.</summary>
    public static T BindShowGrid<T, TSource>(this T viewport, TSource source, Func<TSource, bool> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Viewport3D where TSource : class =>
        viewport.BindToSource(Viewport3D.ShowGridProperty, source, getter, null, UpdateSourceTrigger.PropertyChanged, getterExpression);
}
