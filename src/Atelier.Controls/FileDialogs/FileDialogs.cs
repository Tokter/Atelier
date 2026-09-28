using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>A file type of a file dialog's <see cref="FileDialog.Filter"/>: its description and its patterns.</summary>
/// <param name="Description">The text shown, e.g. "Text files (*.txt)".</param>
/// <param name="Patterns">The patterns, e.g. "*.txt" and "*.log".</param>
public sealed record FileDialogFilter(string Description, IReadOnlyList<string> Patterns)
{
    /// <summary>Gets whether <paramref name="fileName"/> matches one of the patterns (ignoring case).</summary>
    public bool Matches(string fileName)
    {
        foreach (var pattern in Patterns)
        {
            if (pattern is "*" or "*.*" || FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true)) return true;
        }
        return false;
    }

    /// <summary>Gets the extension of the first pattern of the form "*.ext" (e.g. ".txt"), or <c>null</c>.</summary>
    public string? DefaultExtension
    {
        get
        {
            foreach (var pattern in Patterns)
            {
                if (pattern.StartsWith("*.", StringComparison.Ordinal) && pattern.Length > 2 && pattern.IndexOfAny(['*', '?'], 2) < 0) return pattern[1..];
            }
            return null;
        }
    }

    /// <summary>
    /// Parses a filter string in the WPF format: pairs of a description and semicolon-separated patterns, all separated
    /// by '|', e.g. "Text files (*.txt)|*.txt|All files (*.*)|*.*".
    /// </summary>
    /// <exception cref="ArgumentException">The string isn't made of pairs.</exception>
    public static IReadOnlyList<FileDialogFilter> Parse(string? filter)
    {
        if (string.IsNullOrEmpty(filter)) return [];
        var parts = filter.Split('|');
        if (parts.Length % 2 != 0) throw new ArgumentException("The filter must be pairs of a description and patterns separated by '|', e.g. \"Text files (*.txt)|*.txt|All files (*.*)|*.*\".", nameof(filter));
        var filters = new List<FileDialogFilter>();
        for (int i = 0; i < parts.Length; i += 2)
        {
            var patterns = parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            filters.Add(new FileDialogFilter(parts[i].Trim(), patterns.Length > 0 ? patterns : ["*.*"]));
        }
        return filters;
    }
}

/// <summary>
/// The base of the file and folder dialogs: a <see cref="FileDialogView"/> shown in the window (a
/// <see cref="DialogHost"/>), the same on every platform, browsing a <see cref="FileSystem"/>.
/// </summary>
/// <remarks>
/// <para>
/// Like the WPF dialogs, these are plain objects: set the properties, call <see cref="ShowAsync(UIElement)"/>, and read
/// the result when it returns <c>true</c>. They can be shown again.
/// </para>
/// <para>
/// The view has a path bar with clickable folders (click beside them, or Ctrl+L, to type a path), back, forward, up
/// and refresh, a folder tree (known places, custom places and drives), a search box, and a <see cref="DataGrid"/> of
/// the folder: name, date modified, type and size, plus optional columns in the column chooser (right-click the
/// headers): date created, date accessed, extension, attributes, read-only, hidden, link target and full path. The
/// column layout is kept between dialogs (see <see cref="ColumnLayout"/>).
/// </para>
/// </remarks>
public abstract class CommonItemDialog
{
    // The last folder a dialog was accepted in, to start there next time.
    private static readonly Dictionary<Type, string> s_lastDirectory = [];

    /// <summary>Gets or sets the title; <c>null</c> (the default) uses "Open", "Save As" or "Select Folder".</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets the folder shown first; <c>null</c> (the default) starts where the last dialog of this kind was accepted, or at <see cref="DefaultDirectory"/>.</summary>
    public string? InitialDirectory { get; set; }

    /// <summary>Gets or sets the folder shown first when there is no <see cref="InitialDirectory"/> and no earlier folder; <c>null</c> uses the documents folder.</summary>
    public string? DefaultDirectory { get; set; }

    /// <summary>Gets or sets a folder the dialog can't leave: the tree shows only it, and Up stops there. <c>null</c> (the default) allows every folder.</summary>
    public string? RootDirectory { get; set; }

