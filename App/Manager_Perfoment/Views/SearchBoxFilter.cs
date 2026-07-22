using Avalonia.Controls;
using Avalonia.VisualTree;

namespace DGroup.App.ManagerPerformance.Views;

/// <summary>
/// Cau hinh chung cho moi o search-goi-y (AutoCompleteBox) trong 1 view:
/// 1) Bo loc: khop "CHUA chuoi go vao" tren ToString() cua item, khong phan biet hoa thuong
///    (can thiet vi FilterMode mac dinh khong loc item kieu object).
/// 2) Click/focus vao o TRONG -> bung ngay danh sach goi y day du (khong can go gi).
/// </summary>
public static class SearchBoxFilter
{
    public static void Attach(Control root)
    {
        // Loaded: cay visual da dung du -> tim duoc het AutoCompleteBox con.
        root.Loaded += (_, _) =>
        {
            foreach (var box in root.GetVisualDescendants().OfType<AutoCompleteBox>())
            {
                if (box.Tag is "search-attached") continue; // tranh gan trung khi Loaded lai
                box.Tag = "search-attached";

                box.ItemFilter = (search, item) =>
                    string.IsNullOrWhiteSpace(search)
                    || (item?.ToString()?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);

                // Mo goi y ngay khi click/focus vao o con trong. Meo: nudge Text " " -> ""
                // de AutoCompleteBox tu chay vong loc (voi chuoi rong -> hien tat ca).
                void OpenSuggest()
                {
                    if (!string.IsNullOrEmpty(box.Text)) return; // dang co gia tri -> khong tu bung
                    if (box.ItemsSource is null) return;
                    box.Text = " ";
                    box.Text = "";
                    box.IsDropDownOpen = true;
                }

                box.GotFocus += (_, _) => OpenSuggest();
                box.PointerReleased += (_, _) => OpenSuggest();
            }
        };
    }
}
