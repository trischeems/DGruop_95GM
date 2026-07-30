using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GM95.App.ManagerPerformance.Services;

namespace GM95.App.ManagerPerformance.ViewModels;

/// <summary>1 bang trong hop thoai xuat Excel: tich chon xuat hay bo.</summary>
public sealed partial class ExportTableChoice : ObservableObject
{
    [ObservableProperty] private bool _isSelected = true;
    public ExportTable Table { get; }
    public string Name => Table.Name;
    /// <summary>true = bang co cot ngay -> loc theo thoi gian ap dung duoc.</summary>
    public bool HasDate => Table.RowDate is not null;
    public string DateHint => HasDate ? "" : "(không có cột ngày — luôn xuất toàn bộ)";

    public ExportTableChoice(ExportTable table) => Table = table;
}

/// <summary>
/// Tuy chon xuat Excel dung chung cho MOI trang: chon bang nao, pham vi thoi gian
/// (tat ca / theo 1 ngay / tu-den), va che do sheet (moi bang 1 sheet / gop 1 sheet).
/// </summary>
public sealed partial class ExportOptionsViewModel : ObservableObject
{
    public ObservableCollection<ExportTableChoice> Tables { get; }

    // ----- Pham vi thoi gian -----
    public string[] RangeModes { get; } = { "Xuất tất cả", "Theo 1 ngày", "Từ ngày – đến ngày" };
    [ObservableProperty] private string _rangeMode = "Xuất tất cả";
    public bool IsDayMode => RangeMode == "Theo 1 ngày";
    public bool IsRangeMode => RangeMode == "Từ ngày – đến ngày";
    partial void OnRangeModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsDayMode));
        OnPropertyChanged(nameof(IsRangeMode));
    }

    [ObservableProperty] private DateTime? _day = DateTime.Today;
    [ObservableProperty] private DateTime? _fromDate = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime? _toDate = DateTime.Today;

    // ----- Che do sheet -----
    public string[] SheetModes { get; } = { "Mỗi bảng 1 sheet", "Gộp tất cả vào 1 sheet" };
    [ObservableProperty] private string _sheetMode = "Mỗi bảng 1 sheet";
    public bool MergeToOneSheet => SheetMode == "Gộp tất cả vào 1 sheet";

    public ExportOptionsViewModel(IEnumerable<ExportTable> tables) =>
        Tables = new ObservableCollection<ExportTableChoice>(tables.Select(t => new ExportTableChoice(t)));

    /// <summary>
    /// Khoang [from, toExclusive) theo lua chon; (null, null) = khong loc.
    /// Tra ve chuoi loi neu thieu ngay, nguoc lai null.
    /// </summary>
    public (DateTime? From, DateTime? ToExclusive, string? Error) ComputeRange()
    {
        if (IsDayMode)
        {
            if (Day is null) return (null, null, "Chọn ngày cần xuất.");
            var d = Day.Value.Date;
            return (d, d.AddDays(1), null);
        }
        if (IsRangeMode)
        {
            if (FromDate is null || ToDate is null) return (null, null, "Chọn đủ Từ ngày và Đến ngày.");
            var f = FromDate.Value.Date;
            var t = ToDate.Value.Date;
            if (t < f) return (null, null, "'Đến ngày' phải sau hoặc bằng 'Từ ngày'.");
            return (f, t.AddDays(1), null);
        }
        return (null, null, null);   // Xuat tat ca
    }
}
