namespace Atelier.Core.Keybinding;

/// <summary>
/// Placeholder "Global/Debug" command bound to F12. It is the only <see cref="KeybindingAttribute"/> class in
/// Atelier.Core, so it exercises the keybinding source generator for this assembly (the generated
/// <c>KeybindingManager.Initialize()</c> and <c>KeybindingManager.GlobalDebugKeybinding</c>).
/// </summary>
/// <remarks>
/// It does nothing when executed and is only registered if an app calls <c>KeybindingManager.Initialize()</c>.
/// </remarks>
[Keybinding(name: "Debug", group: "Global", defaultKeybinding: "F12")]
public class DebugCommand : AtelierCommand
{
    /// <summary>Does nothing.</summary>
    public override void Execute(object? parameter)
    {
    }
}
