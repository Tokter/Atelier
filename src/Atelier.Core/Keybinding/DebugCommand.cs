using System;
using System.Windows.Input;

namespace Atelier.Core.Keybinding;

[Keybinding(name: "Debug", group: "Global", defaultKeybinding: "F12")]
public class DebugCommand : AtelierCommand
{
    public override void Execute(object? parameter)
    {
    }
}
