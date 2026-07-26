using Avalonia.Controls;
using DGroup.App.ManagerPerformance.Views;

namespace DGroup.App.ManagerPerformance.Views.Pages;

public partial class RoutingsView : UserControl
{
    public RoutingsView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
