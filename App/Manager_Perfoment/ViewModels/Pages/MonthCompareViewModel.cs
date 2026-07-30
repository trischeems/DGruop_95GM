using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GM95.App.ManagerPerformance.Models;
using GM95.App.ManagerPerformance.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace GM95.App.ManagerPerformance.ViewModels.Pages;

/// <summary>1 o tick chon thang trong che do "So sanh cac thang chon".</summary>
public sealed partial class MonthCheck : ObservableObject
{
    private readonly Action _onChange;
    public MonthCheck(int month, Action onChange) { Month = month; _onChange = onChange; }
    public int Month { get; }
    public string Label => "T" + Month;
    [ObservableProperty] private bool _isSelected;
    partial void OnIsSelectedChanged(bool value) => _onChange();
}

/// <summary>
/// Tab So sanh thang: bieu do duong/cot/tron so sanh du lieu giua cac thang.
/// - Mac dinh: ca 12 thang cua nam hien tai (bieu do duong).
/// - Che do "So sanh cac thang chon": chi hien cac thang duoc tick.
/// - Moi khoi du lieu co the doi kieu bieu do (Duong/Cot); khoi ty trong dung bieu do tron.
/// </summary>
public sealed partial class MonthCompareViewModel : PageViewModel, IExportProvider
{
    /// <summary>Cac bang cua trang nay cho nut "Xuất Excel" chung (xuat dung du lieu dang hien thi).</summary>
    public IReadOnlyList<ExportTable> GetExportTables() => new[]
    {
        ExportTable.Create<MonthlyStats>("Số liệu 12 tháng", () => Visible(), rowDate: null,
            ("Tháng", s => s.Month),
            ("Số đơn", s => s.OrderCount),
            ("SL đặt", s => s.OrderQty),
            ("NVL nhập", s => s.StockInQty),
            ("GT nhập", s => s.StockInValue),
            ("NVL xuất", s => s.StockOutQty),
            ("GT xuất", s => s.StockOutValue),
            ("Số phiếu TP", s => s.FgReceiptCount),
            ("SL TP", s => s.FgQty),
            ("Hao hụt", s => s.LossVariance),
            ("Cảnh báo", s => s.AlertCount)),
    };

    private readonly ApiClient _api;

    public MonthCompareViewModel(ApiClient api)
    {
        _api = api;
        for (var m = 1; m <= 12; m++) MonthChecks.Add(new MonthCheck(m, OnMonthTicked));
        // Mac dinh tick thang hien tai + thang truoc (de che do so sanh co san 2 thang).
        var now = DateTime.Now.Month;
        MonthChecks[now - 1].IsSelected = true;
        if (now > 1) MonthChecks[now - 2].IsSelected = true;
    }

    public override string Title => "So sánh tháng";
    public override string Subtitle => "Biểu đồ so sánh dữ liệu giữa các tháng trong năm";

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    // ===== Bo chon nam + che do =====
    public int[] YearOptions { get; } =
        { DateTime.Now.Year - 3, DateTime.Now.Year - 2, DateTime.Now.Year - 1, DateTime.Now.Year, DateTime.Now.Year + 1 };
    [ObservableProperty] private int _year = DateTime.Now.Year;
    partial void OnYearChanged(int value) => _ = LoadAsync();

    public string[] ModeOptions { get; } = { "Cả năm (12 tháng)", "So sánh các tháng chọn" };
    [ObservableProperty] private string _mode = "Cả năm (12 tháng)";
    partial void OnModeChanged(string value) { OnPropertyChanged(nameof(IsCompareMode)); RebuildAll(); }
    public bool IsCompareMode => Mode == "So sánh các tháng chọn";

    public ObservableCollection<MonthCheck> MonthChecks { get; } = new();
    private void OnMonthTicked() { if (IsCompareMode) RebuildAll(); }

    // ===== Kieu bieu do tung khoi =====
    public string[] ChartTypeOptions { get; } = { "Đường", "Cột" };
    [ObservableProperty] private string _productionChartType = "Đường";
    partial void OnProductionChartTypeChanged(string value) => RebuildAll();
    [ObservableProperty] private string _orderChartType = "Đường";
    partial void OnOrderChartTypeChanged(string value) => RebuildAll();
    [ObservableProperty] private string _stockChartType = "Đường";
    partial void OnStockChartTypeChanged(string value) => RebuildAll();
    [ObservableProperty] private string _valueChartType = "Cột";
    partial void OnValueChartTypeChanged(string value) => RebuildAll();
    [ObservableProperty] private string _lossChartType = "Cột";
    partial void OnLossChartTypeChanged(string value) => RebuildAll();
    [ObservableProperty] private string _alertChartType = "Cột";
    partial void OnAlertChartTypeChanged(string value) => RebuildAll();

    // ===== Bieu do tron: chon chi so =====
    public string[] PieMetricOptions { get; } =
    {
        "TP nhập kho (SL)", "SL đặt theo đơn", "Số đơn tạo", "NVL nhập (SL)", "NVL xuất (SL)",
        "Giá trị nhập (VND)", "Giá trị xuất (VND)", "Hao hụt NVL", "Cảnh báo",
    };
    [ObservableProperty] private string _pieMetric = "TP nhập kho (SL)";
    partial void OnPieMetricChanged(string value) => RebuildAll();

    // ===== Series cac khoi =====
    [ObservableProperty] private ISeries[] _productionSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _orderSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _stockSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _valueSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _lossSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _alertSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _pieSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _xAxes = { new Axis() };
    [ObservableProperty] private Axis[] _qtyYAxes = { new Axis { Labeler = v => v.ToString("N0") } };
    [ObservableProperty] private Axis[] _moneyYAxes = { new Axis { Labeler = v => v.ToString("N0") + " đ" } };

