using Avalonia.Controls;
using GM95.App.ManagerPerformance.Views;

namespace GM95.App.ManagerPerformance.Views.Dialogs;

public partial class BomItemsFormView : UserControl
{
    public BomItemsFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
