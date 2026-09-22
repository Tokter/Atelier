using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public class KeybindingLogEntry
{
    public string Time { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Gesture { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Demonstrates class-level [Keybinding] with a parameterless constructor implementing ICommand.
/// Discovered and registered at compile time by Roslyn KeybindingGenerator.
/// </summary>
[Keybinding(name: "ShowHelp", group: "Global", defaultKeybinding: "F1")]
public class ShowShortcutsHelpCommand : AtelierCommand
{
    public static event Action? HelpRequested;

    public override void Execute(object? parameter)
    {
        HelpRequested?.Invoke();
    }
}

/// <summary>
/// Scoped ViewModel for the Rich Text Editor demo panel.
/// Demonstrates [property: Keybinding] attributes on [RelayCommand] methods.
/// </summary>
public partial class EditorScopeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _documentText = "The quick brown fox jumps over the lazy dog. Atelier provides declarative keyboard shortcuts with compile-time source generation.";

    [ObservableProperty]
    private bool _isBold;

    [ObservableProperty]
    private bool _isItalic;

    [ObservableProperty]
    private bool _isUppercase;

    [ObservableProperty]
    private int _saveCount;

    [ObservableProperty]
    private string _lastActionStatus = "Ready. Focus this editor and press Ctrl+S, Ctrl+B, Ctrl+I, Ctrl+K, or Ctrl+U.";

    public event Action<string, string, string, string>? ActionLogged;

    [RelayCommand]
    [property: Keybinding("SaveDocument", "Editor", "Ctrl+S")]
    private void SaveDocument()
    {
        SaveCount++;
        LastActionStatus = $"Document saved successfully (Save #{SaveCount}) at {DateTime.Now:HH:mm:ss}!";
        ActionLogged?.Invoke("Editor", "Ctrl+S", "SaveDocument", nameof(EditorScopeViewModel));
    }

    [RelayCommand]
    [property: Keybinding("ToggleBold", "Editor", "Ctrl+B")]
    private void ToggleBold()
    {
        IsBold = !IsBold;
        LastActionStatus = $"Bold formatting {(IsBold ? "ENABLED" : "disabled")}.";
        ActionLogged?.Invoke("Editor", "Ctrl+B", "ToggleBold", nameof(EditorScopeViewModel));
    }

    [RelayCommand]
    [property: Keybinding("ToggleItalic", "Editor", "Ctrl+I")]
    private void ToggleItalic()
    {
        IsItalic = !IsItalic;
        LastActionStatus = $"Italic formatting {(IsItalic ? "ENABLED" : "disabled")}.";
        ActionLogged?.Invoke("Editor", "Ctrl+I", "ToggleItalic", nameof(EditorScopeViewModel));
    }

    [RelayCommand]
    [property: Keybinding("ClearDocument", "Editor", "Ctrl+K")]
    private void ClearDocument()
    {
        DocumentText = string.Empty;
        LastActionStatus = "Document text cleared (Ctrl+K).";
        ActionLogged?.Invoke("Editor", "Ctrl+K", "ClearDocument", nameof(EditorScopeViewModel));
    }

    [RelayCommand]
    [property: Keybinding("ToggleCase", "Editor", "Ctrl+U")]
    private void ToggleCase()
    {
        IsUppercase = !IsUppercase;
        if (!string.IsNullOrEmpty(DocumentText))
        {
            DocumentText = IsUppercase ? DocumentText.ToUpperInvariant() : DocumentText.ToLowerInvariant();
        }
        LastActionStatus = $"Case converted to {(IsUppercase ? "UPPERCASE" : "lowercase")}.";
        ActionLogged?.Invoke("Editor", "Ctrl+U", "ToggleCase", nameof(EditorScopeViewModel));
    }

    public void Reset()
    {
        DocumentText = "The quick brown fox jumps over the lazy dog. Atelier provides declarative keyboard shortcuts with compile-time source generation.";
        IsBold = false;
        IsItalic = false;
        IsUppercase = false;
        SaveCount = 0;
        LastActionStatus = "Editor scope reset to initial state.";
    }
}

/// <summary>
/// Scoped ViewModel for the Media Player demo panel.
/// Demonstrates single-key and arrow-key gestures ([Space], [R], [M], [Left], [Right]).
/// </summary>
public partial class PlayerScopeViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string _trackTitle = "Symphony No. 5 in C Minor, Op. 67";

    [ObservableProperty]
    private string _artist = "Ludwig van Beethoven";

    [ObservableProperty]
    private float _positionSeconds = 84f;

    [ObservableProperty]
    private float _durationSeconds = 240f;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private string _statusMessage = "Paused at 01:24. Focus this player and press Space, Left, Right, R, or M.";

    public string PositionDisplay => $"{TimeSpan.FromSeconds(PositionSeconds):mm\\:ss} / {TimeSpan.FromSeconds(DurationSeconds):mm\\:ss}";

    public float ProgressFraction => DurationSeconds > 0 ? Math.Clamp(PositionSeconds / DurationSeconds, 0f, 1f) : 0f;

    public event Action<string, string, string, string>? ActionLogged;

    [RelayCommand]
    [property: Keybinding("TogglePlay", "Player", "Space")]
    private void TogglePlay()
    {
        IsPlaying = !IsPlaying;
        StatusMessage = IsPlaying ? "▶ Playing track..." : "⏸ Paused playback";
        OnPropertyChanged(nameof(PositionDisplay));
        OnPropertyChanged(nameof(ProgressFraction));
        ActionLogged?.Invoke("Player", "Space", "TogglePlay", nameof(PlayerScopeViewModel));
    }

    [RelayCommand]
    [property: Keybinding("ResetTrack", "Player", "R")]
    private void ResetTrack()
    {
        PositionSeconds = 0f;
        IsPlaying = false;
        StatusMessage = "⏹ Stopped & reset track to start";
        OnPropertyChanged(nameof(PositionDisplay));
        OnPropertyChanged(nameof(ProgressFraction));
        ActionLogged?.Invoke("Player", "R", "ResetTrack", nameof(PlayerScopeViewModel));
    }

    [RelayCommand]
    [property: Keybinding("SeekBack", "Player", "Left")]
    private void SeekBack()
    {
        PositionSeconds = MathF.Max(0f, PositionSeconds - 5f);
        StatusMessage = $"⏪ Seek -5s ({TimeSpan.FromSeconds(PositionSeconds):mm\\:ss})";
        OnPropertyChanged(nameof(PositionDisplay));
        OnPropertyChanged(nameof(ProgressFraction));
        ActionLogged?.Invoke("Player", "Left", "SeekBack", nameof(PlayerScopeViewModel));
    }

    [RelayCommand]
    [property: Keybinding("SeekForward", "Player", "Right")]
    private void SeekForward()
    {
        PositionSeconds = MathF.Min(DurationSeconds, PositionSeconds + 5f);
        StatusMessage = $"⏩ Seek +5s ({TimeSpan.FromSeconds(PositionSeconds):mm\\:ss})";
        OnPropertyChanged(nameof(PositionDisplay));
        OnPropertyChanged(nameof(ProgressFraction));
        ActionLogged?.Invoke("Player", "Right", "SeekForward", nameof(PlayerScopeViewModel));
    }

    [RelayCommand]
    [property: Keybinding("ToggleMute", "Player", "M")]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        StatusMessage = IsMuted ? "🔇 Audio Muted" : "🔊 Audio Unmuted";
        ActionLogged?.Invoke("Player", "M", "ToggleMute", nameof(PlayerScopeViewModel));
    }

    public void Reset()
    {
        IsPlaying = false;
        PositionSeconds = 84f;
        IsMuted = false;
        StatusMessage = "Player scope reset to initial state.";
        OnPropertyChanged(nameof(PositionDisplay));
        OnPropertyChanged(nameof(ProgressFraction));
    }
}

