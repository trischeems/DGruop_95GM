using Avalonia.Controls;

namespace GM95.App.ManagerPerformance.Views.Pages;

public partial class FinishedGoodsView : UserControl
{
    public FinishedGoodsView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
