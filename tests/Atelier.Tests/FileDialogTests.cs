using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Xunit;

namespace Atelier.Tests;

/// <summary>A file system in memory with Unix-style paths, for the file dialog tests.</summary>
public sealed class InMemoryFileSystem : IFileSystemProvider
{
    private readonly SortedDictionary<string, (bool IsDirectory, long Length, FileAttributes Attributes)> _items = new(StringComparer.Ordinal);

    public InMemoryFileSystem() => _items["/"] = (true, 0, FileAttributes.Directory);

    public InMemoryFileSystem Dir(string path)
    {
        for (string? p = GetFullPath(path); p != null; p = GetParent(p)) _items.TryAdd(p, (true, 0, FileAttributes.Directory));
        return this;
    }

    public InMemoryFileSystem File(string path, long length = 100, FileAttributes attributes = FileAttributes.Normal)
    {
        string full = GetFullPath(path);
        Dir(GetParent(full)!);
        _items[full] = (false, length, attributes);
        return this;
    }

    public StringComparison PathComparison => StringComparison.Ordinal;

    public IReadOnlyCollection<char> InvalidFileNameChars { get; } = ['/', '\0', ':', '*', '?', '"', '<', '>', '|'];

    public IReadOnlyList<FileSystemPlace> GetPlaces() =>
    [
        new("Home", "/home/me", MaterialIconKind.Home),
        new("Documents", "/home/me/Documents", MaterialIconKind.Description),
    ];

    public IReadOnlyList<FileSystemPlace> GetRoots() => [new("/", "/", MaterialIconKind.Computer)];

    public IReadOnlyList<FileSystemEntry> GetEntries(string directory)
    {
        directory = GetFullPath(directory);
        if (!DirectoryExists(directory)) throw new DirectoryNotFoundException(directory);
        var entries = new List<FileSystemEntry>();
        foreach (var (path, item) in _items)
        {
            if (path == "/" || GetParent(path) != directory) continue;
            entries.Add(new FileSystemEntry(path, GetName(path), item.IsDirectory)
            {
                Length = item.IsDirectory ? null : item.Length,
                LastWriteTime = new DateTime(2026, 1, 2, 3, 4, 0),
                CreationTime = new DateTime(2025, 1, 1),
                Attributes = item.Attributes,
            });
        }
        return entries;
    }

    public bool DirectoryExists(string path) => _items.TryGetValue(GetFullPath(path), out var item) && item.IsDirectory;

    public bool FileExists(string path) => _items.TryGetValue(GetFullPath(path), out var item) && !item.IsDirectory;

    public string? GetParent(string path)
    {
        path = GetFullPath(path);
        if (path == "/") return null;
        int slash = path.LastIndexOf('/');
        return slash <= 0 ? "/" : path[..slash];
    }

    public string Combine(string directory, string path) => path.StartsWith('/') ? path : directory.TrimEnd('/') + "/" + path;

    public bool IsPathRooted(string path) => path.StartsWith('/');

    public string GetFullPath(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); continue; }
            parts.Add(part);
        }
        return "/" + string.Join("/", parts);
    }

    public string GetName(string path)
    {
        path = GetFullPath(path);
        return path == "/" ? "/" : path[(path.LastIndexOf('/') + 1)..];
    }

    public void CreateDirectory(string path) => Dir(path);

    public void Move(string path, string newPath)
    {
        path = GetFullPath(path);
        newPath = GetFullPath(newPath);
        foreach (var key in _items.Keys.Where(k => k == path || k.StartsWith(path + "/", StringComparison.Ordinal)).ToList())
        {
            var item = _items[key];
            _items.Remove(key);
            _items[newPath + key[path.Length..]] = item;
        }
    }
}

public class FileDialogTests
{
    private static InMemoryFileSystem Files() => new InMemoryFileSystem()
        .Dir("/home/me/Documents/Projects")
        .Dir("/home/me/Documents/Letters")
        .Dir("/home/me/Pictures")
        .File("/home/me/Documents/notes.txt", 1500)
        .File("/home/me/Documents/todo.txt", 20)
        .File("/home/me/Documents/report.pdf", 2_000_000)
        .File("/home/me/Documents/photo.png", 300_000)
        .File("/home/me/Documents/.secret", 5, FileAttributes.Hidden)
        .File("/home/me/Documents/Letters/letter.txt");

