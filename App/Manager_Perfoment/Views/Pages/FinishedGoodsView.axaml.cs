using Avalonia.Controls;

namespace DGroup.App.ManagerPerformance.Views.Pages;

public partial class FinishedGoodsView : UserControl
{
    public FinishedGoodsView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
