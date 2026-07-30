using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GM95.App.ManagerPerformance.Models;
using GM95.App.ManagerPerformance.Services;

namespace GM95.App.ManagerPerformance.ViewModels.Pages;

/// <summary>
/// Trang "Sổ &amp; thống kê": xem MOI chi so dang bang trong 1 khoang thoi gian —
/// so NVL (ton dau/tong nhap/tong xuat/ton cuoi), nhap xuat chi tiet, san xuat theo ma hang.
/// Chon ky theo THANG hoac TU NGAY – DEN NGAY.
/// </summary>
public sealed partial class StatsViewModel : PageViewModel, IExportProvider
{
    /// <summary>Cac bang cua trang nay cho nut "Xuất Excel" chung (du lieu da theo ky dang chon tren trang).</summary>
    public IReadOnlyList<ExportTable> GetExportTables() => new[]
    {
        ExportTable.Create<MaterialLedgerRow>("Sổ NVL", () => Ledger, rowDate: null,
            ("SKU", r => r.Sku),
            ("Tên NVL", r => r.Name),
            ("ĐVT", r => r.UomName),
            ("Tồn đầu kỳ", r => r.OpeningQty),
            ("Tổng nhập", r => r.InQty),
            ("GT nhập (VND)", r => r.InValue),
            ("Tổng xuất", r => r.OutQty),
            ("GT xuất (VND)", r => r.OutValue),
            ("Tồn cuối kỳ", r => r.ClosingQty),
            ("Tồn hiện tại", r => r.CurrentOnHand),
            ("Khả dụng", r => r.CurrentAvailable),
            ("Hoạt động", r => r.IsActive)),
        ExportTable.Create<StockTransaction>("Nhập xuất chi tiết", () => Transactions, rowDate: t => t.CreatedAt,
            ("Mã NVL", t => t.MaterialSku),
            ("Tên NVL", t => t.MaterialName),
            ("Loại", t => Converters.CodeToVietnameseConverter.Translate(t.TxnType)),
            ("SL", t => t.Quantity),
            ("ĐVT", t => t.MaterialUomName),
            ("Đơn giá", t => t.UnitCost),
            ("Tồn sau", t => t.BalanceAfter),
            ("Kho", t => t.WarehouseName),
            ("Ngày", t => t.CreatedAt)),
        ExportTable.Create<ProductionSummaryRow>("Sản xuất theo mã hàng", () => Production, rowDate: null,
            ("SKU", p => p.Sku),
            ("Tên mã hàng", p => p.Name),
            ("ĐVT", p => p.UomName),
            ("Số đơn", p => p.OrderCount),
            ("SL đặt", p => p.OrderQty),
            ("SL lỗi (công đoạn)", p => p.DefectQty),
            ("TP nhập kho", p => p.FgQty),
            ("GT NVL xuất (VND)", p => p.IssueValue)),
    };

    private readonly ApiClient _api;

    public StatsViewModel(ApiClient api) => _api = api;

