using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using GM95.App.ManagerPerformance.ViewModels;

namespace GM95.App.ManagerPerformance.Services;

/// <summary>
/// Dieu phoi xuat Excel cho MOI trang: mo hop thoai tuy chon (chon bang / pham vi ngay / che do sheet),
/// hoi noi luu file, roi ghi .xlsx. Trang chi can implement IExportProvider.
/// </summary>
public static class ExportService
{
    public static async Task ExportAsync(PageViewModel? page)
    {
        if (page is not IExportProvider provider)
        {
            await DialogService.InfoAsync("Xuất Excel", "Trang này chưa có bảng dữ liệu để xuất.");
            return;
        }
        var tables = provider.GetExportTables();
        if (tables.Count == 0)
        {
            await DialogService.InfoAsync("Xuất Excel", "Trang này chưa có bảng dữ liệu để xuất.");
            return;
        }

        var vm = new ExportOptionsViewModel(tables);
        string? savedPath = null;
        var savedSummary = "";

        var saved = await DialogService.ShowFormAsync(
            $"Xuất Excel — {page.Title}",
            new Views.Dialogs.ExportFormView { DataContext = vm },
            async () =>
            {
                var chosen = vm.Tables.Where(t => t.IsSelected).ToList();
                if (chosen.Count == 0) return "Chọn ít nhất một bảng để xuất.";

                var (from, toEx, rangeErr) = vm.ComputeRange();
                if (rangeErr is not null) return rangeErr;

                // Chup du lieu + loc theo thoi gian (chi bang co cot ngay).
                var data = new List<(ExportTable Table, IReadOnlyList<object> Rows)>();
                foreach (var c in chosen)
                {
                    var rows = c.Table.Rows().ToList();
                    if (c.Table.RowDate is not null && from is not null && toEx is not null)
                        rows = rows.Where(o =>
                        {
                            var d = c.Table.RowDate(o);
                            return d is not null && d >= from && d < toEx;
                        }).ToList();
                    data.Add((c.Table, rows));
                }

                var path = await PickSaveFileAsync(page.Title);
                if (path is null) return "Chưa chọn nơi lưu file (bấm Huỷ nếu muốn thoát).";

                await Task.Run(() => ExcelExporter.WriteWorkbook(path, data, vm.MergeToOneSheet));

                savedPath = path;
                savedSummary = string.Join("\n", data.Select(d => $"• {d.Table.Name}: {d.Rows.Count} dòng"));
                return null;
            },
            saveText: "Xuất Excel", width: 660, height: 560);

        if (saved && savedPath is not null)
            await DialogService.InfoAsync("Đã xuất Excel", $"File: {savedPath}\n\n{savedSummary}");
    }

    /// <summary>Mo hop thoai chon noi luu .xlsx. Null neu nguoi dung huy.</summary>
    private static async Task<string?> PickSaveFileAsync(string pageTitle)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
            return null;
        var owner = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Lưu file Excel",
            SuggestedFileName = $"GM95_{Sanitize(pageTitle)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            DefaultExtension = "xlsx",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Excel (*.xlsx)") { Patterns = new[] { "*.xlsx" } },
            },
        });
        return file?.TryGetLocalPath();
    }

    // Bo ky tu khong hop le trong ten file.
    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(s.Select(ch => invalid.Contains(ch) || ch == ' ' ? '_' : ch).ToArray());
    }
}
