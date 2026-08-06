using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GM95.App.ManagerPerformance.Models;
using GM95.App.ManagerPerformance.Services;

namespace GM95.App.ManagerPerformance.ViewModels.Pages;

/// <summary>Canh bao he thong (ton thap, don thieu NVL). Dung API alerts that.</summary>
public sealed partial class AlertsViewModel : PageViewModel, IExportProvider
{
    /// <summary>Cac bang cua trang nay cho nut "Xuất Excel" chung (xuat dung du lieu dang hien thi).</summary>
    public IReadOnlyList<ExportTable> GetExportTables() => new[]
    {
        ExportTable.Create<Alert>("Cảnh báo đang mở", () => Alerts, rowDate: a => a.CreatedAt,
            ("ID", a => a.Id),
            ("Loại", a => Converters.CodeToVietnameseConverter.Translate(a.AlertType)),
            ("Mức", a => Converters.CodeToVietnameseConverter.Translate(a.Severity)),
            ("Đối tượng", a => Converters.CodeToVietnameseConverter.Translate(a.EntityType)),
            ("Nội dung", a => a.Message),
            ("Trạng thái", a => Converters.CodeToVietnameseConverter.Translate(a.Status)),
            ("Ngày tạo", a => a.CreatedAt)),
    };

    private readonly ApiClient _api;

    public AlertsViewModel(ApiClient api) => _api = api;

    public override string Title => "Cảnh báo";
    public override string Subtitle => "Tồn thấp & đơn hàng có nguy cơ thiếu NVL";

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<Alert> Alerts { get; } = new();
    [ObservableProperty] private Alert? _selectedAlert;


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
        var (fy, fm) = FilterPeriod;
        var al = await _api.GetAlertsAsync("OPEN", fy, fm);
        Alerts.Clear();
        foreach (var a in al) Alerts.Add(a);
        Status = Alerts.Count == 0 ? "Không có cảnh báo đang mở." : $"{Alerts.Count} cảnh báo đang mở.";
    }

    /// <summary>Quet ton thap & don thieu NVL -> sinh cac canh bao moi.</summary>
    [RelayCommand]
    private async Task GenerateAsync() =>
        await RunAsync("Đang quét & tạo cảnh báo...", async () =>
        {
            await _api.GenerateAlertsAsync();
            await LoadCoreAsync();
            Status = $"Đã quét. {Alerts.Count} cảnh báo đang mở.";
        });

    [RelayCommand]
    private async Task AcknowledgeAsync()
    {
        if (SelectedAlert is null) { Status = "Chọn một cảnh báo."; return; }
        await RunAsync("Đang ghi nhận...", async () =>
        {
            await _api.AcknowledgeAlertAsync(SelectedAlert!.Id);
            await LoadCoreAsync();
            Status = $"Đã ghi nhận. {Alerts.Count} cảnh báo đang mở.";
        });
    }

    [RelayCommand]
    private async Task ResolveAsync()
    {
        if (SelectedAlert is null) { Status = "Chọn một cảnh báo."; return; }
        await RunAsync("Đang xử lý...", async () =>
        {
            await _api.ResolveAlertAsync(SelectedAlert!.Id);
            await LoadCoreAsync();
            Status = $"Đã xử lý. {Alerts.Count} cảnh báo đang mở.";
        });
    }

    // Lenh nap bi bo qua vi dang ban -> chay lai ngay sau khi xong (chong lech du lieu).
    private bool _reloadPending;

    private async Task RunAsync(string busy, Func<Task> action)
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
                await action();
            } while (_reloadPending);
        }
        catch (ApiException ex) { Status = $"Lỗi [{ex.Code}]: {ex.Message}"; }
        catch (Exception ex) { Status = $"Lỗi: {ex.Message}"; }
        finally { IsBusy = false; _reloadPending = false; }
    }
}
