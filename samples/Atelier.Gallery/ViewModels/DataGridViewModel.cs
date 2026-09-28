using System;
using System.Collections.ObjectModel;
using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

/// <summary>A file of the data grid demo, with the attributes a file dialog could show as optional columns.</summary>
public partial class FileEntry : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private bool _isReadOnly;

    public required string Type { get; init; }

    public required string Extension { get; init; }

    public required long Size { get; init; }

    [ObservableProperty]
    private DateTime _modified;

    public required DateTime Created { get; init; }

    public required string Folder { get; init; }

    [ObservableProperty]
    private string _owner = "";

    [ObservableProperty]
    private int _rating;

    public bool IsHidden { get; init; }

    public string Attributes => (IsReadOnly ? "R" : "-") + (IsHidden ? "H" : "-") + (Size > 50_000_000 ? "C" : "-") + "A";

    public MaterialIconKind Icon => Extension switch
    {
        ".png" or ".jpg" or ".svg" => MaterialIconKind.Image,
        ".mp4" or ".mov" => MaterialIconKind.Movie,
        ".mp3" or ".flac" => MaterialIconKind.MusicNote,
        ".pdf" => MaterialIconKind.PictureAsPdf,
        ".cs" or ".json" or ".xml" => MaterialIconKind.Code,
        ".zip" => MaterialIconKind.Archive,
        ".docx" or ".txt" or ".md" => MaterialIconKind.Article,
        _ => MaterialIconKind.InsertDriveFile,
    };

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.##} GB",
    };
}

public partial class DataGridViewModel : PageViewModel
{
    private static readonly (string Extension, string Type, long MaxSize)[] s_types =
    [
        (".png", "PNG image", 8_000_000), (".jpg", "JPEG image", 12_000_000), (".svg", "SVG drawing", 400_000),
        (".mp4", "MP4 video", 2_000_000_000), (".mov", "QuickTime movie", 900_000_000), (".mp3", "MP3 audio", 12_000_000),
        (".flac", "FLAC audio", 60_000_000), (".pdf", "PDF document", 30_000_000), (".cs", "C# source", 80_000),
        (".json", "JSON file", 200_000), (".xml", "XML document", 500_000), (".zip", "Compressed folder", 900_000_000),
        (".docx", "Word document", 4_000_000), (".txt", "Text document", 60_000), (".md", "Markdown file", 40_000),
    ];

    private static readonly string[] s_words =
    [
        "report", "holiday", "invoice", "budget", "draft", "notes", "summary", "photo", "scan", "design", "meeting",
        "backup", "export", "presentation", "sketch", "recording", "archive", "letter", "contract", "readme", "config",
        "schema", "theme", "sprint", "roadmap", "concept", "final", "review", "sample", "demo",
    ];

    private static readonly string[] s_folders = [@"C:\Users\Me\Documents", @"C:\Users\Me\Pictures", @"C:\Users\Me\Music", @"C:\Users\Me\Videos", @"D:\Projects\Atelier", @"D:\Archive"];

    public static readonly string[] Owners = ["Me", "Administrators", "SYSTEM", "Team"];

    [ObservableProperty]
    private DataGridSelectionMode _selectionMode = DataGridSelectionMode.Extended;

    [ObservableProperty]
    private int _selectionModeIndex = 2;

    [ObservableProperty]
    private bool _showFilterRow;

    [ObservableProperty]
    private string _status = "Nothing selected";

    [ObservableProperty]
    private string _savedLayout = "";

    public ObservableCollection<FileEntry> Files { get; } = [];

    public ObservableCollection<FileEntry> SmallList { get; } = [];

    public ObservableCollection<FileEntry> RatedFiles { get; } = [];

    public DataGridViewModel()
    {
        PageIcon = MaterialIconKind.TableRows;
        PageTitle = "Data Grid";
        Keywords = "datagrid data grid table listview columns sort filter virtualization virtualizing rows";

        var random = new Random(42);
        foreach (var file in CreateFiles(random, 10_000)) Files.Add(file);
        foreach (var file in CreateFiles(random, 6)) SmallList.Add(file);
        for (int i = 0; i < 8; i++) RatedFiles.Add(Files[i]); // the same files: ratings show in both grids
    }

    partial void OnSelectionModeIndexChanged(int value) => SelectionMode = (DataGridSelectionMode)Math.Clamp(value, 0, 2);

    [ObservableProperty]
    private DataGridSelectionUnit _selectionUnit = DataGridSelectionUnit.Row;

    [ObservableProperty]
    private int _selectionUnitIndex;

    partial void OnSelectionUnitIndexChanged(int value) => SelectionUnit = value == 1 ? DataGridSelectionUnit.Cell : DataGridSelectionUnit.Row;

    private static FileEntry[] CreateFiles(Random random, int count)
    {
        var files = new FileEntry[count];
        var now = new DateTime(2026, 9, 1, 12, 0, 0);
        for (int i = 0; i < count; i++)
        {
            var (extension, type, maxSize) = s_types[random.Next(s_types.Length)];
            string name = s_words[random.Next(s_words.Length)] + (random.Next(3) == 0 ? "-" + s_words[random.Next(s_words.Length)] : "") + $"-{i + 1:0000}";
            var created = now.AddMinutes(-random.Next(60 * 24 * 900));
            files[i] = new FileEntry
            {
                Name = name + extension,
                Extension = extension,
                Type = type,
                Size = (long)(Math.Pow(random.NextDouble(), 3) * maxSize) + 1,
                Created = created,
                Modified = created.AddMinutes(random.Next((int)Math.Max(1, (now - created).TotalMinutes))),
                Folder = s_folders[random.Next(s_folders.Length)],
                Owner = Owners[random.Next(Owners.Length)],
                IsReadOnly = random.Next(8) == 0,
                IsHidden = random.Next(20) == 0,
                Rating = random.Next(6),
            };
        }
        return files;
    }

    [RelayCommand]
    private void AddFile()
    {
        var file = CreateFiles(new Random(), 1)[0];
        SmallList.Add(file);
        Status = $"Added {file.Name}";
    }

    [RelayCommand]
    private void ClearFiles()
    {
        SmallList.Clear();
        Status = "Cleared the list";
    }

    public void Delete(FileEntry file)
    {
        Files.Remove(file);
        SmallList.Remove(file);
        Status = $"Deleted {file.Name}";
    }
}
