using Atelier.Audio.Renderers;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Audio;

/// <summary>
/// Adds the audio controls' renderers to themes (see <see cref="ThemeExtensions"/>): Material themes get the Material
/// renderers. The audio controls register it themselves.
/// </summary>
public static class AudioTheme
{
    /// <summary>Registers the extension; calling it again does nothing.</summary>
    public static void Register() => ThemeExtensions.Register(Extend);

    /// <summary>Adds the renderers to <paramref name="theme"/>.</summary>
    public static void Extend(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (theme is MaterialTheme material)
        {
            theme.Renderers.Register(new MaterialKnobRenderer(material.Colors));
        }
    }
}
