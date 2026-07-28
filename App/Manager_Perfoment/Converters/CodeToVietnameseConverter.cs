using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace GM95.App.ManagerPerformance.Converters;

/// <summary>
/// Doi MA ky thuat (PENDING, RECEIPT, PCS...) sang CHU TIENG VIET de hien tren man hinh.
///
/// Vi sao khong doi thang trong DB: cac cot nay co CHECK constraint liet ke dung cac ma tren,
/// va nghiep vu (state machine) so sanh theo ma. Doi ma se hong rang buoc + logic.
/// => Giu ma o DB/API, CHI dich luc hien thi.
/// </summary>
public sealed class CodeToVietnameseConverter : IValueConverter
{
    // Gop chung 1 tu dien: cac ma khong trung nhau giua cac nhom.
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // ----- Trang thai chung (don hang / buoc SX / ke hoach / phieu) -----
        ["DRAFT"]            = "Nháp",
        ["CONFIRMED"]        = "Đã xác nhận",
        ["IN_PROGRESS"]      = "Đang làm",
        ["COMPLETED"]        = "Hoàn thành",
        ["CANCELLED"]        = "Đã huỷ",
        ["PENDING"]          = "Chờ làm",
        ["DONE"]             = "Hoàn thành",
        ["ON_HOLD"]          = "Tạm dừng",
        ["PLANNED"]          = "Đã lên kế hoạch",
        ["RELEASED"]         = "Đã phát lệnh",
        ["POSTED"]           = "Đã ghi sổ",

        // ----- BOM / duyet -----
        ["PENDING_APPROVAL"] = "Chờ duyệt",
        ["ACTIVE"]           = "Đang áp dụng",
        ["ARCHIVED"]         = "Lưu trữ",
        ["APPROVED"]         = "Đã duyệt",
        ["REJECTED"]         = "Từ chối",

        // ----- Giu cho NVL -----
        ["CONSUMED"]         = "Đã dùng",

        // ----- Giao dich kho -----
        ["RECEIPT"]          = "Nhập kho",
        ["ISSUE"]            = "Xuất kho",
        ["ADJUST"]           = "Điều chỉnh",
        ["RETURN"]           = "Trả lại",

        // ----- Canh bao -----
        ["OPEN"]             = "Chưa xử lý",
        ["ACKNOWLEDGED"]     = "Đã tiếp nhận",
        ["RESOLVED"]         = "Đã xử lý",
        ["INFO"]             = "Thông tin",
        ["WARN"]             = "Cảnh báo",
        ["CRITICAL"]         = "Nghiêm trọng",
        ["LOW_STOCK"]        = "Tồn thấp",
        ["ORDER_SHORTAGE"]   = "Đơn thiếu NVL",
        ["BOM_PENDING"]      = "BOM chờ duyệt",
        ["MATERIAL"]         = "Nguyên vật liệu",
        ["PRODUCTION_ORDER"] = "Đơn sản xuất",
        ["BOM"]              = "Định mức BOM",

        // ----- Lich su sua BOM -----
        ["ADD_ITEM"]         = "Thêm NVL",
        ["REMOVE_ITEM"]      = "Bỏ NVL",
        ["UPDATE_QTY"]       = "Sửa định mức",
        ["UPDATE_WASTE"]     = "Sửa hao hụt",
        ["STATUS_CHANGE"]    = "Đổi trạng thái",
    };

    /// <summary>Tra ve chu tieng Viet cua 1 ma; khong co trong tu dien thi giu nguyen ma.</summary>
    public static string Translate(string? code) =>
        string.IsNullOrWhiteSpace(code) ? "" :
        Map.TryGetValue(code.Trim(), out var vi) ? vi : code;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Translate(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
