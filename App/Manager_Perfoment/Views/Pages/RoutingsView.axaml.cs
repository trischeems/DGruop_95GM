using Avalonia.Controls;
using GM95.App.ManagerPerformance.Views;

namespace GM95.App.ManagerPerformance.Views.Pages;

public partial class RoutingsView : UserControl
{
    public RoutingsView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
