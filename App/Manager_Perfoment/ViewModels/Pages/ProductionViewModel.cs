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
    // Danh sach don hien o COT TRAI (da loc theo o tim ListFilter).
    public ObservableCollection<ProductionOrder> FilteredOrders { get; } = new();
    public ObservableCollection<ProductionStep> Steps { get; } = new();
    public ObservableCollection<Material> Materials { get; } = new();
    // Danh sach NVL da loc (cho danh sach ben trai trong dialog xuat kho).
    public ObservableCollection<Material> FilteredMaterials { get; } = new();
    [ObservableProperty] private string _materialListFilter = "";
    partial void OnMaterialListFilterChanged(string value) => ApplyMaterialFilter();
    private void ApplyMaterialFilter()
    {
        var kw = (MaterialListFilter ?? "").Trim();
        FilteredMaterials.Clear();
        foreach (var m in Materials)
            if (kw.Length == 0
                || (m.Name?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                || (m.Sku?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false))
                FilteredMaterials.Add(m);
    }
    public ObservableCollection<Warehouse> Warehouses { get; } = new();
    // Danh sach phieu xuat NVL cua don dang chon (M5-16: xem lai phieu da xuat).
    public ObservableCollection<MaterialIssue> Issues { get; } = new();

    public string[] StepStatusOptions { get; } = new[] { "PENDING", "IN_PROGRESS", "DONE", "ON_HOLD", "CANCELLED" };

    // ===== QUY TRINH LINH HOAT (V006): ap mau + them/bo/nhay/doi thu tu buoc rieng cho don =====
    /// <summary>Cac mau quy trinh dang dung (chon de ap vao don).</summary>
    public ObservableCollection<Routing> Routings { get; } = new();
    /// <summary>Danh muc cong doan (chon de them 1 buoc le vao don).</summary>
    public ObservableCollection<Stage> Stages { get; } = new();
    /// <summary>Cong doan da loc theo o tim o cot trai dialog quy trinh.</summary>
    public ObservableCollection<Stage> FilteredStages { get; } = new();

    [ObservableProperty] private string _stageListFilter = "";
    partial void OnStageListFilterChanged(string value) => ApplyStageFilter();
    private void ApplyStageFilter()
    {
        var kw = (StageListFilter ?? "").Trim();
        FilteredStages.Clear();
        foreach (var st in Stages)
            if (kw.Length == 0
                || st.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || st.Code.Contains(kw, StringComparison.OrdinalIgnoreCase))
                FilteredStages.Add(st);
    }

    /// <summary>Mau quy trinh chon trong dialog "Sua quy trinh don".</summary>
    [ObservableProperty] private Routing? _selectedRouting;
    /// <summary>Cong doan chon de them 1 buoc le vao don.</summary>
    [ObservableProperty] private Stage? _selectedStage;
    /// <summary>Bat dau tu buoc thu may cua mau (bo cac buoc truoc do). 1 = tu dau.</summary>
    [ObservableProperty] private int _applyStartSeq = 1;
    /// <summary>true = xoa cac buoc chua lam roi sinh lai theo mau.</summary>
    [ObservableProperty] private bool _applyReplace = true;

    // O tim cot trai: loc Orders theo so don / ten ma hang / SKU.
    [ObservableProperty] private string _listFilter = "";
    partial void OnListFilterChanged(string value) => ApplyOrderFilter();
    private void ApplyOrderFilter()
    {
        var kw = (ListFilter ?? "").Trim();
        FilteredOrders.Clear();
        foreach (var o in Orders)
            if (kw.Length == 0
                || (o.OrderNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                || (o.ProductName?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                || (o.ProductSku?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false))
                FilteredOrders.Add(o);
    }

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
    // Phieu xuat NHIEU DONG: luoi cac dong NVL cho, luu 1 lan.
    public ObservableCollection<IssueLine> IssueLines { get; } = new();
    [ObservableProperty] private IssueLine? _selectedIssueLine;


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
        ApplyOrderFilter();   // cap nhat danh sach cot trai
        // Chon lai; chan hook de khoi kich hoat LoadSteps 2 lan.
        _suppressSelectionHook = true;
        SelectedOrder = Orders.FirstOrDefault(o => o.Id == keepOrder);
        _suppressSelectionHook = false;

        var ms = await _api.GetMaterialsAsync(false, ofy, ofm);
        Materials.Clear();
        foreach (var m in ms) Materials.Add(m);
        ApplyMaterialFilter();   // danh sach NVL cho dialog xuat kho
        SelectedMaterial = Materials.FirstOrDefault(m => m.Id == keepMat);

        // Mau quy trinh + danh muc cong doan (phuc vu sua quy trinh rieng cho don).
        var keepRouting = SelectedRouting?.Id;
        var rts = await _api.GetRoutingsAsync(activeOnly: true);
        Routings.Clear();
        foreach (var r in rts) Routings.Add(r);
        SelectedRouting = Routings.FirstOrDefault(r => r.Id == keepRouting);

        var sts = await _api.GetStagesAsync(activeOnly: true);
        Stages.Clear();
        foreach (var st in sts) Stages.Add(st);
        ApplyStageFilter();

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
        Issues.Clear();
        if (SelectedOrder is null) return;
        var ss = await _api.GetStepsAsync(SelectedOrder.Id);
        foreach (var s in ss) Steps.Add(s);
        SelectedStep = Steps.FirstOrDefault(s => s.Id == keepStep);

        // Nap phieu xuat NVL cua don (M5-16: xem lai phieu da xuat).
        var iss = await _api.GetIssuesAsync(SelectedOrder.Id);
        foreach (var i in iss) Issues.Add(i);

        Status = $"{Steps.Count} công đoạn · {Issues.Count} phiếu xuất NVL.";
    }

    [RelayCommand]
    private async Task InitStepsAsync()
    {
        if (SelectedOrder is null) { Status = "Chọn đơn."; return; }
        await RunAsync("Đang khởi tạo công đoạn...", async () =>
        {
            await _api.InitStepsAsync(SelectedOrder!.Id);
            await LoadStepsCoreAsync();
            Status = $"Đã khởi tạo {Steps.Count} công đoạn theo quy trình của đơn.";
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

    // =================================================================================
    // SUA QUY TRINH RIENG CHO DON (V006)
    // =================================================================================

    /// <summary>Mo CUA SO "Quy trình của đơn": chon mau + bat dau tu buoc nao + them buoc le.</summary>
    [RelayCommand]
    private async Task OpenRoutingDialogAsync()
    {
        if (SelectedOrder is null) { Status = "Chọn đơn sản xuất trước."; return; }

        // Mac dinh chon dung mau don dang dung (neu co).
        SelectedRouting = Routings.FirstOrDefault(r => r.Id == SelectedOrder.RoutingId)
                          ?? Routings.FirstOrDefault(r => r.IsDefault)
                          ?? Routings.FirstOrDefault();
        ApplyStartSeq = 1;
        ApplyReplace = true;
        StageListFilter = "";
        SelectedStage = null;

        var orderId = SelectedOrder.Id;
        var orderNo = SelectedOrder.OrderNo;
        var form = new Views.Dialogs.OrderRoutingFormView { DataContext = this };
        await DialogService.ShowFormAsync($"Quy trình sản xuất — đơn {orderNo}", form, async () =>
        {
            if (SelectedRouting is null) return "Chọn mẫu quy trình để áp cho đơn.";
            if (ApplyStartSeq < 1) return "Bước bắt đầu phải từ 1 trở lên.";

            await _api.ApplyRoutingAsync(orderId, new
            {
                routingId = SelectedRouting!.Id,
                startSeq = ApplyStartSeq,
                replace = ApplyReplace
            });
            await LoadCoreAsync();
            Status = $"Đã áp quy trình {SelectedRouting.Name} cho đơn {orderNo} (bắt đầu từ bước {ApplyStartSeq}).";
            return null;
        }, saveText: "Áp quy trình", width: 900, height: 560, scrollable: false);
    }

    /// <summary>Them 1 cong doan le vao don (ngoai mau) — dung ngay trong dialog quy trinh.</summary>
    [RelayCommand]
    private async Task AddOrderStepAsync()
    {
        if (SelectedOrder is null) { Status = "Chọn đơn sản xuất trước."; return; }
        if (SelectedStage is null) { Status = "Chọn công đoạn ở danh sách bên trái."; return; }

        var orderId = SelectedOrder.Id;
        var stageName = SelectedStage.Name;
        await RunAsync($"Đang thêm công đoạn {stageName}...", async () =>
        {
            await _api.AddOrderStepAsync(orderId, new { stageId = SelectedStage!.Id, seq = (int?)null, note = (string?)null });
            await LoadStepsCoreAsync();
            Status = $"Đã thêm công đoạn {stageName} vào đơn.";
        });
    }

    /// <summary>Bat/tat co BO QUA cho buoc dang chon (nhay buoc).</summary>
    [RelayCommand]
    private async Task ToggleSkipStepAsync()
    {
        if (SelectedStep is null) { Status = "Chọn công đoạn cần bỏ qua."; return; }
        var step = SelectedStep;
        var willSkip = !step.IsSkipped;
        await RunAsync(willSkip ? "Đang đánh dấu bỏ qua..." : "Đang bỏ đánh dấu...", async () =>
        {
            await _api.SkipStepAsync(step.Id, new { isSkipped = willSkip, note = (string?)null });
            await LoadStepsCoreAsync(step.Id);
            Status = willSkip
                ? $"Đã đánh dấu BỎ QUA công đoạn {step.StageName}."
                : $"Đã bỏ đánh dấu, công đoạn {step.StageName} phải làm lại.";
        });
    }

    /// <summary>Xoa han 1 buoc khoi don (chi khi buoc chua chay).</summary>
    [RelayCommand]
    private async Task DeleteOrderStepAsync()
    {
        if (SelectedStep is null) { Status = "Chọn công đoạn cần xoá."; return; }
        var step = SelectedStep;
        var ok = await DialogService.ConfirmAsync(
            "Xoá công đoạn khỏi đơn",
            $"Xoá công đoạn '{step.StageName}' khỏi đơn này?\n\n" +
            "Chỉ xoá được khi công đoạn chưa chạy và chưa có số liệu. " +
            "Nếu đã chạy, hãy dùng 'Bỏ qua' thay vì xoá.",
            "Xoá công đoạn", danger: true);
        if (!ok) return;

        await RunAsync("Đang xoá công đoạn...", async () =>
        {
            await _api.DeleteStepAsync(step.Id);
            await LoadStepsCoreAsync();
            Status = $"Đã xoá công đoạn {step.StageName} khỏi đơn.";
        });
    }

    [RelayCommand]
    private Task MoveStepUpAsync() => MoveStepAsync(-1);

    [RelayCommand]
    private Task MoveStepDownAsync() => MoveStepAsync(+1);

    /// <summary>Doi thu tu buoc dang chon: gui ca danh sach id theo thu tu moi len server.</summary>
    private async Task MoveStepAsync(int delta)
    {
        if (SelectedOrder is null) { Status = "Chọn đơn sản xuất trước."; return; }
        if (SelectedStep is null) { Status = "Chọn công đoạn cần đổi thứ tự."; return; }

        var ids = Steps.Select(s => s.Id).ToList();
        var i = ids.IndexOf(SelectedStep.Id);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= ids.Count) return;
        (ids[i], ids[j]) = (ids[j], ids[i]);

        var orderId = SelectedOrder.Id;
        var stepId = SelectedStep.Id;
        await RunAsync("Đang đổi thứ tự công đoạn...", async () =>
        {
            await _api.ReorderStepsAsync(orderId, new { stepIdsInOrder = ids });
            await LoadStepsCoreAsync(stepId);
            Status = "Đã đổi thứ tự công đoạn.";
        });
    }

    /// <summary>Thêm 1 dòng NVL vào phiếu xuất (lưới trong dialog). Chặn trùng NVL.</summary>
    [RelayCommand]
    private void AddIssueLine()
    {
        if (SelectedMaterial is null) { Status = "Chọn NVL để thêm dòng."; return; }
        if (IssueQty <= 0) { Status = "Số lượng xuất phải > 0."; return; }
        if (IssueLines.Any(l => l.Material?.Id == SelectedMaterial.Id))
        {
            Status = $"NVL {SelectedMaterial.Sku} đã có trong phiếu.";
            return;
        }
        IssueLines.Add(new IssueLine { Material = SelectedMaterial, QtyIssued = IssueQty, UnitCost = IssueUnitCost });
        SelectedMaterial = null; IssueQty = 0; IssueUnitCost = 0;
        Status = $"Phiếu có {IssueLines.Count} dòng.";
    }

    /// <summary>Xoá dòng đang chọn khỏi phiếu xuất.</summary>
    [RelayCommand]
    private void RemoveIssueLine()
    {
        if (SelectedIssueLine is null) { Status = "Chọn một dòng để xoá."; return; }
        IssueLines.Remove(SelectedIssueLine);
    }

    /// <summary>Mở CỬA SỔ riêng "Xuất kho NVL phiếu nhiều dòng" cho đơn đang chọn.</summary>
    [RelayCommand]
    private async Task OpenIssueDialogAsync()
    {
        if (SelectedOrder is null) { Status = "Chọn đơn sản xuất trước."; return; }
        IssueLines.Clear();
        SelectedWarehouse = null; SelectedMaterial = null; IssueQty = 0; IssueUnitCost = 0;
        var form = new Views.Dialogs.IssueFormView { DataContext = this };
        await DialogService.ShowFormAsync(
            $"Xuất kho NVL — đơn {SelectedOrder.OrderNo}", form, async () =>
            {
                if (SelectedWarehouse is null) return "Chọn kho xuất.";
                if (IssueLines.Count == 0) return "Phiếu chưa có dòng NVL nào. Bấm '+ Thêm dòng' trước.";

                var r = await _api.CreateIssueAsync(new
                {
                    productionOrderId = SelectedOrder!.Id,
                    warehouseId = SelectedWarehouse!.Id,
                    note = "Xuất cho SX",
                    items = IssueLines.Select(l => new
                    {
                        materialId = l.Material!.Id, qtyIssued = l.QtyIssued, unitCost = l.UnitCost,
                    }).ToArray(),
                });
                IssueLines.Clear();
                await LoadCoreAsync();
                Status = $"Đã xuất kho phiếu {r.IssueNo} ({r.LineCount} dòng).";
                return null;
            }, saveText: "↓ Xuất kho", width: 900, height: 540, scrollable: false);
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
