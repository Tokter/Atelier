using System;
using CoreHotReload = Atelier.Core.HotReload.HotReloadManager;

namespace Atelier.Markup.HotReload;

/// <summary>
/// Access to .NET Hot Reload notifications for code-built UIs; forwards to <see cref="CoreHotReload"/>. Windows rebuild
/// their content when <see cref="HotReloadTriggered"/> fires.
/// </summary>
public static class HotReloadManager
{
    /// <summary>
    /// Occurs after a hot reload update was applied (or <see cref="TriggerHotReload"/> was called). The event is static:
    /// unsubscribe when the subscriber goes away.
    /// </summary>
    public static event Action? HotReloadTriggered
    {
        add => CoreHotReload.HotReloadTriggered += value;
        remove => CoreHotReload.HotReloadTriggered -= value;
    }

    /// <summary>Called by the runtime before <see cref="UpdateApplication"/> to clear caches of the updated types.</summary>
    /// <param name="types">The updated types, or <c>null</c> if unknown.</param>
    public static void ClearCache(Type[]? types) => CoreHotReload.ClearCache(types);

    /// <summary>Called by the runtime after a metadata update has been applied; raises <see cref="HotReloadTriggered"/>.</summary>
    /// <param name="types">The updated types, or <c>null</c> if unknown.</param>
    public static void UpdateApplication(Type[]? types) => CoreHotReload.UpdateApplication(types);

    /// <summary>Raises <see cref="HotReloadTriggered"/> manually, e.g. from a "reload" command or a test.</summary>
    public static void TriggerHotReload() => CoreHotReload.TriggerHotReload();
}
