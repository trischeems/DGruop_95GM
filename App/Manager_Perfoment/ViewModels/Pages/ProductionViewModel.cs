using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GM95.App.ManagerPerformance.Models;
using GM95.App.ManagerPerformance.Services;

namespace GM95.App.ManagerPerformance.ViewModels.Pages;

/// <summary>Trang San xuat: theo doi cong doan Cat/May/QC va xuat kho NVL cho san xuat.</summary>
public sealed partial class ProductionViewModel : PageViewModel, IExportProvider
{
    /// <summary>Cac bang cua trang nay cho nut "Xuất Excel" chung (xuat dung du lieu dang hien thi).</summary>
    public IReadOnlyList<ExportTable> GetExportTables() => new[]
    {
        ExportTable.Create<ProductionOrder>("Đơn sản xuất", () => FilteredOrders, rowDate: o => o.DueDate,
            ("Số đơn", o => o.OrderNo),
            ("Mã hàng", o => o.ProductSku),
            ("Tên mã hàng", o => o.ProductName),
            ("SL", o => o.Quantity),
            ("ĐVT", o => o.ProductUomName),
            ("Trạng thái", o => Converters.CodeToVietnameseConverter.Translate(o.Status)),
            ("Hạn giao", o => o.DueDate),
            ("Quy trình", o => o.RoutingName)),
        ExportTable.Create<ProductionStep>("Công đoạn của đơn", () => Steps, rowDate: null,
            ("TT", s => s.Seq),
            ("Công đoạn", s => s.StageName),
            ("Trạng thái", s => Converters.CodeToVietnameseConverter.Translate(s.Status)),
            ("Bỏ qua", s => s.IsSkipped),
            ("Vào", s => s.QtyIn),
            ("Ra", s => s.QtyOut),
            ("Lỗi", s => s.QtyDefect),
            ("ĐVT", s => s.ProductUomName)),
        ExportTable.Create<MaterialIssueItem>("NVL đã xuất cho đơn", () => IssueItems, rowDate: t => t.IssuedAt,
            ("Số phiếu", t => t.IssueNo),
            ("Mã NVL", t => t.MaterialSku),
            ("Tên NVL", t => t.MaterialName),
            ("SL xuất", t => t.QtyIssued),
            ("ĐVT", t => t.MaterialUomName),
            ("Đơn giá", t => t.UnitCost),
            ("Kho", t => t.WarehouseName),
            ("Ngày xuất", t => t.IssuedAt)),
    };

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
    // Cac DONG NVL cua phieu xuat cua don dang chon (co ma + ten NVL tung dong).
    public ObservableCollection<MaterialIssueItem> IssueItems { get; } = new();

    // Ton kho hien tai theo NVL (de goi y "con lai bao nhieu" khi xuat kho).
    private readonly Dictionary<long, MaterialStock> _stockByMaterial = new();

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

    // Form tao cong doan moi ngay trong dialog quy trinh (khong phai thoat ra trang danh muc).
    [ObservableProperty] private string _stageCode = "";
    [ObservableProperty] private string _stageName = "";
    [ObservableProperty] private int _stageSeq;
    [ObservableProperty] private bool _stageActive = true;

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

