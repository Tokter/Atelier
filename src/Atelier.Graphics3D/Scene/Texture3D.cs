using SkiaSharp;

namespace Atelier.Graphics3D;

/// <summary>
/// An image for a <see cref="Material3D"/>: RGBA pixels, 8 bits per channel, sampled with mipmaps and repeating at the
/// edges.
/// </summary>
/// <remarks>
/// Color textures hold sRGB values (<see cref="IsSrgb"/>), which the GPU decodes to linear light before shading;
/// textures holding data, such as normal maps, are linear.
/// </remarks>
public sealed class Texture3D
{
    private byte[] _pixels;

    /// <summary>Initializes a texture from tightly packed RGBA pixels, the top row first.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="rgba">The pixels, <c>width × height × 4</c> bytes. The array is kept, not copied.</param>
    /// <param name="srgb"><c>true</c> for colors, <c>false</c> for data such as normal maps.</param>
    /// <exception cref="ArgumentException">The size is not positive or the array has the wrong length.</exception>
    public Texture3D(int width, int height, byte[] rgba, bool srgb = true)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (width <= 0 || height <= 0) throw new ArgumentException("A texture needs a positive size.");
        if (rgba.Length != width * height * 4) throw new ArgumentException($"Expected {width * height * 4} bytes of RGBA pixels.", nameof(rgba));
        Width = width;
        Height = height;
        IsSrgb = srgb;
        _pixels = rgba;
    }

    /// <summary>Creates a texture with the pixels of <paramref name="bitmap"/> (converted to unpremultiplied RGBA).</summary>
    /// <param name="bitmap">The image.</param>
    /// <param name="srgb"><c>true</c> for colors, <c>false</c> for data such as normal maps.</param>
    public static Texture3D FromBitmap(SKBitmap bitmap, bool srgb = true)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var pixels = new byte[info.BytesSize];
        unsafe
        {
            fixed (byte* p = pixels)
            {
                using var pixmap = bitmap.PeekPixels();
                if (pixmap == null || !pixmap.ReadPixels(info, (IntPtr)p, info.RowBytes, 0, 0))
                {
                    throw new ArgumentException("The bitmap's pixels couldn't be read.", nameof(bitmap));
                }
            }
        }
        return new Texture3D(bitmap.Width, bitmap.Height, pixels, srgb);
    }

    /// <summary>Gets the width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets whether the pixels are sRGB colors (decoded to linear when sampled) rather than linear data.</summary>
    public bool IsSrgb { get; }

    /// <summary>Gets the pixels: RGBA, 8 bits per channel, the top row first.</summary>
    public byte[] Pixels => _pixels;

    /// <summary>Gets a number that changes whenever the pixels change.</summary>
    public int Version { get; private set; }

    /// <summary>Occurs after the pixels changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Replaces the pixels with <paramref name="rgba"/>, of the same size. The array is kept, not copied.</summary>
    /// <exception cref="ArgumentException">The array has the wrong length.</exception>
    public void Update(byte[] rgba)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (rgba.Length != _pixels.Length) throw new ArgumentException("The texture's size can't change.", nameof(rgba));
        _pixels = rgba;
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
