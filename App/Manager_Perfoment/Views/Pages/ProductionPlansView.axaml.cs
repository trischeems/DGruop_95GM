using Avalonia.Controls;

namespace DGroup.App.ManagerPerformance.Views.Pages;

public partial class ProductionPlansView : UserControl
{
    public ProductionPlansView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
