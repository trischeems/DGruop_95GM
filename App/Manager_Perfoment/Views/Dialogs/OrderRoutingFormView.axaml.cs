using Avalonia.Controls;
using DGroup.App.ManagerPerformance.Views;

namespace DGroup.App.ManagerPerformance.Views.Dialogs;

public partial class OrderRoutingFormView : UserControl
{
    public OrderRoutingFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
