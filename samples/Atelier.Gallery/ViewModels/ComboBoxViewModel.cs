using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class ComboBoxViewModel : PageViewModel
{
    public ComboBoxViewModel()
    {
        PageIcon = MaterialIconKind.ArrowDropDownCircle;
        PageTitle = "ComboBox";
        Keywords = "combobox select dropdown picker";
    }
}
