namespace Atelier.Core.Platform;

/// <summary>
/// The GPU context a window draws with, for controls that render with the graphics API directly (such as a 3D
/// viewport) instead of through the drawing context.
/// </summary>
/// <remarks>
/// <para>
/// A window's controls are rendered while its context is current, so a control's renderer can issue API calls from
/// <c>Render</c> without calling <see cref="MakeCurrent"/>. GPU objects belong to one device: windows have separate
/// contexts that share nothing, so keep resources per device.
/// </para>
/// <para>
/// Raw API calls change state the window's 2D renderer caches; renderers that use them reset that state afterwards
/// (with SkiaSharp, <c>GRContext.ResetContext()</c>).
/// </para>
/// </remarks>
public interface IGraphicsDevice
{
    /// <summary>Gets the graphics API of the context.</summary>
    GraphicsApi Api { get; }

    /// <summary>Gets the major version of the context, e.g. 3 for OpenGL 3.3.</summary>
    int MajorVersion { get; }

    /// <summary>Gets the minor version of the context, e.g. 3 for OpenGL 3.3.</summary>
    int MinorVersion { get; }

    /// <summary>Gets whether the context is a core profile (no fixed-function or deprecated calls).</summary>
    bool IsCoreProfile { get; }

    /// <summary>Gets whether the device is still usable; <c>false</c> once <see cref="Disposing"/> was raised.</summary>
    bool IsAlive { get; }

    /// <summary>Returns the address of the API function <paramref name="name"/>, or <see cref="IntPtr.Zero"/>.</summary>
    IntPtr GetProcAddress(string name);

    /// <summary>Makes the context current on the calling thread.</summary>
    void MakeCurrent();

    /// <summary>
    /// Occurs while the context is current, before it is destroyed (when its window closes): release the GPU objects
    /// made on this device.
    /// </summary>
    event EventHandler? Disposing;
}

/// <summary>The graphics API of an <see cref="IGraphicsDevice"/>.</summary>
public enum GraphicsApi
{
    /// <summary>Desktop OpenGL.</summary>
    OpenGL,

    /// <summary>OpenGL ES.</summary>
    OpenGLES,
}
