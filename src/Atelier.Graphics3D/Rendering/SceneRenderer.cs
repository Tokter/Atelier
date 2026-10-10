using System.Numerics;
using Atelier.Core.Primitives;
using Silk.NET.OpenGL;

namespace Atelier.Graphics3D.Rendering;

/// <summary>What a frame of a viewport draws, besides the scene.</summary>
internal readonly record struct FrameSettings(
    ViewportShading Shading,
    bool ShowGrid,
    float GridHeight,
    Color BackgroundTop,
    Color BackgroundBottom,
    Color WireframeColor);

/// <summary>
/// Draws a <see cref="Scene3D"/> into a viewport's targets: opaque instances, the grid, transparent instances (back to
/// front), wireframe overlays and on-top instances into the multisampled buffer; then resolves it and tone-maps it
/// over the background into the output texture.
/// </summary>
internal sealed class SceneRenderer
{
    private (float Distance, MeshInstance3D Instance)[] _transparent = new (float, MeshInstance3D)[16];

    public unsafe void Render(GpuDevice gpu, ViewportTargets targets, Scene3D? scene, OrbitCamera camera, in FrameSettings settings)
    {
        var gl = gpu.Gl;
        uint width = (uint)targets.Width, height = (uint)targets.Height;
        float aspect = (float)targets.Width / targets.Height;
        var view = camera.GetView();
        var projection = camera.GetProjection(aspect);
        var viewProjection = view * projection;

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, targets.MsaaFramebuffer);
        gl.Viewport(0, 0, width, height);
        gl.Disable(EnableCap.ScissorTest);
        gl.Disable(EnableCap.StencilTest);
        gl.Disable(EnableCap.FramebufferSrgb);
        gl.Disable(EnableCap.CullFace);
        gl.Disable(EnableCap.PolygonOffsetFill);
        gl.Enable(EnableCap.Multisample);
        gl.Enable(EnableCap.ProgramPointSize);
        gl.ColorMask(true, true, true, true);
        gl.DepthMask(true);
        gl.FrontFace(FrontFaceDirection.Ccw);
        gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        gl.ClearColor(0, 0, 0, 0);
        gl.ClearDepth(1);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(DepthFunction.Less);
        gl.Enable(EnableCap.Blend);
        gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        gl.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        // Skia, sharing the context, leaves its sampler objects bound to the texture units; a bound sampler overrides
        // the textures' own filtering (linear, mipmapped), and Skia's often sample nearest.
        gl.BindSampler(0, 0);
        gl.BindSampler(1, 0);

        var program = gpu.MeshProgram;
        program.Use();
        program.SetViewProjection(viewProjection);
        var cameraPosition = camera.Position;
        var backward = camera.Backward;
        gl.Uniform3(program.CameraPos, cameraPosition.X, cameraPosition.Y, cameraPosition.Z);
        gl.Uniform3(program.ViewDir, backward.X, backward.Y, backward.Z);
        gl.Uniform1(program.Orthographic, camera.Orthographic ? 1 : 0);
        gl.Uniform1(program.Override, 0);
        if (scene != null)
        {
            var sun = scene.SunDirection;
            var sunRadiance = ToLinear(scene.SunColor) * scene.SunIntensity * MathF.PI;
            var sky = ToLinear(scene.SkyColor);
            var ground = ToLinear(scene.GroundColor);
            gl.Uniform3(program.SunDir, sun.X, sun.Y, sun.Z);
            gl.Uniform3(program.SunRadiance, sunRadiance.X, sunRadiance.Y, sunRadiance.Z);
            gl.Uniform3(program.SkyColor, sky.X, sky.Y, sky.Z);
            gl.Uniform3(program.GroundColor, ground.X, ground.Y, ground.Z);
        }

        bool fill = settings.Shading != ViewportShading.Wireframe;
        bool wire = settings.Shading != ViewportShading.Shaded;
        int transparentCount = 0;

        if (scene != null)
        {
            // Opaque instances first; with a wireframe overlay the fill is pushed back so the lines win the depth test.
            if (wire && fill)
            {
                gl.Enable(EnableCap.PolygonOffsetFill);
                gl.PolygonOffset(1f, 1f);
            }
            var instances = scene.Instances;
            for (int index = 0; index < instances.Count; index++)
            {
                var instance = instances[index];
                if (!instance.IsVisible || instance.DrawOnTop || instance.Mesh.IsEmpty) continue;
                if (!fill && instance.Mesh.Topology == PrimitiveTopology.Triangles) continue;
                if (instance.Material.BaseColor.A < 255)
                {
                    if (transparentCount == _transparent.Length) Array.Resize(ref _transparent, _transparent.Length * 2);
                    var center = instance.GetBounds() is { } b ? (b.Min + b.Max) / 2 : Vector3.Zero;
                    _transparent[transparentCount++] = (Vector3.DistanceSquared(center, cameraPosition), instance);
                    continue;
                }
                DrawInstance(gpu, instance);
            }
        }

