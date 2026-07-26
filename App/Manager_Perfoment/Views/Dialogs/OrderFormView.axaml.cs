using Avalonia.Controls;
using DGroup.App.ManagerPerformance.Views;

namespace DGroup.App.ManagerPerformance.Views.Dialogs;

public partial class OrderFormView : UserControl
{
    public OrderFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
