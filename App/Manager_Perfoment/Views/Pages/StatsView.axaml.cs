using Avalonia.Controls;

namespace GM95.App.ManagerPerformance.Views.Pages;

public partial class StatsView : UserControl
{
    public StatsView()
    {
        InitializeComponent();
        SearchBoxFilter.Attach(this);
        // DataGrid khong tu chon dong khi bam chuot PHAI -> menu chuot phai se thao tac nham dong.
        DataGridRightClickSelect.Attach(this);
    }
}
