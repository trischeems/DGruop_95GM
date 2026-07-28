using Avalonia.Controls;
using GM95.App.ManagerPerformance.Views;

namespace GM95.App.ManagerPerformance.Views.Dialogs;

public partial class OrderRoutingFormView : UserControl
{
    public OrderRoutingFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
