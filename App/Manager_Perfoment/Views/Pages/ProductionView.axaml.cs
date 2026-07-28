using Avalonia.Controls;

namespace GM95.App.ManagerPerformance.Views.Pages;

public partial class ProductionView : UserControl
{
    public ProductionView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
