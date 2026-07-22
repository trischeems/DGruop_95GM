using Avalonia.Controls;

namespace DGroup.App.ManagerPerformance.Views.Pages;

public partial class MaterialsView : UserControl
{
    public MaterialsView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
