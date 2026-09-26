using System;
using Atelier.Theming;

namespace Atelier.Tests;

/// <summary>
/// Activates a theme for the duration of a test and resets it afterwards, so its styles don't leak into other tests.
/// </summary>
internal sealed class ActiveTheme : IDisposable
{
    private ActiveTheme(Theme theme)
    {
        ThemeManager.Current = theme;
    }

    /// <summary>Activates <paramref name="theme"/>; dispose the result to remove it again.</summary>
    public static ActiveTheme Use(Theme theme) => new(theme);

    public void Dispose() => ThemeManager.Reset();
}
