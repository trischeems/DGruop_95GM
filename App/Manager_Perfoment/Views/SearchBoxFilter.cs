using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DGroup.App.ManagerPerformance.Views;

/// <summary>
/// Cau hinh chung cho moi o search-goi-y (AutoCompleteBox) trong 1 view:
/// 1) Bo loc: khop "CHUA chuoi go vao" tren ToString() cua item, khong phan biet hoa thuong.
/// 2) Click/focus vao o TRONG -> bung ngay danh sach goi y day du.
/// 3) CHONG NHAY TRUONG: khi dong dropdown (mat focus / chon xong), neu Text hien tai
///    KHONG khop chinh xac item nao thi khoi phuc lai Text theo SelectedItem dang chon.
///    Ly do: AutoCompleteBox la control driven-by-text; khi go 1 phan trung nhieu item
///    no co the tu auto-append/chon nham item khac -> nguoi dung "chon met nhung nhay
///    sang dvt khac". Sua tap trung o day, ap dung cho MOI AutoCompleteBox, khong phai
///    sua tung file XAML.
/// </summary>
public static class SearchBoxFilter
{
    public static void Attach(Control root)
    {
        root.Loaded += (_, _) =>
        {
            foreach (var box in root.GetVisualDescendants().OfType<AutoCompleteBox>())
            {
                if (box.Tag is "search-attached") continue; // tranh gan trung khi Loaded lai
                box.Tag = "search-attached";

                // Loc theo ToString() cua item, khong phan biet hoa thuong.
                // QUAN TRONG: neu chuoi search = dung ten item DANG CHON (vua mo, chua go moi)
                //   -> coi nhu CHUA loc -> hien TAT CA item de nguoi dung cuon/chon.
                //   Nho vay: bam phat la ra ca danh sach; go them chu thi moi loc dan.
                box.ItemFilter = (search, item) =>
                {
                    if (string.IsNullOrWhiteSpace(search)) return true;
                    var sel = box.SelectedItem?.ToString();
                    if (sel is not null && string.Equals(search, sel, StringComparison.Ordinal))
                        return true; // text = item dang chon -> hien tat ca
                    return item?.ToString()?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ?? false;
                };

                // Bam/focus vao o -> XO NGAY danh sach de cuon chon (khong bat buoc go).
                void OpenSuggest()
                {
                    if (box.ItemsSource is null) return;
                    box.IsDropDownOpen = true;
                }

                box.GotFocus += (_, _) => OpenSuggest();
                box.PointerReleased += (_, _) => OpenSuggest();

                // CHONG NHAY: khi dropdown dong, dong bo lai Text <-> SelectedItem.
                // Neu Text khong khop DUY NHAT 1 item -> tra Text ve dung item dang chon
                // (hoac xoa Text neu chua chon gi). Tranh trang thai "Text mot dang,
                // SelectedItem mot neo" khien lan chon sau bi nham.
                box.DropDownClosed += (_, _) => Dispatcher.UIThread.Post(() => SyncTextToSelection(box));
                box.LostFocus += (_, _) => SyncTextToSelection(box);
            }
        };
    }

    /// <summary>
    /// Dong bo Text ve dung SelectedItem: neu chuoi go khong khop CHINH XAC item dang chon,
    /// dat lai Text = SelectedItem.ToString() (hoac rong neu chua chon). Chong nhay/lech.
    /// </summary>
    private static void SyncTextToSelection(AutoCompleteBox box)
    {
        var selectedText = box.SelectedItem?.ToString();
        if (selectedText is null)
        {
            // Chua chon item nao: neu Text dang la chuoi go do dang thi xoa cho sach.
            if (!string.IsNullOrEmpty(box.Text)) box.Text = "";
            return;
        }
        // Da co item chon: dam bao Text hien dung ten item do (khong phai chuoi go 1 phan).
        if (!string.Equals(box.Text, selectedText, StringComparison.Ordinal))
            box.Text = selectedText;
    }
}
