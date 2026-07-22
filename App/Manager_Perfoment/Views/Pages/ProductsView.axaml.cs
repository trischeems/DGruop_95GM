using Avalonia.Controls;

namespace DGroup.App.ManagerPerformance.Views.Pages;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
