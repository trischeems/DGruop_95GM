using Avalonia.Controls;
using GM95.App.ManagerPerformance.Views;

namespace GM95.App.ManagerPerformance.Views.Dialogs;

public partial class StageFormView : UserControl
{
    public StageFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