    /// <summary>Gets or sets whether hidden files and folders are shown. The default is <c>false</c>.</summary>
    public bool ShowHiddenItems { get; set; }

    /// <summary>Gets the folders added to the tree after the known places.</summary>
    public List<string> CustomPlaces { get; } = [];

    /// <summary>Gets or sets whether names with invalid characters are rejected. The default is <c>true</c>.</summary>
    public bool ValidateNames { get; set; } = true;

    /// <summary>Gets or sets the file system browsed. The default is the computer's (<see cref="PhysicalFileSystemProvider"/>).</summary>
    public IFileSystemProvider FileSystem { get; set; } = PhysicalFileSystemProvider.Instance;

    /// <summary>
    /// Gets or sets the columns of the file list (order, widths, visibility, sort), updated when the dialog closes;
    /// <c>null</c> (the default) uses <see cref="SharedColumnLayout"/>. Store it (e.g. <see cref="DataGridLayout.ToJson"/>)
    /// to keep the user's columns between sessions.
    /// </summary>
    public DataGridLayout? ColumnLayout { get; set; }

    /// <summary>Gets or sets the column layout of dialogs without their own <see cref="ColumnLayout"/>; updated when they close.</summary>
    public static DataGridLayout? SharedColumnLayout { get; set; }

    /// <summary>Gets or sets any data of the application.</summary>
    public object? Tag { get; set; }

    /// <summary>Gets the view while the dialog is shown, otherwise <c>null</c>.</summary>
    public FileDialogView? View { get; private set; }

    /// <summary>Gets the title used when <see cref="Title"/> is <c>null</c>.</summary>
    protected abstract string DefaultTitle { get; }

    /// <summary>Gets the title shown.</summary>
    public string EffectiveTitle => string.IsNullOrEmpty(Title) ? DefaultTitle : Title;

    /// <summary>Gets the text of the accept button, e.g. "Open".</summary>
    public abstract string AcceptButtonText { get; }

    /// <summary>Gets whether the list shows files (not only folders).</summary>
    public virtual bool ShowsFiles => true;

    /// <summary>Gets whether several items can be picked.</summary>
    public virtual bool AllowsMultipleSelection => false;

    /// <summary>Gets whether the view has a "New folder" button.</summary>
    public virtual bool ShowsNewFolderButton => false;

    /// <summary>Gets the label of the name box, e.g. "File name".</summary>
    public virtual string NameLabel => "File name";

    /// <summary>
    /// Shows the dialog in the <see cref="DialogHost"/> of <paramref name="owner"/> (normally the window's) and returns
    /// <c>true</c> when the user accepted a choice.
    /// </summary>
    /// <exception cref="InvalidOperationException">There is no dialog host, or the dialog is already shown.</exception>
    public Task<bool> ShowAsync(UIElement owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var host = DialogHost.FindNearestHost(owner) ?? throw new InvalidOperationException("No DialogHost was found for the owner element.");
        return ShowAsync(host);
    }

    /// <summary>Shows the dialog in <paramref name="host"/> and returns <c>true</c> when the user accepted a choice.</summary>
    /// <exception cref="InvalidOperationException">The dialog is already shown.</exception>
    public async Task<bool> ShowAsync(DialogHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (View != null) throw new InvalidOperationException("The dialog is already shown.");
        OnShowing();
        var view = new FileDialogView(this, StartDirectory());
        View = view;
        try
        {
            var response = await view.ShowAsync(host);
            bool accepted = response.Result == DialogResult.Ok;
            if (accepted) s_lastDirectory[GetType()] = view.CurrentDirectory;
            return accepted;
        }
        finally
        {
            var layout = view.FileList.SaveLayout();
            ColumnLayout = layout;
            SharedColumnLayout = layout;
            View = null;
        }
    }

    /// <summary>Called before the view is created, e.g. to check properties.</summary>
    protected virtual void OnShowing()
    {
    }

