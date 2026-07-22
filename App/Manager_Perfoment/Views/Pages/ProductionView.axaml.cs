using Avalonia.Controls;

namespace DGroup.App.ManagerPerformance.Views.Pages;

public partial class ProductionView : UserControl
{
    public ProductionView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
