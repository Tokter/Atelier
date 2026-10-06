using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Graphics3D.Rendering;

namespace Atelier.Graphics3D;

/// <summary>How a <see cref="Viewport3D"/> draws surfaces.</summary>
public enum ViewportShading
{
    /// <summary>Lit, textured surfaces.</summary>
    Shaded,

    /// <summary>Only the triangle edges (lines and points stay as they are).</summary>
    Wireframe,

    /// <summary>Lit surfaces with their triangle edges drawn over them.</summary>
    ShadedWireframe,
}

/// <summary>
/// Shows a <see cref="Scene3D"/> through an <see cref="OrbitCamera"/>, rendered with OpenGL on the window's GPU
/// context: lit and textured meshes, lines and points, an infinite ground grid and an axis gizmo.
/// </summary>
/// <remarks>
/// <para>
/// The scene is drawn into an offscreen multisampled floating-point buffer at the control's pixel size, lit in linear
/// light (a sun with GGX highlights, a sky and ground ambient, light through translucent materials), tone-mapped over a
/// background gradient and drawn into the window like an image. It redraws only when the scene, the camera or its
/// size changes: changing a mesh (<see cref="Mesh3D.UpdatePositions"/> every frame for animation) redraws it.
/// </para>
/// <para>
/// The navigation follows Blender: middle-drag orbits, Shift+middle-drag pans, Ctrl+middle-drag or the wheel zooms
/// (towards the pointer), Alt+drag orbits without a middle button; Home frames everything; numpad 1, 3 and 7 look from
/// the front, right and top (with Ctrl from the opposite side), numpad 2, 4, 6 and 8 orbit in steps, numpad 5 toggles
/// orthographic and Shift+Z wireframe. They are commands of the <see cref="CommandGroup"/> keybinding group (see
/// <see cref="Viewport3DCommands"/>), so users can rebind them.
/// </para>
/// <para>
/// Without a GPU context (rendering without a window, as in tests and snapshots, or OpenGL older than 3.3) the control
/// draws a placeholder and <see cref="IsGpuAvailable"/> is <c>false</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var viewport = new Viewport3D().Scene(scene).Shading(ViewportShading.Shaded);
/// viewport.FrameAll();
/// </code>
/// </example>
public partial class Viewport3D : KeybindingHandler
{
    /// <summary>The keybinding group of the viewport's commands.</summary>
    public const string CommandGroup = "Viewport3D";

    /// <summary>Identifies the <see cref="Scene"/> property.</summary>
    public static readonly BindableProperty<Scene3D?> SceneProperty =
        BindableProperty.Register<Viewport3D, Scene3D?>(nameof(Scene), null, (v, o, n) => ((Viewport3D)v).OnSceneChanged(o, n));

    /// <summary>Identifies the <see cref="Shading"/> property.</summary>
    public static readonly BindableProperty<ViewportShading> ShadingProperty =
        BindableProperty.Register<Viewport3D, ViewportShading>(nameof(Shading), ViewportShading.Shaded, (v, _, _) => ((Viewport3D)v).RequestRender());

    /// <summary>Identifies the <see cref="ShowGrid"/> property.</summary>
    public static readonly BindableProperty<bool> ShowGridProperty =
        BindableProperty.Register<Viewport3D, bool>(nameof(ShowGrid), true, (v, _, _) => ((Viewport3D)v).RequestRender());

    /// <summary>Identifies the <see cref="GridHeight"/> property.</summary>
    public static readonly BindableProperty<float> GridHeightProperty =
        BindableProperty.Register<Viewport3D, float>(nameof(GridHeight), 0f, (v, _, _) => ((Viewport3D)v).RequestRender(), validateValue: float.IsFinite);

    /// <summary>Identifies the <see cref="ShowAxes"/> property.</summary>
    public static readonly BindableProperty<bool> ShowAxesProperty =
        BindableProperty.Register<Viewport3D, bool>(nameof(ShowAxes), true, (v, _, _) => ((Viewport3D)v).RequestRender());

    /// <summary>Identifies the <see cref="BackgroundTop"/> property.</summary>
    public static readonly BindableProperty<Color> BackgroundTopProperty =
        BindableProperty.Register<Viewport3D, Color>(nameof(BackgroundTop), Color.FromRgb(0x6B, 0x76, 0x84), (v, _, _) => ((Viewport3D)v).RequestRender());

