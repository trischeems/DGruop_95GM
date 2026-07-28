using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GM95.App.ManagerPerformance.Converters;

/// <summary>
/// Doi ma trang thai (chuoi) sang mau chu de de phan biet trong DataGrid.
/// Dung cho trang thai don/ke hoach/cong doan/BOM. Mau chon theo y nghia:
///   xam = cho/nhap, xanh duong = dang chay, xanh la = xong, do = huy/tu choi, cam = cho duyet/tam dung.
/// Khong khop -> mau chu mac dinh (xam dam).
/// </summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public static readonly StatusToBrushConverter Instance = new();

    // Cac mau dung chung (hex), du tuong phan tren nen sang.
    private static readonly IBrush Grey   = new SolidColorBrush(Color.Parse("#6B7280")); // PENDING/PLANNED/DRAFT
    private static readonly IBrush Blue   = new SolidColorBrush(Color.Parse("#2563EB")); // IN_PROGRESS/RELEASED/CONFIRMED
    private static readonly IBrush Green  = new SolidColorBrush(Color.Parse("#16A34A")); // DONE/COMPLETED/ACTIVE/APPROVED
    private static readonly IBrush Red    = new SolidColorBrush(Color.Parse("#DC2626")); // CANCELLED/REJECTED
    private static readonly IBrush Orange = new SolidColorBrush(Color.Parse("#D97706")); // ON_HOLD/PENDING_APPROVAL
    private static readonly IBrush Default = new SolidColorBrush(Color.Parse("#374151"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = (value as string)?.Trim().ToUpperInvariant();
        return s switch
        {
            "PENDING" or "PLANNED" or "DRAFT"                       => Grey,
            "IN_PROGRESS" or "RELEASED" or "CONFIRMED"              => Blue,
            "DONE" or "COMPLETED" or "ACTIVE" or "APPROVED"         => Green,
            "CANCELLED" or "REJECTED"                               => Red,
            "ON_HOLD" or "PENDING_APPROVAL"                        => Orange,
            "ARCHIVED"                                             => Grey,
            // Loai giao dich kho (lich su): nhap = xanh la, xuat = do, dieu chinh/tra = cam.
            "RECEIPT"                                             => Green,
            "ISSUE"                                               => Red,
            "ADJUST" or "RETURN"                                  => Orange,
            _                                                     => Default,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
