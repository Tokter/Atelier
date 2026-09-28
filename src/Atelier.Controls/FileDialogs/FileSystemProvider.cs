using System;
using System.Collections.Generic;
using System.IO;

namespace Atelier.Controls;

/// <summary>A file or folder as the file dialogs show it.</summary>
public sealed class FileSystemEntry
{
    /// <summary>Initializes an entry.</summary>
    public FileSystemEntry(string fullPath, string name, bool isDirectory)
    {
        FullPath = fullPath;
        Name = name;
        IsDirectory = isDirectory;
    }

    /// <summary>Gets the full path.</summary>
    public string FullPath { get; }

    /// <summary>Gets the name (with extension).</summary>
    public string Name { get; }

    /// <summary>Gets whether this is a folder.</summary>
    public bool IsDirectory { get; }

    /// <summary>Gets or sets the size in bytes; <c>null</c> for folders.</summary>
    public long? Length { get; init; }

    /// <summary>Gets or sets when the entry was last changed.</summary>
    public DateTime? LastWriteTime { get; init; }

    /// <summary>Gets or sets when the entry was created.</summary>
    public DateTime? CreationTime { get; init; }

    /// <summary>Gets or sets when the entry was last opened.</summary>
    public DateTime? LastAccessTime { get; init; }

    /// <summary>Gets or sets the file system attributes.</summary>
    public FileAttributes Attributes { get; init; }

    /// <summary>Gets or sets where a link points to, or <c>null</c> if the entry isn't a link.</summary>
    public string? LinkTarget { get; init; }

    /// <summary>Gets the extension including the dot, or empty (always empty for folders).</summary>
    public string Extension => IsDirectory ? string.Empty : Path.GetExtension(Name);

    /// <summary>Gets whether the entry is hidden (the hidden attribute, or a name starting with a dot).</summary>
    public bool IsHidden => (Attributes & FileAttributes.Hidden) != 0 || Name.StartsWith('.');

    /// <summary>Gets whether the entry is read-only.</summary>
    public bool IsReadOnly => (Attributes & FileAttributes.ReadOnly) != 0;

    /// <summary>Gets the type description, e.g. "PNG image" or "File folder" (see <see cref="FileTypes"/>).</summary>
    public string TypeName => FileTypes.GetTypeName(this);

