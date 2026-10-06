using System.Numerics;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Graphics3D;
using Atelier.Rendering;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Tests;

public class OrbitCameraTests
{
    [Fact]
    public void TheDefaultView_LooksAlongMinusZ_FromTheFront()
    {
        var camera = new OrbitCamera { Target = new Vector3(1, 2, 3), Distance = 5 };

        Assert.Equal(new Vector3(1, 2, 8), camera.Position);
        Assert.Equal(Vector3.UnitX, camera.Right);
        AssertClose(Vector3.UnitY, camera.Up);
        // The target is straight ahead: in view space it sits on the −Z axis, Distance away.
        var inView = Vector3.Transform(camera.Target, camera.GetView());
        AssertClose(new Vector3(0, 0, -5), inView);
    }

    [Fact]
    public void PositiveYawAndPitch_MoveTheCameraTowardsPlusX_AndAbove()
    {
        var camera = new OrbitCamera { Distance = 10 };
        camera.SetAngles(MathF.PI / 2, 0);
        AssertClose(new Vector3(10, 0, 0), camera.Position);

        camera.SetAngles(0, MathF.PI / 4);
        Assert.True(camera.Position.Y > 7f);
        Assert.True(camera.Position.Z > 7f);
    }

    [Fact]
    public void Pitch_IsKeptShortOfStraightUpAndDown()
    {
        var camera = new OrbitCamera { Pitch = 10 };
        Assert.Equal(OrbitCamera.MaxPitch, camera.Pitch);
        camera.Pitch = -10;
        Assert.Equal(-OrbitCamera.MaxPitch, camera.Pitch);
        Assert.True(float.IsFinite(camera.GetView().M11));
    }

    [Fact]
    public void Frame_FitsTheBoxInTheView()
    {
        var camera = new OrbitCamera { Yaw = 0.7f, Pitch = 0.3f };
        var min = new Vector3(-3, 0, -1);
        var max = new Vector3(5, 2, 1);
        camera.Frame(min, max);

        Assert.Equal(new Vector3(1, 1, 0), camera.Target);
        var viewProjection = camera.GetViewProjection(1f);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
            var clip = Vector4.Transform(new Vector4(corner, 1), viewProjection);
            var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
            Assert.InRange(ndc.X, -1f, 1f);
            Assert.InRange(ndc.Y, -1f, 1f);
            Assert.InRange(ndc.Z, -1f, 1f);
        }
    }

    [Fact]
    public void Orthographic_KeepsTheSizeAtTheTarget()
    {
        var camera = new OrbitCamera { Distance = 10 };
        var edge = new Vector3(0, 10 * MathF.Tan(camera.FieldOfView / 2), 0); // the top edge of the view at the target
        float Project()
        {
            var clip = Vector4.Transform(new Vector4(edge, 1), camera.GetViewProjection(1.5f));
            return clip.Y / clip.W;
        }

        Assert.Equal(1f, Project(), 3);
        camera.Orthographic = true;
        Assert.Equal(1f, Project(), 3);
    }

    [Fact]
    public void Changes_RaiseChanged_OncePerChange()
    {
        var camera = new OrbitCamera();
        int changes = 0;
        camera.Changed += (_, _) => changes++;

        camera.Distance = 3;
        camera.Distance = 3;
        camera.SetAngles(1, 0.2f);
        camera.Pan(1, 0);

        Assert.Equal(3, changes);
    }

    private static void AssertClose(Vector3 expected, Vector3 actual)
    {
        Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"Expected {expected}, got {actual}");
    }
}

public class Mesh3DTests
{
    private static Mesh3D Quad()
    {
        var mesh = new Mesh3D();
        mesh.SetGeometry([new(0, 0, 0), new(2, 0, 0), new(2, 0, -1), new(0, 0, -1)], [0, 1, 2, 0, 2, 3]);
        return mesh;
    }

    [Fact]
    public void SetGeometry_ComputesSmoothNormalsAndBounds()
    {
        var mesh = Quad();

        Assert.All(mesh.Normals!, n => Assert.Equal(Vector3.UnitY, n));
        Assert.Equal((new Vector3(0, 0, -1), new Vector3(2, 0, 0)), mesh.Bounds);
        Assert.False(mesh.IsEmpty);
    }

    [Fact]
    public void ComputeNormals_WeightsFacesByArea()
    {
        // A big triangle facing +Y and a small one facing −X share vertex 0.
        Vector3[] positions = [new(0, 0, 0), new(10, 0, 0), new(0, 0, -10), new(0, 0, 0), new(0, 1, 0), new(0, 0, -1)];
        uint[] indices = [0, 1, 2, 0, 4, 5];
        var normals = Mesh3D.ComputeNormals(positions, indices);

        Assert.True(normals[0].Y > 0.98f);
        Assert.True(normals[0].X < 0f);
        Assert.Equal(1f, normals[0].Length(), 4);
    }

