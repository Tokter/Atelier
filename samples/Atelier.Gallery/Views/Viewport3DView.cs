using Stopwatch = System.Diagnostics.Stopwatch;
using System.Numerics;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Threading;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Graphics3D;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class Viewport3DView : GalleryPage
{
    private readonly Viewport3DViewModel _vm;
    private readonly Viewport3D _viewport;
    private readonly Scene3D _scene = new();
    private readonly Mesh3D _flag;
    private readonly Vector3[] _flagRest;
    private readonly Vector3[] _flagPositions;
    private readonly MeshInstance3D _lines;
    private readonly MeshInstance3D _points;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _animationPending;

    public Viewport3DView(Viewport3DViewModel viewModel)
        : base(MaterialIconKind.ViewInAr, "Viewport 3D",
            "A GPU-rendered 3D view: meshes with textures, normal maps, translucency, lines and points, lit by a sun and " +
            "the sky, with Blender-style navigation, a ground grid and an axis gizmo. It renders with OpenGL on the " +
            "window's context and only redraws when something changes.")
    {
        _vm = viewModel;

        // A ribbed, arched canopy: textured fabric that glows when the sun is behind it.
        var canopy = new MeshInstance3D(SampleMeshes.Canopy(), new Material3D
        {
            BaseColorTexture = SampleMeshes.FabricTexture(),
            NormalTexture = SampleMeshes.RipstopNormalMap(),
            NormalScale = 0.6f,
            Roughness = 0.55f,
            Transmission = 0.65f,
        }) { Name = "Canopy" };

        var knot = new MeshInstance3D(SampleMeshes.TorusKnot(), new Material3D
        {
            BaseColor = Color.FromHex("#E8B04A"),
            Metallic = 1f,
            Roughness = 0.28f,
        }) { Name = "Torus knot", Transform = Matrix4x4.CreateScale(0.55f) * Matrix4x4.CreateTranslation(5.2f, 1.2f, 0.5f) };

        (_flag, _flagRest) = SampleMeshes.Flag();
        _flagPositions = (Vector3[])_flagRest.Clone();
        var flag = new MeshInstance3D(_flag, new Material3D { Roughness = 0.8f, Transmission = 0.4f }) { Name = "Flag" };
        var pole = new MeshInstance3D(SampleMeshes.Cylinder(0.04f, 3.2f, 16), new Material3D { BaseColor = Color.FromHex("#B0B4BA"), Metallic = 1f, Roughness = 0.35f })
        {
            Name = "Flag pole",
            Transform = Matrix4x4.CreateTranslation(-5.5f, 0, -1),
        };

        var (lines, attachments) = SampleMeshes.SuspensionLines();
        _lines = new MeshInstance3D(lines, new Material3D { BaseColor = Color.FromHex("#2A2D33"), Unlit = true }) { Name = "Lines" };
        _points = new MeshInstance3D(attachments, new Material3D { BaseColor = Color.FromHex("#FF6D3A"), Unlit = true, PointSize = 7 })
        {
            Name = "Attachment points",
            DrawOnTop = true,
        };

        _scene.Instances.Add(canopy);
        _scene.Instances.Add(knot);
        _scene.Instances.Add(flag);
        _scene.Instances.Add(pole);
        _scene.Instances.Add(_lines);
        _scene.Instances.Add(_points);
        UpdateSun();

        _viewport = new Viewport3D()
            .Scene(_scene)
            .Height(560)
            .BindShading(_vm, v => v.Shading, (v, shading) => v.Shading = shading)
            .BindShowGrid(_vm, v => v.ShowGrid)
            .OnRendered(OnRendered);
        _viewport.Camera.Target = new Vector3(0, 3.6f, 0);
        _viewport.Camera.SetAngles(0.55f, 0.32f);
        _viewport.Camera.Distance = 19;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Viewport3DViewModel.SunAngle)) UpdateSun();
            if (e.PropertyName == nameof(Viewport3DViewModel.ShowLines)) _lines.IsVisible = _points.IsVisible = _vm.ShowLines;
            if (e.PropertyName == nameof(Viewport3DViewModel.Animate) && _vm.Animate) _viewport.RequestRender();
        };

        var shading = new ComboBox()
            .Items(ViewportShading.Shaded, ViewportShading.ShadedWireframe, ViewportShading.Wireframe)
            .BindSelectedItem(_vm, v => (object)v.Shading, (v, item) => v.Shading = item is ViewportShading s ? s : ViewportShading.Shaded)
            .MinWidth(190)
            .IsCompact(true);

        Settings(
            shading,
            new Switch("Grid").BindIsChecked(_vm, v => v.ShowGrid, (v, on) => v.ShowGrid = on),
            new Switch("Lines").BindIsChecked(_vm, v => v.ShowLines, (v, on) => v.ShowLines = on),
            new Switch("Animate").BindIsChecked(_vm, v => v.Animate, (v, on) => v.Animate = on),
            new Button("Orthographic").Variant(ButtonVariant.Outlined).OnClick(() => _viewport.Camera.Orthographic = !_viewport.Camera.Orthographic),
            new Button("Frame all").Variant(ButtonVariant.Tonal).OnClick(_viewport.FrameAll));

        Sections(ViewportSection(), NavigationSection());
    }

    private UIElement ViewportSection() => Ui.Section("Scene",
        "A translucent canopy with a fabric texture and a ripstop normal map (turn the sun behind it to see it glow), a " +
        "metallic knot, a flag animated every frame with Mesh3D.UpdatePositions, lines and points drawn over the canopy.",
        new Border().CornerRadius(12).ClipToBounds(true).Child(_viewport),
        Ui.SliderSetting("Sun direction (°)", _vm, v => v.SunAngle, (v, a) => v.SunAngle = a, -180, 180),
        Ui.Readout(_vm, v => v.Status),
        Ui.Code("var scene = new Scene3D();\n" +
                "scene.Instances.Add(new MeshInstance3D(mesh, new Material3D { BaseColorTexture = fabric, Transmission = 0.6f }));\n" +
                "var viewport = new Viewport3D().Scene(scene).Shading(ViewportShading.Shaded);\n" +
                "mesh.UpdatePositions(simulatedPositions);   // redraws; cheap enough for every frame"));

    private static UIElement NavigationSection()
    {
        static UIElement Row(string gesture, string action) =>
            new Grid().Columns(GridLength.Pixels(220), GridLength.Star).ColumnSpacing(12).Children(
                new TextBlock(gesture).Bold(),
                new TextBlock(action).Muted().TextWrapping().Column(1));

        return Ui.Section("Navigation",
            "Blender's viewport controls; all of them are commands of the Viewport3D group, rebindable in the keybinding editor.",
            new StackPanel().Spacing(10).Children(
                Row("Middle drag / Alt+drag", "Orbit around the center"),
                Row("Shift+middle drag", "Pan"),
                Row("Wheel / Ctrl+middle drag", "Zoom (the wheel zooms towards the pointer)"),
                Row("Home", "Frame everything"),
                Row("Numpad 1 / 3 / 7", "Front, right and top view (Ctrl: back, left, bottom)"),
                Row("Numpad 2 / 4 / 6 / 8", "Orbit in 15° steps"),
                Row("Numpad 5", "Perspective or orthographic"),
                Row("Shift+Z", "Wireframe")));
    }

    private void UpdateSun()
    {
        float angle = _vm.SunAngle * MathF.PI / 180;
        _scene.SunDirection = new Vector3(MathF.Sin(angle) * 0.75f, 0.65f, MathF.Cos(angle) * 0.75f);
    }

    // The flag waves while animation is on: each frame schedules the next by moving the vertices.
    private void OnRendered()
    {
        if (!_vm.Animate || _animationPending) return;
        _animationPending = true;
        Dispatcher.Post(() =>
        {
            _animationPending = false;
            if (!_vm.Animate || !_viewport.IsAttachedToVisualTree) return;
            float t = (float)_clock.Elapsed.TotalSeconds;
            for (int i = 0; i < _flagRest.Length; i++)
            {
                var p = _flagRest[i];
                float along = p.X - _flagRest[0].X; // 0 at the pole
                float wave = MathF.Sin(along * 2.2f - t * 4f) * 0.18f * along / 2.4f;
                float flutter = MathF.Sin(along * 5f + p.Y * 3f - t * 7f) * 0.03f * along;
                _flagPositions[i] = new Vector3(p.X, p.Y - along * along * 0.03f, p.Z + wave + flutter);
            }
            _flag.UpdatePositions(_flagPositions);
        });
    }
}

