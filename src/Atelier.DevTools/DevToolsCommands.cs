using Atelier.Controls;
using Atelier.Core.Keybinding;

namespace Atelier.DevTools;

/// <summary>
/// Opens or closes the developer tools in the active window (see <see cref="DevToolsManager.ActiveHost"/>). Registered
/// by <see cref="DevToolsManager.RegisterCommands"/> as "DevTools/Toggle" with the shortcut F12.
/// </summary>
public sealed class ToggleDevToolsCommand : AtelierCommand
{
    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => DevToolsManager.ActiveHost?.Invoke() != null;

    /// <inheritdoc/>
    public override void Execute(object? parameter)
    {
        if (DevToolsManager.ActiveHost?.Invoke() is { } host) DevToolsManager.Toggle(host);
    }
}

/// <summary>
/// Starts (or stops) picking an element with the pointer in the active window, opening the developer tools if needed.
/// Registered by <see cref="DevToolsManager.RegisterCommands"/> as "DevTools/PickElement" with the shortcut Ctrl+Shift+C.
/// </summary>
public sealed class PickElementCommand : AtelierCommand
{
    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => DevToolsManager.ActiveHost?.Invoke() != null;

    /// <inheritdoc/>
    public override void Execute(object? parameter)
    {
        if (DevToolsManager.ActiveHost?.Invoke() is { } host && DevToolsManager.Open(host) is { } session)
        {
            session.IsPicking = !session.IsPicking;
        }
    }
}

public static partial class DevToolsManager
{
    /// <summary>The keybinding group of the developer tools' commands, a window group (see <see cref="KeybindingManager.WindowGroups"/>).</summary>
    public const string CommandGroup = "DevTools";

    /// <summary>Gets the command that opens or closes the tools (F12).</summary>
    public static ToggleDevToolsCommand ToggleCommand { get; } = new();

    /// <summary>Gets the command that starts picking an element (Ctrl+Shift+C).</summary>
    public static PickElementCommand PickCommand { get; } = new();

    /// <summary>
    /// Gets or sets how the commands find the window they act on, usually the active window; set by the platform (for
    /// example <c>SilkWindow</c>). <c>null</c> (or a <c>null</c> result) disables the commands.
    /// </summary>
    public static Func<IDevToolsHost?>? ActiveHost { get; set; }

    /// <summary>
    /// Registers the tools' commands in the <see cref="CommandGroup"/> window group, so their shortcuts (F12,
    /// Ctrl+Shift+C) work anywhere in a window, and menus, the command palette and the keybinding editor show them.
    /// Calling it again changes nothing.
    /// </summary>
    public static void RegisterCommands()
    {
        KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor("Toggle", CommandGroup, "F12", ToggleCommand,
            label: "_Developer tools", description: "Open or close the developer tools", icon: MaterialIcons.DeveloperMode));
        KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor("PickElement", CommandGroup, "Ctrl+Shift+C", PickCommand,
            label: "Pick an element", description: "Select an element with the pointer in the developer tools", icon: MaterialIcons.AdsClick));
        KeybindingManager.AddWindowGroup(CommandGroup);
    }
}
