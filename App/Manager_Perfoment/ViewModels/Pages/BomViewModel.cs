using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GM95.App.ManagerPerformance.Models;
using GM95.App.ManagerPerformance.Services;

namespace GM95.App.ManagerPerformance.ViewModels.Pages;

/// <summary>Dinh muc BOM: chon ma hang -> phien ban BOM -> dinh muc NVL + phe duyet. Dung API that.</summary>
public sealed partial class BomViewModel : PageViewModel, IExportProvider
{
    /// <summary>Cac bang cua trang nay cho nut "Xuất Excel" chung (xuat dung du lieu dang hien thi).</summary>
    public IReadOnlyList<ExportTable> GetExportTables() => new[]
    {
        ExportTable.Create<Product>("Mã hàng", () => FilteredProducts, rowDate: null,
            ("ID", p => p.Id),
            ("SKU", p => p.Sku),
            ("Tên mã hàng", p => p.Name),
            ("ĐVT", p => p.UomName),
            ("Hoạt động", p => p.IsActive)),
        ExportTable.Create<Bom>("Các phiên bản BOM", () => Boms, rowDate: b => b.EffectiveFrom,
            ("ID", b => b.Id),
            ("Phiên bản", b => b.Version),
            ("Trạng thái", b => Converters.CodeToVietnameseConverter.Translate(b.Status)),
            ("Hiệu lực từ", b => b.EffectiveFrom),
            ("Ghi chú", b => b.Note)),
        ExportTable.Create<BomItem>("Định mức của BOM đang chọn", () => Items, rowDate: null,
            ("NVL", i => i.MaterialId),
            ("SKU NVL", i => i.MaterialSku),
            ("Tên NVL", i => i.MaterialName),
            ("ĐM/đơn vị", i => i.QtyPerUnit),
            ("ĐVT", i => i.MaterialUomName),
            ("Hao hụt %", i => i.WastePct),
            ("Ghi chú", i => i.Note)),
    };

    private readonly ApiClient _api;

    public BomViewModel(ApiClient api) => _api = api;

    public override string Title => "Định mức BOM";
    public override string Subtitle => "Định mức NVL cho từng mã hàng + phê duyệt";

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    // Danh muc ma hang, phien ban BOM, NVL, dinh muc
    public ObservableCollection<Product> Products { get; } = new();
    // Danh sach ma hang hien o COT TRAI (da loc theo o tim ListFilter).
    public ObservableCollection<Product> FilteredProducts { get; } = new();
    public ObservableCollection<Bom> Boms { get; } = new();
    // Danh sach NVL da loc (cho danh sach ben trai trong dialog them dinh muc).
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

