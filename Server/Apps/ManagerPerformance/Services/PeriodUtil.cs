namespace GM95.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Doi khoang ngay (nguoi dung chon theo gio dia phuong server) thanh 2 moc UTC
/// de so sanh truc tiep voi cot timestamptz: col &gt;= from AND col &lt; toExclusive.
/// Dang range nay dung duoc index (khac EXTRACT(...) phai full scan).
/// </summary>
internal static class PeriodUtil
{
    /// <summary>Dau ngay 'from' theo gio dia phuong -> UTC. Null = khong chan duoi (-infinity).</summary>
    public static DateTime FromUtc(DateTime? from) => from.HasValue
        ? DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Local).ToUniversalTime()
        : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

    /// <summary>Dau ngay KE TIEP 'to' (exclusive) theo gio dia phuong -> UTC. Null = khong chan tren (+infinity).</summary>
    public static DateTime ToExclusiveUtc(DateTime? to) => to.HasValue
        ? DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime()
        : DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
}