    /// <summary>Gets the folder the view starts in: <see cref="InitialDirectory"/>, the last one, <see cref="DefaultDirectory"/>, documents, home, or the first root.</summary>
    protected virtual string StartDirectory()
    {
        var fs = FileSystem;
        foreach (var candidate in StartCandidates())
        {
            if (string.IsNullOrEmpty(candidate)) continue;
            try
            {
                string full = fs.GetFullPath(candidate);
                if (fs.DirectoryExists(full) && IsInsideRoot(full)) return full;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
            {
            }
        }
        if (RootDirectory != null) return fs.GetFullPath(RootDirectory);
        var places = fs.GetPlaces();
        return places.Count > 0 ? places[0].Path : fs.GetRoots()[0].Path;
    }

    /// <summary>Gets the folders to try at start, in order.</summary>
    protected virtual IEnumerable<string?> StartCandidates()
    {
        yield return InitialDirectory;
        yield return s_lastDirectory.GetValueOrDefault(GetType());
        yield return DefaultDirectory;
        foreach (var place in FileSystem.GetPlaces())
        {
            if (place.Name == "Documents") yield return place.Path;
        }
        foreach (var place in FileSystem.GetPlaces()) yield return place.Path;
    }

    internal bool IsInsideRoot(string path) => RootDirectory == null || IsSameOrInside(path, FileSystem.GetFullPath(RootDirectory));

    internal bool IsSameOrInside(string path, string folder)
    {
        var fs = FileSystem;
        for (string? p = path; p != null; p = fs.GetParent(p))
        {
            if (string.Equals(p, folder, fs.PathComparison)) return true;
        }
        return false;
    }

    /// <summary>
    /// Checks and takes the user's choice: <paramref name="names"/> are the names typed or selected (relative to
    /// <paramref name="directory"/> or absolute). Returns <c>true</c> to close the dialog; on <c>false</c> it stays
    /// open (the method may have navigated or shown a message).
    /// </summary>
    protected internal abstract Task<bool> AcceptAsync(FileDialogView view, string directory, IReadOnlyList<string> names);

    /// <summary>Resets the properties to their defaults.</summary>
    public virtual void Reset()
    {
        Title = InitialDirectory = DefaultDirectory = RootDirectory = null;
        ShowHiddenItems = false;
        ValidateNames = true;
        CustomPlaces.Clear();
        ColumnLayout = null;
        Tag = null;
    }

    /// <summary>Gets whether <paramref name="name"/> has characters a file name can't have.</summary>
    protected bool HasInvalidChars(string name)
    {
        var invalid = FileSystem.InvalidFileNameChars;
        foreach (char c in FileSystem.GetName(name))
        {
            if (invalid.Contains(c)) return true;
        }
        return false;
    }

    /// <summary>Splits the text of the name box: quoted names ("a.txt" "b.txt"), or the whole text as one name.</summary>
    public static IReadOnlyList<string> ParseNames(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return [];
        if (!text.Contains('"')) return [text];
        var names = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            int start = text.IndexOf('"', i);
            if (start < 0)
            {
                string rest = text[i..].Trim();
                if (rest.Length > 0) names.Add(rest);
                break;
            }
            string before = text[i..start].Trim();
            if (before.Length > 0) names.Add(before);
            int end = text.IndexOf('"', start + 1);
            if (end < 0) end = text.Length;
            string name = text[(start + 1)..end].Trim();
            if (name.Length > 0) names.Add(name);
            i = end + 1;
        }
        return names;
    }

    /// <summary>Joins names for the name box: one name as it is, several quoted.</summary>
    public static string FormatNames(IReadOnlyList<string> names) =>
        names.Count == 1 ? names[0] : string.Join(" ", names.Select(n => $"\"{n}\""));
}

/// <summary>The base of <see cref="OpenFileDialog"/> and <see cref="SaveFileDialog"/>: file types, names and checks, like WPF's.</summary>
public abstract class FileDialog : CommonItemDialog
{
    private string? _filter;
    private IReadOnlyList<FileDialogFilter> _filters = [];
    private string[] _fileNames = [];

    /// <summary>
    /// Gets or sets the file types offered: pairs of a description and semicolon-separated patterns, separated by '|',
    /// e.g. "Images (*.png;*.jpg)|*.png;*.jpg|All files (*.*)|*.*". Empty (the default) shows all files.
    /// </summary>
    /// <exception cref="ArgumentException">The string isn't made of pairs.</exception>
    public string? Filter
    {
        get => _filter;
        set
        {
            _filters = FileDialogFilter.Parse(value);
            _filter = value;
        }
    }

