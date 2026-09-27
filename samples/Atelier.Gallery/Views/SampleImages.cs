using System;
using Atelier.Controls;
using SkiaSharp;

namespace Atelier.Gallery.Views;

/// <summary>
/// Sample images for the gallery pages, loaded once and shared (an <see cref="Image"/> doesn't dispose a source it was
/// given). The assets live in the repository root; <see cref="Image.LoadImage"/> finds them from the bin folder.
/// </summary>
internal static class SampleImages
{
    private static readonly Lazy<SKImage?> _logo = new(() => Image.LoadImage("Assets/Logo.png"));
    private static readonly Lazy<SKImage?> _appIcon = new(() => Image.LoadImage("Assets/Icons/Atelier.png"));

    /// <summary>The 1254×1254 Atelier logo.</summary>
    public static SKImage? Logo => _logo.Value;

    /// <summary>The 64×64 application icon.</summary>
    public static SKImage? AppIcon => _appIcon.Value;
}
