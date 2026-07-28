using Avalonia.Controls;
using GM95.App.ManagerPerformance.Views;

namespace GM95.App.ManagerPerformance.Views.Dialogs;

public partial class ReceiptFormView : UserControl
{
    public ReceiptFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
