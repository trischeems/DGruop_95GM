using Avalonia.Controls;

namespace DGroup.App.ManagerPerformance.Views.Pages;

public partial class ProductionOrdersView : UserControl
{
    public ProductionOrdersView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
