using System.Diagnostics;
using System.Numerics;
using Atelier.Core.Primitives;
using Atelier.Rendering;
using Atelier.Theming;
using SkiaSharp;

namespace Atelier.Graphics3D.Rendering;

/// <summary>
/// Draws a <see cref="ViewportSurface"/>: renders the scene with OpenGL into the viewport's offscreen targets (on the
/// window's context, which is current while the window renders), then draws the output texture into the 2D canvas as
/// an image, and the axis gizmo over it.
/// </summary>
/// <remarks>
/// Skia shares the context: its queued work is flushed before the raw OpenGL calls, and its cached GL state is reset
/// after them (<see cref="GRContext.ResetContext(GRGlBackendState)"/>), so neither side trips over the other.
/// </remarks>
public sealed class Viewport3DRenderer : ControlRenderer<ViewportSurface>
{
    private const uint GlTexture2D = 0x0DE1;
    private const uint GlRgba8 = 0x8058;

    private static readonly Color s_xAxis = Color.FromRgb(0xE5, 0x48, 0x4D);
    private static readonly Color s_yAxis = Color.FromRgb(0x5B, 0xB9, 0x4A);
    private static readonly Color s_zAxis = Color.FromRgb(0x3E, 0x7B, 0xF0);

    /// <inheritdoc/>
    public override void Render(ViewportSurface element, ref DrawingContext context)
    {
        var viewport = element.Viewport;
        float width = element.Bounds.Width, height = element.Bounds.Height;
        if (width < 1 || height < 1) return;
        var rect = new Rect(0, 0, width, height);

        if (!TryRenderScene(element, viewport, ref context, rect, out string? problem))
        {
            element.LastFrameUsedGpu = false;
            DrawPlaceholder(viewport, ref context, rect, problem);
        }
        else
        {
            element.LastFrameUsedGpu = true;
        }

        if (viewport.ShowAxes) DrawAxisGizmo(viewport.Camera, ref context, rect);
        if (element.LastFrameUsedGpu == true) viewport.RaiseRendered();
    }

    private static bool TryRenderScene(ViewportSurface element, Viewport3D viewport, ref DrawingContext context, Rect rect, out string? problem)
    {
        problem = null;
        var canvas = context.Canvas;
        if (element.Host?.GraphicsDevice is not { IsAlive: true } device || canvas.Context is not GRContext grContext)
        {
            return false;
        }
        var gpu = GpuDevice.Get(device);
        if (gpu == null)
        {
            problem = "The 3D view needs OpenGL 3.3.";
            return false;
        }

        // The image is rendered at the control's size in device pixels.
        var matrix = canvas.TotalMatrix;
        float scaleX = MathF.Sqrt(matrix.ScaleX * matrix.ScaleX + matrix.SkewY * matrix.SkewY);
        float scaleY = MathF.Sqrt(matrix.ScaleY * matrix.ScaleY + matrix.SkewX * matrix.SkewX);
        int pixelWidth = Math.Clamp((int)MathF.Round(rect.Width * scaleX), 1, 16384);
        int pixelHeight = Math.Clamp((int)MathF.Round(rect.Height * scaleY), 1, 16384);
        element.PixelSize = (pixelWidth, pixelHeight);

        grContext.Flush();
        ViewportTargets targets;
        try
        {
            gpu.BeginFrame();
            targets = gpu.EnsureTargets(element.Targets, pixelWidth, pixelHeight, viewport.Samples);
            element.Targets = targets;
            var settings = new FrameSettings(viewport.Shading, viewport.ShowGrid, viewport.GridHeight, viewport.BackgroundTop, viewport.BackgroundBottom, viewport.WireframeColor);
            element.SceneRenderer.Render(gpu, targets, viewport.Scene, viewport.Camera, settings);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or ShaderCompilationException)
        {
            Debug.WriteLine($"[Viewport3D] Rendering failed: {e.Message}");
            problem = e.Message;
            return false;
        }
        finally
        {
            grContext.ResetContext();
        }

        if (targets.WrappedImage is not WrappedTexture wrapped)
        {
            var backend = new GRBackendTexture(pixelWidth, pixelHeight, false, new GRGlTextureInfo(GlTexture2D, targets.OutputTexture, GlRgba8));
            var image = SKImage.FromTexture(grContext, backend, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888, SKAlphaType.Opaque);
            if (image == null)
            {
                backend.Dispose();
                problem = "The 3D image couldn't be shown.";
                return false;
            }
            wrapped = new WrappedTexture(backend, image);
            targets.WrappedImage = wrapped;
        }
        context.DrawImage(wrapped.Image, rect);
        return true;
    }