        if (settings.ShowGrid) DrawGrid(gpu, camera, projection, settings.BackgroundBottom, settings.GridHeight);

        if (transparentCount > 0)
        {
            // Back to front, without writing depth, so overlapping see-through surfaces blend.
            Array.Sort(_transparent, 0, transparentCount, FarthestFirst.Instance);
            program.Use();
            gl.DepthMask(false);
            for (int i = 0; i < transparentCount; i++)
            {
                DrawInstance(gpu, _transparent[i].Instance);
                _transparent[i] = default;
            }
            gl.DepthMask(true);
        }
        gl.Disable(EnableCap.PolygonOffsetFill);

        if (wire && scene != null)
        {
            program.Use();
            var color = fill ? Color.FromRgb(0x08, 0x08, 0x0A) : settings.WireframeColor;
            float alpha = fill ? 0.35f : color.A / 255f;
            var linear = ToLinear(color);
            gl.Uniform1(program.Override, 1);
            gl.Uniform4(program.OverrideColor, linear.X, linear.Y, linear.Z, alpha);
            gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
            gl.DepthFunc(DepthFunction.Lequal);
            gl.DepthMask(false);
            if (!fill) gl.Disable(EnableCap.DepthTest);
            var instances = scene.Instances;
            for (int index = 0; index < instances.Count; index++)
            {
                var instance = instances[index];
                if (!instance.IsVisible || instance.DrawOnTop || instance.Mesh.IsEmpty || instance.Mesh.Topology != PrimitiveTopology.Triangles) continue;
                DrawInstance(gpu, instance);
            }
            gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
            gl.Uniform1(program.Override, 0);
            gl.Enable(EnableCap.DepthTest);
            gl.DepthFunc(DepthFunction.Less);
            gl.DepthMask(true);
        }

        if (scene != null)
        {
            // Helpers drawn over everything.
            program.Use();
            gl.Disable(EnableCap.DepthTest);
            var instances = scene.Instances;
            for (int index = 0; index < instances.Count; index++)
            {
                var instance = instances[index];
                if (instance.IsVisible && instance.DrawOnTop && !instance.Mesh.IsEmpty) DrawInstance(gpu, instance);
            }
            gl.Enable(EnableCap.DepthTest);
        }

