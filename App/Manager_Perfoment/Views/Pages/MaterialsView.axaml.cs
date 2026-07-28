using Avalonia.Controls;

namespace GM95.App.ManagerPerformance.Views.Pages;

public partial class MaterialsView : UserControl
{
    public MaterialsView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
