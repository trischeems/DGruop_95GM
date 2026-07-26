using Avalonia.Controls;
using DGroup.App.ManagerPerformance.Views;

namespace DGroup.App.ManagerPerformance.Views.Dialogs;

public partial class StageFormView : UserControl
{
    public StageFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
