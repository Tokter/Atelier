using Atelier.Charts;
using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Atelier.Gallery.ViewModels;

public partial class ChartsViewModel : PageViewModel
{
    [ObservableProperty]
    private string _visibleRange = string.Empty;

    [ObservableProperty]
    private bool _showLegend = true;

    public ChartsViewModel()
    {
        PageIcon = MaterialIconKind.ShowChart;
        PageTitle = "Charts";
        CommandGroup = XYChart.CommandGroup;
        Keywords = "chart xy plot line scatter series axis legend annotation zoom pan polar graph";
    }
}
