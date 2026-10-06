using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;

namespace Atelier.Graphics3D;

/// <summary>
/// The 3D viewport's navigation commands, registered in the <see cref="Viewport3D.CommandGroup"/> keybinding group with
/// the viewport as their target: users can rebind them in the keybinding editor, and the command palette lists them
/// while the focus is in a viewport. The defaults follow Blender.
/// </summary>
public static class Viewport3DCommands
{
    /// <summary>How far the camera turns per pixel dragged, in radians.</summary>
    public const float OrbitSpeed = 0.008f;

    /// <summary>How much one step of <see cref="ZoomIn"/> or <see cref="ZoomOut"/> zooms.</summary>
    public const float ZoomStep = 1.15f;

    /// <summary>How far the numpad orbit commands turn, in radians (15°).</summary>
    public const float OrbitStep = MathF.PI / 12;

    /// <summary>Gets the command that orbits the camera around its target by dragging (middle drag).</summary>
    public static OrbitCommand Orbit { get; } = new();

    /// <summary>Gets the command that moves the view by dragging (Shift+middle drag).</summary>
    public static PanCommand Pan { get; } = new();

    /// <summary>Gets the command that moves the camera closer or farther by dragging up or down (Ctrl+middle drag).</summary>
    public static DollyCommand Dolly { get; } = new();

    /// <summary>Gets the command that zooms in towards the pointer (wheel up).</summary>
    public static ZoomCommand ZoomIn { get; } = new(ZoomStep);

    /// <summary>Gets the command that zooms out away from the pointer (wheel down).</summary>
    public static ZoomCommand ZoomOut { get; } = new(1 / ZoomStep);

    /// <summary>Gets the command that frames all visible instances (Home).</summary>
    public static Viewport3DCommand FrameAll { get; } = new(v => v.FrameAll());

    /// <summary>Gets the command that looks from the front, along −Z (numpad 1).</summary>
    public static Viewport3DCommand ViewFront { get; } = new(v => v.Camera.SetAngles(0, 0));

    /// <summary>Gets the command that looks from the back, along +Z (Ctrl+numpad 1).</summary>
    public static Viewport3DCommand ViewBack { get; } = new(v => v.Camera.SetAngles(MathF.PI, 0));

    /// <summary>Gets the command that looks from the right, along −X (numpad 3).</summary>
    public static Viewport3DCommand ViewRight { get; } = new(v => v.Camera.SetAngles(MathF.PI / 2, 0));

    /// <summary>Gets the command that looks from the left, along +X (Ctrl+numpad 3).</summary>
    public static Viewport3DCommand ViewLeft { get; } = new(v => v.Camera.SetAngles(-MathF.PI / 2, 0));

    /// <summary>Gets the command that looks from above (numpad 7).</summary>
    public static Viewport3DCommand ViewTop { get; } = new(v => v.Camera.SetAngles(0, OrbitCamera.MaxPitch));

    /// <summary>Gets the command that looks from below (Ctrl+numpad 7).</summary>
    public static Viewport3DCommand ViewBottom { get; } = new(v => v.Camera.SetAngles(0, -OrbitCamera.MaxPitch));

    /// <summary>Gets the command that orbits left by <see cref="OrbitStep"/> (numpad 4).</summary>
    public static Viewport3DCommand OrbitLeft { get; } = new(v => v.Camera.SetAngles(v.Camera.Yaw + OrbitStep, v.Camera.Pitch));

    /// <summary>Gets the command that orbits right by <see cref="OrbitStep"/> (numpad 6).</summary>
    public static Viewport3DCommand OrbitRight { get; } = new(v => v.Camera.SetAngles(v.Camera.Yaw - OrbitStep, v.Camera.Pitch));

    /// <summary>Gets the command that orbits up by <see cref="OrbitStep"/> (numpad 8).</summary>
    public static Viewport3DCommand OrbitUp { get; } = new(v => v.Camera.SetAngles(v.Camera.Yaw, v.Camera.Pitch - OrbitStep));

    /// <summary>Gets the command that orbits down by <see cref="OrbitStep"/> (numpad 2).</summary>
    public static Viewport3DCommand OrbitDown { get; } = new(v => v.Camera.SetAngles(v.Camera.Yaw, v.Camera.Pitch + OrbitStep));

