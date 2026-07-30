using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GM95.App.ManagerPerformance.Models;
using GM95.App.ManagerPerformance.Services;

namespace GM95.App.ManagerPerformance.ViewModels.Pages;

/// <summary>Kho nguyen vat lieu: danh sach NVL + tao moi + nhap kho + xem ton. Dung API that.</summary>
public sealed partial class MaterialsViewModel : PageViewModel, IExportProvider
{
    /// <summary>Cac bang cua trang nay cho nut "Xuất Excel" chung (xuat dung du lieu dang hien thi).</summary>
    public IReadOnlyList<ExportTable> GetExportTables() => new[]
    {
        ExportTable.Create<Material>("Danh mục NVL", () => CatalogView, rowDate: null,
            ("ID", m => m.Id),
            ("SKU", m => m.Sku),
            ("Tên NVL", m => m.Name),
            ("ĐVT", m => m.UomName),
            ("Ngưỡng tồn thấp", m => m.ReorderLevel),
            ("Cỡ lô mua", m => m.ReorderQuantity),
            ("Đơn giá (VND)", m => m.StandardCost),
            ("Hoạt động", m => m.IsActive)),
        ExportTable.Create<MaterialStock>("Tồn kho khả dụng", () => Stock, rowDate: null,
            ("NVL ID", s => s.MaterialId),
            ("SKU", s => s.Sku),
            ("Tên NVL", s => s.Name),
            ("ĐVT", s => s.UomName),
            ("Tồn thực", s => s.TotalOnHand),
            ("Đã giữ chỗ", s => s.TotalReserved),
            ("Khả dụng", s => s.TotalAvailable),
            ("Tồn thấp", s => s.IsLowStock),
            ("Đơn giá gần nhất", s => s.LastUnitCost),
            ("Đơn giá bình quân", s => s.AvgUnitCost),
            ("Giá trị tồn (VND)", s => s.StockValue)),
        ExportTable.Create<StockTransaction>("Lịch sử nhập xuất (sổ kho)", () => History, rowDate: t => t.CreatedAt,
            ("Mã NVL", t => t.MaterialSku),
            ("Tên NVL", t => t.MaterialName),
            ("Loại", t => Converters.CodeToVietnameseConverter.Translate(t.TxnType)),
            ("SL", t => t.Quantity),
            ("ĐVT", t => t.MaterialUomName),
            ("Đơn giá", t => t.UnitCost),
            ("Tồn sau", t => t.BalanceAfter),
            ("Kho", t => t.WarehouseName),
            ("Ngày", t => t.CreatedAt)),
    };

    private readonly ApiClient _api;

    public MaterialsViewModel(ApiClient api) => _api = api;

    public override string Title => "Kho nguyên vật liệu";
    public override string Subtitle => "Danh mục NVL, nhập kho và tồn kho khả dụng";

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<Material> Materials { get; } = new();
    public ObservableCollection<MaterialStock> Stock { get; } = new();
    public ObservableCollection<Uom> Uoms { get; } = new();
    public ObservableCollection<Warehouse> Warehouses { get; } = new();
    // Danh sach NVL da loc (cho danh sach ben trai trong dialog nhap kho).
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

    // Phieu nhap NHIEU DONG: cac dong NVL cho vao phieu (luu 1 lan).
    public ObservableCollection<ReceiptLine> ReceiptLines { get; } = new();
    // Lich su giao dich kho cua NVL dang chon trong bang ton (don gia tung lan nhap/xuat).
    public ObservableCollection<StockTransaction> History { get; } = new();