    private static void DrawPlaceholder(Viewport3D viewport, ref DrawingContext context, Rect rect, string? problem)
    {
        Span<Color> colors = [viewport.BackgroundTop, viewport.BackgroundBottom];
        context.DrawLinearGradient(rect, default, new Point(0, 0), new Point(0, rect.Height), colors);
        string text = problem ?? "3D view (rendered with OpenGL in a window)";
        var size = context.MeasureText(text, 13f);
        context.DrawText(text, new Point((rect.Width - size.Width) / 2, rect.Height / 2 + 5), Color.White.WithAlpha(0.7f), 13f);
    }

    // The axes as the camera sees them, in the bottom left corner: positive axes as labeled dots on lines, negative ones
    // as faint dots; nearer axes are drawn last.
    private static void DrawAxisGizmo(OrbitCamera camera, ref DrawingContext context, Rect rect)
    {
        const float length = 26f;
        if (rect.Width < 120 || rect.Height < 120) return;
        var center = new Point(16 + length + 6, rect.Height - 16 - length - 6);
        var right = camera.Right;
        var up = camera.Up;
        var toward = camera.Backward;

        Span<(float Depth, int Axis, bool Positive)> order = stackalloc (float, int, bool)[6];
        for (int axis = 0; axis < 3; axis++)
        {
            var direction = axis switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, _ => Vector3.UnitZ };
            float depth = Vector3.Dot(direction, toward);
            order[axis * 2] = (depth, axis, true);
            order[axis * 2 + 1] = (-depth, axis, false);
        }
        // Insertion sort: farthest first.
        for (int i = 1; i < order.Length; i++)
        {
            var item = order[i];
            int j = i - 1;
            while (j >= 0 && order[j].Depth > item.Depth)
            {
                order[j + 1] = order[j];
                j--;
            }
            order[j + 1] = item;
        }

        context.DrawCircle(center, length + 12, Color.Black.WithAlpha(0.18f));
        foreach (var (_, axis, positive) in order)
        {
            var direction = axis switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, _ => Vector3.UnitZ } * (positive ? 1 : -1);
            var tip = new Point(center.X + Vector3.Dot(direction, right) * length, center.Y - Vector3.Dot(direction, up) * length);
            var color = axis switch { 0 => s_xAxis, 1 => s_yAxis, _ => s_zAxis };
            if (positive)
            {
                context.DrawLine(center, tip, color, 2f);
                context.DrawCircle(tip, 7.5f, color);
                string label = axis switch { 0 => "X", 1 => "Y", _ => "Z" };
                context.DrawText(label, new Point(tip.X - 3.6f, tip.Y + 3.8f), Color.FromRgb(0x10, 0x10, 0x10), 10.5f, bold: true);
            }
            else
            {
                context.DrawCircle(tip, 5f, color.WithAlpha(0.45f));
            }
        }
    }

    // A borrowed GL texture wrapped for the 2D canvas; disposed before the texture is deleted.
    private sealed class WrappedTexture(GRBackendTexture backend, SKImage image) : IDisposable
    {
        public SKImage Image { get; } = image;

        public void Dispose()
        {
            Image.Dispose();
            backend.Dispose();
        }
    }
}