    /// <summary>Gets the parsed <see cref="Filter"/>.</summary>
    public IReadOnlyList<FileDialogFilter> Filters => _filters;

    /// <summary>Gets or sets the chosen file type, 1-based as in WPF. The default is 1; updated when the dialog closes.</summary>
    public int FilterIndex { get; set; } = 1;

    /// <summary>Gets or sets the full path of the (first) chosen file; before showing, the name (or path) to start with.</summary>
    public string FileName
    {
        get => _fileNames.Length > 0 ? _fileNames[0] : string.Empty;
        set => _fileNames = string.IsNullOrEmpty(value) ? [] : [value];
    }

    /// <summary>Gets the full paths of the chosen files.</summary>
    public string[] FileNames => [.. _fileNames];

    /// <summary>Gets the name (without folder) of the chosen file.</summary>
    public string SafeFileName => Path.GetFileName(FileName);

    /// <summary>Gets the names (without folders) of the chosen files.</summary>
    public string[] SafeFileNames => [.. _fileNames.Select(Path.GetFileName).Select(n => n ?? string.Empty)];

    /// <summary>Gets or sets the extension added to names without one (with <see cref="AddExtension"/>), e.g. "txt" or ".txt".</summary>
    public string? DefaultExt { get; set; }

    /// <summary>
    /// Gets or sets whether a name without an extension gets one: the chosen file type's (e.g. ".txt" for "*.txt"), else
    /// <see cref="DefaultExt"/>. The default is <c>true</c>.
    /// </summary>
    public bool AddExtension { get; set; } = true;

    /// <summary>Gets or sets whether the chosen file must exist. The default is <c>false</c> (<c>true</c> for <see cref="OpenFileDialog"/>).</summary>
    public bool CheckFileExists { get; set; }

    /// <summary>Gets or sets whether the chosen file's folder must exist. The default is <c>true</c>.</summary>
    public bool CheckPathExists { get; set; } = true;

    /// <summary>Occurs when the user accepted files, before the dialog closes; cancel to keep it open (e.g. after showing why).</summary>
    public event EventHandler<CancelEventArgs>? FileOk;

    /// <summary>Gets the chosen file type while the dialog is shown (or the one <see cref="FilterIndex"/> selects).</summary>
    public FileDialogFilter? CurrentFilter => FilterIndex >= 1 && FilterIndex <= _filters.Count ? _filters[FilterIndex - 1] : null;

    /// <inheritdoc/>
    protected override IEnumerable<string?> StartCandidates()
    {
        yield return InitialDirectory;
        // A start file name with a folder starts there.
        if (FileName.Length > 0 && FileSystem.IsPathRooted(FileName)) yield return FileSystem.GetParent(FileName);
        foreach (var candidate in base.StartCandidates()) yield return candidate;
    }

    /// <summary>Gets the name to show in the name box at start: the file name without its folder.</summary>
    public string InitialName => FileName.Length > 0 && FileSystem.IsPathRooted(FileName) ? FileSystem.GetName(FileName) : FileName;

    // The extension to add to a name without one, or null.
    internal string? ExtensionToAdd()
    {
        if (!AddExtension) return null;
        string? extension = CurrentFilter?.DefaultExtension;
        if (extension == null && !string.IsNullOrEmpty(DefaultExt)) extension = DefaultExt.StartsWith('.') ? DefaultExt : "." + DefaultExt;
        return extension;
    }

    /// <summary>Stores the accepted files and raises <see cref="FileOk"/>; returns <c>false</c> if a handler canceled.</summary>
    protected bool Complete(IReadOnlyList<string> paths)
    {
        var previous = _fileNames;
        _fileNames = [.. paths];
        var args = new CancelEventArgs();
        FileOk?.Invoke(this, args);
        if (args.Cancel)
        {
            _fileNames = previous;
            return false;
        }
        return true;
    }