/// <summary>
/// Main ViewModel for the Keybindings showcase gallery page.
/// Manages child scopes (EditorScope, PlayerScope), global bubbling commands (F5, Ctrl+Shift+L),
/// keystroke probe analysis, and live activity logging.
/// </summary>
public partial class KeybindingViewModel : PageViewModel
{
    [ObservableProperty]
    private EditorScopeViewModel _editorScope;

    [ObservableProperty]
    private PlayerScopeViewModel _playerScope;

    [ObservableProperty]
    private ObservableCollection<KeybindingLogEntry> _logs = new();

    // Keystroke Probe State
    [ObservableProperty]
    private string _probeKey = "None";

    [ObservableProperty]
    private string _probeModifiers = "None";

    [ObservableProperty]
    private string _probeGesture = "Press any key in tester";

    [ObservableProperty]
    private string _probeScope = "Waiting for input";

    [ObservableProperty]
    private string _probeMatchedCommand = "None";

    [ObservableProperty]
    private string _helpBannerMessage = "Press F1 for keyboard shortcuts help, or F5 to reset all panels.";

    public KeybindingViewModel()
    {
        PageTitle = "Keybindings";
        PageIcon = MaterialIconKind.Keyboard;

        _editorScope = new EditorScopeViewModel();
        _playerScope = new PlayerScopeViewModel();

        _editorScope.ActionLogged += (group, gesture, cmd, target) => LogAction(group, gesture, cmd, target, "Handled (Local Scope)");
        _playerScope.ActionLogged += (group, gesture, cmd, target) => LogAction(group, gesture, cmd, target, "Handled (Local Scope)");

        ShowShortcutsHelpCommand.HelpRequested += () =>
        {
            HelpBannerMessage = $"[F1 Help] Editor: Ctrl+S (Save), Ctrl+B (Bold), Ctrl+I (Italic), Ctrl+K (Clear), Ctrl+U (Case) | Player: Space (Play/Pause), R (Reset), Left/Right (Seek), M (Mute) | Global: F5 (Reset All), Ctrl+Shift+L (Clear Logs) [{DateTime.Now:HH:mm:ss}]";
            LogAction("Global", "F1", "ShowHelp", nameof(ShowShortcutsHelpCommand), "Handled (Class Keybinding)");
        };

        // Seed initial log
        LogAction("System", "Init", "RegisterKeybindings", "GeneratedKeybindings", "Active & Listening");
    }

