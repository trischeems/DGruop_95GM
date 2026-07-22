using Avalonia.Controls;

namespace DGroup.App.ManagerPerformance.Views.Pages;

public partial class BomView : UserControl
{
    public BomView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
