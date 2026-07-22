using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DGroup.App.ManagerPerformance.Models;
using DGroup.App.ManagerPerformance.Services;

namespace DGroup.App.ManagerPerformance.ViewModels.Pages;

/// <summary>Ke hoach san xuat: chon don, tao ke hoach, doi trang thai, xoa. Dung API that.</summary>
public sealed partial class ProductionPlansViewModel : PageViewModel
{
    private readonly ApiClient _api;

    public ProductionPlansViewModel(ApiClient api) => _api = api;

    public override string Title => "Kế hoạch sản xuất";
    public override string Subtitle => "Lên lịch, chia chuyền cho đơn hàng";

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<ProductionOrder> Orders { get; } = new();
    public ObservableCollection<ProductionPlan> Plans { get; } = new();

    public string[] StatusOptions { get; } = new[] { "PLANNED", "RELEASED", "IN_PROGRESS", "DONE", "CANCELLED" };

    [ObservableProperty] private ProductionOrder? _selectedOrder;
    [ObservableProperty] private ProductionPlan? _selectedPlan;
    [ObservableProperty] private decimal _newPlannedQty = 1;
    [ObservableProperty] private string _newLineCode = "";
    [ObservableProperty] private string _selectedStatus = "PLANNED";

    // Bam 1 dong ke hoach -> tu nap SL + ma chuyen vao form de sua roi bam Luu.
    partial void OnSelectedPlanChanged(ProductionPlan? value)
    {
        if (value is null) return;
        NewPlannedQty = value.PlannedQty;
        NewLineCode = value.LineCode ?? "";
        SelectedStatus = value.Status;
    }


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

        var (ofy, ofm) = FilterPeriod;
        var os = await _api.GetOrdersAsync(null, ofy, ofm);
        Orders.Clear();
        foreach (var o in os) Orders.Add(o);
        // Chon lai; chan hook de khoi kich hoat LoadPlans 2 lan.
        _suppressSelectionHook = true;
        SelectedOrder = Orders.FirstOrDefault(o => o.Id == keepOrder);
        _suppressSelectionHook = false;

        await LoadPlansCoreAsync();
    }

    private bool _suppressSelectionHook;

    partial void OnSelectedOrderChanged(ProductionOrder? value)
    {
        if (_suppressSelectionHook) return;
        _ = LoadPlansCoreAsync();
    }

    // Nap ke hoach cua don dang chon; giu (hoac chon moi) ke hoach theo selectPlanId.
    private async Task LoadPlansCoreAsync(long? selectPlanId = null)
    {
        var keepPlan = selectPlanId ?? SelectedPlan?.Id;

        Plans.Clear();
        long? oid = SelectedOrder?.Id;
        var (fy, fm) = FilterPeriod;
        var ps = await _api.GetPlansAsync(oid, fy, fm);
        foreach (var p in ps) Plans.Add(p);
        SelectedPlan = Plans.FirstOrDefault(p => p.Id == keepPlan);

        Status = $"{Plans.Count} kế hoạch.";
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (SelectedOrder is null) { Status = "Chọn đơn."; return; }
        await RunAsync("Đang tạo kế hoạch...", async () =>
        {
            var id = await _api.CreatePlanAsync(new
            {
                productionOrderId = SelectedOrder!.Id,
                plannedQty = NewPlannedQty,
                plannedStart = (string?)null,
                plannedEnd = (string?)null,
                lineCode = NewLineCode,
                note = (string?)null
            });
            // Nap lai va chon ngay ke hoach vua tao.
            await LoadPlansCoreAsync(id);
            Status = $"Đã tạo kế hoạch id={id}.";
        });
    }

    [RelayCommand]
    private async Task UpdateStatusAsync()
    {
        if (SelectedPlan is null) { Status = "Chọn kế hoạch."; return; }
        await RunAsync("Đang cập nhật...", async () =>
        {
            var planId = SelectedPlan!.Id;
            await _api.UpdatePlanStatusAsync(planId, new { status = SelectedStatus });
            await LoadPlansCoreAsync(planId);
            Status = $"Đã đổi trạng thái -> {SelectedStatus}.";
        });
    }

    /// <summary>Luu SL ke hoach + ma chuyen cho ke hoach dang chon.</summary>
    [RelayCommand]
    private async Task SavePlanAsync()
    {
        if (SelectedPlan is null) { Status = "Chọn một kế hoạch trong bảng để sửa."; return; }
        if (NewPlannedQty <= 0) { Status = "SL kế hoạch phải > 0."; return; }
        await RunAsync("Đang lưu kế hoạch...", async () =>
        {
            var planId = SelectedPlan!.Id;
            await _api.UpdatePlanAsync(planId, new
            {
                plannedQty = NewPlannedQty,
                lineCode = string.IsNullOrWhiteSpace(NewLineCode) ? null : NewLineCode.Trim(),
                note = (string?)null,
            });
            await LoadPlansCoreAsync(planId);
            Status = $"Đã lưu kế hoạch id={planId}.";
        });
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedPlan is null) { Status = "Chọn một kế hoạch trong bảng để xoá."; return; }
        var p = SelectedPlan!;
        var ok = await DialogService.ConfirmAsync(
            "Xoá kế hoạch sản xuất",
            $"Xoá kế hoạch id={p.Id} (SL {p.PlannedQty:0.####}, chuyền {p.LineCode ?? "-"}, trạng thái {p.Status}) " +
            $"của đơn id={p.ProductionOrderId}?\nKế hoạch không kéo theo dữ liệu nào khác.",
            "Xoá", danger: true);
        if (!ok) { Status = "Đã huỷ thao tác xoá."; return; }
        await RunAsync("Đang xoá...", async () =>
        {
            await _api.DeletePlanAsync(p.Id);
            await LoadPlansCoreAsync();
            Status = "Đã xoá kế hoạch.";
        });
    }

    private async Task RunAsync(string busy, Func<Task> a)
    {
        if (IsBusy) return;
        IsBusy = true; Status = busy;
        try { await a(); }
        catch (ApiException ex) { Status = $"Lỗi [{ex.Code}]: {ex.Message}"; }
        catch (Exception ex) { Status = $"Lỗi: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