    // Common handling of one typed name: a folder navigates, a pattern filters. Returns true when handled.
    internal bool TryNavigateOrFilter(FileDialogView view, string directory, IReadOnlyList<string> names)
    {
        if (names.Count != 1) return false;
        string name = names[0];
        if (name.IndexOfAny(['*', '?']) >= 0)
        {
            view.ApplyNamePattern(name);
            return true;
        }
        string full;
        try
        {
            full = FileSystem.GetFullPath(FileSystem.Combine(directory, name));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }
        if (FileSystem.DirectoryExists(full))
        {
            view.Navigate(full);
            view.NameBox.Text = string.Empty;
            return true;
        }
        return false;
    }

    // The full path of a typed name, or null (after a message) if the name isn't valid.
    internal async Task<string?> ResolveAsync(FileDialogView view, string directory, string name)
    {
        if (ValidateNames && HasInvalidChars(name))
        {
            await view.ShowMessageAsync($"The file name \"{name}\" isn't valid.", DialogButtons.Ok);
            return null;
        }
        try
        {
            return FileSystem.GetFullPath(FileSystem.Combine(directory, name));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            await view.ShowMessageAsync($"The file name \"{name}\" isn't valid.", DialogButtons.Ok);
            return null;
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        base.Reset();
        Filter = null;
        FilterIndex = 1;
        _fileNames = [];
        DefaultExt = null;
        AddExtension = true;
        CheckPathExists = true;
    }
}

/// <summary>
/// Lets the user pick one or several (<see cref="Multiselect"/>) existing files, like WPF's OpenFileDialog, in a
/// dialog of the window that looks the same on every platform.
/// </summary>
/// <example>
/// <code>
/// var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg|All files|*.*", Multiselect = true };
/// if (await dialog.ShowAsync(this))
///     foreach (var path in dialog.FileNames) Load(path);
/// </code>
/// </example>
public class OpenFileDialog : FileDialog
{
    /// <summary>Initializes the dialog; <see cref="FileDialog.CheckFileExists"/> is on.</summary>
    public OpenFileDialog()
    {
        CheckFileExists = true;
    }

    /// <summary>Gets or sets whether several files can be chosen (Ctrl and Shift in the list, or "a" "b" typed). The default is <c>false</c>.</summary>
    public bool Multiselect { get; set; }

    /// <summary>Gets or sets whether the view has an "Open as read-only" check box. The default is <c>false</c>.</summary>
    public bool ShowReadOnly { get; set; }

    /// <summary>Gets or sets whether "Open as read-only" is checked; updated when the dialog closes.</summary>
    public bool ReadOnlyChecked { get; set; }

    /// <inheritdoc/>
    protected override string DefaultTitle => "Open";

    /// <inheritdoc/>
    public override string AcceptButtonText => "Open";

    /// <inheritdoc/>
    public override bool AllowsMultipleSelection => Multiselect;

    /// <summary>Opens the chosen file for reading.</summary>
    /// <exception cref="InvalidOperationException">No file was chosen.</exception>
    public Stream OpenFile() => FileName.Length > 0 ? File.OpenRead(FileName) : throw new InvalidOperationException("No file was chosen.");

    /// <summary>Opens the chosen files for reading.</summary>
    public Stream[] OpenFiles() => [.. FileNames.Select(File.OpenRead)];

