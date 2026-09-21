using System;
using System.Reflection.Metadata;
using CoreHotReload = Atelier.Core.HotReload.HotReloadManager;


namespace Atelier.Markup.HotReload;

public static class HotReloadManager
{
    public static event Action? HotReloadTriggered
    {
        add => CoreHotReload.HotReloadTriggered += value;
        remove => CoreHotReload.HotReloadTriggered -= value;
    }

    public static void ClearCache(Type[]? types) => CoreHotReload.ClearCache(types);

    public static void UpdateApplication(Type[]? types) => CoreHotReload.UpdateApplication(types);

    public static void TriggerHotReload() => CoreHotReload.TriggerHotReload();
}