    private static (DialogHost Host, FileDialogView View, Task<bool> Result) Show(CommonItemDialog dialog)
    {
        var host = new DialogHost { Content = new Border() };
        host.AttachToHost();
        host.Measure(new Size(1400, 900));
        host.Arrange(new Rect(0, 0, 1400, 900));
        var result = dialog.ShowAsync(host);
        host.Measure(new Size(1400, 900));
        host.Arrange(new Rect(0, 0, 1400, 900));
        return (host, dialog.View!, result);
    }

    private static string[] Names(FileDialogView view) => view.FileList.View.Cast<FileSystemEntry>().Select(e => e.Name).ToArray();

    private static FileSystemEntry Entry(FileDialogView view, string name) => view.Entries.Single(e => e.Name == name);

    // Answers the message dialog shown over the file dialog.
    private static string AnswerMessage(DialogHost host, DialogResult result)
    {
        var message = Assert.IsType<Dialog>(host.OpenDialogs[^1], exactMatch: true);
        string text = message.Message ?? string.Empty;
        message.Close(result);
        return text;
    }

    [Fact]
    public void Filter_ParsesPairs_AndMatchesPatterns()
    {
        var filters = FileDialogFilter.Parse("Text files (*.txt;*.md)|*.txt;*.md|All files (*.*)|*.*");
        Assert.Equal(2, filters.Count);
        Assert.Equal(new[] { "*.txt", "*.md" }, filters[0].Patterns);
        Assert.True(filters[0].Matches("README.MD"));
        Assert.False(filters[0].Matches("a.pdf"));
        Assert.Equal(".txt", filters[0].DefaultExtension);
        Assert.Null(filters[1].DefaultExtension);
        Assert.True(filters[1].Matches("anything"));
        Assert.Throws<ArgumentException>(() => new OpenFileDialog { Filter = "Text files|*.txt|All" });
    }

    [Fact]
    public void Names_AreQuotedWhenSeveral()
    {
        Assert.Equal(new[] { "a b.txt", "c.txt" }, CommonItemDialog.ParseNames("\"a b.txt\" \"c.txt\""));
        Assert.Equal(new[] { "plain name.txt" }, CommonItemDialog.ParseNames("  plain name.txt "));
        Assert.Equal("\"a.txt\" \"b.txt\"", CommonItemDialog.FormatNames(["a.txt", "b.txt"]));
        Assert.Empty(CommonItemDialog.ParseNames("   "));
    }

    [Fact]
    public void Open_ListsFoldersFirst_ThenFilesOfTheType_WithoutHiddenOnes()
    {
        var (_, view, _) = Show(new OpenFileDialog
        {
            FileSystem = Files(),
            InitialDirectory = "/home/me/Documents",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Pick notes",
        });

        Assert.Equal("/home/me/Documents", view.CurrentDirectory);
        Assert.Equal(new[] { "Letters", "Projects", "notes.txt", "todo.txt" }, Names(view));
        Assert.Equal(new[] { "Text files (*.txt)", "All files (*.*)" }, view.TypeBox.Items);

        view.TypeBox.SelectedIndex = 1;
        Assert.Equal(new[] { "Letters", "Projects", "notes.txt", "photo.png", "report.pdf", "todo.txt" }, Names(view));

        // Type, size and the default columns; the rest are in the column chooser.
        Assert.Equal(new[] { "name", "modified", "type", "size" }, view.FileList.VisibleColumns.Select(c => c.EffectiveKey));
        Assert.Contains(view.FileList.Columns, c => c.EffectiveKey == "attributes" && !c.IsVisible);
        Assert.Equal("2 KB", view.FileList.Columns.Single(c => c.EffectiveKey == "size").GetCellText(Entry(view, "notes.txt")));
        Assert.Equal("Text document", Entry(view, "notes.txt").TypeName);
        Assert.Equal("File folder", Entry(view, "Letters").TypeName);
    }

