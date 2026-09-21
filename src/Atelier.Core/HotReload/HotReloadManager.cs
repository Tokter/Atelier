using System;
using System.Reflection.Metadata;
using Atelier.Core.HotReload;

[assembly: MetadataUpdateHandler(typeof(HotReloadManager))]

namespace Atelier.Core.HotReload;

public static class HotReloadManager
{
    public static event Action? HotReloadTriggered;
    public static event Action<Type[]?>? ApplicationUpdated;

    public static void ClearCache(Type[]? types)
    {
    }

    public static void UpdateApplication(Type[]? types)
    {
        Console.WriteLine("[HotReload] Metadata update received from runtime.");
        HotReloadTriggered?.Invoke();
        ApplicationUpdated?.Invoke(types);
    }

    public static void TriggerHotReload()
    {
        Console.WriteLine("[HotReload] Manual hot reload triggered.");
        HotReloadTriggered?.Invoke();
        ApplicationUpdated?.Invoke(null);
    }
}
