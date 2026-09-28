using System;
using System.Collections.ObjectModel;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

/// <summary>
/// A command class with a [Keybinding] attribute: the source generator registers it (parameterless constructor) in the
/// "Global" group, so F1 works anywhere in the window.
/// </summary>
[Keybinding(name: "ShowHelp", group: "Global", defaultKeybinding: "F1", Label = "_Keyboard shortcuts", Icon = MaterialIcons.Keyboard,
    Description = "Show all keyboard shortcuts")]
public class ShowShortcutsHelpCommand : AtelierCommand
{
    public static event Action? HelpRequested;

    public override void Execute(object? parameter) => HelpRequested?.Invoke();
}

/// <summary>
/// The editor demo. Its commands are registered in the "Editor" group with [property: Command] on [RelayCommand]
/// methods; they run while the focus is inside the editor's KeybindingHandler.
/// </summary>
public partial class EditorScopeViewModel : ObservableObject
{
    private const string SampleText = "The quick brown fox jumps over the lazy dog.";

    // Icons can be SVG too: a whole document (here an outline icon, as copied from an icon site) or just path data.
    private const string UpperCaseIcon =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" " +
        "stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\">" +
        "<path d=\"M3 18 8 6l5 12\"/><path d=\"M4.7 14h6.6\"/><path d=\"M15 18l3-8 3 8\"/><path d=\"M16.1 15.5h3.8\"/></svg>";

    private const string LowerCaseIcon =
        "M7 18q-1.25 0-2.12-.73T4 15.5q0-1.3 1-2.02t2.55-.73q.6 0 1.1.1t.85.28v-.38q0-.7-.48-1.1T7.7 11.25q-.5 0-.95.2t-.8.55L5 11.1q.55-.55 1.22-.83T7.75 10q1.45 0 2.23.72T10.75 12.8V18H9.5v-.9h-.07q-.3.45-.9.68T7 18Zm.25-1.2q.8 0 1.28-.5T9 15.1q-.3-.18-.73-.28T7.35 14.7q-.75 0-1.17.3T5.75 15.8q0 .45.4.73t1.1.27ZM16.75 18q-.8 0-1.4-.38T14.4 16.6h-.08V18H13V6h1.4v3.6l-.07 1.4h.07q.35-.55.95-.93t1.4-.37q1.4 0 2.37 1.1t.98 2.7q0 1.6-.98 2.7T16.75 18Zm-.2-1.3q.85 0 1.43-.66t.57-1.84q0-1.18-.57-1.84t-1.43-.66q-.85 0-1.42.66T14.55 14.2q0 1.18.58 1.84t1.42.66Z";

    [ObservableProperty]
    private string _documentText = SampleText;

    [ObservableProperty]
    private bool _isBold;

    [ObservableProperty]
    private bool _isItalic;

    [ObservableProperty]
    private int _saveCount;

    public event Action<string, string>? ActionLogged;

    [RelayCommand]
    [property: Command("SaveDocument", "Editor", Label = "Save", Icon = MaterialIcons.Save, Description = "Save the document", DefaultKeybinding = "Ctrl+S")]
    private void SaveDocument()
    {
        SaveCount++;
        ActionLogged?.Invoke("Ctrl+S", $"Saved (#{SaveCount})");
    }

    [RelayCommand]
    [property: Command("ToggleBold", "Editor", Label = "Bold", Icon = MaterialIcons.FormatBold, Description = "Make the text bold, or normal again", DefaultKeybinding = "Ctrl+B")]
    private void ToggleBold()
    {
        IsBold = !IsBold;
        ActionLogged?.Invoke("Ctrl+B", IsBold ? "Bold on" : "Bold off");
    }

    [RelayCommand]
    [property: Command("ToggleItalic", "Editor", Label = "Italic", Icon = MaterialIcons.FormatItalic, Description = "Make the text italic, or upright again", DefaultKeybinding = "Ctrl+I")]
    private void ToggleItalic()
    {
        IsItalic = !IsItalic;
        ActionLogged?.Invoke("Ctrl+I", IsItalic ? "Italic on" : "Italic off");
    }

    [RelayCommand]
    [property: Command("ClearDocument", "Editor", Label = "Clear", Icon = MaterialIcons.ClearAll, Description = "Remove all text", DefaultKeybinding = "Ctrl+Shift+K")]
    private void ClearDocument()
    {
        DocumentText = string.Empty;
        ActionLogged?.Invoke("Ctrl+Shift+K", "Cleared the text");
    }

    [RelayCommand]
    [property: Command("UpperCase", "Editor", Label = "Upper case", Icon = UpperCaseIcon, Description = "Convert the text to upper case", DefaultKeybinding = "Ctrl+K, Ctrl+U")]
    private void UpperCase()
    {
        DocumentText = DocumentText.ToUpperInvariant();
        ActionLogged?.Invoke("Ctrl+K, Ctrl+U", "Converted to upper case (chord)");
    }

    [RelayCommand]
    [property: Command("LowerCase", "Editor", Label = "Lower case", Icon = LowerCaseIcon, Description = "Convert the text to lower case", DefaultKeybinding = "Ctrl+K, Ctrl+L")]
    private void LowerCase()
    {
        DocumentText = DocumentText.ToLowerInvariant();
        ActionLogged?.Invoke("Ctrl+K, Ctrl+L", "Converted to lower case (chord)");
    }

    public void Reset()
    {
        DocumentText = SampleText;
        IsBold = false;
        IsItalic = false;
        SaveCount = 0;
    }
}

