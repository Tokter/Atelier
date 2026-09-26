using System;
using System.Windows.Input;

namespace Atelier.Core.Keybinding;

/// <summary>
/// Base class for commands implemented as classes, such as those discovered through <see cref="KeybindingAttribute"/>.
/// </summary>
public abstract class AtelierCommand : ICommand
{
    /// <inheritdoc/>
    /// <remarks>
    /// This event keeps its subscribers alive for as long as the command lives. Long-lived commands
    /// (for example registered keybindings) should only be observed by objects that unsubscribe.
    /// </remarks>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Determines whether the command can run for <paramref name="parameter"/>. Returns <c>true</c> unless overridden.
    /// </summary>
    public virtual bool CanExecute(object? parameter)
    {
        return true;
    }

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="parameter">The command parameter, for keybindings typically the target object.</param>
    public abstract void Execute(object? parameter);

    /// <summary>
    /// Raises <see cref="CanExecuteChanged"/> so that bound controls re-query <see cref="CanExecute"/>.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// An <see cref="ICommand"/> that delegates to an action and an optional can-execute predicate.
/// </summary>
public class AtelierRelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>
    /// Creates a command that ignores its parameter.
    /// </summary>
    /// <param name="execute">The action to run.</param>
    /// <param name="canExecute">An optional predicate; the command can always run when <c>null</c>.</param>
    public AtelierRelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }

    /// <summary>
    /// Creates a command that receives its parameter.
    /// </summary>
    /// <param name="execute">The action to run.</param>
    /// <param name="canExecute">An optional predicate; the command can always run when <c>null</c>.</param>
    public AtelierRelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    /// <inheritdoc/>
    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    /// <summary>
    /// Raises <see cref="CanExecuteChanged"/> so that bound controls re-query <see cref="CanExecute"/>.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
