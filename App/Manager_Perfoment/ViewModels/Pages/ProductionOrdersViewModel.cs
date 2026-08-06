using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GM95.App.ManagerPerformance.Models;
using GM95.App.ManagerPerformance.Services;

namespace GM95.App.ManagerPerformance.ViewModels.Pages;

/// <summary>Man hinh Don hang san xuat: tao don, xac nhan (giu cho NVL), theo doi thieu hut.</summary>
public sealed partial class ProductionOrdersViewModel : PageViewModel, IExportProvider
{
    /// <summary>Cac bang cua trang nay cho nut "Xuất Excel" chung (xuat dung du lieu dang hien thi).</summary>
    public IReadOnlyList<ExportTable> GetExportTables() => new[]
    {
        ExportTable.Create<ProductionOrder>("Danh sách đơn", () => Orders, rowDate: null,
            ("ID", o => o.Id),
            ("Số đơn", o => o.OrderNo),
            ("Mã hàng", o => o.ProductId),
            ("SKU", o => o.ProductSku),
            ("Tên mã hàng", o => o.ProductName),
            ("SL", o => o.Quantity),
            ("ĐVT", o => o.ProductUomName),
            ("Trạng thái", o => Converters.CodeToVietnameseConverter.Translate(o.Status)),
            ("Hạn giao", o => o.DueDate),
            ("Ngày xác nhận", o => o.ConfirmedAt)),
        ExportTable.Create<Reservation>("Giữ chỗ NVL", () => Reservations, rowDate: null,
            ("NVL", r => r.MaterialId),
            ("Mã NVL", r => r.MaterialSku),
            ("Tên NVL", r => r.MaterialName),
            ("Kho", r => r.WarehouseName),
            ("Giữ chỗ", r => r.QtyReserved),
            ("ĐVT", r => r.MaterialUomName),
            ("Trạng thái", r => Converters.CodeToVietnameseConverter.Translate(r.Status))),
        ExportTable.Create<OrderMaterialRequirement>("Nhu cầu / thiếu hụt", () => Requirements, rowDate: null,
            ("NVL", q => q.MaterialId),
            ("Mã NVL", q => q.MaterialSku),
            ("Tên NVL", q => q.MaterialName),
            ("Cần", q => q.RequiredQty),
            ("Khả dụng", q => q.TotalAvailable),
            ("Thiếu", q => q.ShortageQty),
            ("Đề xuất mua", q => q.SuggestedPurchaseQty),
            ("ĐVT", q => q.MaterialUomName)),
    };

    private readonly ApiClient _api;

    public ProductionOrdersViewModel(ApiClient api) => _api = api;

    public override string Title => "Đơn hàng sản xuất";
    public override string Subtitle => "Tạo đơn, xác nhận (giữ chỗ NVL), theo dõi thiếu hụt";

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<Product> Products { get; } = new();
    /// <summary>Ma hang sau khi loc theo o tim o cot trai dialog tao don.</summary>
    public ObservableCollection<Product> FilteredProducts { get; } = new();
    /// <summary>Cac mau quy trinh dang dung — chon khi tao don (V006).</summary>
    public ObservableCollection<Routing> Routings { get; } = new();
    public ObservableCollection<ProductionOrder> Orders { get; } = new();
    public ObservableCollection<Reservation> Reservations { get; } = new();
    public ObservableCollection<OrderMaterialRequirement> Requirements { get; } = new();

    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private ProductionOrder? _selectedOrder;

    /// <summary>O tim ma hang o cot trai dialog tao don.</summary>
    [ObservableProperty] private string _productListFilter = "";
    partial void OnProductListFilterChanged(string value) => ApplyProductFilter();

    /// <summary>Mau quy trinh chon khi tao don (null = dung mau mac dinh cua he thong).</summary>
    [ObservableProperty] private Routing? _selectedRouting;