/// <summary>Procedural meshes and textures for the Viewport 3D page.</summary>
internal static class SampleMeshes
{
    // An arched, ribbed canopy: an airfoil-ish thickness along the chord, cells that puff out between the ribs.
    public static Mesh3D Canopy()
    {
        const int spanSegments = 160, chordSegments = 40, cells = 16;
        const float radius = 4.6f, arc = 1.7f, chord = 2.4f, height = 7.2f;
        var positions = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<uint>();
        for (int surface = 0; surface < 2; surface++)
        {
            uint start = (uint)positions.Count;
            for (int j = 0; j <= chordSegments; j++)
            {
                float v = j / (float)chordSegments;
                // Cosine spacing packs more rows towards the leading edge.
                float x = (1 - MathF.Cos(v * MathF.PI)) / 2;
                float thickness = 0.32f * (1.4845f * MathF.Sqrt(x) - 0.63f * x - 1.758f * x * x + 1.4215f * x * x * x - 0.5075f * x * x * x * x);
                for (int i = 0; i <= spanSegments; i++)
                {
                    float u = i / (float)spanSegments;
                    float theta = (u - 0.5f) * arc;
                    float taper = 1 - 0.45f * MathF.Pow(MathF.Abs(u - 0.5f) * 2, 2.5f);
                    float cellPhase = u * cells - MathF.Floor(u * cells);
                    float billow = 0.05f * MathF.Sin(cellPhase * MathF.PI) * MathF.Sin(MathF.Min(x * 3, 1) * MathF.PI / 2) * (1 - x);
                    float offset = (surface == 0 ? thickness + billow : -thickness * 0.35f - billow * 0.5f) * taper;
                    float r = radius + offset;
                    float z = (x - 0.3f) * chord * taper;
                    positions.Add(new Vector3(MathF.Sin(theta) * r, height + MathF.Cos(theta) * r - radius, z));
                    uvs.Add(new Vector2(u, surface == 0 ? x * 0.5f : 0.5f + x * 0.5f));
                }
            }
            int stride = spanSegments + 1;
            for (int j = 0; j < chordSegments; j++)
            {
                for (int i = 0; i < spanSegments; i++)
                {
                    uint a = start + (uint)(j * stride + i), b = a + 1, c = a + (uint)stride, d = c + 1;
                    if (surface == 0) indices.AddRange([a, c, b, b, c, d]);
                    else indices.AddRange([a, b, c, b, d, c]);
                }
            }
        }
        var mesh = new Mesh3D();
        mesh.SetGeometry([.. positions], [.. indices], texCoords: [.. uvs]);
        return mesh;
    }