/// <summary>
/// The media player demo: single keys ("Space", "R", "M", arrows) in the "Player" group. They only run while the player
/// has the focus, so typing a space in the editor never toggles playback.
/// </summary>
public partial class PlayerScopeViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionDisplay), nameof(Progress))]
    private float _positionSeconds = 84f;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isMuted;

    public float DurationSeconds => 240f;

    public string PositionDisplay => $"{TimeSpan.FromSeconds(PositionSeconds):m\\:ss} / {TimeSpan.FromSeconds(DurationSeconds):m\\:ss}";

    public float Progress => PositionSeconds / DurationSeconds * 100f;

    public event Action<string, string>? ActionLogged;

    [RelayCommand]
    [property: Command("TogglePlay", "Player", Label = "Play or pause", Icon = MaterialIcons.PlayArrow, DefaultKeybinding = "Space")]
    private void TogglePlay()
    {
        IsPlaying = !IsPlaying;
        ActionLogged?.Invoke("Space", IsPlaying ? "Play" : "Pause");
    }

    [RelayCommand]
    [property: Command("ResetTrack", "Player", Label = "Restart", Icon = MaterialIcons.Replay, Description = "Back to the start of the track", DefaultKeybinding = "R")]
    private void ResetTrack()
    {
        PositionSeconds = 0f;
        IsPlaying = false;
        ActionLogged?.Invoke("R", "Back to the start");
    }

    [RelayCommand]
    [property: Command("SeekBack", "Player", Label = "Back 5 seconds", Icon = MaterialIcons.Replay5, DefaultKeybinding = "Left")]
    private void SeekBack()
    {
        PositionSeconds = MathF.Max(0f, PositionSeconds - 5f);
        ActionLogged?.Invoke("Left", "Back 5 s");
    }

    [RelayCommand]
    [property: Command("SeekForward", "Player", Label = "Forward 5 seconds", Icon = MaterialIcons.Forward5, DefaultKeybinding = "Right")]
    private void SeekForward()
    {
        PositionSeconds = MathF.Min(DurationSeconds, PositionSeconds + 5f);
        ActionLogged?.Invoke("Right", "Forward 5 s");
    }

    [RelayCommand]
    [property: Command("ToggleMute", "Player", Label = "Mute", Icon = MaterialIcons.VolumeOff, Description = "Mute or unmute the sound", DefaultKeybinding = "M")]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        ActionLogged?.Invoke("M", IsMuted ? "Muted" : "Unmuted");
    }

    public void Reset()
    {
        PositionSeconds = 84f;
        IsPlaying = false;
        IsMuted = false;
    }
}

public partial class KeybindingViewModel : PageViewModel
{
    public EditorScopeViewModel Editor { get; } = new();

    public PlayerScopeViewModel Player { get; } = new();

    /// <summary>Executed keybindings, newest first.</summary>
    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    private string _pendingChord = "none";

    [ObservableProperty]
    private float _chordTimeoutSeconds = (float)KeybindingHandler.ChordTimeout.TotalSeconds;

    [ObservableProperty]
    private string _probeGesture = "Click the box and press keys";

    [ObservableProperty]
    private string _probeMatch = "—";

    public KeybindingViewModel()
    {
        PageTitle = "Keybindings";
        PageIcon = MaterialIconKind.Keyboard;
        Keywords = "keybinding keyboard shortcut hotkey chord gesture keybindinghandler";

        Editor.ActionLogged += (gesture, action) => Add("Editor", gesture, action);
        Player.ActionLogged += (gesture, action) => Add("Player", gesture, action);
        ShowShortcutsHelpCommand.HelpRequested += () => Add("Global", "F1", "Help requested (command class)");
        Add("Page", "—", "Ready: waiting for a shortcut");
    }

    // ChordTimeout is a static setting shared by all KeybindingHandlers.
    partial void OnChordTimeoutSecondsChanged(float value) => KeybindingHandler.ChordTimeout = TimeSpan.FromSeconds(value);

    [RelayCommand]
    [property: Keybinding("ResetDemos", "Global", "F5", Label = "Reset demos", Icon = MaterialIcons.RestartAlt, Description = "Put the demos back as they were")]
    private void ResetDemos()
    {
        Editor.Reset();
        Player.Reset();
        Add("Global", "F5", "Reset the demos");
    }

    [RelayCommand]
    [property: Keybinding("ClearLog", "Global", "Ctrl+Shift+L", Label = "Clear log", Icon = MaterialIcons.DeleteSweep, Description = "Remove the lines of the log")]
    private void ClearLog() => Log.Clear();

    public void Add(string group, string gesture, string action)
    {
        Log.Insert(0, $"{DateTime.Now:HH:mm:ss}  {group,-6}  {gesture,-14}  {action}");
        while (Log.Count > 10)
        {
            Log.RemoveAt(Log.Count - 1);
        }
    }

    /// <summary>Shows which group, if any, has a keybinding for a key press.</summary>
    public void Probe(Key key, ModifierKeys modifiers)
    {
        var stroke = new KeybindingGesture(key, modifiers);
        ProbeGesture = stroke.ToString();
        foreach (string group in new[] { "Editor", "Player", "Global" })
        {
            if (KeybindingManager.FindKeybinding(group, key, modifiers) is { } match)
            {
                ProbeMatch = $"{group}: {match.Name}";
                return;
            }

            if (KeybindingManager.IsKeybindingPrefix(group, [stroke]))
            {
                ProbeMatch = $"{group}: starts a chord";
                return;
            }
        }
        ProbeMatch = "No keybinding (the key goes on to the focused control)";
    }
}