    /// <summary>Gets the attributes as letters, like a file manager: R(ead-only), H(idden), S(ystem), A(rchive), C(ompressed), E(ncrypted), L(ink).</summary>
    public string AttributeLetters
    {
        get
        {
            Span<char> letters = stackalloc char[7];
            int n = 0;
            if (IsReadOnly) letters[n++] = 'R';
            if (IsHidden) letters[n++] = 'H';
            if ((Attributes & FileAttributes.System) != 0) letters[n++] = 'S';
            if ((Attributes & FileAttributes.Archive) != 0) letters[n++] = 'A';
            if ((Attributes & FileAttributes.Compressed) != 0) letters[n++] = 'C';
            if ((Attributes & FileAttributes.Encrypted) != 0) letters[n++] = 'E';
            if (LinkTarget != null || (Attributes & FileAttributes.ReparsePoint) != 0) letters[n++] = 'L';
            return new string(letters[..n]);
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>A place offered in the file dialogs' folder tree: a known folder, a drive or a custom place.</summary>
/// <param name="Name">The name shown.</param>
/// <param name="Path">The folder.</param>
/// <param name="Icon">The icon shown.</param>
public sealed record FileSystemPlace(string Name, string Path, MaterialIconKind Icon);

/// <summary>
/// The file system the file dialogs browse. <see cref="PhysicalFileSystemProvider"/> is the computer's (on any
/// platform); implement it for virtual file systems (an archive, a remote share) or for tests.
/// </summary>
public interface IFileSystemProvider
{
    /// <summary>Gets the case rule of paths (ordinal ignoring case on Windows and macOS, ordinal elsewhere).</summary>
    StringComparison PathComparison { get; }

    /// <summary>Gets the known folders offered first in the tree (home, desktop, documents, ...), those that exist.</summary>
    IReadOnlyList<FileSystemPlace> GetPlaces();

    /// <summary>Gets the roots: drives, or "/".</summary>
    IReadOnlyList<FileSystemPlace> GetRoots();

    /// <summary>Gets the folders and files in <paramref name="directory"/>, hidden ones included; entries that can't be read are skipped.</summary>
    /// <exception cref="UnauthorizedAccessException">The folder can't be listed.</exception>
    /// <exception cref="IOException">The folder can't be listed.</exception>
    IReadOnlyList<FileSystemEntry> GetEntries(string directory);

    /// <summary>Gets whether <paramref name="path"/> is an existing folder.</summary>
    bool DirectoryExists(string path);

    /// <summary>Gets whether <paramref name="path"/> is an existing file.</summary>
    bool FileExists(string path);

    /// <summary>Gets the folder that contains <paramref name="path"/>, or <c>null</c> for a root.</summary>
    string? GetParent(string path);

    /// <summary>Combines a folder and a relative path (or returns the path if it is absolute).</summary>
    string Combine(string directory, string path);

    /// <summary>Gets whether <paramref name="path"/> is absolute.</summary>
    bool IsPathRooted(string path);

    /// <summary>Normalizes a path: absolute, without "." and "..", without a trailing separator (except for roots).</summary>
    string GetFullPath(string path);

    /// <summary>Gets the name of the last part of <paramref name="path"/> (for a root, the root itself, e.g. "C:").</summary>
    string GetName(string path);

    /// <summary>Gets the characters a file name can't contain.</summary>
    IReadOnlyCollection<char> InvalidFileNameChars { get; }

    /// <summary>Creates a folder.</summary>
    void CreateDirectory(string path);

    /// <summary>Renames or moves a file or folder.</summary>
    void Move(string path, string newPath);
}

/// <summary>The computer's file system through <see cref="System.IO"/>, on Windows, Linux and macOS.</summary>
public class PhysicalFileSystemProvider : IFileSystemProvider
{
    /// <summary>Gets the shared instance.</summary>
    public static PhysicalFileSystemProvider Instance { get; } = new();

    private static readonly EnumerationOptions s_options = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = 0,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
    };

    /// <inheritdoc/>
    public StringComparison PathComparison { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <inheritdoc/>
    public IReadOnlyCollection<char> InvalidFileNameChars { get; } = Path.GetInvalidFileNameChars();

    /// <inheritdoc/>
    public virtual IReadOnlyList<FileSystemPlace> GetPlaces()
    {
        var places = new List<FileSystemPlace>();
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        void Add(string name, string? path, MaterialIconKind icon)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && !places.Exists(p => string.Equals(p.Path, path, PathComparison)))
            {
                places.Add(new FileSystemPlace(name, path, icon));
            }
        }

        Add("Home", home, MaterialIconKind.Home);
        Add("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), MaterialIconKind.DesktopWindows);
        Add("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), MaterialIconKind.Description);
        Add("Downloads", string.IsNullOrEmpty(home) ? null : Path.Combine(home, "Downloads"), MaterialIconKind.Download);
        Add("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), MaterialIconKind.PhotoLibrary);
        Add("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), MaterialIconKind.LibraryMusic);
        Add("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), MaterialIconKind.VideoLibrary);
        return places;
    }

    /// <inheritdoc/>
    public virtual IReadOnlyList<FileSystemPlace> GetRoots()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [new FileSystemPlace("/", "/", MaterialIconKind.Computer)];
        }

        var roots = new List<FileSystemPlace>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                string letter = drive.Name.TrimEnd('\\');
                string label = string.IsNullOrEmpty(drive.VolumeLabel) ? letter : $"{drive.VolumeLabel} ({letter})";
                var icon = drive.DriveType switch
                {
                    DriveType.Removable => MaterialIconKind.Usb,
                    DriveType.Network => MaterialIconKind.Dns,
                    DriveType.CDRom => MaterialIconKind.SdStorage,
                    _ => MaterialIconKind.Storage,
                };
                roots.Add(new FileSystemPlace(label, drive.RootDirectory.FullName, icon));
            }
            catch (IOException)
            {
                // A drive that went away while listing.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        return roots;
    }

    /// <inheritdoc/>
    public virtual IReadOnlyList<FileSystemEntry> GetEntries(string directory)
    {
        var entries = new List<FileSystemEntry>();
        foreach (var info in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", s_options))
        {
            try
            {
                bool isDirectory = info is DirectoryInfo;
                entries.Add(new FileSystemEntry(info.FullName, info.Name, isDirectory)
                {
                    Length = info is FileInfo file ? file.Length : null,
                    LastWriteTime = info.LastWriteTime,
                    CreationTime = info.CreationTime,
                    LastAccessTime = info.LastAccessTime,
                    Attributes = info.Attributes,
                    LinkTarget = info.LinkTarget,
                });
            }
            catch (IOException)
            {
                // Removed while listing, or a broken link.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        return entries;
    }

    /// <inheritdoc/>
    public virtual bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc/>
    public virtual bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public virtual string? GetParent(string path) => Path.GetDirectoryName(GetFullPath(path));

    /// <inheritdoc/>
    public virtual string Combine(string directory, string path) => Path.Combine(directory, path);

    /// <inheritdoc/>
    public virtual bool IsPathRooted(string path) => Path.IsPathFullyQualified(path) || (OperatingSystem.IsWindows() && path.StartsWith(@"\\", StringComparison.Ordinal));

    /// <inheritdoc/>
    public virtual string GetFullPath(string path)
    {
        string full = Path.GetFullPath(path);
        string? root = Path.GetPathRoot(full);
        return full.Length > (root?.Length ?? 0) ? Path.TrimEndingDirectorySeparator(full) : full;
    }

    /// <inheritdoc/>
    public virtual string GetName(string path)
    {
        string full = GetFullPath(path);
        string name = Path.GetFileName(full);
        return name.Length > 0 ? name : full.TrimEnd('\\', '/') is { Length: > 0 } root ? root : full;
    }

    /// <inheritdoc/>
    public virtual void CreateDirectory(string path) => Directory.CreateDirectory(path);

    /// <inheritdoc/>
    public virtual void Move(string path, string newPath)
    {
        if (Directory.Exists(path)) Directory.Move(path, newPath);
        else File.Move(path, newPath);
    }
}

/// <summary>Type names and icons of files by extension, as the file dialogs show them.</summary>
public static class FileTypes
{
    private static readonly Dictionary<string, (string Name, MaterialIconKind Icon)> s_types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = ("Text document", MaterialIconKind.Article),
        [".md"] = ("Markdown document", MaterialIconKind.Article),
        [".log"] = ("Log file", MaterialIconKind.Article),
        [".rtf"] = ("Rich text document", MaterialIconKind.Article),
        [".doc"] = ("Word document", MaterialIconKind.Article),
        [".docx"] = ("Word document", MaterialIconKind.Article),
        [".odt"] = ("OpenDocument text", MaterialIconKind.Article),
        [".pdf"] = ("PDF document", MaterialIconKind.PictureAsPdf),
        [".xls"] = ("Excel workbook", MaterialIconKind.TableChart),
        [".xlsx"] = ("Excel workbook", MaterialIconKind.TableChart),
        [".csv"] = ("CSV file", MaterialIconKind.TableChart),
        [".ppt"] = ("PowerPoint presentation", MaterialIconKind.Slideshow),
        [".pptx"] = ("PowerPoint presentation", MaterialIconKind.Slideshow),
        [".png"] = ("PNG image", MaterialIconKind.Image),
        [".jpg"] = ("JPEG image", MaterialIconKind.Image),
        [".jpeg"] = ("JPEG image", MaterialIconKind.Image),
        [".gif"] = ("GIF image", MaterialIconKind.Image),
        [".bmp"] = ("Bitmap image", MaterialIconKind.Image),
        [".webp"] = ("WebP image", MaterialIconKind.Image),
        [".svg"] = ("SVG drawing", MaterialIconKind.Image),
        [".ico"] = ("Icon", MaterialIconKind.Image),
        [".mp3"] = ("MP3 audio", MaterialIconKind.MusicNote),
        [".wav"] = ("WAV audio", MaterialIconKind.MusicNote),
        [".flac"] = ("FLAC audio", MaterialIconKind.MusicNote),
        [".ogg"] = ("Ogg audio", MaterialIconKind.MusicNote),
        [".mp4"] = ("MP4 video", MaterialIconKind.Movie),
        [".mov"] = ("QuickTime movie", MaterialIconKind.Movie),
        [".mkv"] = ("Matroska video", MaterialIconKind.Movie),
        [".avi"] = ("AVI video", MaterialIconKind.Movie),
        [".zip"] = ("Compressed folder", MaterialIconKind.Archive),
        [".7z"] = ("7-Zip archive", MaterialIconKind.Archive),
        [".rar"] = ("RAR archive", MaterialIconKind.Archive),
        [".tar"] = ("Tar archive", MaterialIconKind.Archive),
        [".gz"] = ("Gzip archive", MaterialIconKind.Archive),
        [".cs"] = ("C# source file", MaterialIconKind.Code),
        [".csproj"] = ("C# project", MaterialIconKind.Code),
        [".sln"] = ("Solution", MaterialIconKind.Code),
        [".slnx"] = ("Solution", MaterialIconKind.Code),
        [".xaml"] = ("XAML file", MaterialIconKind.Code),
        [".xml"] = ("XML document", MaterialIconKind.Code),
        [".json"] = ("JSON file", MaterialIconKind.DataObject),
        [".yml"] = ("YAML file", MaterialIconKind.Code),
        [".yaml"] = ("YAML file", MaterialIconKind.Code),
        [".html"] = ("HTML document", MaterialIconKind.Html),
        [".htm"] = ("HTML document", MaterialIconKind.Html),
        [".css"] = ("Stylesheet", MaterialIconKind.Css),
        [".js"] = ("JavaScript file", MaterialIconKind.Javascript),
        [".ts"] = ("TypeScript file", MaterialIconKind.Code),
        [".py"] = ("Python file", MaterialIconKind.Code),
        [".sh"] = ("Shell script", MaterialIconKind.Terminal),
        [".ps1"] = ("PowerShell script", MaterialIconKind.Terminal),
        [".bat"] = ("Batch file", MaterialIconKind.Terminal),
        [".cmd"] = ("Command script", MaterialIconKind.Terminal),
        [".exe"] = ("Application", MaterialIconKind.Apps),
        [".dll"] = ("Application extension", MaterialIconKind.Settings),
        [".ini"] = ("Configuration settings", MaterialIconKind.Settings),
        [".config"] = ("Configuration file", MaterialIconKind.Settings),
        [".lnk"] = ("Shortcut", MaterialIconKind.Link),
    };

    /// <summary>Gets the type name of an entry: "File folder", a known type, or "XYZ file".</summary>
    public static string GetTypeName(FileSystemEntry entry)
    {
        if (entry.IsDirectory) return "File folder";
        string extension = entry.Extension;
        if (extension.Length <= 1) return "File";
        return s_types.TryGetValue(extension, out var type) ? type.Name : $"{extension[1..].ToUpperInvariant()} file";
    }

    /// <summary>Gets the icon of an entry by its type.</summary>
    public static MaterialIconKind GetIcon(FileSystemEntry entry) =>
        entry.IsDirectory ? MaterialIconKind.Folder
        : s_types.TryGetValue(entry.Extension, out var type) ? type.Icon
        : MaterialIconKind.InsertDriveFile;

    /// <summary>Formats a size like a file manager: "0 KB" for empty files, whole kilobytes (rounded up) below a megabyte, then MB, GB.</summary>
    public static string FormatSize(long bytes) => bytes switch
    {
        0 => "0 KB",
        < 1024 * 1024 => $"{(bytes + 1023) / 1024:N0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.##} GB",
    };
}
