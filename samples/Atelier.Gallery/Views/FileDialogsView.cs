using System;
using System.Linq;
using Atelier.Controls;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class FileDialogsView : GalleryPage
{
    private readonly FileDialogsViewModel _vm;

    // Kept between openings, like an application would, so the last folder, type and columns come back.
    private readonly OpenFileDialog _open = new()
    {
        Title = "Open a document",
        Filter = "Documents (*.txt;*.md;*.pdf)|*.txt;*.md;*.pdf|Images (*.png;*.jpg;*.gif;*.svg)|*.png;*.jpg;*.jpeg;*.gif;*.svg|Code (*.cs;*.json;*.xml)|*.cs;*.json;*.xml|All files (*.*)|*.*",
        FilterIndex = 4,
    };

    private readonly SaveFileDialog _save = new()
    {
        Filter = "Text file (*.txt)|*.txt|Markdown (*.md)|*.md|All files (*.*)|*.*",
        FileName = "notes",
    };

    private readonly FolderBrowserDialog _folder = new()
    {
        Description = "Choose where the export goes. With nothing selected, Select Folder picks the folder shown.",
    };

    public FileDialogsView(FileDialogsViewModel viewModel)
        : base(MaterialIconKind.FolderOpen, "File Dialogs",
            "Open, save and folder dialogs that look and work the same on every platform: a path bar with clickable " +
            "folders, a folder tree, and the folder's items in a data grid with optional columns. The properties follow " +
            "WPF's OpenFileDialog, SaveFileDialog and OpenFolderDialog.")
    {
        _vm = viewModel;
        Settings(new Switch("Show hidden items").ShowThumbIcon().BindIsChecked(_vm, v => v.ShowHiddenItems, (v, on) => v.ShowHiddenItems = on));
        Sections(OpenSection(), SaveSection(), FolderSection(), ColumnsSection());
    }

    private UIElement OpenSection() => Ui.Section("OpenFileDialog",
        "Pick existing files: double-click a folder to open it, a file to pick it, or type a name (\"notes\" finds " +
        "notes.txt), a folder or a pattern such as *.log. The path bar's folders are buttons; click beside them (or " +
        "Ctrl+L) to type a path. Right-click the column headers to add columns such as Date created or Attributes.",
        Ui.Row(
            new Switch("Multiselect").ShowThumbIcon().BindIsChecked(_vm, v => v.Multiselect, (v, on) => v.Multiselect = on),
            new Switch("Read-only box").ShowThumbIcon().BindIsChecked(_vm, v => v.ShowReadOnly, (v, on) => v.ShowReadOnly = on),
            new Switch("Check file exists").ShowThumbIcon().BindIsChecked(_vm, v => v.CheckFileExists, (v, on) => v.CheckFileExists = on)),
        Ui.Row(
            Ui.IconButton(MaterialIconKind.FolderOpen, "Open…").OnClick(async (s, _) =>
            {
                _open.Multiselect = _vm.Multiselect;
                _open.ShowReadOnly = _vm.ShowReadOnly;
                _open.CheckFileExists = _vm.CheckFileExists;
                _open.ShowHiddenItems = _vm.ShowHiddenItems;
                if (await _open.ShowAsync((UIElement)s!))
                {
                    _vm.OpenResult = string.Join("\n", _open.FileNames) + (_open.ReadOnlyChecked ? "\n(read-only)" : "");
                }
                else
                {
                    _vm.OpenResult = "Canceled";
                }
            }),
            Ui.Readout(_vm, v => v.OpenResult)),
        Ui.Code(
            "var dialog = new OpenFileDialog\n" +
            "{\n" +
            "    Filter = \"Documents|*.txt;*.md;*.pdf|All files (*.*)|*.*\",\n" +
            "    Multiselect = true,\n" +
            "};\n" +
            "if (await dialog.ShowAsync(this))\n" +
            "    foreach (var path in dialog.FileNames) Open(path);"));

    private UIElement SaveSection() => Ui.Section("SaveFileDialog",
        "Choose a name and folder: the chosen type's extension is added to names without one, and changing the type " +
        "changes it. Saving over an existing file asks first (OverwritePrompt); CreatePrompt asks before a new file. " +
        "New folder creates a folder and lets you name it; F2 renames the selected item.",
        Ui.Row(
            new Switch("Overwrite prompt").ShowThumbIcon().BindIsChecked(_vm, v => v.OverwritePrompt, (v, on) => v.OverwritePrompt = on),
            new Switch("Create prompt").ShowThumbIcon().BindIsChecked(_vm, v => v.CreatePrompt, (v, on) => v.CreatePrompt = on),
            new Switch("Add extension").ShowThumbIcon().BindIsChecked(_vm, v => v.AddExtension, (v, on) => v.AddExtension = on)),
        Ui.Row(
            Ui.IconButton(MaterialIconKind.Save, "Save as…").OnClick(async (s, _) =>
            {
                _save.OverwritePrompt = _vm.OverwritePrompt;
                _save.CreatePrompt = _vm.CreatePrompt;
                _save.AddExtension = _vm.AddExtension;
                _save.ShowHiddenItems = _vm.ShowHiddenItems;
                // The gallery doesn't write anything: it only shows where the file would go.
                _vm.SaveResult = await _save.ShowAsync((UIElement)s!) ? $"Would save to {_save.FileName} (type {_save.FilterIndex})" : "Canceled";
            }),
            Ui.Readout(_vm, v => v.SaveResult)),
        Ui.Code(
            "var dialog = new SaveFileDialog { Filter = \"Text file (*.txt)|*.txt\", FileName = \"notes\" };\n" +
            "if (await dialog.ShowAsync(this))\n" +
            "    File.WriteAllText(dialog.FileName, text);"));

    private UIElement FolderSection() => Ui.Section("FolderBrowserDialog",
        "Pick one or several folders: the list shows folders only, and a description explains what the folder is for.",
        Ui.Row(
            new Switch("Multiselect").ShowThumbIcon().BindIsChecked(_vm, v => v.FolderMultiselect, (v, on) => v.FolderMultiselect = on),
            new Switch("New folder button").ShowThumbIcon().BindIsChecked(_vm, v => v.ShowNewFolderButton, (v, on) => v.ShowNewFolderButton = on)),
        Ui.Row(
            Ui.IconButton(MaterialIconKind.Folder, "Select folder…").OnClick(async (s, _) =>
            {
                _folder.Multiselect = _vm.FolderMultiselect;
                _folder.ShowNewFolderButton = _vm.ShowNewFolderButton;
                _folder.ShowHiddenItems = _vm.ShowHiddenItems;
                _vm.FolderResult = await _folder.ShowAsync((UIElement)s!) ? string.Join("\n", _folder.FolderNames) : "Canceled";
            }),
            Ui.Readout(_vm, v => v.FolderResult)),
        Ui.Code(
            "var dialog = new FolderBrowserDialog { Description = \"Where should the export go?\" };\n" +
            "if (await dialog.ShowAsync(this)) Export(dialog.FolderName);"));

    private static UIElement ColumnsSection() => Ui.Section("Columns and layout",
        "The list shows Name, Date modified, Type and Size; the column chooser (right-click a header) adds Date created, " +
        "Date accessed, Extension, Attributes, Read-only, Hidden, Link target and Full path. Columns, widths and sort are " +
        "kept for the next dialog (SharedColumnLayout); store ColumnLayout.ToJson() to keep them between sessions. " +
        "FileSystem takes any IFileSystemProvider, e.g. for an archive or a remote share.",
        Ui.Code(
            "settings.FileColumns = dialog.ColumnLayout?.ToJson();   // after closing\n" +
            "CommonItemDialog.SharedColumnLayout = DataGridLayout.FromJson(settings.FileColumns);   // at startup"));
}