    [Fact]
    public void Navigation_PathBarTreeAndHistory_FollowTheFolder()
    {
        var (_, view, _) = Show(new OpenFileDialog { FileSystem = Files(), InitialDirectory = "/home/me/Documents" });
        Assert.Equal(new[] { "/", "/home", "/home/me", "/home/me/Documents" }, view.PathBar.SegmentPaths);
        Assert.False(view.BackButton.IsEnabled);

        // A double-click (Enter) on a folder opens it; the tree shows it.
        view.FileList.SelectedItem = Entry(view, "Letters");
        view.FileList.OnKeyDown(new KeyEventArgs(Key.Enter));
        Assert.Equal("/home/me/Documents/Letters", view.CurrentDirectory);
        Assert.Equal(new[] { "letter.txt" }, Names(view));
        Assert.Equal("Letters", view.FolderTree.SelectedItem?.ToString());

        var segment = view.PathBar.SegmentButtons[2]; // a click on "me" in the path bar
        segment.OnPointerEntered(new PointerEventArgs(Point.Zero));
        segment.OnPointerPressed(new PointerEventArgs(Point.Zero, PointerButtons.Left));
        segment.OnPointerReleased(new PointerEventArgs(Point.Zero, PointerButtons.Left));
        Assert.Equal("/home/me", view.CurrentDirectory);
        Assert.Equal("Home", view.FolderTree.SelectedItem?.ToString());

        view.GoBack();
        Assert.Equal("/home/me/Documents/Letters", view.CurrentDirectory);
        view.GoForward();
        Assert.Equal("/home/me", view.CurrentDirectory);
        view.GoUp();
        Assert.Equal("/home", view.CurrentDirectory);

        // Typing a path.
        view.PathBar.BeginEdit();
        Assert.True(view.PathBar.IsEditing);
        view.PathBar.EditBox.Text = "/home/me/Pictures";
        view.PathBar.EditBox.OnKeyDown(new KeyEventArgs(Key.Enter));
        Assert.Equal("/home/me/Pictures", view.CurrentDirectory);
        Assert.False(view.PathBar.IsEditing);
    }

    [Fact]
    public async Task Open_SelectingAFileFillsTheName_AndOpenReturnsItsPath()
    {
        var dialog = new OpenFileDialog { FileSystem = Files(), InitialDirectory = "/home/me/Documents", Filter = "Text|*.txt" };
        var (_, view, result) = Show(dialog);

        view.FileList.SelectedItem = Entry(view, "todo.txt");
        Assert.Equal("todo.txt", view.NameBox.Text);
        Assert.True(await view.AcceptAsync());
        Assert.True(await result);
        Assert.Equal("/home/me/Documents/todo.txt", dialog.FileName);
        Assert.Equal("todo.txt", dialog.SafeFileName);
        Assert.Equal(1, dialog.FilterIndex);
        Assert.Null(dialog.View);
    }

    [Fact]
    public async Task Open_MultiselectReturnsSeveralFiles()
    {
        var dialog = new OpenFileDialog { FileSystem = Files(), InitialDirectory = "/home/me/Documents", Multiselect = true };
        var (_, view, result) = Show(dialog);
        Assert.Equal(DataGridSelectionMode.Extended, view.FileList.SelectionMode);

        view.FileList.SelectedItems.Add(Entry(view, "notes.txt"));
        view.FileList.SelectedItems.Add(Entry(view, "report.pdf"));
        view.FileList.SelectedItems.Add(Entry(view, "Letters")); // folders don't count
        Assert.Equal("\"notes.txt\" \"report.pdf\"", view.NameBox.Text);

        Assert.True(await view.AcceptAsync());
        Assert.True(await result);
        Assert.Equal(new[] { "/home/me/Documents/notes.txt", "/home/me/Documents/report.pdf" }, dialog.FileNames);
        Assert.Equal(new[] { "notes.txt", "report.pdf" }, dialog.SafeFileNames);
    }