    /// <summary>Gets the command that switches between perspective and orthographic projection (numpad 5).</summary>
    public static Viewport3DCommand ToggleOrthographic { get; } = new(v => v.Camera.Orthographic = !v.Camera.Orthographic);

    /// <summary>Gets the command that switches between shaded and wireframe drawing (Shift+Z).</summary>
    public static Viewport3DCommand ToggleWireframe { get; } = new(v =>
        v.Shading = v.Shading == ViewportShading.Wireframe ? ViewportShading.Shaded : ViewportShading.Wireframe);

    /// <summary>
    /// Registers the commands that aren't registered yet (with the users' changes applied). Viewports call it when
    /// they're created.
    /// </summary>
    public static void Register()
    {
        Add("Orbit", "MiddleDrag", Orbit, "Orbit", "Turn the view around its center by dragging", MaterialIcons.ViewInAr);
        Add("OrbitAlt", "Alt+LeftDrag", Orbit, "Orbit (Alt)", "Turn the view around its center by dragging, without a middle button", MaterialIcons.ViewInAr);
        Add("Pan", "Shift+MiddleDrag", Pan, "Pan", "Move the view by dragging", MaterialIcons.PanTool);
        Add("PanAlt", "Alt+Shift+LeftDrag", Pan, "Pan (Alt)", "Move the view by dragging, without a middle button", MaterialIcons.PanTool);
        Add("Dolly", "Ctrl+MiddleDrag", Dolly, "Zoom by dragging", "Move closer or farther by dragging up or down", MaterialIcons.ZoomIn);
        Add("ZoomIn", "WheelUp", ZoomIn, "Zoom in", "Move closer, towards the pointer", MaterialIcons.ZoomIn);
        Add("ZoomOut", "WheelDown", ZoomOut, "Zoom out", "Move farther, away from the pointer", MaterialIcons.ZoomOut);
        Add("FrameAll", "Home", FrameAll, "_Frame all", "Point the camera at everything shown", MaterialIcons.FitScreen);
        Add("ViewFront", "NumPad1", ViewFront, "Front view", "Look from the front", MaterialIcons.Crop169);
        Add("ViewBack", "Ctrl+NumPad1", ViewBack, "Back view", "Look from the back", MaterialIcons.Crop169);
        Add("ViewRight", "NumPad3", ViewRight, "Right view", "Look from the right", MaterialIcons.Crop169);
        Add("ViewLeft", "Ctrl+NumPad3", ViewLeft, "Left view", "Look from the left", MaterialIcons.Crop169);
        Add("ViewTop", "NumPad7", ViewTop, "Top view", "Look from above", MaterialIcons.Crop169);
        Add("ViewBottom", "Ctrl+NumPad7", ViewBottom, "Bottom view", "Look from below", MaterialIcons.Crop169);
        Add("OrbitLeft", "NumPad4", OrbitLeft, "Orbit left", "Turn the view 15° to the left", MaterialIcons.RotateLeft);
        Add("OrbitRight", "NumPad6", OrbitRight, "Orbit right", "Turn the view 15° to the right", MaterialIcons.RotateRight);
        Add("OrbitUp", "NumPad8", OrbitUp, "Orbit up", "Tilt the view 15° up", MaterialIcons.ArrowUpward);
        Add("OrbitDown", "NumPad2", OrbitDown, "Orbit down", "Tilt the view 15° down", MaterialIcons.ArrowDownward);
        Add("ToggleOrthographic", "NumPad5", ToggleOrthographic, "Perspective / orthographic", "Switch between perspective and orthographic projection", MaterialIcons.Grid3x3);
        Add("ToggleWireframe", "Shift+Z", ToggleWireframe, "Wireframe", "Switch between shaded and wireframe drawing", MaterialIcons.Deblur);
    }

    private static void Add(string name, string keybinding, AtelierCommand command, string label, string description, string icon)
    {
        foreach (var registered in KeybindingManager.GetKeybindings(Viewport3D.CommandGroup))
        {
            if (registered.Name == name) return;
        }
        KeybindingManager.RegisterKeybinding(new KeybindingDescriptor(name, Viewport3D.CommandGroup, keybinding, command,
            label: label, description: description, icon: icon));
    }
}