    [ObservableProperty] private string _newOrderNo = "";
    [ObservableProperty] private decimal _newQuantity = 1;

    // Hang sua tren dau bang: bam 1 dong don -> tu nap so lieu.
    [ObservableProperty] private string _editOrderNo = "";
    [ObservableProperty] private decimal _editQuantity = 1;


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
    // selectOrderId: don muon chon lai sau khi nap (mac dinh giu don dang chon theo Id).
    private async Task LoadCoreAsync(long? selectOrderId = null)
    {
        var keepProduct = SelectedProduct?.Id;
        var keepOrder = selectOrderId ?? SelectedOrder?.Id;

        var (pfy, pfm) = FilterPeriod;
        var ps = await _api.GetProductsAsync(false, pfy, pfm);
        Products.Clear();
        foreach (var p in ps) Products.Add(p);
        ApplyProductFilter();
        SelectedProduct = Products.FirstOrDefault(p => p.Id == keepProduct);

        // Mau quy trinh de chon khi tao don.
        var keepRouting = SelectedRouting?.Id;
        var rts = await _api.GetRoutingsAsync(activeOnly: true);
        Routings.Clear();
        foreach (var r in rts) Routings.Add(r);
        SelectedRouting = Routings.FirstOrDefault(r => r.Id == keepRouting)
                          ?? Routings.FirstOrDefault(r => r.IsDefault);

        var (fy, fm) = FilterPeriod;
        var os = await _api.GetOrdersAsync(null, fy, fm);
        Orders.Clear();
        foreach (var o in os) Orders.Add(o);
        // Chon lai dong cu; chan hook de khoi kich hoat LoadDetailAsync 2 lan.
        _suppressSelectionHook = true;
        SelectedOrder = Orders.FirstOrDefault(o => o.Id == keepOrder);
        _suppressSelectionHook = false;
        await LoadDetailAsync();

        Status = $"{Orders.Count} đơn.";
    }