    // Goi y ton kho cua NVL dang chon trong dialog xuat (con lai bao nhieu de biet duong nhap).
    [ObservableProperty] private string _issueAvailableText = "Chọn NVL để xem tồn còn lại.";
    // Gia da TU DIEN cho NVL truoc do — de doi NVL thi cap nhat lai goi y,
    // nhung KHONG de gia cua NVL cu dinh sang NVL moi, cung khong de gia nguoi dung tu go.
    private decimal _autoFilledCost;
    partial void OnSelectedMaterialChanged(Material? value) => UpdateIssueAvailable();
    private void UpdateIssueAvailable()
    {
        if (SelectedMaterial is null)
        {
            IssueAvailableText = "Chọn NVL để xem tồn còn lại.";
            return;
        }
        if (_stockByMaterial.TryGetValue(SelectedMaterial.Id, out var s))
        {
            IssueAvailableText =
                $"Còn lại (tất cả kho): {s.TotalOnHand:0.####} {s.UomName} (khả dụng {s.TotalAvailable:0.####}, đã giữ chỗ {s.TotalReserved:0.####})";
            // Goi y don gia = gia nhap gan nhat. Chi ghi de khi o gia dang 0 hoac dang mang
            // gia tu-dien cua NVL truoc (gia nguoi dung tu go thi giu nguyen).
            if ((IssueUnitCost == 0 || IssueUnitCost == _autoFilledCost) && s.LastUnitCost > 0)
            {
                IssueUnitCost = s.LastUnitCost;
                _autoFilledCost = s.LastUnitCost;
            }
        }
        else
        {
            IssueAvailableText = $"NVL {SelectedMaterial.Sku} chưa có tồn kho — không thể xuất.";
        }
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

        // Ton kho hien tai (khong loc thang) de goi y "con lai" khi xuat kho.
        var stockRows = await _api.GetStockAsync(false, null, null);
        _stockByMaterial.Clear();
        foreach (var s in stockRows) _stockByMaterial[s.MaterialId] = s;
        UpdateIssueAvailable();

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
        IssueItems.Clear();
        if (SelectedOrder is null) return;
        var ss = await _api.GetStepsAsync(SelectedOrder.Id);
        foreach (var s in ss) Steps.Add(s);
        SelectedStep = Steps.FirstOrDefault(s => s.Id == keepStep);

        // Nap tung DONG NVL cua cac phieu xuat cua don (co ma + ten NVL). Toi da 500 dong moi nhat.
        const int issueLimit = 500;
        var items = await _api.GetIssueItemsAsync(SelectedOrder.Id, issueLimit);
        foreach (var i in items) IssueItems.Add(i);
        var issueCount = items.Select(i => i.MaterialIssueId).Distinct().Count();

        Status = $"{Steps.Count} công đoạn · {issueCount} phiếu xuất NVL ({items.Count} dòng)"
               + (items.Count >= issueLimit ? $" — chỉ hiển thị {issueLimit} dòng mới nhất." : ".");
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

    // Loi cap nhat cong doan — hien do ngay canh form (Status o cuoi trang de bi bo sot).
    [ObservableProperty] private string _stepError = "";

    [RelayCommand]
    private async Task UpdateStepAsync()
    {
        StepError = "";
        if (SelectedStep is null) { StepError = "Chọn một công đoạn trong bảng trước."; return; }
        // CONG THUC: ra + loi khong duoc vuot vao (khop kiem tra server, bao som cho ro).
        if (QtyOut + QtyDefect > QtyIn)
        {
            StepError = $"SL ra ({QtyOut:0.####}) + SL lỗi ({QtyDefect:0.####}) = {QtyOut + QtyDefect:0.####} " +
                        $"vượt SL vào ({QtyIn:0.####}). Sửa lại số liệu trước khi lưu.";
            return;
        }
        await RunAsync("Đang cập nhật công đoạn...", async () =>
        {
            var stepId = SelectedStep!.Id;
            try
            {
                await _api.UpdateStepAsync(stepId, new { status = SelectedStepStatus, qtyIn = QtyIn, qtyOut = QtyOut, qtyDefect = QtyDefect, note = (string?)null });
            }
            catch (ApiException ex)
            {
                StepError = ex.Message;   // hien do ngay canh form
                throw;                    // RunAsync van ghi Status
            }
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
        }, saveText: "Áp quy trình", width: 1040, height: 620, scrollable: false);
    }

    /// <summary>
    /// Tao CONG DOAN MOI ngay trong dialog quy trinh (In, Theu, Say, Dong goi...).
    /// Tao xong nap lai danh muc va chon san cong doan vua tao de bam "+ Them buoc le" duoc luon.
    /// </summary>
    [RelayCommand]
    private async Task NewStageAsync()
    {
        StageCode = StageName = "";
        StageSeq = Stages.Count + 1;
        StageActive = true;

        var form = new Views.Dialogs.StageFormView { DataContext = this };
        await DialogService.ShowFormAsync("Thêm công đoạn", form, async () =>
        {
            if (string.IsNullOrWhiteSpace(StageCode)) return "Nhập mã công đoạn (vd PRINTING).";
            if (string.IsNullOrWhiteSpace(StageName)) return "Nhập tên công đoạn.";

            var newId = await _api.CreateStageAsync(new
            {
                code = StageCode.Trim().ToUpperInvariant(),
                name = StageName.Trim(),
                seq = StageSeq > 0 ? StageSeq : (int?)null
            });

            // Nap lai danh muc de cong doan moi hien ngay o cot trai.
            var sts = await _api.GetStagesAsync(activeOnly: true);
            Stages.Clear();
            foreach (var st in sts) Stages.Add(st);
            StageListFilter = "";
            ApplyStageFilter();
            SelectedStage = Stages.FirstOrDefault(st => st.Id == newId);

            Status = $"Đã thêm công đoạn {StageName}.";
            return null;
        }, saveText: "Thêm công đoạn", width: 520, height: 360);
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
    // Loi/goi y trong dialog xuat kho — hien ngay trong cua so (Status trang bi dialog che).
    [ObservableProperty] private string _issueError = "";

    [RelayCommand]
    private void AddIssueLine()
    {
        IssueError = "";
        if (SelectedMaterial is null) { IssueError = "Chọn NVL ở danh sách bên trái trước."; return; }
        if (IssueQty <= 0) { IssueError = "Số lượng xuất phải > 0."; return; }
        if (IssueLines.Any(l => l.Material?.Id == SelectedMaterial.Id))
        {
            IssueError = $"NVL {SelectedMaterial.Sku} đã có trong phiếu. Xoá dòng cũ nếu muốn sửa.";
            return;
        }
        // Chan xuat qua TONG ton cac kho (server con kiem tra chinh xac theo kho da chon trong transaction).
        if (_stockByMaterial.TryGetValue(SelectedMaterial.Id, out var s) && IssueQty > s.TotalOnHand)
        {
            IssueError = $"Tồn không đủ: {SelectedMaterial.Sku} còn tổng cộng {s.TotalOnHand:0.####} {s.UomName} ở mọi kho, cần xuất {IssueQty:0.####}.";
            return;
        }
        IssueLines.Add(new IssueLine { Material = SelectedMaterial, QtyIssued = IssueQty, UnitCost = IssueUnitCost });
        SelectedMaterial = null; IssueQty = 0; IssueUnitCost = 0; _autoFilledCost = 0;
        Status = $"Phiếu có {IssueLines.Count} dòng.";
    }

    /// <summary>Xoá dòng đang chọn khỏi phiếu xuất.</summary>
    [RelayCommand]
    private void RemoveIssueLine()
    {
        if (SelectedIssueLine is null) { IssueError = "Chọn một dòng để xoá."; return; }
        IssueLines.Remove(SelectedIssueLine);
        IssueError = "";
    }

    /// <summary>Mở CỬA SỔ riêng "Xuất kho NVL phiếu nhiều dòng" cho đơn đang chọn.</summary>
    [RelayCommand]
    private async Task OpenIssueDialogAsync()
    {
        if (SelectedOrder is null) { Status = "Chọn đơn sản xuất trước."; return; }
        IssueLines.Clear();
        SelectedWarehouse = null; SelectedMaterial = null; IssueQty = 0; IssueUnitCost = 0;
        _autoFilledCost = 0;
        IssueError = "";
        UpdateIssueAvailable();
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
