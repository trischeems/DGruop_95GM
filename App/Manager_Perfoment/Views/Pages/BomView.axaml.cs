using Avalonia.Controls;

namespace GM95.App.ManagerPerformance.Views.Pages;

public partial class BomView : UserControl
{
    public BomView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