        // Resolve the samples, then tone-map over the background into the 8-bit output.
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, targets.MsaaFramebuffer);
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, targets.ResolveFramebuffer);
        gl.BlitFramebuffer(0, 0, (int)width, (int)height, 0, 0, (int)width, (int)height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, targets.OutputFramebuffer);
        gl.Viewport(0, 0, width, height);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.Blend);
        var composite = gpu.CompositeProgram;
        composite.Use();
        var top = ToSrgbVector(settings.BackgroundTop);
        var bottom = ToSrgbVector(settings.BackgroundBottom);
        gl.Uniform3(composite.BackgroundTop, top.X, top.Y, top.Z);
        gl.Uniform3(composite.BackgroundBottom, bottom.X, bottom.Y, bottom.Z);
        gl.Uniform1(composite.Exposure, scene?.Exposure ?? 1f);
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, targets.ResolveTexture);
        gl.BindVertexArray(gpu.EmptyVao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 3);

        // Leave neutral state behind; the 2D renderer resets its own afterwards.
        gl.BindVertexArray(0);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.UseProgram(0);
        gl.Disable(EnableCap.ProgramPointSize);
        gl.Disable(EnableCap.CullFace);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private static unsafe void DrawInstance(GpuDevice gpu, MeshInstance3D instance)
    {
        var gl = gpu.Gl;
        var program = gpu.MeshProgram;
        var mesh = instance.Mesh;
        var material = instance.Material;
        var gpuMesh = gpu.GetMesh(mesh);

        program.SetModel(instance.Transform);
        var baseColor = ToLinear(material.BaseColor);
        gl.Uniform4(program.BaseColor, baseColor.X, baseColor.Y, baseColor.Z, material.BaseColor.A / 255f);
        gl.Uniform1(program.Roughness, material.Roughness);
        gl.Uniform1(program.Metallic, material.Metallic);
        gl.Uniform1(program.Transmission, material.Transmission);
        gl.Uniform1(program.NormalScale, material.NormalScale);
        gl.Uniform1(program.Unlit, material.Unlit ? 1 : 0);
        gl.Uniform1(program.HasNormals, gpuMesh.HasNormals ? 1 : 0);
        gl.Uniform1(program.PointSize, material.PointSize);
        gl.Uniform1(program.RoundPoints, mesh.Topology == PrimitiveTopology.Points ? 1 : 0);

        bool baseTexture = material.BaseColorTexture != null && gpuMesh.HasTexCoords;
        gl.Uniform1(program.HasBaseColorTexture, baseTexture ? 1 : 0);
        if (baseTexture)
        {
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, gpu.GetTexture(material.BaseColorTexture!));
        }
        bool normalTexture = material.NormalTexture != null && gpuMesh.HasTexCoords && gpuMesh.HasNormals;
        gl.Uniform1(program.HasNormalTexture, normalTexture ? 1 : 0);
        if (normalTexture)
        {
            gl.ActiveTexture(TextureUnit.Texture1);
            gl.BindTexture(TextureTarget.Texture2D, gpu.GetTexture(material.NormalTexture!));
            gl.ActiveTexture(TextureUnit.Texture0);
        }

        bool cull = !material.DoubleSided && mesh.Topology == PrimitiveTopology.Triangles;
        if (cull)
        {
            gl.Enable(EnableCap.CullFace);
            gl.CullFace(TriangleFace.Back);
        }

        gl.BindVertexArray(gpuMesh.Vao);
        // Attributes the mesh lacks read these constants.
        if (!gpuMesh.HasNormals) gl.VertexAttrib3(1, 0f, 1f, 0f);
        if (!gpuMesh.HasTexCoords) gl.VertexAttrib2(2, 0f, 0f);
        if (!gpuMesh.HasColors) gl.VertexAttrib4(3, 1f, 1f, 1f, 1f);
        var mode = mesh.Topology switch
        {
            PrimitiveTopology.Lines => PrimitiveType.Lines,
            PrimitiveTopology.Points => PrimitiveType.Points,
            _ => PrimitiveType.Triangles,
        };
        gl.DrawElements(mode, gpuMesh.IndexCount, DrawElementsType.UnsignedInt, (void*)0);
        gl.BindVertexArray(0);

        if (cull) gl.Disable(EnableCap.CullFace);
    }

    private static void DrawGrid(GpuDevice gpu, OrbitCamera camera, in Matrix4x4 projection, Color background, float height)
    {
        var gl = gpu.Gl;
        // Camera-relative matrices (the view without its translation): far from the origin, world coordinates in the
        // shader would lose the precision the line anti-aliasing needs, and the lines would turn jagged.
        var position = camera.Position;
        var relativeView = Matrix4x4.CreateLookAt(Vector3.Zero, camera.Target - position, camera.Up);
        var viewProjection = relativeView * projection;
        if (!Matrix4x4.Invert(viewProjection, out var inverse)) return;
        var grid = gpu.GridProgram;
        grid.Use();
        grid.SetMatrices(viewProjection, inverse);
        gl.Uniform3(grid.CameraPos, position.X, position.Y, position.Z);
        // Lines every power of ten that suits the zoom: 1 m minor lines from about 7 m away.
        float cell = MathF.Pow(10, MathF.Floor(MathF.Log10(Math.Max(camera.Distance * 0.15f, 1e-3f))));
        // The camera's place within a major cell (computed in double precision): the shader adds it to camera-relative
        // positions to find the lines.
        double major = cell * 10.0;
        gl.Uniform2(grid.GridOffset,
            (float)(position.X - Math.Floor(position.X / major) * major),
            (float)(position.Z - Math.Floor(position.Z / major) * major));
        gl.Uniform1(grid.CellSize, cell);
        gl.Uniform1(grid.FadeDistance, Math.Max(camera.Distance * 6f, cell * 40f));
        bool dark = Luminance(background) < 0.45f;
        var line = dark ? new Vector3(0.55f) : new Vector3(0.12f);
        gl.Uniform3(grid.LineColor, line.X, line.Y, line.Z);
        gl.Uniform1(grid.LineAlpha, dark ? 0.55f : 0.45f);
        gl.Uniform1(grid.GridHeight, height);
        gl.DepthMask(false);
        gl.DepthFunc(DepthFunction.Lequal);
        gl.BindVertexArray(gpu.EmptyVao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        gl.BindVertexArray(0);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
    }

    /// <summary>Converts an sRGB color to linear RGB.</summary>
    public static Vector3 ToLinear(Color color) => new(SrgbToLinear(color.R), SrgbToLinear(color.G), SrgbToLinear(color.B));

    private static Vector3 ToSrgbVector(Color color) => new(color.R / 255f, color.G / 255f, color.B / 255f);

    private static float SrgbToLinear(byte value)
    {
        float c = value / 255f;
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>Gets the relative luminance of an sRGB color, 0 to 1.</summary>
    public static float Luminance(Color color)
    {
        var linear = ToLinear(color);
        return 0.2126f * linear.X + 0.7152f * linear.Y + 0.0722f * linear.Z;
    }

    private sealed class FarthestFirst : IComparer<(float Distance, MeshInstance3D Instance)>
    {
        public static readonly FarthestFirst Instance = new();

        public int Compare((float Distance, MeshInstance3D Instance) x, (float Distance, MeshInstance3D Instance) y) =>
            y.Distance.CompareTo(x.Distance);
    }
}