    [RelayCommand]
    [property: Keybinding("ResetAllDemos", "Global", "F5")]
    private void ResetAllDemos()
    {
        EditorScope.Reset();
        PlayerScope.Reset();
        HelpBannerMessage = "All demo panels have been reset to defaults (F5 pressed).";
        LogAction("Global", "F5", "ResetAllDemos", nameof(KeybindingViewModel), "Handled (Global Bubble)");
    }

    [RelayCommand]
    [property: Keybinding("ClearLogs", "Global", "Ctrl+Shift+L")]
    private void ClearLogs()
    {
        Logs.Clear();
        LogAction("Global", "Ctrl+Shift+L", "ClearLogs", nameof(KeybindingViewModel), "Logs Cleared");
    }

    public void LogAction(string group, string gesture, string command, string target, string status = "Success")
    {
        var entry = new KeybindingLogEntry
        {
            Time = DateTime.Now.ToString("HH:mm:ss"),
            Group = group,
            Gesture = gesture,
            Command = command,
            Target = target,
            Status = status
        };

        if (Logs.Count >= 40)
        {
            Logs.RemoveAt(Logs.Count - 1);
        }
        Logs.Insert(0, entry);
    }

    public void UpdateProbe(Key key, ModifierKeys modifiers)
    {
        ProbeKey = key.ToString();
        ProbeModifiers = modifiers == ModifierKeys.None ? "None" : modifiers.ToString();

        var gesture = new KeybindingGesture(key, modifiers);
        ProbeGesture = gesture.ToString();

        // Check if any group matches
        var editorMatch = KeybindingManager.FindKeybinding("Editor", key, modifiers);
        var playerMatch = KeybindingManager.FindKeybinding("Player", key, modifiers);
        var globalMatch = KeybindingManager.FindKeybinding("Global", key, modifiers);

        if (editorMatch != null)
        {
            ProbeScope = "Editor Scope (Local)";
            ProbeMatchedCommand = $"{editorMatch.Name} ({editorMatch.Keybinding})";
        }
        else if (playerMatch != null)
        {
            ProbeScope = "Player Scope (Local)";
            ProbeMatchedCommand = $"{playerMatch.Name} ({playerMatch.Keybinding})";
        }
        else if (globalMatch != null)
        {
            ProbeScope = "Global Scope (Ancestral Bubble)";
            ProbeMatchedCommand = $"{globalMatch.Name} ({globalMatch.Keybinding})";
        }
        else
        {
            ProbeScope = "Unhandled / Passthrough";
            ProbeMatchedCommand = "None (will bubble to OS / Window)";
        }
    }

    public void SimulateGesture(string group, Key key, ModifierKeys modifiers)
    {
        UpdateProbe(key, modifiers);

        object? target = group switch
        {
            "Editor" => EditorScope,
            "Player" => PlayerScope,
            "Global" => this,
            _ => null
        };

        bool executed = KeybindingManager.TryExecuteGesture(group, key, modifiers, target);
        if (!executed && group != "Global")
        {
            // Bubble to global!
            executed = KeybindingManager.TryExecuteGesture("Global", key, modifiers, this);
            if (executed)
            {
                LogAction("Global", new KeybindingGesture(key, modifiers).ToString(), "BubbledToGlobal", nameof(KeybindingViewModel), "Bubbled Up Successfully");
            }
        }
    }
}