    private void ApplyProductFilter()
    {
        var kw = (ProductListFilter ?? "").Trim();
        FilteredProducts.Clear();
        foreach (var p in Products)
            if (kw.Length == 0
                || (p.Name?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                || (p.Sku?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false))
                FilteredProducts.Add(p);
    }

    private bool _suppressSelectionHook;

    partial void OnSelectedOrderChanged(ProductionOrder? value)
    {
        // Nap du lieu don vao hang sua (ke ca khi chon lai sau reload).
        if (value is not null)
        {
            EditOrderNo = value.OrderNo;
            EditQuantity = value.Quantity;
        }
        if (_suppressSelectionHook) return;
        _ = LoadDetailAsync();
    }

    private async Task LoadDetailAsync()
    {
        Reservations.Clear();
        Requirements.Clear();
        if (SelectedOrder is null) return;

        var rs = await _api.GetReservationsAsync(SelectedOrder.Id);
        foreach (var r in rs) Reservations.Add(r);

        var rq = await _api.GetOrderRequirementsAsync(SelectedOrder.Id);
        foreach (var q in rq) Requirements.Add(q);
    }

    // ===== Tao don NHIEU MAT HANG: luoi cac dong (ma hang + SL + quy trinh), luu 1 lan =====
    /// <summary>Cac mat hang dang cho trong don sap tao.</summary>
    public ObservableCollection<OrderLineInput> NewOrderLines { get; } = new();
    [ObservableProperty] private OrderLineInput? _selectedNewLine;
    /// <summary>Loi/goi y trong dialog tao don (Status cua trang bi dialog che).</summary>
    [ObservableProperty] private string _orderFormError = "";

    /// <summary>Them mat hang dang chon vao luoi don. Chan trung ma hang.</summary>
    [RelayCommand]
    private void AddOrderLine()
    {
        OrderFormError = "";
        if (SelectedProduct is null) { OrderFormError = "Chọn mã hàng ở danh sách bên trái trước."; return; }
        if (NewQuantity <= 0) { OrderFormError = "Số lượng phải lớn hơn 0."; return; }
        if (NewOrderLines.Any(l => l.Product?.Id == SelectedProduct.Id))
        {
            OrderFormError = $"Mã hàng {SelectedProduct.Sku} đã có trong đơn — sửa số lượng ở dòng cũ.";
            return;
        }
        NewOrderLines.Add(new OrderLineInput
        {
            Product = SelectedProduct,
            Quantity = NewQuantity,
            Routing = SelectedRouting,
        });
        SelectedProduct = null; NewQuantity = 1;
    }

    /// <summary>Bo mat hang dang chon khoi luoi don.</summary>
    [RelayCommand]
    private void RemoveOrderLine()
    {
        if (SelectedNewLine is null) { OrderFormError = "Chọn một dòng để bỏ."; return; }
        NewOrderLines.Remove(SelectedNewLine);
        OrderFormError = "";
    }

    /// <summary>Mo CUA SO rieng de tao don san xuat NHIEU MAT HANG. Luu 1 lan cho ca don.</summary>
    [RelayCommand]
    private async Task CreateAsync()
    {
        NewOrderNo = ""; SelectedProduct = null; NewQuantity = 1;
        ProductListFilter = "";
        NewOrderLines.Clear();
        OrderFormError = "";
        SelectedRouting ??= Routings.FirstOrDefault(r => r.IsDefault) ?? Routings.FirstOrDefault();
        var form = new Views.Dialogs.OrderFormView { DataContext = this };
        await DialogService.ShowFormAsync("Tạo đơn sản xuất", form, async () =>
        {
            if (string.IsNullOrWhiteSpace(NewOrderNo)) return "Nhập số đơn.";
            // Chua bam "+ Thêm mặt hàng" nhung da chon san 1 ma hang -> tu them cho tien.
            if (NewOrderLines.Count == 0 && SelectedProduct is not null && NewQuantity > 0) AddOrderLine();
            if (NewOrderLines.Count == 0) return "Đơn chưa có mặt hàng nào. Chọn mã hàng rồi bấm '+ Thêm mặt hàng'.";

            var orderNo = NewOrderNo.Trim();
            var id = await _api.CreateOrderAsync(new
            {
                orderNo,
                dueDate = (string?)null,
                note = (string?)null,
                items = NewOrderLines.Select(l => new
                {
                    productId = l.Product!.Id,
                    bomId = (long?)null,
                    routingId = l.Routing?.Id,
                    quantity = l.Quantity,
                    note = (string?)null,
                }).ToArray(),
            });
            var n = NewOrderLines.Count;
            NewOrderLines.Clear();
            await LoadCoreAsync(id);
            Status = $"Đã tạo đơn {orderNo} (DRAFT) với {n} mặt hàng.";
            return null;
        }, saveText: "Tạo đơn", width: 980, height: 560, scrollable: false);
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (SelectedOrder is null) return;
        await RunAsync("Đang xác nhận + giữ chỗ...", async () =>
        {
            await _api.ConfirmOrderAsync(SelectedOrder!.Id);
            await LoadCoreAsync();
            Status = "Đã xác nhận, đã giữ chỗ NVL.";
        });
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (SelectedOrder is null) return;
        await RunAsync("Đang huỷ...", async () =>
        {
            await _api.CancelOrderAsync(SelectedOrder!.Id);
            await LoadCoreAsync();
            Status = "Đã huỷ đơn, giải phóng giữ chỗ.";
        });
    }

    /// <summary>Luu so luong moi cho don dang chon. Chi don DRAFT; don khac popup giai thich.</summary>
    [RelayCommand]
    private async Task SaveOrderAsync()
    {
        if (SelectedOrder is null) { Status = "Chọn một đơn trong bảng để sửa."; return; }
        if (EditQuantity <= 0) { Status = "Số lượng phải > 0."; return; }
        var o = SelectedOrder!;
        if (o.Status != "DRAFT")
        {
            await DialogService.InfoAsync(
                "Không sửa được đơn này",
                $"Đơn {o.OrderNo} đang ở trạng thái {o.Status}.\n" +
                "Chỉ đơn DRAFT (nháp) mới sửa được số lượng — đơn đã xác nhận đã giữ chỗ NVL theo số cũ.\n" +
                "Muốn sửa: bấm Huỷ đơn (nhả giữ chỗ) rồi tạo đơn mới với số đúng.");
            return;
        }
        // Sua so luong lam thay doi nhu cau NVL / thieu hut cua don -> canh bao truoc.
        var ok = await DialogService.ConfirmAsync(
            "Xác nhận sửa đơn",
            $"Đổi số lượng đơn {o.OrderNo}: {o.Quantity:0.####} → {EditQuantity:0.####}.\n" +
            "Nhu cầu NVL, thiếu hụt và đề xuất mua của đơn sẽ tính lại theo số mới.",
            "Tiếp tục sửa");
        if (!ok) { Status = "Đã huỷ thao tác sửa."; return; }
        await RunAsync("Đang lưu đơn...", async () =>
        {
            await _api.UpdateOrderAsync(o.Id, new { quantity = EditQuantity, dueDate = (string?)null, note = (string?)null });
            await LoadCoreAsync();
            Status = $"Đã lưu đơn {o.OrderNo} (SL mới {EditQuantity:0.####}).";
        });
    }

    /// <summary>Xoa don: hoi impact -> popup liet ke du lieu bi xoa theo / ly do bi chan.</summary>
    [RelayCommand]
    private async Task DeleteOrderAsync()
    {
        if (SelectedOrder is null) { Status = "Chọn một đơn trong bảng để xoá."; return; }
        var o = SelectedOrder!;
        await RunAsync("Đang kiểm tra ảnh hưởng...", async () =>
        {
            var impact = await _api.GetOrderImpactAsync(o.Id);
            if (impact is null) { Status = "Không tìm thấy đơn trên server."; return; }

            if (!impact.CanDelete)
            {
                await DialogService.InfoAsync(
                    "Không xoá được đơn này",
                    impact.IssueCount > 0 || impact.ReceiptCount > 0 || impact.LossReportCount > 0
                        ? $"Đơn {o.OrderNo} đã phát sinh chứng từ:\n" +
                          $"• {impact.IssueCount} phiếu xuất NVL · {impact.ReceiptCount} phiếu nhập TP · {impact.LossReportCount} dòng hao hụt\n" +
                          "Chứng từ đã ghi sổ không xoá kèm đơn được — chỉ có thể Huỷ đơn."
                        : $"Đơn {o.OrderNo} đang ở trạng thái {impact.Status}.\n" +
                          "Chỉ xoá được đơn DRAFT hoặc CANCELLED. Hãy bấm Huỷ đơn trước.");
                return;
            }

            var ok = await DialogService.ConfirmAsync(
                "Xoá đơn sản xuất",
                $"Xoá vĩnh viễn đơn {o.OrderNo} ({impact.Status})?\n" +
                "Dữ liệu bị xoá kèm theo:\n" +
                $"• {impact.PlanCount} kế hoạch sản xuất\n" +
                $"• {impact.StepCount} công đoạn (Cắt/May/QC/Nhập TP)\n" +
                $"• {impact.ReservationCount} phiếu giữ chỗ NVL (tồn kho được nhả lại nếu còn giữ)",
                "Xoá đơn", danger: true);
            if (!ok) { Status = "Đã huỷ thao tác xoá."; return; }
            await _api.DeleteOrderAsync(o.Id);
            _suppressSelectionHook = true;
            SelectedOrder = null;
            _suppressSelectionHook = false;
            await LoadCoreAsync();
            Status = $"Đã xoá đơn {o.OrderNo}.";
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