    /// <summary>Identifies the <see cref="BackgroundBottom"/> property.</summary>
    public static readonly BindableProperty<Color> BackgroundBottomProperty =
        BindableProperty.Register<Viewport3D, Color>(nameof(BackgroundBottom), Color.FromRgb(0x2B, 0x2F, 0x35), (v, _, _) => ((Viewport3D)v).RequestRender());

    /// <summary>Identifies the <see cref="WireframeColor"/> property.</summary>
    public static readonly BindableProperty<Color> WireframeColorProperty =
        BindableProperty.Register<Viewport3D, Color>(nameof(WireframeColor), Color.FromRgb(0xC9, 0xCF, 0xD8), (v, _, _) => ((Viewport3D)v).RequestRender());

    /// <summary>Identifies the <see cref="Samples"/> property.</summary>
    public static readonly BindableProperty<int> SamplesProperty =
        BindableProperty.Register<Viewport3D, int>(nameof(Samples), 4, (v, _, _) => ((Viewport3D)v).RequestRender(),
            validateValue: s => s >= 1 && s <= 32);

    private readonly ViewportSurface _surface;
    private Scene3D? _observedScene;
    private bool _isAttached;

    static Viewport3D()
    {
        Graphics3DTheme.Register();
        IsFocusableProperty.OverrideDefaultValue<Viewport3D>(true);
        ClipToBoundsProperty.OverrideDefaultValue<Viewport3D>(true);
    }

    /// <summary>Initializes a viewport looking at the origin from the front, slightly from the right and above.</summary>
    public Viewport3D()
    {
        Viewport3DCommands.Register();
        AdditionalScopes.Add(new KeybindingScope(CommandGroup, this));
        Camera = new OrbitCamera { Yaw = 0.6f, Pitch = 0.4f };
        Camera.Changed += (_, _) => RequestRender();
        _surface = new ViewportSurface(this);
        Content = _surface;
    }

    /// <summary>Gets or sets the scene shown, or <c>null</c> for an empty view.</summary>
    public Scene3D? Scene { get => GetValue(SceneProperty); set => SetValue(SceneProperty, value); }

    /// <summary>Gets the camera the scene is seen through.</summary>
    public OrbitCamera Camera { get; }

    /// <summary>Gets or sets how surfaces are drawn. The default is <see cref="ViewportShading.Shaded"/>.</summary>
    public ViewportShading Shading { get => GetValue(ShadingProperty); set => SetValue(ShadingProperty, value); }

    /// <summary>Gets or sets whether the ground grid (at <see cref="GridHeight"/>, with the X axis in red and the Z axis in blue) is drawn. The default is <c>true</c>.</summary>
    public bool ShowGrid { get => GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }

    /// <summary>
    /// Gets or sets the height (y) of the ground grid. The default is 0. Moving it lets the grid stay under a subject that
    /// travels (for example below a simulated aircraft) while its lines stay fixed in the world, showing the motion.
    /// </summary>
    public float GridHeight { get => GetValue(GridHeightProperty); set => SetValue(GridHeightProperty, value); }

    /// <summary>Gets or sets whether the axis gizmo is drawn in the bottom left corner. The default is <c>true</c>.</summary>
    public bool ShowAxes { get => GetValue(ShowAxesProperty); set => SetValue(ShowAxesProperty, value); }

    /// <summary>Gets or sets the background color at the top (sRGB).</summary>
    public Color BackgroundTop { get => GetValue(BackgroundTopProperty); set => SetValue(BackgroundTopProperty, value); }

    /// <summary>Gets or sets the background color at the bottom (sRGB).</summary>
    public Color BackgroundBottom { get => GetValue(BackgroundBottomProperty); set => SetValue(BackgroundBottomProperty, value); }

    /// <summary>
    /// Gets or sets the color of the edges in <see cref="ViewportShading.Wireframe"/> mode (light gray by default). Over shaded
    /// surfaces (<see cref="ViewportShading.ShadedWireframe"/>) edges are drawn in translucent black instead.
    /// </summary>
    public Color WireframeColor { get => GetValue(WireframeColorProperty); set => SetValue(WireframeColorProperty, value); }