    public override string Title => "Sổ & thống kê";
    public override string Subtitle => "Sổ NVL, nhập xuất, sản xuất — theo tháng hoặc từ ngày đến ngày";

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    // ===== Chon ky: theo THANG hoac TU–DEN =====
    public string[] ModeOptions { get; } = { "Theo tháng", "Từ ngày – đến ngày" };
    [ObservableProperty] private string _filterMode = "Theo tháng";
    public bool IsMonthMode => FilterMode == "Theo tháng";
    public bool IsRangeMode => !IsMonthMode;
    partial void OnFilterModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsMonthMode));
        OnPropertyChanged(nameof(IsRangeMode));
        _ = LoadAsync();
    }

    public string[] FilterMonthOptions { get; } =
        { "Tất cả", "Cả năm", "T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9", "T10", "T11", "T12" };
    public int[] FilterYearOptions { get; } =
        { DateTime.Now.Year - 2, DateTime.Now.Year - 1, DateTime.Now.Year, DateTime.Now.Year + 1 };
    [ObservableProperty] private string _filterMonth = "T" + DateTime.Now.Month;
    [ObservableProperty] private int _filterYear = DateTime.Now.Year;
    partial void OnFilterMonthChanged(string value) { if (IsMonthMode) _ = LoadAsync(); }
    partial void OnFilterYearChanged(int value) { if (IsMonthMode) _ = LoadAsync(); }

    // Khoang tu–den (che do "Từ ngày – đến ngày"); bam "Xem" de nap.
    [ObservableProperty] private DateTime? _fromDate = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime? _toDate = DateTime.Today;

    /// <summary>Doi lua chon hien tai thanh khoang (from, to) gui len server (null = khong chan).</summary>
    private (DateTime? From, DateTime? To) PeriodRange
    {
        get
        {
            if (IsRangeMode) return (FromDate?.Date, ToDate?.Date);
            if (FilterMonth == "Tất cả") return (null, null);
            if (FilterMonth == "Cả năm")
                return (new DateTime(FilterYear, 1, 1), new DateTime(FilterYear, 12, 31));
            var m = int.Parse(FilterMonth.TrimStart('T'));
            var first = new DateTime(FilterYear, m, 1);
            return (first, first.AddMonths(1).AddDays(-1));
        }
    }

    // ===== Du lieu 3 bang =====
    public ObservableCollection<MaterialLedgerRow> Ledger { get; } = new();          // da loc theo o tim
    private readonly List<MaterialLedgerRow> _allLedger = new();
    public ObservableCollection<StockTransaction> Transactions { get; } = new();
    public ObservableCollection<ProductionSummaryRow> Production { get; } = new();

    // O tim SKU/ten trong bang so NVL.
    [ObservableProperty] private string _ledgerSearch = "";
    partial void OnLedgerSearchChanged(string value) => ApplyLedgerFilter();
    private void ApplyLedgerFilter()
    {
        var kw = (LedgerSearch ?? "").Trim();
        Ledger.Clear();
        foreach (var r in _allLedger)
            if (kw.Length == 0
                || r.Sku.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || r.Name.Contains(kw, StringComparison.OrdinalIgnoreCase))
                Ledger.Add(r);
    }

    // ===== Dai tong hop tren dau trang =====
    [ObservableProperty] private string _sumInText = "0";
    [ObservableProperty] private string _sumOutText = "0";
    [ObservableProperty] private string _sumInValueText = "0";
    [ObservableProperty] private string _sumOutValueText = "0";
    [ObservableProperty] private string _sumOrderText = "0";
    [ObservableProperty] private string _sumFgText = "0";

    public override Task OnActivatedAsync() => LoadAsync();

    [RelayCommand]
    private Task LoadAsync() => RunAsync("Đang tải số liệu...", LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        var (from, to) = PeriodRange;

        var ledger = await _api.GetMaterialLedgerAsync(from, to);
        _allLedger.Clear();
        _allLedger.AddRange(ledger);
        ApplyLedgerFilter();

        var txns = await _api.GetStockTransactionsAsync(null, 500, from, to);
        Transactions.Clear();
        foreach (var t in txns) Transactions.Add(t);

        var prod = await _api.GetProductionSummaryAsync(from, to);
        Production.Clear();
        foreach (var p in prod) Production.Add(p);

        // Tong hop nhanh tren dau trang (tinh tu so NVL de khop moi loai giao dich).
        SumInText = ledger.Sum(r => r.InQty).ToString("0.####");
        SumOutText = ledger.Sum(r => r.OutQty).ToString("0.####");
        SumInValueText = ledger.Sum(r => r.InValue).ToString("#,##0") + " đ";
        SumOutValueText = ledger.Sum(r => r.OutValue).ToString("#,##0") + " đ";
        SumOrderText = prod.Sum(p => p.OrderCount).ToString("#,##0");
        SumFgText = prod.Sum(p => p.FgQty).ToString("0.####");

        var label = from is null && to is null ? "toàn bộ thời gian"
            : $"{(from is null ? "…" : from.Value.ToString("dd/MM/yyyy"))} – {(to is null ? "…" : to.Value.ToString("dd/MM/yyyy"))}";
        Status = $"Kỳ {label}: {_allLedger.Count} NVL · {Transactions.Count} giao dịch · {Production.Count} mã hàng có phát sinh.";
    }

    // Doi bo loc trong luc dang tai: ghi nho va tu nap lai sau khi xong (khong nuot mat lua chon).
    private bool _reloadPending;

    private async Task RunAsync(string busy, Func<Task> action)
    {
        if (IsBusy) { _reloadPending = true; return; }
        IsBusy = true; Status = busy;
        try
        {
            do
            {
                _reloadPending = false;
                await action();
            } while (_reloadPending);   // nguoi dung vua doi bo loc -> nap lai theo lua chon moi nhat
        }
        catch (ApiException ex) { Status = $"Lỗi [{ex.Code}]: {ex.Message}"; }
        catch (Exception ex) { Status = $"Lỗi: {ex.Message}"; }
        finally { IsBusy = false; _reloadPending = false; }
    }
}
