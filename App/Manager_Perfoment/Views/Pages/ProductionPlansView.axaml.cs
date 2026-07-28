using Avalonia.Controls;

namespace GM95.App.ManagerPerformance.Views.Pages;

public partial class ProductionPlansView : UserControl
{
    public ProductionPlansView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