    // ===== O tim kiem danh muc NVL (loc tai cho tren danh sach da tai) =====
    public ObservableCollection<Material> CatalogView { get; } = new();
    [ObservableProperty] private string _catalogSearch = "";
    partial void OnCatalogSearchChanged(string value) => ApplyCatalogFilter();
    private void ApplyCatalogFilter()
    {
        var kw = (CatalogSearch ?? "").Trim();
        CatalogView.Clear();
        foreach (var m in Materials)
            if (kw.Length == 0
                || (m.Name?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                || (m.Sku?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false))
                CatalogView.Add(m);
    }

    // ===== Sidebar "SKU tuong tu" trong dialog tao NVL (goi y chong tao trung) =====
    public ObservableCollection<Material> SimilarMaterials { get; } = new();
    [ObservableProperty] private string _similarStatus = "Gõ SKU hoặc tên để xem các NVL gần giống.";
    private CancellationTokenSource? _similarCts;
    partial void OnNewSkuChanged(string value) => _ = RefreshSimilarAsync();
    partial void OnNewNameChanged(string value) => _ = RefreshSimilarAsync();
    private async Task RefreshSimilarAsync()
    {
        _similarCts?.Cancel();
        var cts = _similarCts = new CancellationTokenSource();
        // Uu tien tim theo SKU dang go; chua go SKU thi tim theo ten.
        var q = !string.IsNullOrWhiteSpace(NewSku) ? NewSku.Trim() : (NewName ?? "").Trim();
        if (q.Length < 2)
        {
            SimilarMaterials.Clear();
            SimilarStatus = "Gõ SKU hoặc tên để xem các NVL gần giống.";
            return;
        }
        try
        {
            await Task.Delay(250, cts.Token);   // debounce go phim
            var found = await _api.SearchMaterialsAsync(q, 10, cts.Token);
            if (cts.Token.IsCancellationRequested) return;
            SimilarMaterials.Clear();
            foreach (var m in found) SimilarMaterials.Add(m);
            SimilarStatus = found.Count == 0
                ? "Chưa có SKU nào gần giống — an toàn để tạo."
                : $"{found.Count} NVL gần giống — kiểm tra để tránh tạo trùng:";
        }
        catch (OperationCanceledException) { /* go tiep -> lan sau */ }
        catch { /* goi y chi la phu, khong chan viec tao */ }
    }

    // Form tao NVL (chon ĐVT tu dropdown thay vi go id)
    [ObservableProperty] private string _newSku = "";
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private Uom? _selectedUom;
    [ObservableProperty] private decimal _newReorderLevel = 0;
    [ObservableProperty] private decimal _newReorderQuantity = 0;
    [ObservableProperty] private decimal _newStandardCost = 0;

    // Form nhap kho: kho chung cho ca phieu + o nhap 1 dong (bam "Them dong" de day vao luoi).
    [ObservableProperty] private Warehouse? _receiveWarehouse;
    [ObservableProperty] private Material? _receiveMaterial;
    [ObservableProperty] private decimal _receiveQuantity = 0;
    [ObservableProperty] private decimal _receiveUnitCost = 0;
    // Dong dang chon trong luoi phieu (de xoa).
    [ObservableProperty] private ReceiptLine? _selectedReceiptLine;
    // NVL dang chon trong bang ton (de xem lich su giao dich).
    [ObservableProperty] private MaterialStock? _selectedStockRow;

    partial void OnSelectedStockRowChanged(MaterialStock? value) => _ = LoadHistoryAsync(value?.MaterialId);

    // Hang sua tren dau bang danh muc: bam 1 dong -> tu nap du lieu vao cac field.
    [ObservableProperty] private Material? _selectedListMaterial;
    [ObservableProperty] private string _editSku = "";
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private decimal _editReorderLevel;
    [ObservableProperty] private decimal _editReorderQuantity;
    [ObservableProperty] private decimal _editStandardCost;
    [ObservableProperty] private bool _editIsActive = true;

    partial void OnSelectedListMaterialChanged(Material? value)
    {
        if (value is null) return;
        EditSku = value.Sku;
        EditName = value.Name;
        EditReorderLevel = value.ReorderLevel;
        EditReorderQuantity = value.ReorderQuantity;
        EditStandardCost = value.StandardCost;
        EditIsActive = value.IsActive;
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
    private Task LoadAsync() => RunAsync("Đang tải...", LoadCoreAsync);

    // Nap du lieu KHONG boc RunAsync: cac lenh ghi goi truc tiep ham nay sau khi
    // thanh cong de bang tu cap nhat (RunAsync co guard IsBusy nen khong goi long nhau duoc).
    // Giu nguyen lua chon dang co tren cac dropdown theo Id sau khi nap lai.
    private async Task LoadCoreAsync()
    {
        var keepUom = SelectedUom?.Id;
        var keepWh = ReceiveWarehouse?.Id;
        var keepMat = ReceiveMaterial?.Id;
        var keepListSel = SelectedListMaterial?.Id;

        var uoms = await _api.GetUomsAsync();
        Uoms.Clear(); foreach (var u in uoms) Uoms.Add(u);
        SelectedUom = Uoms.FirstOrDefault(u => u.Id == keepUom);

        var whs = await _api.GetWarehousesAsync();
        Warehouses.Clear(); foreach (var w in whs) Warehouses.Add(w);
        ReceiveWarehouse = Warehouses.FirstOrDefault(w => w.Id == keepWh);

        var (fy, fm) = FilterPeriod;
        var mats = await _api.GetMaterialsAsync(activeOnly: false, year: fy, month: fm);
        Materials.Clear(); foreach (var m in mats) Materials.Add(m);
        ApplyMaterialFilter();   // danh sach NVL cho dialog nhap kho
        ApplyCatalogFilter();    // danh muc tren trang (co o tim kiem)
        ReceiveMaterial = Materials.FirstOrDefault(m => m.Id == keepMat);
        SelectedListMaterial = Materials.FirstOrDefault(m => m.Id == keepListSel);

        var stock = await _api.GetStockAsync(false, fy, fm);
        Stock.Clear(); foreach (var s in stock) Stock.Add(s);

        // Lich su: chua chon NVL thi hien cac giao dich moi nhat (moi NVL).
        await LoadHistoryAsync(SelectedStockRow?.MaterialId);

        Status = $"{Materials.Count} NVL · {Stock.Count} dòng tồn kho.";
    }

    /// <summary>Mo CUA SO rieng de tao NVL. Dien xong bam Luu -> goi API, dong dialog, nap lai bang.</summary>
    [RelayCommand]
    private async Task CreateMaterialAsync()
    {
        // Reset o nhap cho lan tao moi (ca sidebar goi y SKU tuong tu).
        NewSku = ""; NewName = ""; SelectedUom = null;
        NewReorderLevel = 0; NewReorderQuantity = 0; NewStandardCost = 0;
        SimilarMaterials.Clear();
        SimilarStatus = "Gõ SKU hoặc tên để xem các NVL gần giống.";

        var form = new Views.Dialogs.MaterialFormView { DataContext = this };
        await DialogService.ShowFormAsync("Tạo nguyên vật liệu", form, async () =>
        {
            // Validate ngay trong dialog: tra ve chuoi loi -> giu dialog mo.
            if (string.IsNullOrWhiteSpace(NewSku)) return "Nhập SKU.";
            if (string.IsNullOrWhiteSpace(NewName)) return "Nhập tên NVL.";
            if (SelectedUom is null) return "Chọn đơn vị tính.";

            var sku = NewSku.Trim();
            var id = await _api.CreateMaterialAsync(new
            {
                sku, name = NewName.Trim(), uomId = SelectedUom!.Id,
                categoryId = (long?)null, reorderLevel = NewReorderLevel,
                reorderQuantity = NewReorderQuantity, standardCost = NewStandardCost,
            });
            await LoadCoreAsync();
            Status = $"Đã tạo NVL id={id} ({sku}).";
            return null; // thanh cong -> dong dialog
        }, saveText: "Tạo NVL", width: 960, height: 440);
    }

    /// <summary>Them 1 dong NVL vao phieu nhap (luoi). Chan trung NVL trong cung phieu.</summary>
    [RelayCommand]
    private void AddReceiptLine()
    {
        if (ReceiveMaterial is null) { Status = "Chọn NVL để thêm dòng."; return; }
        if (ReceiveQuantity <= 0) { Status = "Số lượng nhập phải > 0."; return; }
        if (ReceiptLines.Any(l => l.Material?.Id == ReceiveMaterial.Id))
        {
            Status = $"NVL {ReceiveMaterial.Sku} đã có trong phiếu. Xoá dòng cũ nếu muốn sửa.";
            return;
        }
        ReceiptLines.Add(new ReceiptLine
        {
            Material = ReceiveMaterial, QtyReceived = ReceiveQuantity, UnitCost = ReceiveUnitCost,
        });
        // Reset o nhap dong cho lan sau.
        ReceiveMaterial = null; ReceiveQuantity = 0; ReceiveUnitCost = 0;
        Status = $"Phiếu có {ReceiptLines.Count} dòng. Bấm 'Lưu phiếu nhập' để nhập kho.";
    }

    /// <summary>Xoa dong dang chon khoi phieu nhap.</summary>
    [RelayCommand]
    private void RemoveReceiptLine()
    {
        if (SelectedReceiptLine is null) { Status = "Chọn một dòng trong phiếu để xoá."; return; }
        ReceiptLines.Remove(SelectedReceiptLine);
        Status = $"Phiếu còn {ReceiptLines.Count} dòng.";
    }

    /// <summary>Mo CUA SO rieng "Nhap kho phieu nhieu dong". Them nhieu NVL roi luu 1 lan.</summary>
    [RelayCommand]
    private async Task OpenReceiptDialogAsync()
    {
        ReceiptLines.Clear();
        ReceiveWarehouse = null; ReceiveMaterial = null; ReceiveQuantity = 0; ReceiveUnitCost = 0;
        var form = new Views.Dialogs.ReceiptFormView { DataContext = this };
        await DialogService.ShowFormAsync("Nhập kho (phiếu nhiều dòng)", form, async () =>
        {
            if (ReceiveWarehouse is null) return "Chọn kho nhập.";
            if (ReceiptLines.Count == 0) return "Phiếu chưa có dòng NVL nào. Bấm '+ Thêm dòng' trước.";

            var r = await _api.CreateReceiptAsync(new
            {
                warehouseId = ReceiveWarehouse!.Id,
                note = "Nhập từ app",
                items = ReceiptLines.Select(l => new
                {
                    materialId = l.Material!.Id, qtyReceived = l.QtyReceived, unitCost = l.UnitCost,
                }).ToArray(),
            });
            var n = ReceiptLines.Count;
            ReceiptLines.Clear();
            await LoadCoreAsync();
            Status = $"Đã lưu phiếu nhập {r.ReceiptNo} ({r.LineCount} dòng NVL).";
            return null;
        }, saveText: "Lưu phiếu nhập", width: 900, height: 540, scrollable: false);
    }

    // Nap lich su giao dich kho: co NVL -> loc theo NVL do; null -> giao dich moi nhat cua moi NVL.
    private async Task LoadHistoryAsync(long? materialId)
    {
        try
        {
            var txns = await _api.GetStockTransactionsAsync(materialId, 50);
            History.Clear();
            foreach (var t in txns) History.Add(t);
        }
        catch (Exception ex) { Status = $"Không tải được lịch sử nhập/xuất: {ex.Message}"; }
    }

    /// <summary>Luu thay doi cua dong NVL dang chon (hang field tren dau bang).</summary>
    [RelayCommand]
    private async Task SaveMaterialAsync()
    {
        if (SelectedListMaterial is null) { Status = "Chọn một dòng NVL trong bảng để sửa."; return; }
        if (string.IsNullOrWhiteSpace(EditName)) { Status = "Tên NVL không được rỗng."; return; }
        await RunAsync("Đang lưu NVL...", async () =>
        {
            var m = SelectedListMaterial!;
            await _api.UpdateMaterialAsync(m.Id, new
            {
                name = EditName.Trim(),
                categoryId = m.CategoryId,
                reorderLevel = EditReorderLevel,
                reorderQuantity = EditReorderQuantity,
                standardCost = EditStandardCost,
                isActive = EditIsActive,
            });
            await LoadCoreAsync();
            Status = $"Đã lưu NVL {m.Sku}.";
        });
    }

    /// <summary>
    /// Xoa NVL dang chon: hoi impact truoc -> popup canh bao ro anh huong.
    /// Chua dung o dau -> xoa vinh vien; dang duoc dung -> de xuat NGUNG HOAT DONG.
    /// </summary>
    [RelayCommand]
    private async Task DeleteMaterialAsync()
    {
        if (SelectedListMaterial is null) { Status = "Chọn một dòng NVL trong bảng để xoá."; return; }
        var m = SelectedListMaterial!;
        await RunAsync("Đang kiểm tra ảnh hưởng...", async () =>
        {
            var impact = await _api.GetMaterialImpactAsync(m.Id);
            if (impact is null) { Status = "Không tìm thấy NVL trên server."; return; }

            if (impact.CanDelete)
            {
                var ok = await DialogService.ConfirmAsync(
                    "Xoá nguyên vật liệu",
                    $"Xoá vĩnh viễn NVL {m.Sku} — {m.Name}?\n" +
                    "NVL này chưa được sử dụng ở đâu (không tồn kho, không định mức, không phiếu).",
                    "Xoá", danger: true);
                if (!ok) { Status = "Đã huỷ thao tác xoá."; return; }
                await _api.DeleteMaterialAsync(m.Id);
                SelectedListMaterial = null;
                await LoadCoreAsync();
                Status = $"Đã xoá NVL {m.Sku}.";
            }
            else
            {
                var ok = await DialogService.ConfirmAsync(
                    "Không thể xoá vĩnh viễn — NVL đang được sử dụng",
                    $"NVL {m.Sku} — {m.Name} đang liên quan tới:\n" +
                    $"• {impact.StockRowCount} dòng tồn kho (đang tồn {impact.TotalOnHand:0.####})\n" +
                    $"• {impact.BomItemCount} dòng định mức BOM\n" +
                    $"• {impact.ReservationCount} phiếu giữ chỗ · {impact.IssueLineCount} dòng phiếu xuất kho\n" +
                    $"• {impact.TransactionCount} bút toán sổ kho · {impact.LossReportCount} dòng hao hụt\n\n" +
                    "Chuyển sang NGƯNG HOẠT ĐỘNG thay thế? (ẩn khỏi danh mục hoạt động, " +
                    "toàn bộ dữ liệu cũ giữ nguyên; có thể bật lại bằng ô Hoạt động + Lưu)",
                    "Ngưng hoạt động", danger: true);
                if (!ok) { Status = "Đã huỷ thao tác."; return; }
                await _api.UpdateMaterialAsync(m.Id, new
                {
                    name = m.Name, categoryId = m.CategoryId,
                    reorderLevel = m.ReorderLevel, reorderQuantity = m.ReorderQuantity,
                    standardCost = m.StandardCost, isActive = false,
                });
                await LoadCoreAsync();
                Status = $"Đã ngưng hoạt động NVL {m.Sku}.";
            }
        });
    }

    private async Task RunAsync(string busy, Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; Status = busy;
        try { await action(); }
        catch (ApiException ex) { Status = $"Lỗi [{ex.Code}]: {ex.Message}"; }
        catch (Exception ex) { Status = $"Lỗi: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
