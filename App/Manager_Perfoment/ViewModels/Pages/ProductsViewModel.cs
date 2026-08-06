using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GM95.App.ManagerPerformance.Models;
using GM95.App.ManagerPerformance.Services;

namespace GM95.App.ManagerPerformance.ViewModels.Pages;

/// <summary>Danh muc ma hang thanh pham: danh sach + tao moi. Dung API that.</summary>
public sealed partial class ProductsViewModel : PageViewModel, IExportProvider
{
    /// <summary>Cac bang cua trang nay cho nut "Xuất Excel" chung (xuat dung du lieu dang hien thi).</summary>
    public IReadOnlyList<ExportTable> GetExportTables() => new[]
    {
        ExportTable.Create<Product>("Danh mục mã hàng", () => Products, rowDate: null,
            ("ID", p => p.Id),
            ("SKU", p => p.Sku),
            ("Tên", p => p.Name),
            ("ĐVT", p => p.UomName),
            ("Hoạt động", p => p.IsActive)),
    };

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
        var ps = await _api.GetProductsAsync(false);
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

    // Lenh nap bi bo qua vi dang ban -> chay lai ngay sau khi xong (chong lech du lieu).
    private bool _reloadPending;

    private async Task RunAsync(string busy, Func<Task> a)
    {
        // Dang ban ma nguoi dung doi bo loc / bam lam moi: ghi nho de tu nap lai sau,
        // KHONG nuot lenh (truoc day bang se lech so voi lua chon tren man hinh).
        if (IsBusy) { _reloadPending = true; return; }
        IsBusy = true; Status = busy;
        try
        {
            do
            {
                _reloadPending = false;
                await a();
            } while (_reloadPending);
        }
        catch (ApiException ex) { Status = $"Lỗi [{ex.Code}]: {ex.Message}"; }
        catch (Exception ex) { Status = $"Lỗi: {ex.Message}"; }
        finally { IsBusy = false; _reloadPending = false; }
    }
}
