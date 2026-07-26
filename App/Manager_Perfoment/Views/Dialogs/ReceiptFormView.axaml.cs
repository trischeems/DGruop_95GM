using Avalonia.Controls;
using DGroup.App.ManagerPerformance.Views;

namespace DGroup.App.ManagerPerformance.Views.Dialogs;

public partial class ReceiptFormView : UserControl
{
    public ReceiptFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
