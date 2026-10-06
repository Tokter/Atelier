using Atelier.Graphics3D.Rendering;
using Atelier.Theming;

namespace Atelier.Graphics3D;

/// <summary>Adds the 3D controls' renderers to every theme.</summary>
public static class Graphics3DTheme
{
    /// <summary>Registers the renderers with all current and future themes (controls call it when first used).</summary>
    public static void Register() => ThemeExtensions.Register(Extend);

    /// <summary>Adds the 3D controls' renderers to <paramref name="theme"/>.</summary>
    public static void Extend(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        theme.Renderers.Register(new Viewport3DRenderer());
    }
}