    private List<MonthlyStats> _stats = new();

    public override Task OnActivatedAsync() => LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; Status = "Đang tải số liệu năm " + Year + "...";
        try
        {
            _stats = await _api.GetMonthlyStatsAsync(Year);
            RebuildAll();
            var active = _stats.Count(s => s.OrderCount > 0 || s.StockInQty > 0 || s.FgQty > 0 || s.AlertCount > 0);
            Status = $"Năm {Year}: {active}/12 tháng có dữ liệu.";
        }
        catch (ApiException ex) { Status = $"Lỗi [{ex.Code}]: {ex.Message}"; }
        catch (Exception ex) { Status = $"Lỗi: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    /// <summary>Cac thang dang hien: ca 12 hoac chi cac thang duoc tick.</summary>
    private List<MonthlyStats> Visible()
    {
        if (_stats.Count == 0) return new List<MonthlyStats>();
        if (!IsCompareMode) return _stats;
        var picked = MonthChecks.Where(c => c.IsSelected).Select(c => c.Month).ToHashSet();
        return _stats.Where(s => picked.Contains(s.Month)).ToList();
    }

    private static readonly SKColor Blue = new(0x25, 0x63, 0xEB);
    private static readonly SKColor Green = new(0x05, 0x96, 0x69);
    private static readonly SKColor Red = new(0xDC, 0x26, 0x26);
    private static readonly SKColor Orange = new(0xD9, 0x77, 0x06);
    private static readonly SKColor Gray = new(0x6B, 0x72, 0x80);
    private static readonly SKColor[] PieColors =
    {
        new(0x25,0x63,0xEB), new(0x05,0x96,0x69), new(0xDC,0x26,0x26), new(0xD9,0x77,0x06),
        new(0x7C,0x3A,0xED), new(0x0E,0xA5,0xE9), new(0xDB,0x27,0x77), new(0x65,0xA3,0x0D),
        new(0x0D,0x94,0x88), new(0xEA,0x58,0x0C), new(0x64,0x74,0x8B), new(0xCA,0x8A,0x04),
    };

    /// <summary>Dung 1 series theo kieu bieu do dang chon (Duong/Cot).</summary>
    private static ISeries Build(string type, string name, IEnumerable<double> values, SKColor color) =>
        type == "Cột"
            ? new ColumnSeries<double>
            {
                Name = name, Values = values.ToArray(),
                Fill = new SolidColorPaint(color), MaxBarWidth = 46,
            }
            : new LineSeries<double>
            {
                Name = name, Values = values.ToArray(),
                Stroke = new SolidColorPaint(color) { StrokeThickness = 3 },
                GeometryStroke = new SolidColorPaint(color) { StrokeThickness = 3 },
                Fill = null, GeometrySize = 9,
                // Duong THANG noi cac diem (linear), khong lam cong kieu Bezier.
                LineSmoothness = 0,
            };

    private double[] M(Func<MonthlyStats, decimal> f) => Visible().Select(s => (double)f(s)).ToArray();

    private double PieValue(MonthlyStats s) => PieMetric switch
    {
        "SL đặt theo đơn" => (double)s.OrderQty,
        "Số đơn tạo" => s.OrderCount,
        "NVL nhập (SL)" => (double)s.StockInQty,
        "NVL xuất (SL)" => (double)s.StockOutQty,
        "Giá trị nhập (VND)" => (double)s.StockInValue,
        "Giá trị xuất (VND)" => (double)s.StockOutValue,
        "Hao hụt NVL" => (double)s.LossVariance,
        "Cảnh báo" => s.AlertCount,
        _ => (double)s.FgQty,
    };

    /// <summary>Dung lai toan bo bieu do theo du lieu + che do + kieu bieu do hien tai.</summary>
    private void RebuildAll()
    {
        var vis = Visible();
        XAxes = new[] { new Axis { Labels = vis.Select(s => "Tháng " + s.Month).ToArray() } };

        ProductionSeries = new[]
        {
            Build(ProductionChartType, "SL đặt theo đơn", M(s => s.OrderQty), Gray),
            Build(ProductionChartType, "TP nhập kho", M(s => s.FgQty), Blue),
        };
        OrderSeries = new[] { Build(OrderChartType, "Số đơn tạo", M(s => s.OrderCount), Blue) };
        StockSeries = new[]
        {
            Build(StockChartType, "NVL nhập", M(s => s.StockInQty), Green),
            Build(StockChartType, "NVL xuất", M(s => s.StockOutQty), Orange),
        };
        ValueSeries = new[]
        {
            Build(ValueChartType, "Giá trị nhập (VND)", M(s => s.StockInValue), Green),
            Build(ValueChartType, "Giá trị xuất (VND)", M(s => s.StockOutValue), Orange),
        };
        LossSeries = new[] { Build(LossChartType, "Hao hụt (cấp phát − định mức)", M(s => s.LossVariance), Red) };
        AlertSeries = new[] { Build(AlertChartType, "Cảnh báo phát sinh", M(s => s.AlertCount), Orange) };

        PieSeries = vis
            .Where(s => PieValue(s) > 0)
            .Select(s => (ISeries)new PieSeries<double>
            {
                Name = "Tháng " + s.Month,
                Values = new[] { PieValue(s) },
                Fill = new SolidColorPaint(PieColors[(s.Month - 1) % PieColors.Length]),
            })
            .ToArray();
    }
}
