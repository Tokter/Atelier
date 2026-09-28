using System.Collections.ObjectModel;
using System.Linq;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

/// <summary>A document shown in a browser-style tab.</summary>
public partial class DocumentTab : ObservableObject
{
    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private MaterialIconKind _icon = MaterialIconKind.Description;

    [ObservableProperty]
    private bool _isModified;

    public string Body { get; init; } = "";
}

public partial class TabsViewModel : PageViewModel
{
    private int _untitled;

    public ObservableCollection<DocumentTab> Documents { get; } = [];

    [ObservableProperty]
    private int _selectedDocument;

    [ObservableProperty]
    private bool _closeable = true;

    [ObservableProperty]
    private bool _showAddButton = true;

    [ObservableProperty]
    private bool _canReorder = true;

    [ObservableProperty]
    private string _lastEvent = "Drag a tab, close one, or click +";

    [ObservableProperty]
    private string _order = "";

    public TabsViewModel()
    {
        PageIcon = MaterialIconKind.Tab;
        PageTitle = "Tabs";
        Keywords = "tab tabs tabcontrol tabitem browser close reorder drag header template";
        Documents.CollectionChanged += (_, _) => UpdateOrder();
        Reset();
    }

    private void UpdateOrder() => Order = "Order: " + string.Join(", ", Documents.Select(d => d.Title));

    public DocumentTab CreateDocument() => new()
    {
        Title = $"Untitled-{++_untitled}",
        Icon = MaterialIconKind.NoteAdd,
        IsModified = true,
        Body = "A new, unsaved document. Its dot shows that it has changes.",
    };

    [RelayCommand]
    [property: Command("Reset", "Tabs", Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
    private void Reset()
    {
        _untitled = 0;
        Documents.Clear();
        Documents.Add(new DocumentTab { Title = "README.md", Icon = MaterialIconKind.Description, Body = "Atelier is a retained-mode UI for .NET on SkiaSharp." });
        Documents.Add(new DocumentTab { Title = "Program.cs", Icon = MaterialIconKind.Code, IsModified = true, Body = "var app = new SilkApplication();" });
        Documents.Add(new DocumentTab { Title = "theme.json", Icon = MaterialIconKind.DataObject, Body = "{ \"seed\": \"#6750A4\" }" });
        Documents.Add(new DocumentTab { Title = "logo.png", Icon = MaterialIconKind.Image, Body = "An image file." });
        SelectedDocument = 0;
        Closeable = true;
        ShowAddButton = true;
        CanReorder = true;
        LastEvent = "Drag a tab, close one, or click +";
        UpdateOrder();
    }
}
