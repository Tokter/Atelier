using System;
using System.Text;
using System.Windows.Input;

namespace Atelier.Core.Keybinding;

/// <summary>
/// A statically registered descriptor of a command, wrapping an <see cref="ICommand"/> instance with its identity,
/// presentation and keybinding. Compatible with Native AOT without runtime reflection.
/// Normalizes keybinding gestures to canonical modifier order upon creation.
/// </summary>
public sealed class KeybindingDescriptor : IKeybindingDescriptor
{
    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Group { get; }

    /// <inheritdoc />
    public string Keybinding { get; }

    /// <summary>
    /// Gets the underlying <see cref="ICommand"/> instance.
    /// </summary>
    public ICommand Command { get; }

    /// <inheritdoc />
    public string Label { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public string Icon { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeybindingDescriptor"/> class.
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <param name="group">The logical category group of the command.</param>
    /// <param name="keybinding">The keyboard shortcut; normalized with <see cref="KeybindingGesture.Normalize"/>.</param>
    /// <param name="command">The command to execute.</param>
    /// <param name="label">The text shown for the command; <see langword="null"/> or empty makes one from <paramref name="name"/> (see <see cref="LabelFromName"/>).</param>
    /// <param name="description">What the command does, or <see langword="null"/>.</param>
    /// <param name="icon">The name of the command's icon, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="group"/> or <paramref name="command"/> is <see langword="null"/>.</exception>
    public KeybindingDescriptor(string name, string group, string keybinding, ICommand command,
        string? label = null, string? description = null, string? icon = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Group = group ?? throw new ArgumentNullException(nameof(group));
        Keybinding = KeybindingGesture.Normalize(keybinding);
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Label = string.IsNullOrWhiteSpace(label) ? LabelFromName(name) : label;
        Description = description ?? string.Empty;
        Icon = icon ?? string.Empty;
    }

    /// <summary>
    /// Makes a label from a command name: words split at capitals, underscores and hyphens, and all but the first
    /// word lowercased unless it is an acronym ("ToggleTheme" becomes "Toggle theme", "OpenURL" becomes "Open URL").
    /// </summary>
    public static string LabelFromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var words = new StringBuilder();
        var word = new StringBuilder();

        void Flush()
        {
            if (word.Length == 0) return;
            if (words.Length > 0) words.Append(' ');
            bool acronym = word.Length > 1 && word.ToString().AsSpan().IndexOfAnyExceptInRange('A', 'Z') < 0;
            words.Append(words.Length == 0 || acronym ? word.ToString() : word.ToString().ToLowerInvariant());
            word.Clear();
        }

        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c is '_' or '-' or ' ')
            {
                Flush();
                continue;
            }

            // A capital starts a word, except inside an acronym ("URL" stays together until "URLList" reaches "List").
            if (char.IsUpper(c) && word.Length > 0)
            {
                bool previousUpper = char.IsUpper(word[^1]);
                bool nextLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (!previousUpper || nextLower) Flush();
            }
            word.Append(c);
        }
        Flush();

        if (words.Length > 0) words[0] = char.ToUpperInvariant(words[0]);
        return words.ToString();
    }
}
