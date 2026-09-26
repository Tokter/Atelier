using System;
using System.IO;
using System.Reflection;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using SkiaSharp;

namespace Atelier.Controls;

/// <summary>
/// Specifies in which direction content may be scaled by a <see cref="Stretch"/> mode.
/// </summary>
public enum StretchDirection
{
    /// <summary>The content is scaled up or down as the stretch mode requires.</summary>
    Both = 0,
    /// <summary>The content is only scaled up; content larger than the space keeps its natural size.</summary>
    UpOnly,
    /// <summary>The content is only scaled down; content smaller than the space keeps its natural size.</summary>
    DownOnly
}

/// <summary>
/// Displays an <see cref="SKImage"/>, scaled according to <see cref="Stretch"/> and <see cref="StretchDirection"/>.
/// </summary>
/// <remarks>
/// <para>
/// The image is laid out inside <see cref="Control.Padding"/> and drawn clipped to that area, so
/// <see cref="Stretch.UniformToFill"/> crops overflow. Within its bounds the image is positioned by the element's
/// <see cref="UIElement.HorizontalAlignment"/> and <see cref="UIElement.VerticalAlignment"/> (centered when stretched).
/// </para>
/// <para>
/// Images created by the control itself (the <see cref="Image(SKBitmap)"/> and <see cref="Image(string)"/> constructors)
/// are disposed when <see cref="Source"/> is replaced; images you assign are never disposed by the control.
/// </para>
/// </remarks>
public class Image : Control
{
    /// <summary>Identifies the <see cref="Source"/> property.</summary>
    public static readonly BindableProperty<SKImage?> SourceProperty =
        BindableProperty.Register<Image, SKImage?>(
            nameof(Source),
            null,
            (s, o, n) => ((Image)s).OnSourceChanged(o, n),
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="Stretch"/> property.</summary>
    public static readonly BindableProperty<Stretch> StretchProperty =
        BindableProperty.Register<Image, Stretch>(
            nameof(Stretch),
            Stretch.Uniform,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="StretchDirection"/> property.</summary>
    public static readonly BindableProperty<StretchDirection> StretchDirectionProperty =
        BindableProperty.Register<Image, StretchDirection>(
            nameof(StretchDirection),
            StretchDirection.Both,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender);

    /// <summary>Gets or sets the image to display, or <c>null</c> for none. The default is <c>null</c>.</summary>
    public SKImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Gets or sets how the image is resized to fill the available space. The default is <see cref="Stretch.Uniform"/>.</summary>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>
    /// Gets or sets whether <see cref="Stretch"/> may scale the image up, down or both. The default is
    /// <see cref="StretchDirection.Both"/>.
    /// </summary>
    public StretchDirection StretchDirection
    {
        get => GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    // An image this control created from a bitmap or file, disposed when it is replaced.
    private SKImage? _ownedSource;

    /// <summary>Initializes an image control without a source.</summary>
    public Image() { }

    /// <summary>Initializes an image control showing <paramref name="source"/>, which the control does not dispose.</summary>
    /// <param name="source">The image to show.</param>
    public Image(SKImage source)
    {
        Source = source;
    }

    /// <summary>
    /// Initializes an image control showing a snapshot of <paramref name="bitmap"/>. The created image is owned (and
    /// disposed on replacement) by the control; the bitmap is not.
    /// </summary>
    /// <param name="bitmap">The bitmap to show.</param>
    public Image(SKBitmap bitmap)
    {
        SetOwnedSource(SKImage.FromBitmap(bitmap));
    }

    /// <summary>
    /// Initializes an image control showing the image loaded with <see cref="LoadImage"/>. The loaded image is owned (and
    /// disposed on replacement) by the control. Shows nothing if the image can't be found.
    /// </summary>
    /// <param name="pathOrResource">A file path or embedded resource name.</param>
    public Image(string pathOrResource)
    {
        SetOwnedSource(LoadImage(pathOrResource));
    }

    private void SetOwnedSource(SKImage? image)
    {
        Source = image;
        _ownedSource = image;
    }

    private void OnSourceChanged(SKImage? oldValue, SKImage? newValue)
    {
        if (oldValue != null && ReferenceEquals(oldValue, _ownedSource) && !ReferenceEquals(oldValue, newValue))
        {
            _ownedSource = null;
            oldValue.Dispose();
        }
    }

    /// <summary>
    /// Loads an image from a file path (absolute, relative to the working directory, to the application directory or to
    /// one of its parent directories) or from an embedded resource with that file name. The caller owns the result.
    /// </summary>
    /// <param name="pathOrResource">The path or resource file name.</param>
    /// <returns>The decoded image, or <c>null</c> if it can't be found or decoded.</returns>
    public static SKImage? LoadImage(string pathOrResource)
    {
        if (string.IsNullOrWhiteSpace(pathOrResource)) return null;

        // 1. Direct file path
        if (File.Exists(pathOrResource))
        {
            using var stream = File.OpenRead(pathOrResource);
            return SKImage.FromEncodedData(stream);
        }

        // 2. Relative to AppDomain base directory
        string baseRelative = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pathOrResource);
        if (File.Exists(baseRelative))
        {
            using var stream = File.OpenRead(baseRelative);
            return SKImage.FromEncodedData(stream);
        }

        // 3. Search up parent directories
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 6 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, pathOrResource);
            if (File.Exists(candidate))
            {
                using var stream = File.OpenRead(candidate);
                return SKImage.FromEncodedData(stream);
            }
            dir = Path.GetDirectoryName(dir);
        }

        // 4. Embedded resource across assemblies
        string fileName = Path.GetFileName(pathOrResource);
        var assemblies = new[]
        {
            typeof(Image).Assembly,
            Assembly.GetEntryAssembly(),
            Assembly.GetExecutingAssembly()
        };

        foreach (var asm in assemblies)
        {
            if (asm == null) continue;
            try
            {
                var names = asm.GetManifestResourceNames();
                string? resName = Array.Find(names, n => n.Equals(fileName, StringComparison.OrdinalIgnoreCase) || n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
                if (resName != null)
                {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream != null)
                    {
                        return SKImage.FromEncodedData(stream);
                    }
                }
            }
            catch { }
        }

        return null;
    }

