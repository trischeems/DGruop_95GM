using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DGroup.App.ManagerPerformance.Models;
using DGroup.App.ManagerPerformance.Services;

namespace DGroup.App.ManagerPerformance.ViewModels.Pages;

/// <summary>Trang San xuat: theo doi cong doan Cat/May/QC va xuat kho NVL cho san xuat.</summary>
public sealed partial class ProductionViewModel : PageViewModel
{
    private readonly ApiClient _api;
    public ProductionViewModel(ApiClient api) => _api = api;

    public override string Title => "Sản xuất (Cắt/May/QC)";
    public override string Subtitle => "Theo dõi công đoạn và xuất NVL cho sản xuất";

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<ProductionOrder> Orders { get; } = new();
    public ObservableCollection<ProductionStep> Steps { get; } = new();
    public ObservableCollection<Material> Materials { get; } = new();
    public ObservableCollection<Warehouse> Warehouses { get; } = new();

    public string[] StepStatusOptions { get; } = new[] { "PENDING", "IN_PROGRESS", "DONE", "ON_HOLD", "CANCELLED" };

    [ObservableProperty] private ProductionOrder? _selectedOrder;
    [ObservableProperty] private ProductionStep? _selectedStep;
    [ObservableProperty] private string _selectedStepStatus = "IN_PROGRESS";
    [ObservableProperty] private decimal _qtyIn = 0;
    [ObservableProperty] private decimal _qtyOut = 0;
    [ObservableProperty] private decimal _qtyDefect = 0;

    // Bam 1 dong cong doan -> tu nap trang thai + so lieu vao form sua.
    partial void OnSelectedStepChanged(ProductionStep? value)
    {
        if (value is null) return;
        SelectedStepStatus = value.Status;
        QtyIn = value.QtyIn;
        QtyOut = value.QtyOut;
        QtyDefect = value.QtyDefect;
    }

    [ObservableProperty] private Warehouse? _selectedWarehouse;
    [ObservableProperty] private Material? _selectedMaterial;
    [ObservableProperty] private decimal _issueQty = 0;
    [ObservableProperty] private decimal _issueUnitCost = 0;


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
    private Task LoadAsync() => RunAsync("Đang tải...", () => LoadCoreAsync());

    // Nap du lieu KHONG boc RunAsync (de lenh ghi goi lai duoc — guard IsBusy chan goi long nhau).
    private async Task LoadCoreAsync()
    {
        var keepOrder = SelectedOrder?.Id;
        var keepWh = SelectedWarehouse?.Id;
        var keepMat = SelectedMaterial?.Id;

        var (ofy, ofm) = FilterPeriod;
        var os = await _api.GetOrdersAsync(null, ofy, ofm);
        Orders.Clear();
        foreach (var o in os) Orders.Add(o);
        // Chon lai; chan hook de khoi kich hoat LoadSteps 2 lan.
        _suppressSelectionHook = true;
        SelectedOrder = Orders.FirstOrDefault(o => o.Id == keepOrder);
        _suppressSelectionHook = false;

        var ms = await _api.GetMaterialsAsync(false, ofy, ofm);
        Materials.Clear();
        foreach (var m in ms) Materials.Add(m);
        SelectedMaterial = Materials.FirstOrDefault(m => m.Id == keepMat);

        var ws = await _api.GetWarehousesAsync();
        Warehouses.Clear();
        foreach (var w in ws) Warehouses.Add(w);
        SelectedWarehouse = Warehouses.FirstOrDefault(w => w.Id == keepWh);

        await LoadStepsCoreAsync();
    }

    private bool _suppressSelectionHook;

    partial void OnSelectedOrderChanged(ProductionOrder? value)
    {
        if (_suppressSelectionHook) return;
        _ = LoadStepsCoreAsync();
    }

    // Nap cong doan cua don dang chon; giu nguyen cong doan dang chon theo Id.
    private async Task LoadStepsCoreAsync(long? selectStepId = null)
    {
        var keepStep = selectStepId ?? SelectedStep?.Id;

        Steps.Clear();
        if (SelectedOrder is null) return;
        var ss = await _api.GetStepsAsync(SelectedOrder.Id);
        foreach (var s in ss) Steps.Add(s);
        SelectedStep = Steps.FirstOrDefault(s => s.Id == keepStep);

        Status = $"{Steps.Count} công đoạn.";
    }

    [RelayCommand]
    private async Task InitStepsAsync()
    {
        if (SelectedOrder is null) { Status = "Chọn đơn."; return; }
        await RunAsync("Đang khởi tạo công đoạn...", async () =>
        {
            await _api.InitStepsAsync(SelectedOrder!.Id);
            await LoadStepsCoreAsync();
            Status = "Đã khởi tạo 4 công đoạn.";
        });
    }

    [RelayCommand]
    private async Task UpdateStepAsync()
    {
        if (SelectedStep is null) { Status = "Chọn công đoạn."; return; }
        await RunAsync("Đang cập nhật công đoạn...", async () =>
        {
            var stepId = SelectedStep!.Id;
            await _api.UpdateStepAsync(stepId, new { status = SelectedStepStatus, qtyIn = QtyIn, qtyOut = QtyOut, qtyDefect = QtyDefect, note = (string?)null });
            await LoadStepsCoreAsync(stepId);
            Status = "Đã cập nhật công đoạn.";
        });
    }

    [RelayCommand]
    private async Task IssueAsync()
    {
        if (SelectedOrder is null || SelectedWarehouse is null || SelectedMaterial is null) { Status = "Chọn đơn, kho, NVL."; return; }
        if (IssueQty <= 0) { Status = "Số lượng xuất > 0."; return; }
        await RunAsync("Đang xuất kho NVL...", async () =>
        {
            var r = await _api.CreateIssueAsync(new
            {
                productionOrderId = SelectedOrder!.Id,
                warehouseId = SelectedWarehouse!.Id,
                note = "Xuất cho SX",
                items = new[] { new { materialId = SelectedMaterial!.Id, qtyIssued = IssueQty, unitCost = IssueUnitCost } }
            });
            IssueQty = 0;
            // Xuat kho doi trang thai don (IN_PROGRESS) + ton kho -> nap lai toan bo.
            await LoadCoreAsync();
            Status = $"Đã xuất kho phiếu {r.IssueNo} ({r.LineCount} dòng).";
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
