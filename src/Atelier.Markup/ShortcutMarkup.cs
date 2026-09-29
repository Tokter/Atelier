using System;
using Atelier.Controls;

namespace Atelier.Markup;

/// <summary>Fluent methods for <see cref="ShortcutView"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ShortcutViewMarkup
{
    /// <summary>Sets the shortcut shown as key caps, e.g. <c>"Ctrl+K, Ctrl+C"</c>.</summary>
    public static T Shortcut<T>(this T view, string? shortcut) where T : ShortcutView => view.Set(ShortcutView.ShortcutProperty, shortcut);
}

/// <summary>Fluent methods for <see cref="ShortcutRecorder"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ShortcutRecorderMarkup
{
    /// <summary>Sets the shortcut; recording replaces it.</summary>
    public static T Shortcut<T>(this T recorder, string? shortcut) where T : ShortcutRecorder => recorder.Set(ShortcutRecorder.ShortcutProperty, shortcut);

    /// <summary>Sets the text shown when there is no shortcut.</summary>
    public static T Placeholder<T>(this T recorder, string placeholder) where T : ShortcutRecorder => recorder.Set(ShortcutRecorder.PlaceholderProperty, placeholder);

    /// <summary>Handles <see cref="ShortcutRecorder.ShortcutRecorded"/>, raised with the new shortcut when a recording is accepted.</summary>
    public static T OnShortcutRecorded<T>(this T recorder, Action<string> handler) where T : ShortcutRecorder
    {
        recorder.ShortcutRecorded += (_, shortcut) => handler(shortcut);
        return recorder;
    }
}

/// <summary>Fluent methods for <see cref="KeybindingEditor"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class KeybindingEditorMarkup
{
    /// <summary>Sets the search: only matching commands are listed.</summary>
    public static T SearchText<T>(this T editor, string searchText) where T : KeybindingEditor => editor.Set(KeybindingEditor.SearchTextProperty, searchText);
}
