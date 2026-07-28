using Avalonia.Controls;

namespace GM95.App.ManagerPerformance.Views.Pages;

public partial class ProductionOrdersView : UserControl
{
    public ProductionOrdersView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
