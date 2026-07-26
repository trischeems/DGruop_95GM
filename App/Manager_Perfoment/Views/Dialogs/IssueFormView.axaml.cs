using Avalonia.Controls;
using DGroup.App.ManagerPerformance.Views;

namespace DGroup.App.ManagerPerformance.Views.Dialogs;

public partial class IssueFormView : UserControl
{
    public IssueFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
