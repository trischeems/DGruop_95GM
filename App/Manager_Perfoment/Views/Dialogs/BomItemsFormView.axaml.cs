using Avalonia.Controls;
using DGroup.App.ManagerPerformance.Views;

namespace DGroup.App.ManagerPerformance.Views.Dialogs;

public partial class BomItemsFormView : UserControl
{
    public BomItemsFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