    /// <inheritdoc/>
    protected internal override async Task<bool> AcceptAsync(FileDialogView view, string directory, IReadOnlyList<string> names)
    {
        if (names.Count == 0) return false;
        if (TryNavigateOrFilter(view, directory, names)) return false;
        if (names.Count > 1 && !Multiselect)
        {
            await view.ShowMessageAsync("Choose a single file.", DialogButtons.Ok);
            return false;
        }

        var paths = new List<string>();
        foreach (var name in names)
        {
            string? path = await ResolveAsync(view, directory, name);
            if (path == null) return false;

            // "report" finds "report.txt" when the type's extension is added.
            if (!FileSystem.FileExists(path) && Path.GetExtension(path).Length == 0 && ExtensionToAdd() is { } extension && FileSystem.FileExists(path + extension))
            {
                path += extension;
            }

            string? folder = FileSystem.GetParent(path);
            if (CheckPathExists && (folder == null || !FileSystem.DirectoryExists(folder)))
            {
                await view.ShowMessageAsync($"{name}\nThe path doesn't exist. Check the path and try again.", DialogButtons.Ok);
                return false;
            }
            if (CheckFileExists && !FileSystem.FileExists(path))
            {
                await view.ShowMessageAsync($"{name}\nFile not found. Check the file name and try again.", DialogButtons.Ok);
                return false;
            }
            paths.Add(path);
        }

        ReadOnlyChecked = view.ReadOnlyBox.IsChecked == true;
        return Complete(paths);
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        base.Reset();
        CheckFileExists = true;
        Multiselect = ShowReadOnly = ReadOnlyChecked = false;
    }
}

/// <summary>
/// Lets the user choose where to save a file, like WPF's SaveFileDialog: a name (with the chosen type's extension
/// added), a prompt before replacing an existing file, and a "New folder" button.
/// </summary>
/// <example>
/// <code>
/// var dialog = new SaveFileDialog { Filter = "Text files (*.txt)|*.txt", FileName = "notes" };
/// if (await dialog.ShowAsync(this)) File.WriteAllText(dialog.FileName, text);
/// </code>
/// </example>
public class SaveFileDialog : FileDialog
{
    /// <summary>Gets or sets whether to ask before replacing an existing file. The default is <c>true</c>.</summary>
    public bool OverwritePrompt { get; set; } = true;

    /// <summary>Gets or sets whether to ask before creating a file that doesn't exist. The default is <c>false</c>.</summary>
    public bool CreatePrompt { get; set; }

    /// <inheritdoc/>
    protected override string DefaultTitle => "Save As";

    /// <inheritdoc/>
    public override string AcceptButtonText => "Save";

    /// <inheritdoc/>
    public override bool ShowsNewFolderButton => true;

    /// <summary>Creates (or truncates) the chosen file for writing.</summary>
    /// <exception cref="InvalidOperationException">No file was chosen.</exception>
    public Stream OpenFile() => FileName.Length > 0 ? File.Create(FileName) : throw new InvalidOperationException("No file was chosen.");

    /// <inheritdoc/>
    protected internal override async Task<bool> AcceptAsync(FileDialogView view, string directory, IReadOnlyList<string> names)
    {
        if (names.Count == 0) return false;
        if (TryNavigateOrFilter(view, directory, names)) return false;
        string name = names[0];
        string? path = await ResolveAsync(view, directory, name);
        if (path == null) return false;

        if (Path.GetExtension(path).Length == 0 && ExtensionToAdd() is { } extension) path += extension;
        string fileName = FileSystem.GetName(path);

        string? folder = FileSystem.GetParent(path);
        if (CheckPathExists && (folder == null || !FileSystem.DirectoryExists(folder)))
        {
            await view.ShowMessageAsync($"{name}\nThe path doesn't exist. Check the path and try again.", DialogButtons.Ok);
            return false;
        }
        if (FileSystem.DirectoryExists(path))
        {
            view.Navigate(path);
            return false;
        }

        bool exists = FileSystem.FileExists(path);
        if (exists && OverwritePrompt
            && await view.ShowMessageAsync($"{fileName} already exists.\nDo you want to replace it?", DialogButtons.YesNo, "Confirm Save As") != DialogResult.Yes)
        {
            return false;
        }
        if (!exists && CheckFileExists)
        {
            await view.ShowMessageAsync($"{fileName}\nFile not found. Check the file name and try again.", DialogButtons.Ok);
            return false;
        }
        if (!exists && CreatePrompt
            && await view.ShowMessageAsync($"{fileName} doesn't exist.\nDo you want to create it?", DialogButtons.YesNo, "Confirm Save As") != DialogResult.Yes)
        {
            return false;
        }
        return Complete([path]);
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        base.Reset();
        OverwritePrompt = true;
        CreatePrompt = false;
    }
}

/// <summary>
/// Lets the user pick one or several (<see cref="Multiselect"/>) folders, like WPF's OpenFolderDialog and WinForms'
/// FolderBrowserDialog. The list shows folders only; accepting with nothing selected picks the folder shown.
/// </summary>
/// <example>
/// <code>
/// var dialog = new FolderBrowserDialog { Description = "Where should the export go?" };
/// if (await dialog.ShowAsync(this)) Export(dialog.FolderName);
/// </code>
/// </example>
public class FolderBrowserDialog : CommonItemDialog
{
    private string[] _folderNames = [];

