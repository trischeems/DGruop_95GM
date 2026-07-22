using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DGroup.App.ManagerPerformance.Views.Pages;

public partial class MonthCompareView : UserControl
{
    public MonthCompareView()
    {
        InitializeComponent();

        // Bieu do LiveCharts nuot su kien lan chuot -> trang khong cuon duoc.
        // Bat wheel o tang TUNNEL (truoc khi chart nhan) va tu cuon ScrollViewer.
        // Tien the chan luon viec lan chuot vo tinh doi gia tri cac ComboBox tren trang.
        AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            var sv = this.FindControl<ScrollViewer>("Scroller");
            if (sv is null) return;
            sv.Offset = new Vector(sv.Offset.X, sv.Offset.Y - e.Delta.Y * 80);
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
    }
}
