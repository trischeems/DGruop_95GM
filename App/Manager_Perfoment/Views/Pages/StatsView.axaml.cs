using Avalonia.Controls;

namespace GM95.App.ManagerPerformance.Views.Pages;

public partial class StatsView : UserControl
{
    public StatsView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
