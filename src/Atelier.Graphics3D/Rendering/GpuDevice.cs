using System.Diagnostics;
using System.Numerics;
using Atelier.Core.Platform;
using Silk.NET.OpenGL;

namespace Atelier.Graphics3D.Rendering;

/// <summary>
/// The OpenGL objects of one window's context: shader programs, the GPU copies of meshes and textures, and the render
/// targets of the viewports drawn there. Released when the window's context goes away.
/// </summary>
internal sealed class GpuDevice
{
    // Meshes and textures not drawn for this many frames are released (and uploaded again if they come back).
    private const int EvictAfterFrames = 600;

    private static readonly Dictionary<IGraphicsDevice, GpuDevice> s_devices = [];

    private readonly IGraphicsDevice _device;
    private readonly Dictionary<Mesh3D, MeshGpu> _meshes = [];
    private readonly Dictionary<Texture3D, TextureGpu> _textures = [];
    private readonly List<ViewportTargets> _targets = [];
    private readonly List<ViewportTargets> _releaseQueue = [];
    private readonly List<Mesh3D> _evictMeshes = [];
    private readonly List<Texture3D> _evictTextures = [];

    private GpuDevice(IGraphicsDevice device)
    {
        _device = device;
        Gl = GL.GetApi(device.GetProcAddress);
        MeshProgram = new MeshProgram(Gl);
        GridProgram = new GridProgram(Gl);
        CompositeProgram = new CompositeProgram(Gl);
        EmptyVao = Gl.GenVertexArray();
        Gl.GetInteger((GetPName)0x8D57 /* GL_MAX_SAMPLES */, out int maxSamples);
        MaxSamples = Math.Max(1, maxSamples);
        MaxAnisotropy = HasExtension("GL_EXT_texture_filter_anisotropic") || HasExtension("GL_ARB_texture_filter_anisotropic")
            ? GetFloat(0x84FF /* GL_MAX_TEXTURE_MAX_ANISOTROPY */)
            : 1f;
        device.Disposing += OnDeviceDisposing;
    }

    public GL Gl { get; }
    public MeshProgram MeshProgram { get; }
    public GridProgram GridProgram { get; }
    public CompositeProgram CompositeProgram { get; }
    public uint EmptyVao { get; }
    public int MaxSamples { get; }
    public float MaxAnisotropy { get; }
    public long Frame { get; private set; }

    /// <summary>Gets the GPU objects of <paramref name="device"/>, creating them on first use (its context must be current).</summary>
    public static GpuDevice? Get(IGraphicsDevice device)
    {
        if (!device.IsAlive || device.Api != GraphicsApi.OpenGL || device.MajorVersion < 3 || device.MajorVersion == 3 && device.MinorVersion < 3)
        {
            return null;
        }
        if (s_devices.TryGetValue(device, out var gpu)) return gpu;
        try
        {
            gpu = new GpuDevice(device);
        }
        catch (Exception e) when (e is InvalidOperationException or ShaderCompilationException or EntryPointNotFoundException)
        {
            Debug.WriteLine($"[Viewport3D] OpenGL is unavailable: {e.Message}");
            return null;
        }
        s_devices[device] = gpu;
        return gpu;
    }

    /// <summary>Starts a frame: releases what was queued and, now and then, what hasn't been drawn for a while.</summary>
    public void BeginFrame()
    {
        Frame++;
        foreach (var targets in _releaseQueue)
        {
            if (targets.IsReleaseRequested) DestroyTargets(targets);
        }
        _releaseQueue.Clear();

        if (Frame % 120 != 0) return;
        foreach (var (mesh, gpu) in _meshes)
        {
            if (Frame - gpu.LastUsedFrame > EvictAfterFrames) _evictMeshes.Add(mesh);
        }
        foreach (var mesh in _evictMeshes)
        {
            _meshes[mesh].Dispose(Gl);
            _meshes.Remove(mesh);
        }
        _evictMeshes.Clear();
        foreach (var (texture, gpu) in _textures)
        {
            if (Frame - gpu.LastUsedFrame > EvictAfterFrames) _evictTextures.Add(texture);
        }
        foreach (var texture in _evictTextures)
        {
            Gl.DeleteTexture(_textures[texture].Handle);
            _textures.Remove(texture);
        }
        _evictTextures.Clear();
    }

