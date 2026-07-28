using Avalonia.Controls;
using GM95.App.ManagerPerformance.Views;

namespace GM95.App.ManagerPerformance.Views.Dialogs;

public partial class IssueFormView : UserControl
{
    public IssueFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