    /// <summary>Gets or sets the number of multisamples per pixel for anti-aliasing (1 to 32, limited by the GPU). The default is 4.</summary>
    public int Samples { get => GetValue(SamplesProperty); set => SetValue(SamplesProperty, value); }

    /// <summary>
    /// Gets whether the viewport can render with the GPU: it is in a window with an OpenGL 3.3 context. Before the first
    /// frame, whether the window offers a context.
    /// </summary>
    public bool IsGpuAvailable => _surface.LastFrameUsedGpu ?? Host?.GraphicsDevice?.IsAlive == true;

    /// <summary>
    /// Gets where the pointer is over the viewport, in its coordinates, or <c>null</c> when it isn't over it. Zooming
    /// towards the pointer uses it.
    /// </summary>
    public Point? PointerPosition { get; private set; }

    /// <summary>Gets the size of the viewport's image in device pixels, as of the last frame.</summary>
    public (int Width, int Height) PixelSize => _surface.PixelSize;

    /// <summary>Occurs after a frame was rendered with the GPU.</summary>
    public event EventHandler? Rendered;

    /// <summary>Points the camera at the visible instances so they fill the view (keeps the direction it looks from).</summary>
    public void FrameAll()
    {
        if (Scene?.GetBounds() is { } bounds) Camera.Frame(bounds.Min, bounds.Max);
        else Camera.Frame(new(-1, -1, -1), new(1, 1, 1));
    }

    /// <summary>
    /// Asks for a new frame. Changes to the scene and the camera do this already; call it after changing something the
    /// viewport can't observe.
    /// </summary>
    public void RequestRender() => _surface.InvalidateVisual();

    internal void RaiseRendered() => Rendered?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc/>
    /// <remarks>Takes the focus (for the viewport's shortcuts) unless an element inside takes it.</remarks>
    public override void OnPreviewPointerPressed(PointerEventArgs e)
    {
        PointerPosition = e.Position;
        base.OnPreviewPointerPressed(e);
        if (!IsFocused) Focus();
    }

    /// <inheritdoc/>
    public override void OnPreviewPointerMoved(PointerEventArgs e)
    {
        PointerPosition = e.Position;
        base.OnPreviewPointerMoved(e);
    }

    /// <inheritdoc/>
    public override void OnPreviewPointerWheel(PointerWheelEventArgs e)
    {
        PointerPosition = e.Position;
        base.OnPreviewPointerWheel(e);
    }

    /// <inheritdoc/>
    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsPointerCaptured) PointerPosition = null;
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        _isAttached = true;
        Observe(Scene);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree()
    {
        _isAttached = false;
        Observe(null);
        // The GPU targets are released at the next frame of their window (when its context is current); coming back
        // before then reuses them.
        _surface.ReleaseTargets();
        base.OnDetachedFromVisualTree();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        // A viewport takes the space it is given; without a limit it asks for a modest default.
        return new Size(
            float.IsFinite(availableSize.Width) ? availableSize.Width : 320,
            float.IsFinite(availableSize.Height) ? availableSize.Height : 240);
    }

    private void OnSceneChanged(Scene3D? oldScene, Scene3D? newScene)
    {
        if (_isAttached) Observe(newScene);
        RequestRender();
    }

    private void Observe(Scene3D? scene)
    {
        if (ReferenceEquals(_observedScene, scene)) return;
        if (_observedScene != null) _observedScene.Changed -= OnSceneContentChanged;
        _observedScene = scene;
        if (scene != null) scene.Changed += OnSceneContentChanged;
    }

    private void OnSceneContentChanged(object? sender, EventArgs e) => RequestRender();
}

/// <summary>
/// The part of a <see cref="Viewport3D"/> that shows the rendered image (drawn by the viewport renderer, which renders
/// the scene with the GPU first).
/// </summary>
public sealed class ViewportSurface : Control
{
    internal ViewportSurface(Viewport3D viewport)
    {
        Viewport = viewport;
    }

    /// <summary>Gets the viewport the surface belongs to.</summary>
    public Viewport3D Viewport { get; }

    internal ViewportTargets? Targets { get; set; }
    internal SceneRenderer SceneRenderer { get; } = new();
    internal bool? LastFrameUsedGpu { get; set; }
    internal (int Width, int Height) PixelSize { get; set; }

    internal void ReleaseTargets()
    {
        if (Targets is { } targets) targets.Device.QueueRelease(targets);
    }
}
