using Avalonia.Controls;
using DGroup.App.ManagerPerformance.Views;

namespace DGroup.App.ManagerPerformance.Views.Dialogs;

public partial class ProductFormView : UserControl
{
    public ProductFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