    // Colored panels across the span with a fine ripstop grid.
    public static Texture3D FabricTexture()
    {
        const int size = 512;
        Color[] palette = [Color.FromHex("#F4F1EA"), Color.FromHex("#E53935"), Color.FromHex("#FFB300"), Color.FromHex("#1E88E5"), Color.FromHex("#F4F1EA")];
        var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                int band = (int)(MathF.Abs(u - 0.5f) * 2 * palette.Length * 0.999f);
                var c = palette[Math.Min(band, palette.Length - 1)];
                bool grid = x % 8 == 0 || y % 8 == 0;
                float shade = grid ? 0.86f : 1f;
                int o = (y * size + x) * 4;
                pixels[o] = (byte)(c.R * shade);
                pixels[o + 1] = (byte)(c.G * shade);
                pixels[o + 2] = (byte)(c.B * shade);
                pixels[o + 3] = 255;
            }
        }
        return new Texture3D(size, size, pixels);
    }

    // Raised threads every 8 pixels (the ripstop grid), as a tangent-space normal map.
    public static Texture3D RipstopNormalMap()
    {
        const int size = 512;
        var heights = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float hx = MathF.Max(0, 1 - MathF.Abs((x % 8) - 0f) / 1.5f) + MathF.Max(0, 1 - MathF.Abs((x % 8) - 8f) / 1.5f);
                float hy = MathF.Max(0, 1 - MathF.Abs((y % 8) - 0f) / 1.5f) + MathF.Max(0, 1 - MathF.Abs((y % 8) - 8f) / 1.5f);
                heights[y * size + x] = MathF.Max(hx, hy) + 0.15f * MathF.Sin(x * 1.3f) * MathF.Sin(y * 1.3f);
            }
        }
        var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = heights[y * size + (x + 1) % size] - heights[y * size + (x + size - 1) % size];
                float dy = heights[((y + 1) % size) * size + x] - heights[((y + size - 1) % size) * size + x];
                // The image's y runs down; the OpenGL convention's green points up.
                var n = Vector3.Normalize(new Vector3(-dx, dy, 2f));
                int o = (y * size + x) * 4;
                pixels[o] = (byte)((n.X * 0.5f + 0.5f) * 255);
                pixels[o + 1] = (byte)((n.Y * 0.5f + 0.5f) * 255);
                pixels[o + 2] = (byte)((n.Z * 0.5f + 0.5f) * 255);
                pixels[o + 3] = 255;
            }
        }
        return new Texture3D(size, size, pixels, srgb: false);
    }

    public static Mesh3D TorusKnot(int p = 2, int q = 3, int segments = 400, int sides = 24)
    {
        Vector3 Curve(float t)
        {
            float r = 2 + MathF.Cos(q * t);
            return new Vector3(r * MathF.Cos(p * t), -MathF.Sin(q * t), r * MathF.Sin(p * t));
        }

        var positions = new Vector3[(segments + 1) * (sides + 1)];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments * MathF.PI * 2;
            var c = Curve(t);
            var tangent = Vector3.Normalize(Curve(t + 0.001f) - c);
            var normal = Vector3.Normalize(Vector3.Cross(tangent, Vector3.UnitY));
            var binormal = Vector3.Cross(normal, tangent);
            for (int j = 0; j <= sides; j++)
            {
                float a = j / (float)sides * MathF.PI * 2;
                positions[i * (sides + 1) + j] = c + (normal * MathF.Cos(a) + binormal * MathF.Sin(a)) * 0.45f;
            }
        }
        var mesh = new Mesh3D();
        mesh.SetGeometry(positions, GridIndices(segments, sides));
        return mesh;
    }

    public static (Mesh3D Mesh, Vector3[] RestPositions) Flag()
    {
        const int columns = 48, rows = 28;
        const float width = 2.4f, height = 1.4f;
        var positions = new Vector3[(columns + 1) * (rows + 1)];
        var colors = new Vector4[positions.Length];
        for (int j = 0; j <= rows; j++)
        {
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns, v = j / (float)rows;
                positions[j * (columns + 1) + i] = new Vector3(-5.5f + u * width, 3.1f - v * height, -1);
                // Three horizontal stripes, in linear color.
                colors[j * (columns + 1) + i] = v < 1 / 3f ? new Vector4(0.02f, 0.25f, 0.6f, 1) : v < 2 / 3f ? new Vector4(0.9f, 0.9f, 0.88f, 1) : new Vector4(0.7f, 0.05f, 0.04f, 1);
            }
        }
        var mesh = new Mesh3D();
        mesh.SetGeometry(positions, GridIndices(rows, columns), colors: colors);
        return (mesh, (Vector3[])positions.Clone());
    }

    public static Mesh3D Cylinder(float radius, float height, int sides)
    {
        var positions = new Vector3[(sides + 1) * 2];
        for (int i = 0; i <= sides; i++)
        {
            float a = i / (float)sides * MathF.PI * 2;
            positions[i * 2] = new Vector3(MathF.Cos(a) * radius, 0, MathF.Sin(a) * radius);
            positions[i * 2 + 1] = new Vector3(MathF.Cos(a) * radius, height, MathF.Sin(a) * radius);
        }
        var indices = new List<uint>();
        for (int i = 0; i < sides; i++)
        {
            uint a = (uint)(i * 2);
            indices.AddRange([a, a + 1, a + 2, a + 2, a + 1, a + 3]);
        }
        var mesh = new Mesh3D();
        mesh.SetGeometry(positions, [.. indices]);
        return mesh;
    }

    // Lines from attachment points under the canopy down to two risers, like a paraglider's suspension lines.
    public static (Mesh3D Lines, Mesh3D Points) SuspensionLines()
    {
        var positions = new List<Vector3> { new(-0.22f, 1.1f, 0.25f), new(0.22f, 1.1f, 0.25f) };
        var indices = new List<uint>();
        var attachments = new List<Vector3>();
        const float radius = 4.6f, arc = 1.7f, height = 7.2f;
        for (int i = 0; i <= 16; i++)
        {
            float u = i / 16f;
            float theta = (u - 0.5f) * arc * 0.98f;
            float taper = 1 - 0.45f * MathF.Pow(MathF.Abs(u - 0.5f) * 2, 2.5f);
            for (int row = 0; row < 3; row++)
            {
                float x = 0.08f + row * 0.28f;
                float r = radius - 0.06f * taper;
                var p = new Vector3(MathF.Sin(theta) * r, height + MathF.Cos(theta) * r - radius, (x - 0.3f) * 2.4f * taper);
                attachments.Add(p);
                positions.Add(p);
                indices.Add((uint)(positions.Count - 1));
                indices.Add(u < 0.5f ? 0u : 1u);
            }
        }
        var lines = new Mesh3D(PrimitiveTopology.Lines);
        lines.SetGeometry([.. positions], [.. indices]);
        var points = new Mesh3D(PrimitiveTopology.Points);
        points.SetGeometry([.. attachments], Enumerable.Range(0, attachments.Count).Select(i => (uint)i).ToArray());
        return (lines, points);
    }

    // Two triangles per cell of a (rows + 1) × (columns + 1) vertex grid, counter-clockwise seen from +Z.
    private static uint[] GridIndices(int rows, int columns)
    {
        var indices = new uint[rows * columns * 6];
        int k = 0;
        for (int j = 0; j < rows; j++)
        {
            for (int i = 0; i < columns; i++)
            {
                uint a = (uint)(j * (columns + 1) + i), b = a + 1, c = a + (uint)(columns + 1), d = c + 1;
                indices[k++] = a; indices[k++] = c; indices[k++] = b;
                indices[k++] = b; indices[k++] = c; indices[k++] = d;
            }
        }
        return indices;
    }
}
