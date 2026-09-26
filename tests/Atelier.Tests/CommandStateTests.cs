using System;
using System.Windows.Input;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Tests;

public class ToggleableCommand : ICommand
{
    private bool _canExecute;
    private EventHandler? _canExecuteChanged;

    public ToggleableCommand(bool canExecute) => _canExecute = canExecute;

    public int SubscriberCount => _canExecuteChanged?.GetInvocationList().Length ?? 0;

    public event EventHandler? CanExecuteChanged
    {
        add => _canExecuteChanged += value;
        remove => _canExecuteChanged -= value;
    }

    public bool CanExecuteValue
    {
        get => _canExecute;
        set { _canExecute = value; _canExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }

    public bool CanExecute(object? parameter) => _canExecute;
    public void Execute(object? parameter) { }
}

public class CommandStateTests
{
    [Fact]
    public void Button_IsDisabled_WhileCommandCannotExecute_AndContentInheritsIt()
    {
        var content = new TextBlock("Save");
        var button = new Button { Content = content, Command = new ToggleableCommand(canExecute: false) };

        Assert.False(button.IsEnabled);
        Assert.False(content.IsEnabled);
        Assert.Equal(ValueSource.Coerced, button.GetValueSource(UIElement.IsEnabledProperty));
    }

    [Fact]
    public void AttachedButton_FollowsCanExecuteChanged()
    {
        var command = new ToggleableCommand(canExecute: true);
        var root = new StackPanel();
        var button = new Button("Save") { Command = command };
        root.Add(button);
        root.AttachToHost();
        Assert.True(button.IsEnabled);

        command.CanExecuteValue = false;
        Assert.False(button.IsEnabled);

        command.CanExecuteValue = true;
        Assert.True(button.IsEnabled);
    }

    [Fact]
    public void LocalIsEnabledFalse_StaysFalse_AndReturnsAfterCommandRelease()
    {
        var command = new ToggleableCommand(canExecute: false);
        var root = new StackPanel();
        var button = new Button("Save") { IsEnabled = false, Command = command };
        root.Add(button);
        root.AttachToHost();

        command.CanExecuteValue = true;
        Assert.False(button.IsEnabled); // the app's own value still applies

        button.IsEnabled = true;
        Assert.True(button.IsEnabled);
    }

    [Fact]
    public void CommandParameterChange_ReevaluatesCanExecute()
    {
        var button = new Button("Delete") { Command = new ParameterCheckingCommand() };
        Assert.False(button.IsEnabled);

        button.CommandParameter = "item";

        Assert.True(button.IsEnabled);
    }

    [Fact]
    public void RemovedButton_IsNoLongerReferencedByCommand()
    {
        var command = new ToggleableCommand(canExecute: true);
        var root = new StackPanel();
        var button = new Button("Save") { Command = command };
        root.AttachToHost();

        root.Add(button);
        Assert.Equal(1, command.SubscriberCount);

        root.Remove(button);
        Assert.Equal(0, command.SubscriberCount);
    }

    [Fact]
    public void ReplacingCommand_MovesSubscription()
    {
        var first = new ToggleableCommand(canExecute: true);
        var second = new ToggleableCommand(canExecute: false);
        var root = new StackPanel();
        var button = new Button("Save") { Command = first };
        root.Add(button);
        root.AttachToHost();

        button.Command = second;

        Assert.Equal(0, first.SubscriberCount);
        Assert.Equal(1, second.SubscriberCount);
        Assert.False(button.IsEnabled);
    }

    private sealed class ParameterCheckingCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => parameter != null;
        public void Execute(object? parameter) { }
    }
}
