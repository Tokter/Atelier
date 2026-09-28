using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Atelier.Core.Keybinding;

namespace Atelier.Gallery.Infrastructure;

/// <summary>
/// Keeps the user's changes to the gallery's commands (labels, icons and shortcuts; see
/// <see cref="KeybindingManager.SetCustomization"/>) in a JSON file in the user's application data folder: loaded at
/// startup and saved after every change.
/// </summary>
internal static class CommandSettings
{
    /// <summary>Gets the settings file: <c>%APPDATA%\Atelier\Gallery\commands.json</c> on Windows.</summary>
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Atelier", "Gallery", "commands.json");

    /// <summary>Loads the saved customizations and saves them again whenever they change.</summary>
    public static void LoadAndKeepSaved()
    {
        try
        {
            if (File.Exists(FilePath)) KeybindingManager.ImportCustomizations(File.ReadAllText(FilePath));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"[Gallery] Couldn't read the command settings from {FilePath}: {e.Message}");
        }

        KeybindingManager.CustomizationsChanged += (_, _) => Save();
    }

    private static void Save()
    {
        try
        {
            if (KeybindingManager.Customizations.Count == 0)
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, KeybindingManager.ExportCustomizations());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[Gallery] Couldn't save the command settings to {FilePath}: {e.Message}");
        }
    }
}
