using System;
using System.Collections.Generic;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Threading;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>What a button shows of its command when it has no <see cref="ContentControl.Content"/> of its own.</summary>
public enum CommandDisplay
{
    /// <summary>The command's icon before its label (only the label if it has no icon).</summary>
    IconAndLabel,

    /// <summary>Only the icon (the label if it has no icon); the tooltip then names the command.</summary>
    Icon,

    /// <summary>Only the label.</summary>
    Label,
}

public abstract partial class ButtonBase
{
    /// <summary>Identifies the <see cref="CommandDisplay"/> property.</summary>
    public static readonly BindableProperty<CommandDisplay> CommandDisplayProperty =
        BindableProperty.Register<ButtonBase, CommandDisplay>(nameof(CommandDisplay), CommandDisplay.IconAndLabel,
            (s, o, n) => ((ButtonBase)s).RebuildCommandContent());

    /// <summary>The size of a command's icon next to its label.</summary>
    protected const float CommandIconSize = 18f;

    /// <summary>The size of a command's icon shown alone.</summary>
    protected const float CommandIconOnlySize = 20f;

    private UIElement? _commandContent;
    private string? _commandContentKey;
    private bool _commandLabelShown;
    private bool _isObservingRegistrations;
    private Action? _updateCommandInfo;

    /// <summary>
    /// Gets or sets what the control shows of a registered <see cref="Command"/> (see <see cref="CommandInfo"/>) while it
    /// has no <see cref="ContentControl.Content"/> of its own. The default is <see cref="Controls.CommandDisplay.IconAndLabel"/>.
    /// </summary>
    public CommandDisplay CommandDisplay { get => GetValue(CommandDisplayProperty); set => SetValue(CommandDisplayProperty, value); }

    /// <summary>
    /// Gets the registration of <see cref="Command"/> (declared with <c>[Command]</c> or <c>[Keybinding]</c>): its label,
    /// description, icon and shortcut; <c>null</c> if the command isn't registered.
    /// </summary>
    /// <remarks>
    /// Without <see cref="ContentControl.Content"/> of its own the control shows the command's icon and label (see
    /// <see cref="CommandDisplay"/>), and without a tooltip of its own it shows <see cref="CommandToolTip"/>. Updated when
    /// the command, the DataContext or the registrations change.
    /// </remarks>
    public IKeybindingDescriptor? CommandInfo { get; private set; }

    /// <summary>
    /// Gets the tooltip made from <see cref="CommandInfo"/>, shown while no tooltip is set: the command's description (or
    /// its label when only the icon shows, with the description on a second line) and its shortcut, e.g.
    /// <c>"Switch between the light and dark theme (Ctrl+T)"</c>; <c>null</c> if there's nothing to show.
    /// </summary>
    public virtual string? CommandToolTip
    {
        get
        {
            if (CommandInfo is not { } info) return null;
            bool labelShown = Content != null || _commandLabelShown;
            string label = AccessText.Parse(info.Label, out _);
            string gesture = info.Keybinding.Length > 0 ? KeybindingGesture.FormatForDisplay(info.Keybinding) : string.Empty;

            string first = labelShown ? info.Description : label;
            if (gesture.Length > 0) first = first.Length > 0 ? $"{first} ({gesture})" : gesture;
            string? second = !labelShown && info.Description.Length > 0 ? info.Description : null;
            string text = second != null ? $"{first}\n{second}" : first;
            return text.Length > 0 ? text : null;
        }
    }

    /// <summary>
    /// Gets whether the control shows its command's icon and label while it has no content. <c>true</c> by default;
    /// controls that present the command elsewhere (such as a menu item's header) return <c>false</c>.
    /// </summary>
    protected virtual bool ShowsCommandContent => true;

    /// <inheritdoc/>
    protected override object? DisplayedContent => Content ?? _commandContent;

    /// <summary>
    /// Gets the keybinding group whose registration of a command wins when it has several: the group of the nearest
    /// <see cref="KeybindingHandler"/> around the control.
    /// </summary>
    protected virtual string? CommandGroup
    {
        get
        {
            for (VisualNode? node = Parent; node != null; node = node.Parent)
            {
                if (node is KeybindingHandler handler) return handler.Group;
            }
            return null;
        }
    }

    /// <summary>
    /// Gets the objects <see cref="Command"/> may belong to, for commands registered on a view model's command property:
    /// the DataContexts from the control up.
    /// </summary>
    protected virtual IEnumerable<object?> CommandTargets()
    {
        object? last = null;
        for (VisualNode? node = this; node != null; node = node.Parent)
        {
            if (node is UIElement { DataContext: { } dc } && !ReferenceEquals(dc, last))
            {
                last = dc;
                yield return dc;
            }
        }
    }

    /// <summary>Called when <see cref="CommandInfo"/> changed or its registration was updated. The base implementation does nothing.</summary>
    protected virtual void OnCommandInfoChanged()
    {
    }

    /// <summary>Looks up <see cref="CommandInfo"/> again, e.g. after the objects <see cref="CommandTargets"/> returns changed.</summary>
    protected void UpdateCommandInfo()
    {
        CommandInfo = Command is { } command ? KeybindingManager.FindCommand(command, CommandGroup, CommandTargets()) : null;
        RebuildCommandContent();
        OnCommandInfoChanged();
    }

    private void ObserveCommandRegistrations(bool observe)
    {
        if (observe == _isObservingRegistrations) return;
        _isObservingRegistrations = observe;
        if (observe) KeybindingManager.KeybindingsChanged += OnCommandRegistrationsChanged;
        else KeybindingManager.KeybindingsChanged -= OnCommandRegistrationsChanged;
    }

    private void OnCommandRegistrationsChanged(object? sender, EventArgs e)
    {
        if (Command == null) return;
        if (Dispatcher.CheckAccess()) UpdateCommandInfo();
        else Dispatcher.Post(_updateCommandInfo ??= UpdateCommandInfo);
    }

    // Builds the icon and label shown while there is no content; unchanged presentation keeps the current elements.
    private void RebuildCommandContent()
    {
        var info = ShowsCommandContent ? CommandInfo : null;
        bool icon = info != null && CommandDisplay != CommandDisplay.Label && IconSource.IsValid(info.Icon);
        bool label = info != null && (CommandDisplay != CommandDisplay.Icon || !icon);
        string? key = info == null ? null : $"{(icon ? info.Icon : null)}\n{(label ? info.Label : null)}";
        _commandLabelShown = label;
        if (key == _commandContentKey) return;
        _commandContentKey = key;

        UIElement? content = null;
        if (info != null)
        {
            var iconElement = icon ? Icon.FromSource(info.Icon, label ? CommandIconSize : CommandIconOnlySize) : null;
            var text = label ? new TextBlock(AccessText.Parse(info.Label, out _)) : null;
            if (iconElement != null) iconElement.VerticalAlignment = VerticalAlignment.Center;
            if (text != null) text.VerticalAlignment = VerticalAlignment.Center;

            if (iconElement != null && text != null)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
                row.Add(iconElement);
                row.Add(text);
                content = row;
            }
            else
            {
                content = (UIElement?)iconElement ?? text;
            }
        }

        _commandContent = content;
        if (Content == null) UpdateContentDisplay();
    }
}
