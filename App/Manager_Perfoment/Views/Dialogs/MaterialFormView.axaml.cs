using Avalonia.Controls;
using GM95.App.ManagerPerformance.Views;

namespace GM95.App.ManagerPerformance.Views.Dialogs;

public partial class MaterialFormView : UserControl
{
    public MaterialFormView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
    }
}
