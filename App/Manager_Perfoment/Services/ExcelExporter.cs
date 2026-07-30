using ClosedXML.Excel;

namespace GM95.App.ManagerPerformance.Services;

/// <summary>
/// Ghi file .xlsx tu cac bang da chon (dung ClosedXML). 2 che do:
///  - moi bang 1 sheet (mac dinh);
///  - gop tat ca vao 1 sheet (cac bang xep doc, co tieu de tung bang) — dung khi muon xem lien ket.
/// </summary>
public static class ExcelExporter
{
    /// <summary>Ghi workbook. rows cua tung bang DA duoc loc theo thoi gian truoc khi goi.</summary>
    public static void WriteWorkbook(
        string filePath,
        IReadOnlyList<(ExportTable Table, IReadOnlyList<object> Rows)> tables,
        bool mergeToOneSheet)
    {
        using var wb = new XLWorkbook();
        if (mergeToOneSheet)
        {
            var ws = wb.Worksheets.Add("Tổng hợp");
            var row = 1;
            foreach (var (table, rows) in tables)
            {
                // Tieu de bang (to, dam) roi den header + du lieu; cach 2 dong giua cac bang.
                ws.Cell(row, 1).Value = table.Name;
                ws.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(13);
                row++;
                row = WriteTable(ws, row, table, rows);
                row += 2;
            }
            ws.Columns().AdjustToContents();
            foreach (var col in ws.ColumnsUsed())
                if (col.Width > 60) col.Width = 60;
        }
        else
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (table, rows) in tables)
            {
                var ws = wb.Worksheets.Add(UniqueSheetName(table.Name, used));
                var end = WriteTable(ws, 1, table, rows);
                ws.SheetView.FreezeRows(1);                    // ghim dong header khi cuon
                ws.RangeUsed()?.SetAutoFilter();               // co san bo loc cua Excel
                ws.Columns().AdjustToContents();
                foreach (var col in ws.ColumnsUsed())
                    if (col.Width > 60) col.Width = 60;        // chan cot qua dai (ghi chu...)
                _ = end;
            }
        }
        wb.SaveAs(filePath);
    }

    /// <summary>Ghi 1 bang bat dau tu dong startRow. Tra ve dong ke tiep sau bang.</summary>
    private static int WriteTable(IXLWorksheet ws, int startRow, ExportTable table, IReadOnlyList<object> rows)
    {
        // Header
        for (var c = 0; c < table.Columns.Count; c++)
        {
            var cell = ws.Cell(startRow, c + 1);
            cell.Value = table.Columns[c].Header;
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E8ECF1"));
            cell.Style.Border.SetBottomBorder(XLBorderStyleValues.Thin);
        }

        // Du lieu
        var r = startRow + 1;
        foreach (var rowObj in rows)
        {
            for (var c = 0; c < table.Columns.Count; c++)
            {
                var cell = ws.Cell(r, c + 1);
                switch (table.Columns[c].Value(rowObj))
                {
                    case null: break;
                    case DateTime dt:
                        cell.Value = dt;
                        cell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                        break;
                    case bool b: cell.Value = b ? "Có" : "Không"; break;
                    case decimal d: cell.Value = d; break;
                    case double d: cell.Value = d; break;
                    case float f: cell.Value = f; break;
                    case int i: cell.Value = i; break;
                    case long l: cell.Value = l; break;
                    case string s: cell.Value = s; break;
                    default: cell.Value = table.Columns[c].Value(rowObj)?.ToString() ?? ""; break;
                }
            }
            r++;
        }
        return r;
    }

    // Ten sheet Excel: toi da 31 ky tu, khong chua : \ / ? * [ ] va phai duy nhat trong workbook.
    private static string UniqueSheetName(string name, HashSet<string> used)
    {
        var cleaned = new string(name.Select(ch => @":\/?*[]".Contains(ch) ? ' ' : ch).ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "Bang";
        if (cleaned.Length > 31) cleaned = cleaned[..31];
        var candidate = cleaned;
        var n = 2;
        while (!used.Add(candidate))
        {
            var suffix = $" ({n++})";
            candidate = (cleaned.Length + suffix.Length > 31 ? cleaned[..(31 - suffix.Length)] : cleaned) + suffix;
        }
        return candidate;
    }
}
