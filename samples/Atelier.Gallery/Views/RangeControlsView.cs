using Atelier.Controls;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class RangeControlsView : GalleryPage
{
    private readonly RangeControlsViewModel _vm;

    public RangeControlsView(RangeControlsViewModel viewModel)
        : base(MaterialIconKind.Tune, "Sliders & Progress", "Coming soon.")
    {
        _vm = viewModel;
    }
}
