using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class RangeControlsViewModel : PageViewModel
{
    public RangeControlsViewModel()
    {
        PageIcon = MaterialIconKind.Tune;
        PageTitle = "Sliders & Progress";
        Keywords = "slider progressbar progress range value";
    }
}
