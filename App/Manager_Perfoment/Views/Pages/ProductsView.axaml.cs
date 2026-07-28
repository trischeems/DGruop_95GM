using Avalonia.Controls;

namespace GM95.App.ManagerPerformance.Views.Pages;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
