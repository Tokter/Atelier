using System;
using System.Windows.Input;

namespace Atelier.Core.Keybinding;

/// <summary>
/// An <see cref="ICommand"/> adapter that delegates execution of an instance property command
/// on a target of type <typeparamref name="TTarget"/> provided as the command parameter.
/// </summary>
/// <typeparam name="TTarget">The target type containing the command property.</typeparam>
public sealed class PropertyKeybindingCommand<TTarget> : ICommand where TTarget : class
{
    private readonly Func<TTarget, ICommand?> _commandGetter;
    private readonly string _name;

    public event EventHandler? CanExecuteChanged;

    public PropertyKeybindingCommand(string name, Func<TTarget, ICommand?> commandGetter)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _commandGetter = commandGetter ?? throw new ArgumentNullException(nameof(commandGetter));
    }

    public bool CanExecute(object? parameter)
    {
        if (parameter is not TTarget target)
            return false;

        var cmd = _commandGetter(target);
        return cmd?.CanExecute(null) ?? false;
    }

    public void Execute(object? parameter)
    {
        if (parameter is not TTarget target)
        {
            throw new InvalidOperationException(
                $"Cannot execute keybinding '{_name}': expected target instance of type '{typeof(TTarget).Name}', but received '{(parameter == null ? "null" : parameter.GetType().Name)}'.");
        }

        var cmd = _commandGetter(target);
        if (cmd == null)
        {
            throw new InvalidOperationException(
                $"Cannot execute keybinding '{_name}': command property on '{typeof(TTarget).Name}' returned null.");
        }

        cmd.Execute(null);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
