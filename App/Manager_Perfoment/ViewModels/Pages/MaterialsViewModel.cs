using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DGroup.App.ManagerPerformance.Models;
using DGroup.App.ManagerPerformance.Services;

namespace DGroup.App.ManagerPerformance.ViewModels.Pages;

/// <summary>Kho nguyen vat lieu: danh sach NVL + tao moi + nhap kho + xem ton. Dung API that.</summary>
public sealed partial class MaterialsViewModel : PageViewModel
{
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

    // Form tao NVL (chon ĐVT tu dropdown thay vi go id)
    [ObservableProperty] private string _newSku = "";
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private Uom? _selectedUom;
    [ObservableProperty] private decimal _newReorderLevel = 0;
    [ObservableProperty] private decimal _newReorderQuantity = 0;
    [ObservableProperty] private decimal _newStandardCost = 0;

    // Form nhap kho (chon kho & NVL tu dropdown)
    [ObservableProperty] private Warehouse? _receiveWarehouse;
    [ObservableProperty] private Material? _receiveMaterial;
    [ObservableProperty] private decimal _receiveQuantity = 0;
    [ObservableProperty] private decimal _receiveUnitCost = 0;

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
        ReceiveMaterial = Materials.FirstOrDefault(m => m.Id == keepMat);
        SelectedListMaterial = Materials.FirstOrDefault(m => m.Id == keepListSel);

        var stock = await _api.GetStockAsync(false, fy, fm);
        Stock.Clear(); foreach (var s in stock) Stock.Add(s);

        Status = $"{Materials.Count} NVL · {Stock.Count} dòng tồn kho.";
    }

    [RelayCommand]
    private async Task CreateMaterialAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSku) || string.IsNullOrWhiteSpace(NewName) || SelectedUom is null)
        {
            Status = "Nhập SKU, Tên và chọn ĐVT trước khi tạo.";
            return;
        }
        await RunAsync("Đang tạo NVL...", async () =>
        {
            var sku = NewSku.Trim();
            var id = await _api.CreateMaterialAsync(new
            {
                sku, name = NewName.Trim(), uomId = SelectedUom!.Id,
                categoryId = (long?)null, reorderLevel = NewReorderLevel,
                reorderQuantity = NewReorderQuantity, standardCost = NewStandardCost,
            });
            NewSku = ""; NewName = "";
            await LoadCoreAsync();
            Status = $"Đã tạo NVL id={id} ({sku}).";
        });
    }

    [RelayCommand]
    private async Task ReceiveStockAsync()
    {
        if (ReceiveMaterial is null || ReceiveWarehouse is null || ReceiveQuantity <= 0)
        {
            Status = "Chọn kho, NVL và số lượng nhập > 0.";
            return;
        }
        await RunAsync("Đang nhập kho...", async () =>
        {
            var r = await _api.ReceiveStockAsync(new
            {
                warehouseId = ReceiveWarehouse!.Id, materialId = ReceiveMaterial!.Id,
                quantity = ReceiveQuantity, unitCost = ReceiveUnitCost, note = "Nhập từ app",
            });
            ReceiveQuantity = 0;
            await LoadCoreAsync();
            Status = $"Đã nhập kho. Tồn sau nhập NVL {r.MaterialId} = {r.BalanceAfter}.";
        });
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
