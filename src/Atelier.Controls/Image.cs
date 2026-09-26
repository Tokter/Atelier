using System;
using System.IO;
using System.Reflection;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using SkiaSharp;

namespace Atelier.Controls;

public class Image : Control
{
    public static readonly BindableProperty<SKImage?> SourceProperty =
        BindableProperty.Register<Image, SKImage?>(
            nameof(Source),
            null,
            (s, o, n) =>
            {
                var img = (Image)s;
                img.InvalidateMeasure();
                img.InvalidateVisual();
            });

    public static readonly BindableProperty<Stretch> StretchProperty =
        BindableProperty.Register<Image, Stretch>(
            nameof(Stretch),
            Stretch.Uniform,
            options: PropertyOptions.AffectsRender);

    public SKImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public Image() { }

    public Image(SKImage source)
    {
        Source = source;
    }

    public Image(SKBitmap bitmap)
    {
        Source = SKImage.FromBitmap(bitmap);
    }

    public Image(string pathOrResource)
    {
        Source = LoadImage(pathOrResource);
    }

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

    public static SKBitmap? LoadBitmap(string pathOrResource)
    {
        var img = LoadImage(pathOrResource);
        return img != null ? SKBitmap.FromImage(img) : null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Source == null)
            return Size.Zero;

        float srcW = Source.Width;
        float srcH = Source.Height;

        if (float.IsInfinity(availableSize.Width) && float.IsInfinity(availableSize.Height))
            return new Size(srcW, srcH);

        switch (Stretch)
        {
            case Stretch.None:
                return new Size(srcW, srcH);

            case Stretch.Fill:
                return new Size(
                    float.IsInfinity(availableSize.Width) ? srcW : availableSize.Width,
                    float.IsInfinity(availableSize.Height) ? srcH : availableSize.Height);

            case Stretch.Uniform:
            default:
                if (float.IsInfinity(availableSize.Width))
                {
                    float scale = availableSize.Height / srcH;
                    return new Size(srcW * scale, availableSize.Height);
                }
                if (float.IsInfinity(availableSize.Height))
                {
                    float scale = availableSize.Width / srcW;
                    return new Size(availableSize.Width, srcH * scale);
                }
                float scaleX = availableSize.Width / srcW;
                float scaleY = availableSize.Height / srcH;
                float minScale = Math.Min(scaleX, scaleY);
                return new Size(srcW * minScale, srcH * minScale);

            case Stretch.UniformToFill:
                if (float.IsInfinity(availableSize.Width) || float.IsInfinity(availableSize.Height))
                    return new Size(srcW, srcH);
                float maxScale = Math.Max(availableSize.Width / srcW, availableSize.Height / srcH);
                return new Size(srcW * maxScale, srcH * maxScale);
        }
    }
}