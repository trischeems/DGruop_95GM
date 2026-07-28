using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GM95.App.ManagerPerformance.Models;
using GM95.App.ManagerPerformance.Services;

namespace GM95.App.ManagerPerformance.ViewModels.Pages;

/// <summary>Tong quan: cac the KPI + bang "Sap het hang" (NVL ton thap). Dung API that.</summary>
public sealed partial class DashboardViewModel : PageViewModel
{
    private readonly ApiClient _api;

    public DashboardViewModel(ApiClient api) => _api = api;

    public override string Title => "Tổng quan";
    public override string Subtitle => "Tình hình kho nguyên vật liệu hiện tại";

    // KPI
    [ObservableProperty] private int _materialCount;
    [ObservableProperty] private decimal _totalAvailable;
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private string _connectionState = "Đang kết nối...";

    public ObservableCollection<MaterialStock> LowStock { get; } = new();


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
    private async Task LoadAsync()
    {
        try
        {
            if (!await _api.HealthAsync())
            {
                ConnectionState = "⚠ Chưa kết nối server. Mở run_server.bat rồi bấm Làm mới.";
                return;
            }

            var (fy, fm) = FilterPeriod;
            var stock = await _api.GetStockAsync(false, fy, fm);
            var low = await _api.GetLowStockAsync(fy, fm);

            MaterialCount = stock.Count;
            TotalAvailable = stock.Sum(s => s.TotalAvailable);
            LowStockCount = low.Count;

            LowStock.Clear();
            foreach (var s in low) LowStock.Add(s);

            ConnectionState = "● Đã kết nối server";
        }
        catch (ApiException ex)
        {
            ConnectionState = $"⚠ Lỗi [{ex.Code}]: {ex.Message}";
        }
    }
}
