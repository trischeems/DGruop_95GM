using Avalonia.Controls;
using DGroup.App.ManagerPerformance.Views;

namespace DGroup.App.ManagerPerformance.Views.Dialogs;

public partial class MaterialFormView : UserControl
{
    public MaterialFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
