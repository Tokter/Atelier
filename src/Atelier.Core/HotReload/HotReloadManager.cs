using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using Atelier.Core.HotReload;

[assembly: MetadataUpdateHandler(typeof(HotReloadManager))]

namespace Atelier.Core.HotReload;

/// <summary>
/// Receives .NET Hot Reload notifications (via <see cref="MetadataUpdateHandlerAttribute"/>) and forwards them to the UI,
/// so that windows can rebuild their content after code changes.
/// </summary>
/// <remarks>
/// The events are static: subscribers stay alive until they unsubscribe, so windows should unsubscribe when closed.
/// </remarks>
public static class HotReloadManager
{
    /// <summary>
    /// Occurs after a hot reload update was applied (or <see cref="TriggerHotReload"/> was called).
    /// </summary>
    public static event Action? HotReloadTriggered;

    /// <summary>
    /// Occurs after a hot reload update was applied, with the updated types (<c>null</c> if unknown).
    /// </summary>
    public static event Action<Type[]?>? ApplicationUpdated;

    /// <summary>
    /// Called by the runtime before <see cref="UpdateApplication"/> to clear caches of the updated types.
    /// Atelier keeps no per-type caches that need clearing, so this does nothing.
    /// </summary>
    /// <param name="types">The updated types, or <c>null</c> if unknown.</param>
    public static void ClearCache(Type[]? types)
    {
    }

    /// <summary>
    /// Called by the runtime after a metadata update has been applied.
    /// </summary>
    /// <param name="types">The updated types, or <c>null</c> if unknown.</param>
    public static void UpdateApplication(Type[]? types)
    {
        Debug.WriteLine("[HotReload] Metadata update received from runtime.");
        HotReloadTriggered?.Invoke();
        ApplicationUpdated?.Invoke(types);
    }

    /// <summary>
    /// Raises the hot reload events manually, e.g. from a debug keybinding.
    /// </summary>
    public static void TriggerHotReload()
    {
        Debug.WriteLine("[HotReload] Manual hot reload triggered.");
        HotReloadTriggered?.Invoke();
        ApplicationUpdated?.Invoke(null);
    }
}
