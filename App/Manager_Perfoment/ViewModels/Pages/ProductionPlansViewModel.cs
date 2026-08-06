using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GM95.App.ManagerPerformance.Models;
using GM95.App.ManagerPerformance.Services;

namespace GM95.App.ManagerPerformance.ViewModels.Pages;

/// <summary>Ke hoach san xuat: chon don, tao ke hoach, doi trang thai, xoa. Dung API that.</summary>
public sealed partial class ProductionPlansViewModel : PageViewModel, IExportProvider
{
    /// <summary>Cac bang cua trang nay cho nut "Xuất Excel" chung (xuat dung du lieu dang hien thi).</summary>
    public IReadOnlyList<ExportTable> GetExportTables() => new[]
    {
        ExportTable.Create<ProductionOrder>("Đơn sản xuất", () => FilteredOrders, rowDate: o => o.DueDate,
            ("ID", o => o.Id),
            ("Số đơn", o => o.OrderNo),
            ("SKU", o => o.ProductSku),
            ("Mã hàng", o => o.ProductName),
            ("SL đơn", o => o.Quantity),
            ("ĐVT", o => o.ProductUomName),
            ("Trạng thái", o => Converters.CodeToVietnameseConverter.Translate(o.Status)),
            ("Hạn giao", o => o.DueDate)),
        ExportTable.Create<ProductionPlan>("Danh sách kế hoạch", () => Plans, rowDate: p => p.PlannedStart,
            ("ID", p => p.Id),
            ("Đơn", p => p.ProductionOrderId),
            ("Số đơn", p => p.OrderNo),
            ("SL kế hoạch", p => p.PlannedQty),
            ("ĐVT", p => p.ProductUomName),
            ("Chuyền", p => p.LineCode),
            ("Trạng thái", p => Converters.CodeToVietnameseConverter.Translate(p.Status)),
            ("Bắt đầu", p => p.PlannedStart),
            ("Kết thúc", p => p.PlannedEnd),
            ("Ghi chú", p => p.Note)),
    };

    private readonly ApiClient _api;

    public ProductionPlansViewModel(ApiClient api) => _api = api;

    public override string Title => "Kế hoạch sản xuất";
    public override string Subtitle => "Lên lịch, chia chuyền cho đơn hàng";

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<ProductionOrder> Orders { get; } = new();
    // Danh sach don hien o COT TRAI (da loc theo o tim ListFilter). Bam 1 dong = chon don.
    public ObservableCollection<ProductionOrder> FilteredOrders { get; } = new();
    public ObservableCollection<ProductionPlan> Plans { get; } = new();

    // ===== MAT HANG cua don dang chon (V007: ke hoach lap cho TUNG mat hang) =====
    public ObservableCollection<ProductionOrderItem> OrderItems { get; } = new();
    [ObservableProperty] private ProductionOrderItem? _selectedOrderItem;
    /// <summary>Don co tu 2 mat hang tro len -> bat buoc chon mat hang khi lap ke hoach.</summary>
    public bool HasMultipleItems => OrderItems.Count > 1;

    // O tim cot trai: loc Orders theo so don / ten ma hang / SKU (khong phan biet hoa thuong).
    [ObservableProperty] private string _listFilter = "";
    partial void OnListFilterChanged(string value) => ApplyOrderFilter();

