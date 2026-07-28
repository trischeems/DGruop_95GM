using Avalonia.Controls;
using GM95.App.ManagerPerformance.Views;

namespace GM95.App.ManagerPerformance.Views.Dialogs;

public partial class OrderFormView : UserControl
{
    public OrderFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
