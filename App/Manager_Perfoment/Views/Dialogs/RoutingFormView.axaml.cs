using Avalonia.Controls;
using DGroup.App.ManagerPerformance.Views;

namespace DGroup.App.ManagerPerformance.Views.Dialogs;

public partial class RoutingFormView : UserControl
{
    public RoutingFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
