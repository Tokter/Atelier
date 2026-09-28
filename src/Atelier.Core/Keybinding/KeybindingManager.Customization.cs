using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Atelier.Core.Keybinding;

public static partial class KeybindingManager
{
    private static readonly Dictionary<(string Group, string Name), CommandCustomization> s_customizations = new();

    /// <summary>
    /// Occurs after the user's customizations changed (<see cref="SetCustomization"/>, <see cref="ClearCustomizations"/>,
    /// <see cref="ImportCustomizations"/>), e.g. to save them. <see cref="KeybindingsChanged"/> follows for registered
    /// commands, so controls update.
    /// </summary>
    public static event EventHandler? CustomizationsChanged;

    /// <summary>Gets the user's customizations, by command group and name; registered or not.</summary>
    public static IReadOnlyDictionary<(string Group, string Name), CommandCustomization> Customizations => s_customizations;

    /// <summary>Gets the user's changes to the command <paramref name="name"/> in <paramref name="group"/>, or <c>null</c>.</summary>
    public static CommandCustomization? GetCustomization(string group, string name) =>
        s_customizations.TryGetValue((group, name), out var customization) ? customization : null;

    /// <summary>
    /// Sets the user's changes to the command <paramref name="name"/> in <paramref name="group"/>: its label, icon or
    /// shortcut. The registered command shows them from now on (buttons, menus, tooltips and the key handling follow),
    /// and a command registered later gets them when it registers. <c>null</c> or an empty customization restores the
    /// command's own values.
    /// </summary>
    /// <exception cref="ArgumentException">The keybinding is neither empty nor a valid gesture or chord.</exception>
    public static void SetCustomization(string group, string name, CommandCustomization? customization)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(name);
        customization = Validate(customization);
        if (Equals(GetCustomization(group, name), customization)) return;

        if (customization == null) s_customizations.Remove((group, name));
        else s_customizations[(group, name)] = customization;

        if (Reapply(group, name)) OnKeybindingsChanged();
        CustomizationsChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Restores the command's own label, icon and shortcut (see <see cref="SetCustomization"/>).</summary>
    public static void ResetCustomization(string group, string name) => SetCustomization(group, name, null);

    /// <summary>Restores the own label, icon and shortcut of every command.</summary>
    public static void ClearCustomizations()
    {
        if (s_customizations.Count == 0) return;
        var keys = s_customizations.Keys.ToArray();
        s_customizations.Clear();
        bool registered = false;
        foreach (var (group, name) in keys) registered |= Reapply(group, name);
        if (registered) OnKeybindingsChanged();
        CustomizationsChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Gets the command as registered, without the user's changes (see <see cref="CustomizedKeybindingDescriptor"/>).</summary>
    public static IKeybindingDescriptor GetDefault(IKeybindingDescriptor descriptor) =>
        descriptor is CustomizedKeybindingDescriptor customized ? customized.Original : descriptor;

    /// <summary>
    /// Writes the customizations as JSON, for <see cref="ImportCustomizations"/>:
    /// <c>{ "commands": [ { "group": "Global", "name": "ToggleTheme", "keybinding": "Ctrl+Shift+T" } ] }</c>, with
    /// only the changed values of each command.
    /// </summary>
    public static string ExportCustomizations()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("commands");
            foreach (var ((group, name), customization) in s_customizations.OrderBy(c => c.Key.Group, StringComparer.Ordinal).ThenBy(c => c.Key.Name, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("group", group);
                writer.WriteString("name", name);
                if (customization.Label != null) writer.WriteString("label", customization.Label);
                if (customization.Icon != null) writer.WriteString("icon", customization.Icon);
                if (customization.Keybinding != null) writer.WriteString("keybinding", customization.Keybinding);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Reads customizations written by <see cref="ExportCustomizations"/> and applies them. Entries without a group and
    /// name, or with an invalid keybinding, are skipped.
    /// </summary>
    /// <param name="json">The JSON.</param>
    /// <param name="replace"><c>true</c> to remove the current customizations first; <c>false</c> to add to them.</param>
    /// <returns>The number of customizations read.</returns>
    /// <exception cref="JsonException"><paramref name="json"/> is not valid JSON.</exception>
    public static int ImportCustomizations(string json, bool replace = true)
    {
        ArgumentNullException.ThrowIfNull(json);
        var imported = new List<((string Group, string Name) Key, CommandCustomization Customization)>();
        using (var document = JsonDocument.Parse(json))
        {
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("commands", out var commands)
                && commands.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in commands.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object) continue;
                    string? group = String(entry, "group"), name = String(entry, "name");
                    if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(name)) continue;
                    try
                    {
                        var customization = Validate(new CommandCustomization(String(entry, "label"), String(entry, "icon"), String(entry, "keybinding")));
                        if (customization != null) imported.Add(((group, name), customization));
                    }
                    catch (ArgumentException)
                    {
                        // An invalid keybinding: skip the entry.
                    }
                }
            }
        }

        var changed = new HashSet<(string, string)>(replace ? s_customizations.Keys : Enumerable.Empty<(string, string)>());
        if (replace) s_customizations.Clear();
        foreach (var (key, customization) in imported)
        {
            s_customizations[key] = customization;
            changed.Add(key);
        }

        bool registered = false;
        foreach (var (group, name) in changed) registered |= Reapply(group, name);
        if (registered) OnKeybindingsChanged();
        if (changed.Count > 0) CustomizationsChanged?.Invoke(null, EventArgs.Empty);
        return imported.Count;

        static string? String(JsonElement entry, string property) =>
            entry.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    // The registered form of a descriptor: with the user's customization, if there is one.
    private static IKeybindingDescriptor ApplyCustomization(IKeybindingDescriptor descriptor)
    {
        var original = GetDefault(descriptor);
        return s_customizations.TryGetValue((original.Group, original.Name), out var customization)
            ? new CustomizedKeybindingDescriptor(original, customization)
            : original;
    }

    // Re-registers the command with its current customization; false if it isn't registered.
    private static bool Reapply(string group, string name)
    {
        if (!RegisteredKeybindings.TryGetValue(group, out var byName) || !byName.TryGetValue(name, out var registered))
        {
            return false;
        }
        var applied = ApplyCustomization(registered);
        byName[name] = applied;
        WarmGestureCache(applied.Keybinding);
        ReportConflicts(applied);
        return true;
    }

    // Normalizes the keybinding; null for a customization that changes nothing.
    private static CommandCustomization? Validate(CommandCustomization? customization)
    {
        if (customization == null || customization.IsEmpty) return null;
        if (customization.Keybinding is { Length: > 0 } keybinding)
        {
            if (!KeybindingGesture.TryParseSequence(keybinding, out _))
            {
                throw new ArgumentException($"'{keybinding}' is not a valid keybinding.", nameof(customization));
            }
            customization = customization with { Keybinding = KeybindingGesture.Normalize(keybinding) };
        }
        return customization;
    }
}