    /// <summary>Gets the GPU copy of <paramref name="mesh"/>, uploading what changed since the last draw.</summary>
    public MeshGpu GetMesh(Mesh3D mesh)
    {
        if (!_meshes.TryGetValue(mesh, out var gpu))
        {
            gpu = new MeshGpu(Gl);
            _meshes[mesh] = gpu;
        }
        gpu.Sync(Gl, mesh);
        gpu.LastUsedFrame = Frame;
        return gpu;
    }

    /// <summary>Gets the GPU texture of <paramref name="texture"/>, uploading it when its pixels changed.</summary>
    public uint GetTexture(Texture3D texture)
    {
        if (!_textures.TryGetValue(texture, out var gpu))
        {
            gpu = new TextureGpu { Handle = Gl.GenTexture(), Version = -1 };
            _textures[texture] = gpu;
        }
        if (gpu.Version != texture.Version)
        {
            UploadTexture(gpu.Handle, texture);
            gpu.Version = texture.Version;
        }
        gpu.LastUsedFrame = Frame;
        return gpu.Handle;
    }

    /// <summary>Gets render targets of the size and sample count, reusing <paramref name="current"/> when it fits.</summary>
    public ViewportTargets EnsureTargets(ViewportTargets? current, int width, int height, int samples)
    {
        samples = Math.Clamp(samples, 1, MaxSamples);
        if (current != null && current.Device == this && !current.IsDestroyed && current.Width == width && current.Height == height && current.Samples == samples)
        {
            current.IsReleaseRequested = false;
            return current;
        }
        if (current != null && current.Device == this && !current.IsDestroyed) DestroyTargets(current);
        var targets = ViewportTargets.Create(this, width, height, samples);
        _targets.Add(targets);
        return targets;
    }

    /// <summary>Releases <paramref name="targets"/> at the next frame on this device (when the context is current).</summary>
    public void QueueRelease(ViewportTargets targets)
    {
        targets.IsReleaseRequested = true;
        if (!_releaseQueue.Contains(targets)) _releaseQueue.Add(targets);
    }

    private void DestroyTargets(ViewportTargets targets)
    {
        if (!_targets.Remove(targets)) return;
        targets.Destroy(Gl);
    }

    private unsafe void UploadTexture(uint handle, Texture3D texture)
    {
        Gl.BindTexture(TextureTarget.Texture2D, handle);
        Gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        var internalFormat = texture.IsSrgb ? InternalFormat.Srgb8Alpha8 : InternalFormat.Rgba8;
        fixed (byte* pixels = texture.Pixels)
        {
            Gl.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, (uint)texture.Width, (uint)texture.Height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        }
        Gl.GenerateMipmap(TextureTarget.Texture2D);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        if (MaxAnisotropy > 1f)
        {
            Gl.TexParameter(TextureTarget.Texture2D, (TextureParameterName)0x84FE /* GL_TEXTURE_MAX_ANISOTROPY */, Math.Min(8f, MaxAnisotropy));
        }
        Gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private bool HasExtension(string name)
    {
        Gl.GetInteger(GetPName.NumExtensions, out int count);
        for (uint i = 0; i < count; i++)
        {
            if (Gl.GetStringS(StringName.Extensions, i) == name) return true;
        }
        return false;
    }

    private float GetFloat(int pname)
    {
        Gl.GetFloat((GetPName)pname, out float value);
        return value;
    }

    private void OnDeviceDisposing(object? sender, EventArgs e)
    {
        _device.Disposing -= OnDeviceDisposing;
        s_devices.Remove(_device);
        try
        {
            foreach (var targets in _targets.ToArray()) targets.Destroy(Gl);
            _targets.Clear();
            foreach (var mesh in _meshes.Values) mesh.Dispose(Gl);
            _meshes.Clear();
            foreach (var texture in _textures.Values) Gl.DeleteTexture(texture.Handle);
            _textures.Clear();
            MeshProgram.Dispose();
            GridProgram.Dispose();
            CompositeProgram.Dispose();
            Gl.DeleteVertexArray(EmptyVao);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Viewport3D] Releasing OpenGL objects failed: {ex.Message}");
        }
    }

