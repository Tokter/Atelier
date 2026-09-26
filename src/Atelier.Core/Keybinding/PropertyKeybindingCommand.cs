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

    /// <inheritdoc/>
    /// <remarks>
    /// Raised only by <see cref="RaiseCanExecuteChanged"/>; changes reported by the target's own command are not forwarded,
    /// because the inner command depends on the target passed at execution time, so there is no single command to observe.
    /// </remarks>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyKeybindingCommand{TTarget}"/> class.
    /// </summary>
    /// <param name="name">The keybinding name, used in error messages.</param>
    /// <param name="commandGetter">Returns the command property of a target instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="commandGetter"/> is <see langword="null"/>.</exception>
    public PropertyKeybindingCommand(string name, Func<TTarget, ICommand?> commandGetter)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _commandGetter = commandGetter ?? throw new ArgumentNullException(nameof(commandGetter));
    }

    /// <summary>
    /// Determines whether the target's command can execute.
    /// </summary>
    /// <param name="parameter">The target instance of type <typeparamref name="TTarget"/>.</param>
    /// <returns>
    /// The result of the target command's <see cref="ICommand.CanExecute"/> (called with a <see langword="null"/> parameter);
    /// <see langword="false"/> if <paramref name="parameter"/> is not a <typeparamref name="TTarget"/> or the command is <see langword="null"/>.
    /// </returns>
    public bool CanExecute(object? parameter)
    {
        if (parameter is not TTarget target)
            return false;

        var cmd = _commandGetter(target);
        return cmd?.CanExecute(null) ?? false;
    }

    /// <summary>
    /// Executes the target's command with a <see langword="null"/> parameter.
    /// </summary>
    /// <param name="parameter">The target instance of type <typeparamref name="TTarget"/>.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="parameter"/> is not a <typeparamref name="TTarget"/>, or the target's command property is <see langword="null"/>.
    /// </exception>
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

    /// <summary>
    /// Raises <see cref="CanExecuteChanged"/>.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