    /// <summary>Gets or sets whether several folders can be chosen. The default is <c>false</c>.</summary>
    public bool Multiselect { get; set; }

    /// <summary>Gets or sets a text shown under the title, e.g. what the folder is for.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets whether the view has a "New folder" button. The default is <c>true</c>.</summary>
    public bool ShowNewFolderButton { get; set; } = true;

    /// <summary>Gets or sets the full path of the (first) chosen folder; before showing, a folder to start in.</summary>
    public string FolderName
    {
        get => _folderNames.Length > 0 ? _folderNames[0] : string.Empty;
        set => _folderNames = string.IsNullOrEmpty(value) ? [] : [value];
    }

    /// <summary>Gets the full paths of the chosen folders.</summary>
    public string[] FolderNames => [.. _folderNames];

    /// <summary>Gets the name (without its parent) of the chosen folder.</summary>
    public string SafeFolderName => FolderName.Length > 0 ? FileSystem.GetName(FolderName) : string.Empty;

    /// <summary>Gets the names (without parents) of the chosen folders.</summary>
    public string[] SafeFolderNames => [.. _folderNames.Select(FileSystem.GetName)];

    /// <summary>Occurs when the user accepted folders, before the dialog closes; cancel to keep it open.</summary>
    public event EventHandler<CancelEventArgs>? FolderOk;

    /// <inheritdoc/>
    protected override string DefaultTitle => "Select Folder";

    /// <inheritdoc/>
    public override string AcceptButtonText => "Select Folder";

    /// <inheritdoc/>
    public override bool ShowsFiles => false;

    /// <inheritdoc/>
    public override bool AllowsMultipleSelection => Multiselect;

    /// <inheritdoc/>
    public override bool ShowsNewFolderButton => ShowNewFolderButton;

    /// <inheritdoc/>
    public override string NameLabel => "Folder";

    /// <inheritdoc/>
    protected override IEnumerable<string?> StartCandidates()
    {
        yield return InitialDirectory;
        if (FolderName.Length > 0) yield return FolderName;
        foreach (var candidate in base.StartCandidates()) yield return candidate;
    }

    /// <inheritdoc/>
    protected internal override async Task<bool> AcceptAsync(FileDialogView view, string directory, IReadOnlyList<string> names)
    {
        var paths = new List<string>();
        if (names.Count == 0)
        {
            paths.Add(directory); // nothing chosen: the folder shown
        }
        else
        {
            if (names.Count > 1 && !Multiselect)
            {
                await view.ShowMessageAsync("Choose a single folder.", DialogButtons.Ok);
                return false;
            }
            foreach (var name in names)
            {
                if (ValidateNames && HasInvalidChars(name))
                {
                    await view.ShowMessageAsync($"The folder name \"{name}\" isn't valid.", DialogButtons.Ok);
                    return false;
                }
                string path;
                try
                {
                    path = FileSystem.GetFullPath(FileSystem.Combine(directory, name));
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
                {
                    await view.ShowMessageAsync($"The folder name \"{name}\" isn't valid.", DialogButtons.Ok);
                    return false;
                }
                if (!FileSystem.DirectoryExists(path))
                {
                    await view.ShowMessageAsync($"{name}\nThe folder doesn't exist. Check the name and try again.", DialogButtons.Ok);
                    return false;
                }
                if (!IsInsideRoot(path))
                {
                    await view.ShowMessageAsync($"{name}\nChoose a folder inside {RootDirectory}.", DialogButtons.Ok);
                    return false;
                }
                paths.Add(path);
            }
        }

        var previous = _folderNames;
        _folderNames = [.. paths];
        var args = new CancelEventArgs();
        FolderOk?.Invoke(this, args);
        if (args.Cancel)
        {
            _folderNames = previous;
            return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        base.Reset();
        _folderNames = [];
        Multiselect = false;
        Description = null;
        ShowNewFolderButton = true;
    }
}