    private sealed class TextureGpu
    {
        public uint Handle;
        public int Version;
        public long LastUsedFrame;
    }
}

/// <summary>The GPU buffers of a <see cref="Mesh3D"/>: positions and normals (updated in place), texture coordinates, colors and indices.</summary>
internal sealed class MeshGpu
{
    private uint _positionBuffer;
    private uint _normalBuffer;
    private uint _texCoordBuffer;
    private uint _colorBuffer;
    private uint _indexBuffer;
    private int _geometryVersion = -1;
    private int _positionsVersion = -1;

    public MeshGpu(GL gl)
    {
        Vao = gl.GenVertexArray();
    }

    public uint Vao { get; }
    public uint IndexCount { get; private set; }
    public bool HasNormals { get; private set; }
    public bool HasTexCoords { get; private set; }
    public bool HasColors { get; private set; }
    public long LastUsedFrame { get; set; }

    public unsafe void Sync(GL gl, Mesh3D mesh)
    {
        if (_geometryVersion != mesh.GeometryVersion)
        {
            Rebuild(gl, mesh);
            _geometryVersion = mesh.GeometryVersion;
            _positionsVersion = mesh.PositionsVersion;
            return;
        }
        if (_positionsVersion == mesh.PositionsVersion) return;

        fixed (Vector3* positions = mesh.Positions)
        {
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _positionBuffer);
            gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(mesh.Positions.Length * sizeof(Vector3)), positions);
        }
        if (mesh.Normals is { } normals)
        {
            if (_normalBuffer == 0)
            {
                // Normals appeared through UpdatePositions: rebuild to add the buffer.
                Rebuild(gl, mesh);
            }
            else
            {
                fixed (Vector3* n = normals)
                {
                    gl.BindBuffer(BufferTargetARB.ArrayBuffer, _normalBuffer);
                    gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(normals.Length * sizeof(Vector3)), n);
                }
            }
        }
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _positionsVersion = mesh.PositionsVersion;
    }

    private unsafe void Rebuild(GL gl, Mesh3D mesh)
    {
        DeleteBuffers(gl);
        gl.BindVertexArray(Vao);

        _positionBuffer = CreateBuffer(gl, mesh.Positions, 0, 3, BufferUsageARB.DynamicDraw);
        HasNormals = mesh.Normals != null;
        _normalBuffer = mesh.Normals is { } normals ? CreateBuffer(gl, normals, 1, 3, BufferUsageARB.DynamicDraw) : 0;
        HasTexCoords = mesh.TexCoords != null;
        _texCoordBuffer = mesh.TexCoords is { } uvs ? CreateBuffer(gl, uvs, 2, 2, BufferUsageARB.StaticDraw) : 0;
        HasColors = mesh.Colors != null;
        _colorBuffer = mesh.Colors is { } colors ? CreateBuffer(gl, colors, 3, 4, BufferUsageARB.StaticDraw) : 0;
        if (!HasNormals) gl.DisableVertexAttribArray(1);
        if (!HasTexCoords) gl.DisableVertexAttribArray(2);
        if (!HasColors) gl.DisableVertexAttribArray(3);

        _indexBuffer = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer);
        fixed (uint* indices = mesh.Indices)
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(mesh.Indices.Length * sizeof(uint)), indices, BufferUsageARB.StaticDraw);
        }
        IndexCount = (uint)mesh.Indices.Length;

        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
    }

    private static unsafe uint CreateBuffer<T>(GL gl, T[] data, uint location, int components, BufferUsageARB usage) where T : unmanaged
    {
        uint buffer = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
        fixed (T* p = data)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(T)), p, usage);
        }
        gl.EnableVertexAttribArray(location);
        gl.VertexAttribPointer(location, components, VertexAttribPointerType.Float, false, (uint)sizeof(T), (void*)0);
        return buffer;
    }

    private void DeleteBuffers(GL gl)
    {
        foreach (uint buffer in (ReadOnlySpan<uint>)[_positionBuffer, _normalBuffer, _texCoordBuffer, _colorBuffer, _indexBuffer])
        {
            if (buffer != 0) gl.DeleteBuffer(buffer);
        }
        _positionBuffer = _normalBuffer = _texCoordBuffer = _colorBuffer = _indexBuffer = 0;
    }

    public void Dispose(GL gl)
    {
        DeleteBuffers(gl);
        gl.DeleteVertexArray(Vao);
    }
}

