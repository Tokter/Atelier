using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class ListsViewModel : PageViewModel
{
    public ListsViewModel()
    {
        PageIcon = MaterialIconKind.ViewList;
        PageTitle = "Lists";
        Keywords = "listbox itemscontrol list items selection";
    }
}