/// <summary>A command that runs on a <see cref="Viewport3D"/> (its target).</summary>
/// <param name="execute">What the command does.</param>
public sealed class Viewport3DCommand(Action<Viewport3D> execute) : AtelierCommand
{
    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is Viewport3D;

    /// <inheritdoc/>
    public override void Execute(object? parameter)
    {
        if (parameter is Viewport3D viewport) execute(viewport);
    }
}

/// <summary>Orbits a <see cref="Viewport3D"/>'s camera by dragging, like turning the scene by hand; Escape turns it back.</summary>
public sealed class OrbitCommand : DragCommand
{
    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is Viewport3D;

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start) =>
        start.Target is Viewport3D viewport ? new Operation(viewport.Camera, start.ScreenPosition) : null;

    private sealed class Operation(OrbitCamera camera, Point start) : IDragOperation
    {
        private readonly float _yaw = camera.Yaw;
        private readonly float _pitch = camera.Pitch;

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            var delta = screenPosition - start;
            // Dragging right turns the scene right (the camera left); dragging down shows more of the top.
            camera.SetAngles(_yaw - delta.X * Viewport3DCommands.OrbitSpeed, _pitch + delta.Y * Viewport3DCommands.OrbitSpeed);
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers) => Update(screenPosition, modifiers);

        public void Cancel() => camera.SetAngles(_yaw, _pitch);
    }
}

/// <summary>Pans a <see cref="Viewport3D"/> by dragging: the point under the pointer follows it; Escape moves back.</summary>
public sealed class PanCommand : DragCommand
{
    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is Viewport3D;

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start) =>
        start.Target is Viewport3D viewport ? new Operation(viewport, start.ScreenPosition) : null;

    private sealed class Operation(Viewport3D viewport, Point start) : IDragOperation
    {
        private readonly System.Numerics.Vector3 _target = viewport.Camera.Target;
        private readonly float _unitsPerPixel = viewport.Camera.UnitsPerPixel(viewport.Bounds.Height);

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            var delta = screenPosition - start;
            var camera = viewport.Camera;
            camera.Target = _target;
            camera.Pan(-delta.X * _unitsPerPixel, delta.Y * _unitsPerPixel);
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers) => Update(screenPosition, modifiers);

        public void Cancel() => viewport.Camera.Target = _target;
    }
}

/// <summary>Moves a <see cref="Viewport3D"/>'s camera closer (drag up) or farther (drag down); Escape moves it back.</summary>
public sealed class DollyCommand : DragCommand
{
    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is Viewport3D;

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start) =>
        start.Target is Viewport3D viewport ? new Operation(viewport.Camera, start.ScreenPosition) : null;

    private sealed class Operation(OrbitCamera camera, Point start) : IDragOperation
    {
        private readonly float _distance = camera.Distance;

        public void Update(Point screenPosition, ModifierKeys modifiers) =>
            camera.Distance = _distance * MathF.Exp((screenPosition.Y - start.Y) * 0.01f);

        public void Complete(Point screenPosition, ModifierKeys modifiers) => Update(screenPosition, modifiers);

        public void Cancel() => camera.Distance = _distance;
    }
}

/// <summary>
/// Zooms a <see cref="Viewport3D"/> by a factor: the camera moves towards the point under the pointer (or the center
/// when the pointer isn't over the viewport), which stays under the pointer.
/// </summary>
/// <param name="factor">How much closer the camera gets (above 1 zooms in).</param>
public sealed class ZoomCommand(float factor) : AtelierCommand
{
    /// <summary>Gets the factor the distance is divided by.</summary>
    public float Factor { get; } = factor;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is Viewport3D;

    /// <inheritdoc/>
    public override void Execute(object? parameter)
    {
        if (parameter is not Viewport3D viewport) return;
        var camera = viewport.Camera;
        float height = viewport.Bounds.Height;
        if (viewport.PointerPosition is { } pointer && height > 0)
        {
            // The point under the pointer on the plane through the target stays put: the target moves towards it by the
            // share of the distance the zoom takes away.
            float unitsPerPixel = camera.UnitsPerPixel(height);
            float right = (pointer.X - viewport.Bounds.Width / 2) * unitsPerPixel;
            float up = -(pointer.Y - height / 2) * unitsPerPixel;
            float share = 1 - 1 / Factor;
            camera.Pan(right * share, up * share);
        }
        camera.Distance /= Factor;
    }
}