/// <summary>
/// The render targets of one viewport: a multisampled floating-point color and depth buffer the scene is drawn into,
/// the resolved scene texture, and the 8-bit output texture the composite pass writes and the 2D canvas draws.
/// </summary>
internal sealed class ViewportTargets
{
    private ViewportTargets(GpuDevice device, int width, int height, int samples)
    {
        Device = device;
        Width = width;
        Height = height;
        Samples = samples;
    }

    public GpuDevice Device { get; }
    public int Width { get; }
    public int Height { get; }
    public int Samples { get; }
    public uint MsaaFramebuffer { get; private set; }
    public uint MsaaColor { get; private set; }
    public uint MsaaDepth { get; private set; }
    public uint ResolveFramebuffer { get; private set; }
    public uint ResolveTexture { get; private set; }
    public uint OutputFramebuffer { get; private set; }
    public uint OutputTexture { get; private set; }
    public bool IsReleaseRequested { get; set; }
    public bool IsDestroyed { get; private set; }

    /// <summary>Gets or sets the 2D image wrapping <see cref="OutputTexture"/>, disposed with the targets.</summary>
    public IDisposable? WrappedImage { get; set; }

    public static unsafe ViewportTargets Create(GpuDevice device, int width, int height, int samples)
    {
        var gl = device.Gl;
        var t = new ViewportTargets(device, width, height, samples);

        t.MsaaFramebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, t.MsaaFramebuffer);
        t.MsaaColor = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, t.MsaaColor);
        gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, (uint)samples, InternalFormat.Rgba16f, (uint)width, (uint)height);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, t.MsaaColor);
        t.MsaaDepth = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, t.MsaaDepth);
        gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, (uint)samples, InternalFormat.Depth24Stencil8, (uint)width, (uint)height);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, t.MsaaDepth);
        CheckComplete(gl, "scene");

        t.ResolveTexture = CreateTexture(gl, InternalFormat.Rgba16f, PixelType.HalfFloat, width, height);
        t.ResolveFramebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, t.ResolveFramebuffer);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, t.ResolveTexture, 0);
        CheckComplete(gl, "resolve");

        t.OutputTexture = CreateTexture(gl, InternalFormat.Rgba8, PixelType.UnsignedByte, width, height);
        t.OutputFramebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, t.OutputFramebuffer);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, t.OutputTexture, 0);
        CheckComplete(gl, "output");

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
        return t;
    }

    private static unsafe uint CreateTexture(GL gl, InternalFormat format, PixelType type, int width, int height)
    {
        uint texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.TexImage2D(TextureTarget.Texture2D, 0, format, (uint)width, (uint)height, 0, PixelFormat.Rgba, type, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        return texture;
    }

    private static void CheckComplete(GL gl, string name)
    {
        var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new InvalidOperationException($"The {name} framebuffer is incomplete ({status}).");
        }
    }

    public void Destroy(GL gl)
    {
        WrappedImage?.Dispose();
        WrappedImage = null;
        gl.DeleteFramebuffer(MsaaFramebuffer);
        gl.DeleteRenderbuffer(MsaaColor);
        gl.DeleteRenderbuffer(MsaaDepth);
        gl.DeleteFramebuffer(ResolveFramebuffer);
        gl.DeleteTexture(ResolveTexture);
        gl.DeleteFramebuffer(OutputFramebuffer);
        gl.DeleteTexture(OutputTexture);
        MsaaFramebuffer = MsaaColor = MsaaDepth = ResolveFramebuffer = ResolveTexture = OutputFramebuffer = OutputTexture = 0;
        IsDestroyed = true;
    }
}