    [Fact]
    public void UpdatePositions_MovesTheVertices_AndRecomputesNormals()
    {
        var mesh = Quad();
        int geometryVersion = mesh.GeometryVersion;
        int changes = 0;
        mesh.Changed += (_, _) => changes++;

        // Tilt the quad: the far edge rises.
        mesh.UpdatePositions(new Vector3[] { new(0, 0, 0), new(2, 0, 0), new(2, 1, -1), new(0, 1, -1) });

        Assert.Equal(geometryVersion, mesh.GeometryVersion);
        Assert.Equal(1, changes);
        Assert.Equal(1f, mesh.Bounds.Max.Y);
        Assert.True(mesh.Normals![0].Z > 0.5f);
        Assert.Throws<ArgumentException>(() => mesh.UpdatePositions(new Vector3[3]));
    }

    [Fact]
    public void SetGeometry_RejectsBadIndicesAndArrays()
    {
        var mesh = new Mesh3D();
        Assert.Throws<ArgumentException>(() => mesh.SetGeometry([Vector3.Zero], [0, 1, 0]));
        Assert.Throws<ArgumentException>(() => mesh.SetGeometry([Vector3.Zero], [0], texCoords: []));
    }

    [Fact]
    public void LinesAndPoints_GetNoComputedNormals()
    {
        var lines = new Mesh3D(PrimitiveTopology.Lines);
        lines.SetGeometry([Vector3.Zero, Vector3.One], [0, 1]);
        Assert.Null(lines.Normals);
    }
}

public class Scene3DTests
{
    [Fact]
    public void ChangesToInstancesMeshesMaterialsAndTextures_RaiseChanged()
    {
        var scene = new Scene3D();
        var mesh = new Mesh3D();
        var material = new Material3D();
        var texture = new Texture3D(1, 1, [255, 255, 255, 255]);
        int changes = 0;
        scene.Changed += (_, _) => changes++;

        var instance = new MeshInstance3D(mesh, material);
        scene.Instances.Add(instance);
        Assert.Equal(1, changes);
        mesh.SetGeometry([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], [0, 1, 2]);
        Assert.Equal(2, changes);
        material.BaseColorTexture = texture;
        Assert.Equal(3, changes);
        texture.Update([0, 0, 0, 255]);
        Assert.Equal(4, changes);
        instance.Transform = Matrix4x4.CreateTranslation(1, 0, 0);
        Assert.Equal(5, changes);
        scene.SunDirection = new Vector3(0, 2, 0);
        Assert.Equal(Vector3.UnitY, scene.SunDirection);
        Assert.Equal(6, changes);

        scene.Instances.Remove(instance);
        Assert.Equal(7, changes);
        mesh.UpdatePositions(mesh.Positions);
        Assert.Equal(7, changes); // removed instances aren't observed any more
    }

    [Fact]
    public void GetBounds_CoversVisibleInstances_InSceneCoordinates()
    {
        var mesh = new Mesh3D();
        mesh.SetGeometry([new(-1, -1, -1), new(1, 1, 1), Vector3.Zero], [0, 1, 2]);
        var scene = new Scene3D();
        scene.Instances.Add(new MeshInstance3D(mesh, new Material3D()) { Transform = Matrix4x4.CreateTranslation(10, 0, 0) });
        scene.Instances.Add(new MeshInstance3D(mesh, new Material3D()) { IsVisible = false, Transform = Matrix4x4.CreateTranslation(-50, 0, 0) });

        Assert.Equal((new Vector3(9, -1, -1), new Vector3(11, 1, 1)), scene.GetBounds());
        Assert.Null(new Scene3D().GetBounds());
    }

    [Fact]
    public void Textures_CheckTheirPixelCount()
    {
        Assert.Throws<ArgumentException>(() => new Texture3D(2, 2, new byte[4]));
        var texture = new Texture3D(2, 1, new byte[8], srgb: false);
        Assert.False(texture.IsSrgb);
        Assert.Throws<ArgumentException>(() => texture.Update(new byte[4]));
    }
}

