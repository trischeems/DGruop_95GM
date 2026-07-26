using Avalonia.Controls;
using DGroup.App.ManagerPerformance.Views;

namespace DGroup.App.ManagerPerformance.Views.Dialogs;

public partial class PlanFormView : UserControl
{
    public PlanFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
