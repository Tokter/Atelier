using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Atelier.Gallery.ViewModels;

public partial class FileDialogsViewModel : PageViewModel
{
    // Open
    [ObservableProperty]
    private bool _multiselect;

    [ObservableProperty]
    private bool _showReadOnly;

    [ObservableProperty]
    private bool _checkFileExists = true;

    [ObservableProperty]
    private bool _showHiddenItems;

    [ObservableProperty]
    private string _openResult = "No file opened yet";

    // Save
    [ObservableProperty]
    private bool _overwritePrompt = true;

    [ObservableProperty]
    private bool _createPrompt;

    [ObservableProperty]
    private bool _addExtension = true;

    [ObservableProperty]
    private string _saveResult = "Nothing saved yet";

    // Folder
    [ObservableProperty]
    private bool _folderMultiselect;

    [ObservableProperty]
    private bool _showNewFolderButton = true;

    [ObservableProperty]
    private string _folderResult = "No folder chosen yet";

    public FileDialogsViewModel()
    {
        PageIcon = MaterialIconKind.FolderOpen;
        PageTitle = "File Dialogs";
        Keywords = "open save file folder browser dialog filter path explorer";
    }
}
