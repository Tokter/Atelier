using System;
using System.IO;
using Atelier.Controls;
using SkiaSharp;

namespace Atelier.Gallery.Views;

/// <summary>
/// Sample images for the gallery pages, loaded once and shared (an <see cref="Image"/> doesn't dispose a source it was
/// given).
/// </summary>
internal static class SampleImages
{
    private static readonly Lazy<SKImage?> _logo = new(() => Load("Assets/Logo.png"));
    private static readonly Lazy<SKImage?> _appIcon = new(() => Load("Assets/Icons/Atelier.png"));

    /// <summary>The 1254×1254 Atelier logo.</summary>
    public static SKImage? Logo => _logo.Value;

    /// <summary>The 64×64 application icon.</summary>
    public static SKImage? AppIcon => _appIcon.Value;

    // The assets live in the repository root. Image.LoadImage searches only a few parent directories, which doesn't
    // reach it from bin/Debug/net9.0, so walk up from the output directory here.
    private static SKImage? Load(string relativePath)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return Image.LoadImage(candidate);
            }
        }
        return Image.LoadImage(relativePath);
    }
}
