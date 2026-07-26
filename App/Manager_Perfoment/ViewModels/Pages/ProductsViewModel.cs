using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DGroup.App.ManagerPerformance.Models;
using DGroup.App.ManagerPerformance.Services;

namespace DGroup.App.ManagerPerformance.ViewModels.Pages;

/// <summary>Danh muc ma hang thanh pham: danh sach + tao moi. Dung API that.</summary>
public sealed partial class ProductsViewModel : PageViewModel
{
    private readonly ApiClient _api;

    public ProductsViewModel(ApiClient api) => _api = api;

    public override string Title => "Mã hàng";
    public override string Subtitle => "Danh mục thành phẩm cần sản xuất";

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    // Danh muc ma hang + don vi tinh
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<Uom> Uoms { get; } = new();

    // Form tao ma hang
    [ObservableProperty] private string _newSku = "";
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private Uom? _selectedUom;

    // Hang sua tren dau bang: bam 1 dong -> tu nap du lieu vao cac field.
    [ObservableProperty] private Product? _selectedListProduct;
    [ObservableProperty] private string _editSku = "";
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private Uom? _editUom;
    [ObservableProperty] private bool _editIsActive = true;

    partial void OnSelectedListProductChanged(Product? value)
    {
        if (value is null) return;
        EditSku = value.Sku;
        EditName = value.Name;
        EditUom = Uoms.FirstOrDefault(u => u.Id == value.UomId);
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

    // Nap du lieu KHONG boc RunAsync (de lenh ghi goi lai duoc — guard IsBusy chan goi long nhau).
    private async Task LoadCoreAsync()
    {
        var keepUom = SelectedUom?.Id;

        // Tai don vi tinh truoc de co lua chon cho form
        var us = await _api.GetUomsAsync();
        Uoms.Clear();
        foreach (var u in us) Uoms.Add(u);
        SelectedUom = Uoms.FirstOrDefault(u => u.Id == keepUom);

        var keepSel = SelectedListProduct?.Id;
        var (fy, fm) = FilterPeriod;
        var ps = await _api.GetProductsAsync(false, fy, fm);
        Products.Clear();
        foreach (var p in ps) Products.Add(p);
        SelectedListProduct = Products.FirstOrDefault(p => p.Id == keepSel);

        Status = $"{Products.Count} mã hàng.";
    }

    /// <summary>Mo CUA SO rieng de tao ma hang. Dien xong bam Luu -> goi API, dong dialog, nap lai.</summary>
    [RelayCommand]
    private async Task CreateAsync()
    {
        NewSku = ""; NewName = ""; SelectedUom = null;
        var form = new Views.Dialogs.ProductFormView { DataContext = this };
        await DialogService.ShowFormAsync("Tạo mã hàng", form, async () =>
        {
            if (string.IsNullOrWhiteSpace(NewSku)) return "Nhập SKU.";
            if (string.IsNullOrWhiteSpace(NewName)) return "Nhập tên mã hàng.";
            if (SelectedUom is null) return "Chọn đơn vị tính.";

            var sku = NewSku.Trim();
            var id = await _api.CreateProductAsync(new { sku, name = NewName.Trim(), uomId = SelectedUom!.Id });
            await LoadCoreAsync();
            Status = $"Đã tạo mã hàng id={id} ({sku}).";
            return null;
        }, saveText: "Tạo mã hàng", height: 320);
    }

    /// <summary>Luu thay doi cua ma hang dang chon (hang field tren dau bang).</summary>
    [RelayCommand]
    private async Task SaveProductAsync()
    {
        if (SelectedListProduct is null) { Status = "Chọn một dòng mã hàng trong bảng để sửa."; return; }
        if (string.IsNullOrWhiteSpace(EditName) || EditUom is null) { Status = "Tên và ĐVT không được rỗng."; return; }
        await RunAsync("Đang lưu mã hàng...", async () =>
        {
            var p = SelectedListProduct!;
            await _api.UpdateProductAsync(p.Id, new
            {
                name = EditName.Trim(),
                uomId = EditUom!.Id,
                isActive = EditIsActive,
            });
            await LoadCoreAsync();
            Status = $"Đã lưu mã hàng {p.Sku}.";
        });
    }

    /// <summary>Xoa ma hang: hoi impact -> popup canh bao; dang duoc dung -> de xuat ngung hoat dong.</summary>
    [RelayCommand]
    private async Task DeleteProductAsync()
    {
        if (SelectedListProduct is null) { Status = "Chọn một dòng mã hàng trong bảng để xoá."; return; }
        var p = SelectedListProduct!;
        await RunAsync("Đang kiểm tra ảnh hưởng...", async () =>
        {
            var impact = await _api.GetProductImpactAsync(p.Id);
            if (impact is null) { Status = "Không tìm thấy mã hàng trên server."; return; }

            if (impact.CanDelete)
            {
                var ok = await DialogService.ConfirmAsync(
                    "Xoá mã hàng",
                    $"Xoá vĩnh viễn mã hàng {p.Sku} — {p.Name}?\n" +
                    "Mã hàng này chưa có BOM, đơn sản xuất hay phiếu nhập TP nào.",
                    "Xoá", danger: true);
                if (!ok) { Status = "Đã huỷ thao tác xoá."; return; }
                await _api.DeleteProductAsync(p.Id);
                SelectedListProduct = null;
                await LoadCoreAsync();
                Status = $"Đã xoá mã hàng {p.Sku}.";
            }
            else
            {
                var ok = await DialogService.ConfirmAsync(
                    "Không thể xoá vĩnh viễn — mã hàng đang được sử dụng",
                    $"Mã hàng {p.Sku} — {p.Name} đang liên quan tới:\n" +
                    $"• {impact.BomCount} bản định mức BOM\n" +
                    $"• {impact.OrderCount} đơn sản xuất\n" +
                    $"• {impact.ReceiptCount} phiếu nhập thành phẩm\n\n" +
                    "Chuyển sang NGƯNG HOẠT ĐỘNG thay thế? (ẩn khỏi danh mục hoạt động, " +
                    "dữ liệu cũ giữ nguyên; bật lại bằng ô Hoạt động + Lưu)",
                    "Ngưng hoạt động", danger: true);
                if (!ok) { Status = "Đã huỷ thao tác."; return; }
                await _api.UpdateProductAsync(p.Id, new { name = p.Name, uomId = p.UomId, isActive = false });
                await LoadCoreAsync();
                Status = $"Đã ngưng hoạt động mã hàng {p.Sku}.";
            }
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
