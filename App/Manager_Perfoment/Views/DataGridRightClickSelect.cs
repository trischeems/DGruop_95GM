using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace GM95.App.ManagerPerformance.Views;

/// <summary>
/// Avalonia DataGrid KHONG tu chon dong khi bam chuot PHAI — menu chuot phai se thao tac nham
/// tren dong dang chon truoc do. Helper nay bat su kien o pha TUNNEL (truoc khi grid xu ly)
/// va chon dong nam duoi con tro, de menu luon ung voi dong nguoi dung vua bam.
///
/// Cach dung trong code-behind cua trang:  DataGridRightClickSelect.Attach(this);
/// (goi 1 lan; tu gan cho MOI DataGrid trong trang, ke ca grid nam trong TabControl nap sau).
/// </summary>
public static class DataGridRightClickSelect
{
    private const string AttachedTag = "rmb-select-attached";

    public static void Attach(Control root)
    {
        // Grid trong TabItem chi duoc dung khi tab mo lan dau -> gan lai moi lan Loaded.
        root.Loaded += (_, _) => AttachAll(root);
        if (root.IsLoaded) AttachAll(root);
    }

    private static void AttachAll(Control root)
    {
        foreach (var grid in root.GetVisualDescendants().OfType<DataGrid>())
        {
            if (grid.Tag as string == AttachedTag) continue;   // tranh gan trung
            grid.Tag = AttachedTag;
            grid.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed,
                RoutingStrategies.Tunnel);                     // TUNNEL: chay truoc xu ly cua grid
        }
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        var point = e.GetCurrentPoint(grid);
        if (!point.Properties.IsRightButtonPressed) return;    // chi xu ly chuot phai

        // Tim dong nam duoi con tro roi chon no (khong huy su kien -> menu van bung ra).
        if (e.Source is Visual src)
        {
            var row = src.FindAncestorOfType<DataGridRow>();
            if (row?.DataContext is not null) grid.SelectedItem = row.DataContext;
        }
    }
}