    /// <summary>
    /// Loads an image like <see cref="LoadImage"/> and returns it as a bitmap. The caller owns the result.
    /// </summary>
    /// <param name="pathOrResource">The path or resource file name.</param>
    /// <returns>The decoded bitmap, or <c>null</c> if it can't be found or decoded.</returns>
    public static SKBitmap? LoadBitmap(string pathOrResource)
    {
        using var img = LoadImage(pathOrResource);
        return img != null ? SKBitmap.FromImage(img) : null;
    }

    /// <summary>
    /// Computes the horizontal and vertical scale factors for showing content of <paramref name="contentSize"/> in
    /// <paramref name="availableSize"/> (WPF semantics): an infinite dimension follows the scale of the other one, both
    /// infinite (or <see cref="Stretch.None"/>) gives 1, and <paramref name="direction"/> limits the result.
    /// </summary>
    /// <param name="availableSize">The space to fill; dimensions may be infinite.</param>
    /// <param name="contentSize">The natural size of the content.</param>
    /// <param name="stretch">The stretch mode.</param>
    /// <param name="direction">The allowed scaling direction.</param>
    /// <returns>The scale factors as a size (width = x scale, height = y scale).</returns>
    public static Size ComputeScale(Size availableSize, Size contentSize, Stretch stretch, StretchDirection direction)
    {
        float scaleX = 1f;
        float scaleY = 1f;
        bool constrainedWidth = !float.IsPositiveInfinity(availableSize.Width);
        bool constrainedHeight = !float.IsPositiveInfinity(availableSize.Height);

        if (stretch != Stretch.None && (constrainedWidth || constrainedHeight))
        {
            scaleX = contentSize.Width <= 0 ? 0f : availableSize.Width / contentSize.Width;
            scaleY = contentSize.Height <= 0 ? 0f : availableSize.Height / contentSize.Height;

            if (!constrainedWidth)
            {
                scaleX = scaleY;
            }
            else if (!constrainedHeight)
            {
                scaleY = scaleX;
            }
            else if (stretch == Stretch.Uniform)
            {
                scaleX = scaleY = Math.Min(scaleX, scaleY);
            }
            else if (stretch == Stretch.UniformToFill)
            {
                scaleX = scaleY = Math.Max(scaleX, scaleY);
            }

            if (direction == StretchDirection.UpOnly)
            {
                scaleX = Math.Max(scaleX, 1f);
                scaleY = Math.Max(scaleY, 1f);
            }
            else if (direction == StretchDirection.DownOnly)
            {
                scaleX = Math.Min(scaleX, 1f);
                scaleY = Math.Min(scaleY, 1f);
            }
        }

        return new Size(scaleX, scaleY);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var source = Source;
        if (source == null)
            return Size.Zero;

        var padding = Padding;
        var inner = new Size(
            Math.Max(0f, availableSize.Width - padding.Horizontal),
            Math.Max(0f, availableSize.Height - padding.Vertical));
        var natural = new Size(source.Width, source.Height);
        var scale = ComputeScale(inner, natural, Stretch, StretchDirection);

        return new Size(natural.Width * scale.Width + padding.Horizontal, natural.Height * scale.Height + padding.Vertical);
    }
}
