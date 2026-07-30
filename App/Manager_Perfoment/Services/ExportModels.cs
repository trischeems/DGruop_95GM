namespace GM95.App.ManagerPerformance.Services;

/// <summary>1 cot cua bang xuat Excel: tieu de + ham lay gia tri tu 1 dong (object da boxing).</summary>
public sealed class ExportColumn
{
    public string Header { get; }
    public Func<object, object?> Value { get; }

    public ExportColumn(string header, Func<object, object?> value)
    {
        Header = header;
        Value = value;
    }
}

/// <summary>
/// Mo ta 1 bang co the xuat Excel tren 1 trang: ten bang, cot, nguon dong (lay luc xuat),
/// va ham lay NGAY cua dong (null = bang khong co ngay -> khong loc theo thoi gian).
/// </summary>
public sealed class ExportTable
{
    public string Name { get; init; } = "";
    public IReadOnlyList<ExportColumn> Columns { get; init; } = Array.Empty<ExportColumn>();
    public Func<IEnumerable<object>> Rows { get; init; } = () => Enumerable.Empty<object>();
    public Func<object, DateTime?>? RowDate { get; init; }

    /// <summary>Tao bang xuat co kieu (goi tu ViewModel — tranh cast tay tung cot).</summary>
    public static ExportTable Create<T>(
        string name,
        Func<IEnumerable<T>> rows,
        Func<T, DateTime?>? rowDate,
        params (string Header, Func<T, object?> Value)[] columns) =>
        new()
        {
            Name = name,
            Rows = () => rows().Cast<object>(),
            RowDate = rowDate is null ? null : o => rowDate((T)o),
            Columns = columns.Select(c => new ExportColumn(c.Header, o => c.Value((T)o))).ToArray(),
        };
}

/// <summary>
/// Trang nao muon co nut "Xuất Excel" (nut chung tren header app) thi ViewModel cua trang
/// implement interface nay: liet ke cac bang dang hien thi + cot cua chung.
/// </summary>
public interface IExportProvider
{
    IReadOnlyList<ExportTable> GetExportTables();
}