    [Fact]
    public async Task Open_MissingFile_ShowsWhy_AndStaysOpen()
    {
        var (host, view, result) = Show(new OpenFileDialog { FileSystem = Files(), InitialDirectory = "/home/me/Documents" });
        view.NameBox.Text = "missing.txt";
        var accept = view.AcceptAsync();
        Assert.Contains("File not found", AnswerMessage(host, DialogResult.Ok));
        Assert.False(await accept);
        Assert.False(result.IsCompleted);

        // A typed folder opens it; a pattern filters.
        view.NameBox.Text = "Letters";
        Assert.False(await view.AcceptAsync());
        Assert.Equal("/home/me/Documents/Letters", view.CurrentDirectory);
        Assert.Equal(string.Empty, view.NameBox.Text);
        view.Navigate("/home/me/Documents");
        view.NameBox.Text = "*.pdf";
        Assert.False(await view.AcceptAsync());
        Assert.Equal(new[] { "Letters", "Projects", "report.pdf" }, Names(view));

        // An existing file without its extension is found through the type's extension.
        var dialog = new OpenFileDialog { FileSystem = Files(), InitialDirectory = "/home/me/Documents", Filter = "Text|*.txt" };
        var (_, view2, result2) = Show(dialog);
        view2.NameBox.Text = "notes";
        Assert.True(await view2.AcceptAsync());
        Assert.True(await result2);
        Assert.Equal("/home/me/Documents/notes.txt", dialog.FileName);
    }

    [Fact]
    public async Task Open_FileOkCanKeepTheDialogOpen()
    {
        var dialog = new OpenFileDialog { FileSystem = Files(), InitialDirectory = "/home/me/Documents" };
        int calls = 0;
        dialog.FileOk += (_, e) => e.Cancel = ++calls == 1;
        var (_, view, result) = Show(dialog);
        view.NameBox.Text = "todo.txt";
        Assert.False(await view.AcceptAsync());
        Assert.Equal(string.Empty, dialog.FileName);
        Assert.True(await view.AcceptAsync());
        Assert.True(await result);
    }

    [Fact]
    public async Task Save_AddsTheTypesExtension_AndAsksBeforeReplacing()
    {
        var dialog = new SaveFileDialog { FileSystem = Files(), InitialDirectory = "/home/me/Documents", Filter = "Text|*.txt|Markdown|*.md", FileName = "draft" };
        var (host, view, result) = Show(dialog);
        Assert.Equal("draft", view.NameBox.Text);
        Assert.Equal(Visibility.Visible, view.NewFolderButton.Visibility);

        view.NameBox.Text = "notes";
        var accept = view.AcceptAsync();
        Assert.Contains("notes.txt already exists", AnswerMessage(host, DialogResult.No));
        Assert.False(await accept);

        accept = view.AcceptAsync();
        AnswerMessage(host, DialogResult.Yes);
        Assert.True(await accept);
        Assert.True(await result);
        Assert.Equal("/home/me/Documents/notes.txt", dialog.FileName);
    }

    [Fact]
    public async Task Save_TypeChangeChangesTheExtension_AndCreatePromptAsks()
    {
        var dialog = new SaveFileDialog { FileSystem = Files(), InitialDirectory = "/home/me/Documents", Filter = "Text|*.txt|Markdown|*.md", CreatePrompt = true };
        var (host, view, result) = Show(dialog);
        view.NameBox.Text = "plan.txt";
        view.TypeBox.SelectedIndex = 1;
        Assert.Equal("plan.md", view.NameBox.Text);

        var accept = view.AcceptAsync();
        Assert.Contains("plan.md doesn't exist", AnswerMessage(host, DialogResult.Yes));
        Assert.True(await accept);
        Assert.True(await result);
        Assert.Equal("/home/me/Documents/plan.md", dialog.FileName);
        Assert.Equal(2, dialog.FilterIndex);
    }

    [Fact]
    public async Task Save_RejectsInvalidNamesAndMissingFolders()
    {
        var (host, view, _) = Show(new SaveFileDialog { FileSystem = Files(), InitialDirectory = "/home/me/Documents" });
        view.NameBox.Text = "a|b.txt";
        var accept = view.AcceptAsync();
        Assert.Contains("isn't valid", AnswerMessage(host, DialogResult.Ok));
        Assert.False(await accept);

        view.NameBox.Text = "Nowhere/new.txt";
        accept = view.AcceptAsync();
        Assert.Contains("path doesn't exist", AnswerMessage(host, DialogResult.Ok));
        Assert.False(await accept);
    }

