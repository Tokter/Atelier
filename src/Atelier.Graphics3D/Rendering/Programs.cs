using System.Numerics;
using Silk.NET.OpenGL;

namespace Atelier.Graphics3D.Rendering;

/// <summary>A shader failed to compile or link.</summary>
internal sealed class ShaderCompilationException(string message) : Exception(message);

/// <summary>A linked GLSL program with helpers for setting uniforms by cached location.</summary>
internal abstract class ShaderProgram : IDisposable
{
    protected ShaderProgram(GL gl, string vertexSource, string fragmentSource)
    {
        Gl = gl;
        uint vertex = Compile(ShaderType.VertexShader, vertexSource);
        uint fragment = Compile(ShaderType.FragmentShader, fragmentSource);
        Handle = gl.CreateProgram();
        gl.AttachShader(Handle, vertex);
        gl.AttachShader(Handle, fragment);
        gl.LinkProgram(Handle);
        gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int linked);
        gl.DetachShader(Handle, vertex);
        gl.DetachShader(Handle, fragment);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);
        if (linked == 0)
        {
            string log = gl.GetProgramInfoLog(Handle);
            gl.DeleteProgram(Handle);
            throw new ShaderCompilationException($"{GetType().Name} didn't link: {log}");
        }
    }

    protected GL Gl { get; }
    public uint Handle { get; }

    public void Use() => Gl.UseProgram(Handle);

    protected int Location(string name) => Gl.GetUniformLocation(Handle, name);

    protected unsafe void SetMatrix(int location, in Matrix4x4 matrix)
    {
        // System.Numerics matrices are row-major for row vectors; read column-major, GLSL sees the transpose, so
        // "matrix * vector" in GLSL matches "vector * matrix" here.
        fixed (Matrix4x4* m = &matrix)
        {
            Gl.UniformMatrix4(location, 1, false, (float*)m);
        }
    }

    private uint Compile(ShaderType type, string source)
    {
        uint shader = Gl.CreateShader(type);
        Gl.ShaderSource(shader, source);
        Gl.CompileShader(shader);
        Gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compiled);
        if (compiled == 0)
        {
            string log = Gl.GetShaderInfoLog(shader);
            Gl.DeleteShader(shader);
            throw new ShaderCompilationException($"{GetType().Name} ({type}) didn't compile: {log}");
        }
        return shader;
    }

    public void Dispose() => Gl.DeleteProgram(Handle);
}

internal sealed class MeshProgram : ShaderProgram
{
    public MeshProgram(GL gl) : base(gl, Shaders.MeshVertex, Shaders.MeshFragment)
    {
        Model = Location("uModel");
        NormalMatrix = Location("uNormalMatrix");
        ViewProjection = Location("uViewProjection");
        PointSize = Location("uPointSize");
        BaseColor = Location("uBaseColor");
        HasBaseColorTexture = Location("uHasBaseColorTexture");
        HasNormalTexture = Location("uHasNormalTexture");
        HasNormals = Location("uHasNormals");
        NormalScale = Location("uNormalScale");
        Roughness = Location("uRoughness");
        Metallic = Location("uMetallic");
        Transmission = Location("uTransmission");
        Unlit = Location("uUnlit");
        RoundPoints = Location("uRoundPoints");
        Override = Location("uOverride");
        OverrideColor = Location("uOverrideColor");
        CameraPos = Location("uCameraPos");
        ViewDir = Location("uViewDir");
        Orthographic = Location("uOrthographic");
        SunDir = Location("uSunDir");
        SunRadiance = Location("uSunRadiance");
        SkyColor = Location("uSkyColor");
        GroundColor = Location("uGroundColor");
        Use();
        gl.Uniform1(Location("uBaseColorTexture"), 0);
        gl.Uniform1(Location("uNormalTexture"), 1);
        gl.UseProgram(0);
    }

    public int Model { get; }
    public int NormalMatrix { get; }
    public int ViewProjection { get; }
    public int PointSize { get; }
    public int BaseColor { get; }
    public int HasBaseColorTexture { get; }
    public int HasNormalTexture { get; }
    public int HasNormals { get; }
    public int NormalScale { get; }
    public int Roughness { get; }
    public int Metallic { get; }
    public int Transmission { get; }
    public int Unlit { get; }
    public int RoundPoints { get; }
    public int Override { get; }
    public int OverrideColor { get; }
    public int CameraPos { get; }
    public int ViewDir { get; }
    public int Orthographic { get; }
    public int SunDir { get; }
    public int SunRadiance { get; }
    public int SkyColor { get; }
    public int GroundColor { get; }

    public void SetModel(in Matrix4x4 model)
    {
        SetMatrix(Model, model);
        var normal = Matrix4x4.Invert(model, out var inverse) ? Matrix4x4.Transpose(inverse) : Matrix4x4.Identity;
        SetMatrix(NormalMatrix, normal);
    }

    public void SetViewProjection(in Matrix4x4 viewProjection) => SetMatrix(ViewProjection, viewProjection);
}

internal sealed class GridProgram : ShaderProgram
{
    public GridProgram(GL gl) : base(gl, Shaders.FullscreenVertex, Shaders.GridFragment)
    {
        InverseViewProjectionLocation = Location("uInverseViewProjection");
        ViewProjectionLocation = Location("uViewProjection");
        CameraPos = Location("uCameraPos");
        CellSize = Location("uCellSize");
        FadeDistance = Location("uFadeDistance");
        LineColor = Location("uLineColor");
        LineAlpha = Location("uLineAlpha");
        GridHeight = Location("uGridHeight");
        GridOffset = Location("uGridOffset");
    }

    private int InverseViewProjectionLocation { get; }
    private int ViewProjectionLocation { get; }
    public int CameraPos { get; }
    public int CellSize { get; }
    public int FadeDistance { get; }
    public int LineColor { get; }
    public int LineAlpha { get; }
    public int GridHeight { get; }
    public int GridOffset { get; }

    public void SetMatrices(in Matrix4x4 viewProjection, in Matrix4x4 inverse)
    {
        SetMatrix(ViewProjectionLocation, viewProjection);
        SetMatrix(InverseViewProjectionLocation, inverse);
    }
}

internal sealed class CompositeProgram : ShaderProgram
{
    public CompositeProgram(GL gl) : base(gl, Shaders.FullscreenVertex, Shaders.CompositeFragment)
    {
        BackgroundTop = Location("uBackgroundTop");
        BackgroundBottom = Location("uBackgroundBottom");
        Exposure = Location("uExposure");
        Use();
        gl.Uniform1(Location("uScene"), 0);
        gl.UseProgram(0);
    }

    public int BackgroundTop { get; }
    public int BackgroundBottom { get; }
    public int Exposure { get; }
}