public class Viewport3DTests
{
    [Fact]
    public void WithoutAGpu_ItLaysOut_AndDrawsAPlaceholder()
    {
        using var _ = ActiveTheme.Use(MaterialTheme.CreateLight());
        var viewport = new Viewport3D { Scene = new Scene3D() };
        viewport.Measure(new Size(400, 300));
        viewport.Arrange(new Rect(0, 0, 400, 300));

        Assert.Equal(new Size(400, 300), viewport.DesiredSize);
        Assert.False(viewport.IsGpuAvailable);

        using var bitmap = new SkiaSharp.SKBitmap(400, 300);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        using var paints = new PaintRegistry();
        var context = new DrawingContext(canvas, paints);
        VisualTreeRenderer.Render(viewport, ref context, ThemeVisualPresenter.Instance);

        // The background gradient: lighter at the top than at the bottom.
        var top = bitmap.GetPixel(200, 2);
        var bottom = bitmap.GetPixel(200, 297);
        Assert.True(top.Red > bottom.Red);
        Assert.False(viewport.IsGpuAvailable);
    }

[Fact]    public void GridHeight_DefaultsToZero_AndRejectsNonFiniteValues()    {        var viewport = new Viewport3D().GridHeight(-12.5f);        Assert.Equal(-12.5f, viewport.GridHeight);        Assert.Equal(0f, new Viewport3D().GridHeight);        Assert.ThrowsAny<ArgumentException>(() => viewport.GridHeight = float.NaN);    }
    [Fact]
    public void FrameAll_PointsTheCameraAtTheScene()
    {
        var mesh = new Mesh3D();
        mesh.SetGeometry([new(10, 0, 0), new(12, 2, 0), new(10, 2, 0)], [0, 1, 2]);
        var scene = new Scene3D();
        scene.Instances.Add(new MeshInstance3D(mesh, new Material3D()));
        var viewport = new Viewport3D { Scene = scene };

        viewport.FrameAll();

        Assert.Equal(new Vector3(11, 1, 0), viewport.Camera.Target);
    }

    [Fact]
    public void TheNavigationCommands_AreRegistered_AndActOnTheViewport()
    {
        var viewport = new Viewport3D();
        viewport.Camera.SetAngles(1, 0.5f);

        Run(viewport, "ViewFront");
        Assert.Equal(0f, viewport.Camera.Yaw);
        Assert.Equal(0f, viewport.Camera.Pitch);
        Run(viewport, "ViewTop");
        Assert.Equal(OrbitCamera.MaxPitch, viewport.Camera.Pitch);
        Run(viewport, "ToggleOrthographic");
        Assert.True(viewport.Camera.Orthographic);
        Run(viewport, "ToggleWireframe");
        Assert.Equal(ViewportShading.Wireframe, viewport.Shading);

        Assert.Equal("MiddleDrag", KeybindingManager.FindCommand(Viewport3D.CommandGroup, "Orbit")!.Keybinding);
        Assert.Equal("NumPad1", KeybindingManager.FindCommand(Viewport3D.CommandGroup, "ViewFront")!.Keybinding);
    }

    [Fact]
    public void ZoomingAtThePointer_KeepsThePointUnderItInPlace()
    {
        var viewport = new Viewport3D();
        viewport.Measure(new Size(400, 300));
        viewport.Arrange(new Rect(0, 0, 400, 300));
        viewport.Camera.Distance = 10;
        // Over the right edge of the view, halfway up.
        viewport.OnPreviewPointerMoved(new PointerEventArgs(new Point(400, 150), new Point(400, 150)));
        float unitsPerPixel = viewport.Camera.UnitsPerPixel(300);
        var pointUnderPointer = viewport.Camera.Target + viewport.Camera.Right * 200 * unitsPerPixel;

        Run(viewport, "ZoomIn");

        Assert.Equal(10 / Viewport3DCommands.ZoomStep, viewport.Camera.Distance, 4);
        var afterUnitsPerPixel = viewport.Camera.UnitsPerPixel(300);
        var nowUnderPointer = viewport.Camera.Target + viewport.Camera.Right * 200 * afterUnitsPerPixel;
        Assert.True(Vector3.Distance(pointUnderPointer, nowUnderPointer) < 1e-4f);
    }

    [Fact]
    public void DraggingWithTheOrbitTool_TurnsTheCamera_AndEscapeTurnsItBack()
    {
        var viewport = new Viewport3D();
        var start = new DragStart(viewport, viewport, viewport, new Point(100, 100), PointerButtons.Middle, ModifierKeys.None);
        var drag = Viewport3DCommands.Orbit.BeginDrag(start)!;
        float yaw = viewport.Camera.Yaw, pitch = viewport.Camera.Pitch;

        drag.Update(new Point(150, 110), ModifierKeys.None);
        Assert.Equal(yaw - 50 * Viewport3DCommands.OrbitSpeed, viewport.Camera.Yaw, 5);
        Assert.Equal(pitch + 10 * Viewport3DCommands.OrbitSpeed, viewport.Camera.Pitch, 5);

        drag.Cancel();
        Assert.Equal(yaw, viewport.Camera.Yaw);
        Assert.Equal(pitch, viewport.Camera.Pitch);
    }

    private static void Run(Viewport3D viewport, string name) =>
        KeybindingManager.FindCommand(Viewport3D.CommandGroup, name)!.Command.Execute(viewport);
}