    /// <summary>
    /// Rebuild FilteredOrders tu Orders theo ListFilter. Goi sau khi nap Orders hoac doi o tim.
    /// FilteredOrders la ItemsSource cua ListBox (SelectedItem TwoWay): Clear() lam ListBox bo chon
    /// va ghi nguoc null vao SelectedOrder -> phai chan hook trong luc rebuild, chon lai sau khi xong.
    /// </summary>
    private void ApplyOrderFilter()
    {
        var kw = (ListFilter ?? "").Trim();
        var keep = SelectedOrder;
        var wasSuppressed = _suppressSelectionHook;
        _suppressSelectionHook = true;
        try
        {
            FilteredOrders.Clear();
            foreach (var o in Orders)
            {
                if (kw.Length == 0
                    || (o.OrderNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (o.ProductName?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (o.ProductSku?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false))
                    FilteredOrders.Add(o);
            }
            // Giu nguyen don dang chon neu no van lot qua o tim; khong con thi bo chon.
            SelectedOrder = keep is not null && FilteredOrders.Contains(keep) ? keep : null;
        }
        finally { _suppressSelectionHook = wasSuppressed; }

        // Don dang chon bi o tim loc ra ngoai -> bang ke hoach phai trong theo.
        // Khi goi tu LoadCoreAsync (dang suppress) thi bo qua: ham do tu nap lai o buoc cuoi.
        if (!wasSuppressed && !ReferenceEquals(SelectedOrder, keep)) _ = LoadPlansCoreAsync();
    }

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
    private async Task LoadCoreAsync(long? selectPlanId = null)
    {
        var keepOrder = SelectedOrder?.Id;

        var (ofy, ofm) = FilterPeriod;
        var os = await _api.GetOrdersAsync(null, ofy, ofm);
        // Chan hook TU TRUOC khi dung den cac danh sach: Orders.Clear() / ApplyOrderFilter() lam
        // ListBox bo chon va ghi nguoc null vao SelectedOrder. Neu hook con song, no ban ra mot lan
        // nap ke hoach voi orderId = null (server tra ke hoach cua MOI don) chay song song ben duoi
        // -> bang tron ke hoach 2 don va lua chon nhay lung tung.
        var wasSuppressed = _suppressSelectionHook;   // co the dang mo form (form cung chan hook)
        _suppressSelectionHook = true;
        try
        {
            Orders.Clear();
            foreach (var o in os) Orders.Add(o);
            ApplyOrderFilter();   // cap nhat danh sach cot trai theo o tim
            // Chon lai theo dung danh sach dang hien o cot trai (FilteredOrders — ItemsSource cua
            // ListBox); chi khi khong co moi lay tu Orders (danh sach goc).
            SelectedOrder = FilteredOrders.FirstOrDefault(o => o.Id == keepOrder)
                            ?? Orders.FirstOrDefault(o => o.Id == keepOrder);
        }
        finally { _suppressSelectionHook = wasSuppressed; }

        await LoadPlansCoreAsync(selectPlanId);
    }

    private bool _suppressSelectionHook;

    // So thu tu cua lan nap ke hoach: ket qua ve TRE hon lan moi nhat se bi bo qua.
    private int _plansLoadGen;

    partial void OnSelectedOrderChanged(ProductionOrder? value)
    {
        if (_suppressSelectionHook) return;
        _ = LoadPlansCoreAsync();
    }


    // Nap danh sach MAT HANG cua don dang chon (V007). Giu lai mat hang dang chon neu con.
    private async Task LoadOrderItemsCoreAsync()
    {
        var keepItem = SelectedOrderItem?.Id;
        var items = SelectedOrder is null
            ? new List<ProductionOrderItem>()
            : await _api.GetOrderItemsAsync(SelectedOrder.Id);
        OrderItems.Clear();
        foreach (var it in items) OrderItems.Add(it);
        SelectedOrderItem = OrderItems.FirstOrDefault(i => i.Id == keepItem) ?? OrderItems.FirstOrDefault();
        OnPropertyChanged(nameof(HasMultipleItems));
    }

    // Nap ke hoach cua don dang chon; giu (hoac chon moi) ke hoach theo selectPlanId.
    private async Task LoadPlansCoreAsync(long? selectPlanId = null)
    {
        var gen = ++_plansLoadGen;   // danh dau: tu day tro di chi ket qua cua lan nay moi duoc do vao bang
        await LoadOrderItemsCoreAsync();   // mat hang cua don (de lap ke hoach theo tung mat hang)
        var keepPlan = selectPlanId ?? SelectedPlan?.Id;

        // Chua chon don thi KHONG goi API: orderId = null lam server tra ke hoach cua moi don.
        var order = SelectedOrder;
        if (order is null)
        {
            Plans.Clear();
            SelectedPlan = null;
            Status = "Chọn một đơn sản xuất ở cột trái để xem kế hoạch.";
            return;
        }

        var (fy, fm) = FilterPeriod;
        var ps = await _api.GetPlansAsync(order.Id, fy, fm);
        if (gen != _plansLoadGen) return;   // da co lan nap moi hon -> bo ket qua cu, khong dong vao bang

        // Chi xoa bang SAU khi da co du lieu ve va chac chan minh la lan moi nhat
        // (xoa truoc khi await -> 2 lan nap chong nhau deu xoa bang rong roi cung do vao = tron don).
        Plans.Clear();
        foreach (var p in ps) Plans.Add(p);
        SelectedPlan = Plans.FirstOrDefault(p => p.Id == keepPlan);
        // Ke hoach dang chon phai thuoc dung don dang chon — khong de lenh ghi cham vao ke hoach don khac.
        if (SelectedPlan is not null && SelectedPlan.ProductionOrderId != SelectedOrder?.Id)
            SelectedPlan = null;

        Status = $"{Plans.Count} kế hoạch.";
    }

    /// <summary>Mo CUA SO rieng de tao ke hoach. Cho chon don + SL + ma chuyen.</summary>
    [RelayCommand]
    private async Task CreateAsync()
    {
        NewPlannedQty = 1; NewLineCode = "";
        var orderBefore = SelectedOrder;
        // Form nay dung CHUNG VM va bind SelectedOrder TwoWay: moi lan go trong o chon don,
        // AutoCompleteBox ban ra hang loat lan doi SelectedOrder (ke ca null) -> chan hook cho
        // toi khi dong form, roi tu nap lai neu don da doi.
        _suppressSelectionHook = true;
        var form = new Views.Dialogs.PlanFormView { DataContext = this };
        try
        {
            await DialogService.ShowFormAsync("Tạo kế hoạch sản xuất", form, async () =>
            {
                if (SelectedOrder is null) return "Chọn đơn sản xuất.";
                if (NewPlannedQty <= 0) return "Số lượng kế hoạch phải > 0.";

                if (SelectedOrderItem is null) return "Chọn mặt hàng cần lập kế hoạch.";
                var id = await _api.CreatePlanAsync(new
                {
                    productionOrderId = SelectedOrder!.Id,
                    productionOrderItemId = SelectedOrderItem!.Id,
                    plannedQty = NewPlannedQty,
                    plannedStart = (string?)null,
                    plannedEnd = (string?)null,
                    lineCode = NewLineCode,
                    note = (string?)null
                });
                await LoadCoreAsync(id);   // don vua co ke hoach -> nap lai ca cot trai
                Status = $"Đã tạo kế hoạch id={id}.";
                return null;
            }, saveText: "Tạo kế hoạch", height: 320);
        }
        finally { _suppressSelectionHook = false; }

        // Dong form ma don dang chon da khac (nguoi dung doi trong form) -> nap lai bang cho khop.
        // So theo Id: sau khi tao xong da nap lai danh sach nen doi tuong don la ban moi.
        if (SelectedOrder?.Id != orderBefore?.Id) await LoadPlansCoreAsync();
    }

    // Loi thao tac ke hoach — hien do ngay tren bang (Status cuoi trang mo, de bi bo sot).
    [ObservableProperty] private string _planError = "";

    [RelayCommand]
    private async Task UpdateStatusAsync()
    {
        PlanError = "";
        if (SelectedPlan is null) { PlanError = "Chọn một kế hoạch trong bảng trước."; return; }
        await RunAsync("Đang cập nhật...", async () =>
        {
            var planId = SelectedPlan!.Id;
            try
            {
                await _api.UpdatePlanStatusAsync(planId, new { status = SelectedStatus });
            }
            catch (ApiException ex) { PlanError = ex.Message; throw; }
            // Nap lai CA cot trai: ke hoach chay xong (DONE) keo theo trang thai don doi -> the
            // trang thai o danh sach don phai cap nhat theo, khong chi rieng bang ke hoach.
            await LoadCoreAsync(planId);
            Status = $"Đã đổi trạng thái -> {SelectedStatus}.";
        });
    }

    /// <summary>
    /// Luu ke hoach dang chon: SL + ma chuyen, VA ap luon trang thai dang chon tren ComboBox
    /// (truoc day nut Luu bo quen truong trang thai -> nguoi dung doi DONE bam Luu khong an thua).
    /// </summary>
    [RelayCommand]
    private async Task SavePlanAsync()
    {
        PlanError = "";
        if (SelectedPlan is null) { PlanError = "Chọn một kế hoạch trong bảng để sửa."; return; }
        if (NewPlannedQty <= 0) { PlanError = "SL kế hoạch phải > 0."; return; }
        await RunAsync("Đang lưu kế hoạch...", async () =>
        {
            var planId = SelectedPlan!.Id;
            var curStatus = SelectedPlan!.Status;
            var wantStatus = SelectedStatus;
            var fieldsSaved = false;
            try
            {
                await _api.UpdatePlanAsync(planId, new
                {
                    plannedQty = NewPlannedQty,
                    lineCode = string.IsNullOrWhiteSpace(NewLineCode) ? null : NewLineCode.Trim(),
                    note = (string?)null,
                });
                fieldsSaved = true;
                if (!string.IsNullOrEmpty(wantStatus) && wantStatus != curStatus)
                    await _api.UpdatePlanStatusAsync(planId, new { status = wantStatus });
            }
            catch (ApiException ex)
            {
                // 2 buoc luu tach roi: neu SL/chuyen da luu ma doi trang thai bi tu choi,
                // van phai nap lai de bang khop voi server, va noi ro phan nao da vao.
                PlanError = fieldsSaved
                    ? $"Đã lưu SL/chuyền, nhưng đổi trạng thái bị từ chối: {ex.Message}"
                    : ex.Message;
                await LoadPlansCoreAsync(planId);
                throw;
            }
            // Doi trang thai ke hoach co the doi luon trang thai don -> nap lai ca cot trai.
            await LoadCoreAsync(planId);
            Status = wantStatus != curStatus
                ? $"Đã lưu kế hoạch id={planId} và chuyển trạng thái -> {wantStatus}."
                : $"Đã lưu kế hoạch id={planId}.";
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

    // Doi bo loc trong luc dang chay: ghi nho va tu nap lai sau khi xong (khong nuot mat lua chon).
    private bool _reloadPending;

    private async Task RunAsync(string busy, Func<Task> a)
    {
        if (IsBusy) { _reloadPending = true; return; }
        IsBusy = true; Status = busy;
        try
        {
            _reloadPending = false;
            await a();
            // Nguoi dung vua doi thang/nam trong luc dang chay -> nap lai theo lua chon moi nhat.
            // Chi NAP LAI, khong chay lai `a`: `a` co the la lenh ghi (luu/xoa) — chay 2 lan la sai.
            while (_reloadPending)
            {
                _reloadPending = false;
                await LoadCoreAsync();
            }
        }
        catch (ApiException ex) { Status = $"Lỗi [{ex.Code}]: {ex.Message}"; }
        catch (Exception ex) { Status = $"Lỗi: {ex.Message}"; }
        finally { IsBusy = false; _reloadPending = false; }
    }
}
