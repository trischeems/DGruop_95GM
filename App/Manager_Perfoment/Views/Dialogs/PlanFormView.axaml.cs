using Avalonia.Controls;
using GM95.App.ManagerPerformance.Views;

namespace GM95.App.ManagerPerformance.Views.Dialogs;

public partial class PlanFormView : UserControl
{
    public PlanFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
