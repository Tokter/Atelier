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
[Keybinding(name: "ShowHelp", group: "Global", defaultKeybinding: "F1")]
public class ShowShortcutsHelpCommand : AtelierCommand
{
    public static event Action? HelpRequested;

    public override void Execute(object? parameter) => HelpRequested?.Invoke();
}

/// <summary>
/// The editor demo. Its commands are registered in the "Editor" group with [property: Keybinding] on [RelayCommand]
/// methods; they run while the focus is inside the editor's KeybindingHandler.
/// </summary>
public partial class EditorScopeViewModel : ObservableObject
{
    private const string SampleText = "The quick brown fox jumps over the lazy dog.";

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
    [property: Keybinding("SaveDocument", "Editor", "Ctrl+S")]
    private void SaveDocument()
    {
        SaveCount++;
        ActionLogged?.Invoke("Ctrl+S", $"Saved (#{SaveCount})");
    }

    [RelayCommand]
    [property: Keybinding("ToggleBold", "Editor", "Ctrl+B")]
    private void ToggleBold()
    {
        IsBold = !IsBold;
        ActionLogged?.Invoke("Ctrl+B", IsBold ? "Bold on" : "Bold off");
    }

    [RelayCommand]
    [property: Keybinding("ToggleItalic", "Editor", "Ctrl+I")]
    private void ToggleItalic()
    {
        IsItalic = !IsItalic;
        ActionLogged?.Invoke("Ctrl+I", IsItalic ? "Italic on" : "Italic off");
    }

    [RelayCommand]
    [property: Keybinding("ClearDocument", "Editor", "Ctrl+Shift+K")]
    private void ClearDocument()
    {
        DocumentText = string.Empty;
        ActionLogged?.Invoke("Ctrl+Shift+K", "Cleared the text");
    }

    [RelayCommand]
    [property: Keybinding("UpperCase", "Editor", "Ctrl+K, Ctrl+U")]
    private void UpperCase()
    {
        DocumentText = DocumentText.ToUpperInvariant();
        ActionLogged?.Invoke("Ctrl+K, Ctrl+U", "Converted to upper case (chord)");
    }

    [RelayCommand]
    [property: Keybinding("LowerCase", "Editor", "Ctrl+K, Ctrl+L")]
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
    [property: Keybinding("TogglePlay", "Player", "Space")]
    private void TogglePlay()
    {
        IsPlaying = !IsPlaying;
        ActionLogged?.Invoke("Space", IsPlaying ? "Play" : "Pause");
    }

    [RelayCommand]
    [property: Keybinding("ResetTrack", "Player", "R")]
    private void ResetTrack()
    {
        PositionSeconds = 0f;
        IsPlaying = false;
        ActionLogged?.Invoke("R", "Back to the start");
    }

    [RelayCommand]
    [property: Keybinding("SeekBack", "Player", "Left")]
    private void SeekBack()
    {
        PositionSeconds = MathF.Max(0f, PositionSeconds - 5f);
        ActionLogged?.Invoke("Left", "Back 5 s");
    }

    [RelayCommand]
    [property: Keybinding("SeekForward", "Player", "Right")]
    private void SeekForward()
    {
        PositionSeconds = MathF.Min(DurationSeconds, PositionSeconds + 5f);
        ActionLogged?.Invoke("Right", "Forward 5 s");
    }

    [RelayCommand]
    [property: Keybinding("ToggleMute", "Player", "M")]
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
    [property: Keybinding("ResetDemos", "Global", "F5")]
    private void ResetDemos()
    {
        Editor.Reset();
        Player.Reset();
        Add("Global", "F5", "Reset the demos");
    }

    [RelayCommand]
    [property: Keybinding("ClearLog", "Global", "Ctrl+Shift+L")]
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
