using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DGroup.App.ManagerPerformance.Models;
using DGroup.App.ManagerPerformance.Services;

namespace DGroup.App.ManagerPerformance.ViewModels.Pages;

/// <summary>Man hinh Nhap kho thanh pham + doi chieu hao hut so voi dinh muc.</summary>
public sealed partial class FinishedGoodsViewModel : PageViewModel
{
    private readonly ApiClient _api;

    public FinishedGoodsViewModel(ApiClient api) => _api = api;

    public override string Title => "Nhập kho thành phẩm & Hao hụt";
    public override string Subtitle => "Nhập thành phẩm đạt, đối chiếu hao hụt so với định mức";

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<ProductionOrder> Orders { get; } = new();
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<Warehouse> Warehouses { get; } = new();
    public ObservableCollection<FinishedGoodsReceipt> Receipts { get; } = new();
    public ObservableCollection<LossReport> Losses { get; } = new();

    [ObservableProperty] private ProductionOrder? _selectedOrder;
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private Warehouse? _selectedWarehouse;
    [ObservableProperty] private decimal _qtyReceived = 0;
    [ObservableProperty] private FinishedGoodsReceipt? _selectedReceipt;


    // ===== Bo loc thang/nam ("Tất cả" = khong loc; "Cả năm" = ca nam dang chon) =====
    public string[] FilterMonthOptions { get; } =
        { "Tất cả", "Cả năm", "T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9", "T10", "T11", "T12" };
    public int[] FilterYearOptions { get; } =
        { DateTime.Now.Year - 2, DateTime.Now.Year - 1, DateTime.Now.Year, DateTime.Now.Year + 1 };
    [ObservableProperty] private string _filterMonth = "T" + DateTime.Now.Month;
    [ObservableProperty] private int _filterYear = DateTime.Now.Year;
    partial void OnFilterMonthChanged(string value) => _ = LoadAsync();
    partial void OnFilterYearChanged(int value) => _ = LoadAsync();
    private (int? Year, int? Month) FilterPeriod =>
        FilterMonth == "Tất cả" ? (null, null)
        : FilterMonth == "Cả năm" ? (FilterYear, null)
        : (FilterYear, int.Parse(FilterMonth.TrimStart('T')));

    public override Task OnActivatedAsync() => LoadAsync();

    [RelayCommand]
    private Task LoadAsync() => RunAsync("Đang tải...", LoadCoreAsync);

    // Nap du lieu KHONG boc RunAsync (de lenh ghi goi lai duoc — guard IsBusy chan goi long nhau).
    private async Task LoadCoreAsync()
    {
        var keepOrder = SelectedOrder?.Id;
        var keepProduct = SelectedProduct?.Id;
        var keepWh = SelectedWarehouse?.Id;

        var (ofy, ofm) = FilterPeriod;
        var os = await _api.GetOrdersAsync(null, ofy, ofm);
        Orders.Clear();
        foreach (var o in os) Orders.Add(o);
        // Chon lai; chan hook de khoi kich hoat LoadDetail 2 lan.
        _suppressSelectionHook = true;
        SelectedOrder = Orders.FirstOrDefault(o => o.Id == keepOrder);
        _suppressSelectionHook = false;

        var ps = await _api.GetProductsAsync(false, ofy, ofm);
        Products.Clear();
        foreach (var p in ps) Products.Add(p);
        SelectedProduct = Products.FirstOrDefault(p => p.Id == keepProduct);

        var ws = await _api.GetWarehousesAsync();
        Warehouses.Clear();
        foreach (var w in ws) Warehouses.Add(w);
        SelectedWarehouse = Warehouses.FirstOrDefault(w => w.Id == keepWh);

        await LoadDetailAsync();
    }

    private bool _suppressSelectionHook;

    partial void OnSelectedOrderChanged(ProductionOrder? value)
    {
        if (_suppressSelectionHook) return;
        _ = LoadDetailAsync();
    }

    private async Task LoadDetailAsync()
    {
        Receipts.Clear();
        Losses.Clear();
        if (SelectedOrder is null) return;

        var (fy, fm) = FilterPeriod;
        var rs = await _api.GetFinishedGoodsAsync(SelectedOrder.Id, fy, fm);
        foreach (var r in rs) Receipts.Add(r);

        var ls = await _api.GetLossReportsAsync(SelectedOrder.Id);
        foreach (var l in ls) Losses.Add(l);
    }

    [RelayCommand]
    private async Task ReceiveAsync()
    {
        if (SelectedOrder is null || SelectedProduct is null || SelectedWarehouse is null)
        {
            Status = "Chọn đơn, mã hàng, kho.";
            return;
        }
        if (QtyReceived <= 0)
        {
            Status = "Số lượng nhập > 0.";
            return;
        }

        await RunAsync("Đang nhập kho TP...", async () =>
        {
            var id = await _api.CreateFinishedGoodsAsync(new
            {
                productionOrderId = SelectedOrder!.Id,
                productId = SelectedProduct!.Id,
                warehouseId = SelectedWarehouse!.Id,
                qtyReceived = QtyReceived,
                note = (string?)null
            });
            QtyReceived = 0;
            await LoadCoreAsync();
            Status = $"Đã nhập kho TP id={id}. Đơn COMPLETED.";
        });
    }

    /// <summary>Xoa phieu nhap TP dang chon: popup neu ro anh huong (don doi trang thai, hao hut lech).</summary>
    [RelayCommand]
    private async Task DeleteReceiptAsync()
    {
        if (SelectedReceipt is null) { Status = "Chọn một phiếu nhập TP trong bảng để xoá."; return; }
        var r = SelectedReceipt!;
        await RunAsync("Đang kiểm tra ảnh hưởng...", async () =>
        {
            var impact = await _api.GetFinishedGoodsImpactAsync(r.Id);
            if (impact is null) { Status = "Không tìm thấy phiếu trên server."; return; }

            var msg = $"Xoá phiếu {impact.ReceiptNo} (SL nhập {impact.QtyReceived:0.####})?\n" +
                      $"• Đơn đang ở trạng thái {impact.OrderStatus}.";
            if (impact.WillRevertOrder)
                msg += "\n• Đây là phiếu cuối cùng của đơn — đơn sẽ quay lại IN_PROGRESS.";
            if (impact.LossReportCount > 0)
                msg += $"\n• Đơn có {impact.LossReportCount} dòng hao hụt đã tính — sẽ LỆCH sau khi xoá, " +
                       "hãy bấm 'Tính hao hụt' lại.";
            var ok = await DialogService.ConfirmAsync("Xoá phiếu nhập thành phẩm", msg, "Xoá phiếu", danger: true);
            if (!ok) { Status = "Đã huỷ thao tác xoá."; return; }

            await _api.DeleteFinishedGoodsAsync(r.Id);
            SelectedReceipt = null;
            await LoadCoreAsync();
            Status = $"Đã xoá phiếu {impact.ReceiptNo}. Nhớ tính lại hao hụt nếu đơn còn phiếu khác.";
        });
    }

    [RelayCommand]
    private async Task GenerateLossAsync()
    {
        if (SelectedOrder is null) return;
        await RunAsync("Đang đối chiếu hao hụt...", async () =>
        {
            await _api.GenerateLossAsync(SelectedOrder!.Id);
            await LoadDetailAsync();
            Status = "Đã tính hao hụt.";
        });
    }

    private async Task RunAsync(string busy, Func<Task> a)
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = busy;
        try { await a(); }
        catch (ApiException ex) { Status = $"Lỗi [{ex.Code}]: {ex.Message}"; }
        catch (Exception ex) { Status = $"Lỗi: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
