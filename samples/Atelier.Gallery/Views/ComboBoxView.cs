using Atelier.Controls;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class ComboBoxView : GalleryPage
{
    private readonly ComboBoxViewModel _vm;

    public ComboBoxView(ComboBoxViewModel viewModel)
        : base(MaterialIconKind.ArrowDropDownCircle, "ComboBox", "Coming soon.")
    {
        _vm = viewModel;
    }
}
