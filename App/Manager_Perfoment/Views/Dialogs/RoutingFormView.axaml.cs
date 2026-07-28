using Avalonia.Controls;
using GM95.App.ManagerPerformance.Views;

namespace GM95.App.ManagerPerformance.Views.Dialogs;

public partial class RoutingFormView : UserControl
{
    public RoutingFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