    [Fact]
    public async Task FolderBrowser_ListsFolders_AndPicksTheSelectedOrTheShownFolder()
    {
        var dialog = new FolderBrowserDialog { FileSystem = Files(), InitialDirectory = "/home/me/Documents", Description = "Where to export" };
        var (_, view, result) = Show(dialog);
        Assert.Equal(new[] { "Letters", "Projects" }, Names(view));
        Assert.Equal("Select Folder", ((TextBlock)view.AcceptButton.Content!).Text);
        Assert.Equal(Visibility.Collapsed, view.TypeBox.Visibility);

        view.FileList.SelectedItem = Entry(view, "Projects");
        Assert.Equal("Projects", view.NameBox.Text);
        Assert.True(await view.AcceptAsync());
        Assert.True(await result);
        Assert.Equal("/home/me/Documents/Projects", dialog.FolderName);
        Assert.Equal("Projects", dialog.SafeFolderName);

        // Nothing chosen: the folder shown.
        var (_, view2, result2) = Show(dialog);
        view2.Navigate("/home/me/Pictures");
        Assert.True(await view2.AcceptAsync());
        Assert.True(await result2);
        Assert.Equal("/home/me/Pictures", dialog.FolderName);
    }

    [Fact]
    public void RootDirectory_LimitsNavigation()
    {
        var (_, view, _) = Show(new FolderBrowserDialog { FileSystem = Files(), RootDirectory = "/home/me/Documents" });
        Assert.Equal("/home/me/Documents", view.CurrentDirectory);
        Assert.False(view.UpButton.IsEnabled);
        Assert.False(view.Navigate("/home/me"));
        Assert.Equal(new[] { "/home/me/Documents" }, view.PathBar.SegmentPaths);
        Assert.Single(view.FolderTree.RootItems);
    }

    [Fact]
    public void HiddenItems_ShowWhenAsked()
    {
        var (_, view, _) = Show(new OpenFileDialog { FileSystem = Files(), InitialDirectory = "/home/me/Documents", ShowHiddenItems = true });
        Assert.Contains(".secret", Names(view));
        Assert.Contains("H", Entry(view, ".secret").AttributeLetters);
    }

    [Fact]
    public void NewFolder_IsCreated_AndRenamedInTheList()
    {
        var fs = Files();
        var (_, view, _) = Show(new FolderBrowserDialog { FileSystem = fs, InitialDirectory = "/home/me/Documents" });
        string? path = view.CreateNewFolder();
        Assert.Equal("/home/me/Documents/New folder", path);
        Assert.True(view.FileList.IsEditing);

        ((TextBox)view.FileList.EditContext!.Editor!).Text = "Exports";
        Assert.True(view.FileList.CommitEdit());
        Assert.True(fs.DirectoryExists("/home/me/Documents/Exports"));
        Assert.False(fs.DirectoryExists("/home/me/Documents/New folder"));
        Assert.Contains("Exports", Names(view));
        Assert.Equal("Exports", view.NameBox.Text);

        // Names that exist or aren't valid are refused.
        view.FileList.BeginEdit(Entry(view, "Exports"), view.FileList.Columns[0]);
        ((TextBox)view.FileList.EditContext!.Editor!).Text = "Letters";
        Assert.False(view.FileList.CommitEdit());
        Assert.Contains("already", view.FileList.EditContext!.Error);
        view.FileList.CancelEdit();
    }

    [Fact]
    public async Task ColumnLayout_IsKeptForTheNextDialog()
    {
        CommonItemDialog.SharedColumnLayout = null;
        var dialog = new OpenFileDialog { FileSystem = Files(), InitialDirectory = "/home/me/Documents" };
        var (_, view, result) = Show(dialog);
        view.FileList.Columns.Single(c => c.EffectiveKey == "attributes").IsVisible = true;
        view.Close(DialogResult.Cancel);
        Assert.False(await result);
        Assert.NotNull(dialog.ColumnLayout);

        var (_, view2, _) = Show(new OpenFileDialog { FileSystem = Files(), InitialDirectory = "/home/me/Documents" });
        Assert.True(view2.FileList.Columns.Single(c => c.EffectiveKey == "attributes").IsVisible);
        CommonItemDialog.SharedColumnLayout = null;
    }
}