    // O tim cot trai: loc Products theo ten / SKU.
    [ObservableProperty] private string _listFilter = "";
    partial void OnListFilterChanged(string value) => ApplyProductFilter();
    private void ApplyProductFilter()
    {
        var kw = (ListFilter ?? "").Trim();
        FilteredProducts.Clear();
        foreach (var p in Products)
            if (kw.Length == 0
                || (p.Name?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                || (p.Sku?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false))
                FilteredProducts.Add(p);
    }
    public ObservableCollection<Material> Materials { get; } = new();
    public ObservableCollection<BomItem> Items { get; } = new();

    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private Bom? _selectedBom;

    // Form tao BOM
    [ObservableProperty] private string _newBomNote = "";

    // Form them/sua dinh muc (bam 1 dong trong bang -> tu nap vao form, Luu = upsert)
    [ObservableProperty] private Material? _selectedMaterial;
    [ObservableProperty] private decimal _newQtyPerUnit = 1;
    [ObservableProperty] private decimal _newWastePct = 0;
    [ObservableProperty] private BomItem? _selectedItem;

    // Dialog "them NHIEU NVL cung luc": luoi cac dong dinh muc cho, luu 1 lan.
    public ObservableCollection<BomLineInput> BomLines { get; } = new();
    [ObservableProperty] private BomLineInput? _selectedBomLine;

    partial void OnSelectedItemChanged(BomItem? value)
    {
        if (value is null) return;
        SelectedMaterial = Materials.FirstOrDefault(m => m.Id == value.MaterialId);
        NewQtyPerUnit = value.QtyPerUnit;
        NewWastePct = value.WastePct;
    }

    /// <summary>
    /// Sua/xoa dinh muc cua BOM khong con DRAFT anh huong du lieu lien quan (don dang dung BOM)
    /// -> popup canh bao van de + Tiep tuc / Huy theo dung quy trinh.
    /// </summary>
    private async Task<bool> ConfirmIfBomLockedAsync(string action)
    {
        if (SelectedBom is null) return false;
        if (SelectedBom.Status == "DRAFT") return true;
        return await DialogService.ConfirmAsync(
            "Cảnh báo: dữ liệu liên quan sẽ đổi theo",
            $"BOM v{SelectedBom.Version} đang ở trạng thái {SelectedBom.Status} (không phải bản nháp).\n" +
            $"{action} sẽ ảnh hưởng ngay tới các đơn đang dùng BOM này:\n" +
            "• Nhu cầu NVL / thiếu hụt / đề xuất mua tính lại theo số mới\n" +
            "• Sản lượng tối đa và hao hụt định mức cũng đổi theo\n\n" +
            "Khuyến nghị: tạo bản BOM mới rồi gửi duyệt thay thế. Vẫn muốn tiếp tục?",
            "Tiếp tục", danger: false);
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
    private async Task LoadCoreAsync(long? selectBomId = null)
    {
        var keepProduct = SelectedProduct?.Id;
        var keepMaterial = SelectedMaterial?.Id;

        var (pfy, pfm) = FilterPeriod;
        var ps = await _api.GetProductsAsync(false, pfy, pfm);
        Products.Clear();
        foreach (var p in ps) Products.Add(p);
        ApplyProductFilter();   // cap nhat danh sach ma hang cot trai
        // Chon lai; chan hook de khoi kich hoat LoadBoms 2 lan.
        _suppressSelectionHook = true;
        SelectedProduct = Products.FirstOrDefault(p => p.Id == keepProduct);
        _suppressSelectionHook = false;

        var ms = await _api.GetMaterialsAsync(false, pfy, pfm);
        Materials.Clear();
        foreach (var m in ms) Materials.Add(m);
        ApplyMaterialFilter();   // danh sach NVL cho dialog them dinh muc
        SelectedMaterial = Materials.FirstOrDefault(m => m.Id == keepMaterial);

        await LoadBomsCoreAsync(selectBomId);
    }

    private bool _suppressSelectionHook;

    partial void OnSelectedProductChanged(Product? value)
    {
        if (_suppressSelectionHook) return;
        _ = LoadBomsCoreAsync();
    }

    // Nap phien ban BOM cua ma hang dang chon; giu (hoac chon moi) BOM theo selectBomId.
    private async Task LoadBomsCoreAsync(long? selectBomId = null)
    {
        var keepBom = selectBomId ?? SelectedBom?.Id;

        Boms.Clear();
        if (SelectedProduct is null)
        {
            _suppressSelectionHook = true;
            SelectedBom = null;
            _suppressSelectionHook = false;
            Items.Clear();
            return;
        }

        var bs = await _api.GetBomsAsync(SelectedProduct.Id);
        foreach (var b in bs) Boms.Add(b);
        // Chon lai; chan hook de khoi kich hoat LoadItems 2 lan.
        _suppressSelectionHook = true;
        SelectedBom = Boms.FirstOrDefault(b => b.Id == keepBom) ?? Boms.FirstOrDefault();
        _suppressSelectionHook = false;
        await LoadItemsAsync();

        Status = $"{Boms.Count} phiên bản BOM cho {SelectedProduct.Name}.";
    }

    partial void OnSelectedBomChanged(Bom? value)
    {
        if (_suppressSelectionHook) return;
        _ = LoadItemsAsync();
    }

    private async Task LoadItemsAsync()
    {
        Items.Clear();
        if (SelectedBom is null) return;
        var d = await _api.GetBomAsync(SelectedBom.Id);
        if (d?.Items != null) foreach (var it in d.Items) Items.Add(it);
    }

    [RelayCommand]
    private async Task CreateBomAsync()
    {
        if (SelectedProduct is null) { Status = "Chọn mã hàng."; return; }
        await RunAsync("Đang tạo BOM...", async () =>
        {
            var id = await _api.CreateBomAsync(new { productId = SelectedProduct!.Id, note = NewBomNote });
            NewBomNote = "";
            // Nap lai va chon ngay BOM vua tao de them dinh muc.
            await LoadBomsCoreAsync(id);
            Status = $"Đã tạo BOM id={id} (DRAFT).";
        });
    }

    [RelayCommand]
    private async Task AddItemAsync()
    {
        if (SelectedBom is null || SelectedMaterial is null) { Status = "Chọn BOM và NVL."; return; }
        if (NewQtyPerUnit <= 0) { Status = "ĐM/đơn vị phải > 0."; return; }
        if (!await ConfirmIfBomLockedAsync("Sửa định mức")) { Status = "Đã huỷ thao tác."; return; }
        await RunAsync("Đang lưu định mức...", async () =>
        {
            await _api.UpsertBomItemAsync(SelectedBom!.Id, new { materialId = SelectedMaterial!.Id, qtyPerUnit = NewQtyPerUnit, wastePct = NewWastePct, note = (string?)null });
            await LoadItemsAsync();
            Status = "Đã lưu định mức.";
        });
    }

    // ----- Dialog "Thêm nhiều NVL cùng lúc" -----

    /// <summary>Thêm 1 dòng NVL vào lưới định mức (trong dialog). Chặn trùng NVL.</summary>
    [RelayCommand]
    private void AddBomLine()
    {
        if (SelectedMaterial is null) { Status = "Chọn NVL để thêm dòng."; return; }
        if (NewQtyPerUnit <= 0) { Status = "ĐM/đơn vị phải > 0."; return; }
        if (BomLines.Any(l => l.Material?.Id == SelectedMaterial.Id))
        {
            Status = $"NVL {SelectedMaterial.Sku} đã có trong danh sách.";
            return;
        }
        BomLines.Add(new BomLineInput { Material = SelectedMaterial, QtyPerUnit = NewQtyPerUnit, WastePct = NewWastePct });
        SelectedMaterial = null; NewQtyPerUnit = 1; NewWastePct = 0;
        Status = $"Danh sách có {BomLines.Count} NVL.";
    }

    /// <summary>Xoá dòng đang chọn khỏi lưới.</summary>
    [RelayCommand]
    private void RemoveBomLine()
    {
        if (SelectedBomLine is null) { Status = "Chọn một dòng để xoá."; return; }
        BomLines.Remove(SelectedBomLine);
    }

    /// <summary>Mở CỬA SỔ riêng để thêm NHIỀU NVL cùng lúc vào BOM đang chọn, lưu 1 lần.</summary>
    [RelayCommand]
    private async Task AddManyItemsAsync()
    {
        if (SelectedBom is null) { Status = "Chọn một phiên bản BOM trước."; return; }
        if (!await ConfirmIfBomLockedAsync("Thêm định mức")) { Status = "Đã huỷ thao tác."; return; }
        BomLines.Clear();
        SelectedMaterial = null; NewQtyPerUnit = 1; NewWastePct = 0;
        var form = new Views.Dialogs.BomItemsFormView { DataContext = this };
        await DialogService.ShowFormAsync(
            $"Thêm định mức — BOM v{SelectedBom.Version}", form, async () =>
            {
                if (BomLines.Count == 0) return "Chưa có NVL nào. Bấm 'Thêm dòng' để thêm.";
                // Luu tung dong (server upsert). Trung NVL da co trong BOM se ghi de dinh muc.
                foreach (var l in BomLines.ToList())
                    await _api.UpsertBomItemAsync(SelectedBom!.Id, new
                    {
                        materialId = l.Material!.Id, qtyPerUnit = l.QtyPerUnit, wastePct = l.WastePct, note = (string?)null,
                    });
                var n = BomLines.Count;
                BomLines.Clear();
                await LoadItemsAsync();
                Status = $"Đã thêm/cập nhật {n} dòng định mức.";
                return null;
            }, saveText: "Lưu tất cả", width: 900, height: 560, scrollable: false);
    }

    /// <summary>Xoa dong dinh muc dang chon (popup canh bao neu BOM khong con DRAFT).</summary>
    [RelayCommand]
    private async Task DeleteItemAsync()
    {
        if (SelectedBom is null || SelectedItem is null) { Status = "Chọn một dòng định mức trong bảng để xoá."; return; }
        var it = SelectedItem!;
        if (!await ConfirmIfBomLockedAsync("Xoá dòng định mức")) { Status = "Đã huỷ thao tác."; return; }
        var matName = Materials.FirstOrDefault(m => m.Id == it.MaterialId)?.Name ?? $"id={it.MaterialId}";
        var ok = await DialogService.ConfirmAsync(
            "Xoá dòng định mức",
            $"Xoá NVL {matName} khỏi BOM v{SelectedBom!.Version} " +
            $"(ĐM {it.QtyPerUnit:0.####}/đơn vị, hao hụt {it.WastePct:0.####}%)?",
            "Xoá dòng", danger: true);
        if (!ok) { Status = "Đã huỷ thao tác xoá."; return; }
        await RunAsync("Đang xoá dòng định mức...", async () =>
        {
            await _api.DeleteBomItemAsync(SelectedBom!.Id, it.MaterialId);
            SelectedItem = null;
            await LoadItemsAsync();
            Status = $"Đã xoá NVL {matName} khỏi định mức.";
        });
    }

    /// <summary>Xoa ca ban BOM dang chon: hoi impact -> chi DRAFT/REJECTED va chua co don dung.</summary>
    [RelayCommand]
    private async Task DeleteBomAsync()
    {
        if (SelectedBom is null) { Status = "Chọn một phiên bản BOM để xoá."; return; }
        var b = SelectedBom!;
        await RunAsync("Đang kiểm tra ảnh hưởng...", async () =>
        {
            var impact = await _api.GetBomImpactAsync(b.Id);
            if (impact is null) { Status = "Không tìm thấy BOM trên server."; return; }

            if (!impact.CanDelete)
            {
                await DialogService.InfoAsync(
                    "Không xoá được BOM này",
                    impact.OrderCount > 0
                        ? $"BOM v{impact.Version} đang được {impact.OrderCount} đơn sản xuất chốt dùng — không thể xoá."
                        : $"BOM v{impact.Version} đang ở trạng thái {impact.Status}.\n" +
                          "Chỉ xoá được bản DRAFT hoặc REJECTED. Bản ACTIVE cần bị thay thế bằng bản mới được duyệt.");
                return;
            }

            var ok = await DialogService.ConfirmAsync(
                "Xoá phiên bản BOM",
                $"Xoá BOM v{impact.Version} ({impact.Status}) gồm {impact.ItemCount} dòng định mức?\n" +
                "Lịch sử thay đổi và phiếu duyệt của bản này bị xoá theo.",
                "Xoá BOM", danger: true);
            if (!ok) { Status = "Đã huỷ thao tác xoá."; return; }
            await _api.DeleteBomAsync(b.Id);
            await LoadBomsCoreAsync();
            Status = $"Đã xoá BOM v{impact.Version}.";
        });
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (SelectedBom is null) return;
        await RunAsync("Đang gửi duyệt...", async () =>
        {
            await _api.SubmitBomAsync(SelectedBom!.Id);
            await LoadBomsCoreAsync();
            Status = "Đã gửi duyệt.";
        });
    }

    [RelayCommand]
    private async Task ApproveAsync()
    {
        if (SelectedBom is null) return;
        await RunAsync("Đang duyệt...", async () =>
        {
            await _api.ApproveBomAsync(SelectedBom!.Id, new { decidedBy = (long?)null, note = "Duyệt" });
            await LoadBomsCoreAsync();
            Status = "Đã duyệt -> ACTIVE.";
        });
    }

    [RelayCommand]
    private async Task RejectAsync()
    {
        if (SelectedBom is null) return;
        await RunAsync("Đang từ chối...", async () =>
        {
            await _api.RejectBomAsync(SelectedBom!.Id, new { decidedBy = (long?)null, note = "Từ chối" });
            await LoadBomsCoreAsync();
            Status = "Đã từ chối.";
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
