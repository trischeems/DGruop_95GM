using Avalonia.Controls;
using GM95.App.ManagerPerformance.Views;

namespace GM95.App.ManagerPerformance.Views.Dialogs;

public partial class ProductFormView : UserControl
{
    public ProductFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
