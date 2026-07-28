using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using System.Linq;

namespace GM95.App.ManagerPerformance.Services;

/// <summary>
/// Popup xac nhan dung chung cho SUA/XOA du lieu (theo mau: canh bao van de + [Tiep tuc]/[Huy]).
/// Dung window modal dung bang code de moi ViewModel goi truc tiep, khong can axaml rieng.
/// </summary>
public static class DialogService
{
    /// <summary>
    /// Hien popup canh bao. Tra ve true neu nguoi dung bam nut xac nhan (Tiep tuc/Xoa...),
    /// false neu bam Huy hoac dong cua so.
    /// </summary>
    /// <param name="title">Tieu de ngan (VD: "Xoá nguyên vật liệu").</param>
    /// <param name="message">Noi dung: xoa/sua cai gi, anh huong toi dau (xuong dong bang \n).</param>
    /// <param name="confirmText">Chu tren nut xac nhan (VD: "Xoá", "Tiếp tục sửa").</param>
    /// <param name="danger">true = nut xac nhan mau do (xoa); false = mau xanh (sua/tiep tuc).</param>
    public static async Task<bool> ConfirmAsync(
        string title, string message, string confirmText = "Tiếp tục", bool danger = false)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
            return true; // chay khong co UI (test) -> coi nhu dong y

        var accent = new SolidColorBrush(Color.Parse(danger ? "#DC2626" : "#2563EB"));

        var confirmBtn = new Button
        {
            Content = confirmText,
            MinHeight = 34,
            MinWidth = 110,
            Background = accent,
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold,
            CornerRadius = new CornerRadius(4),
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var cancelBtn = new Button
        {
            Content = "Huỷ",
            MinHeight = 34,
            MinWidth = 90,
            Background = Brushes.White,
            Foreground = new SolidColorBrush(Color.Parse("#1F2937")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D9DDE3")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        var dialog = new Window
        {
            Title = title,
            Width = 560,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White,
            Content = new StackPanel
            {
                Margin = new Thickness(24, 20),
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 16,
                        FontWeight = FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.Parse("#1F2937")),
                    },
                    new TextBlock
                    {
                        Text = message,
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.Parse("#374151")),
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 20,
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Margin = new Thickness(0, 6, 0, 0),
                        Children = { cancelBtn, confirmBtn },
                    },
                },
            },
        };

        confirmBtn.Click += (_, _) => dialog.Close(true);
        cancelBtn.Click += (_, _) => dialog.Close(false);

        // Owner = cua so dang active (khong phai luon MainWindow) — de dialog LONG trong dialog
        // (vd "Thêm công đoạn" mo tu trong "Tạo mẫu quy trình") nam DE LEN cha, khong chui ra sau.
        var owner = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true;
    }

    /// <summary>Popup chi thong bao (1 nut "Đã hiểu") — dung khi hanh dong bi chan han.</summary>
    public static Task InfoAsync(string title, string message) =>
        ConfirmAsync(title, message, "Đã hiểu", danger: false);

    /// <summary>
    /// Mo 1 CUA SO nhap lieu tach roi (Window that, keo/resize duoc, o giua app chinh).
    /// content = phan than form (cac o nhap, luoi...). Day co san nut [Luu] / [Huy] + dong trang thai.
    /// onSave: chay khi bam Luu; tra ve null = THANH CONG (dong dialog), tra ve chuoi = LOI
    ///   (hien do do, giu dialog mo de sua tiep). Ham tu bat exception -> hien message.
    /// Tra ve true neu da luu thanh cong, false neu huy/dong.
    /// </summary>
    /// <param name="scrollable">
    /// true (mac dinh) = boc noi dung trong ScrollViewer — hop voi form 1 cot chi gom cac o nhap.
    /// false = KHONG boc, form tu quan ly chieu cao — BAT BUOC voi form co cot danh sach/luoi ben trong,
    /// vi ScrollViewer cho con cao vo han khien ListBox/DataGrid khong tu cuon duoc ma keo dai ca trang.
    /// </param>
    public static async Task<bool> ShowFormAsync(
        string title, Control content, Func<Task<string?>> onSave,
        string saveText = "Lưu", double width = 640, double height = 520, bool scrollable = true)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
            return false;

        var accent = new SolidColorBrush(Color.Parse("#2563EB"));

        // Dong trang thai (hien loi validate/khi luu).
        var errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#DC2626")),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
        };

        var saveBtn = new Button
        {
            Content = saveText, MinHeight = 36, MinWidth = 120,
            Background = accent, Foreground = Brushes.White, FontWeight = FontWeight.SemiBold,
            CornerRadius = new CornerRadius(4), HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var cancelBtn = new Button
        {
            Content = "Huỷ", MinHeight = 36, MinWidth = 90,
            Background = Brushes.White, Foreground = new SolidColorBrush(Color.Parse("#1F2937")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D9DDE3")), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Margin = new Thickness(0, 12, 0, 0),
        };
        Grid.SetColumn(errorText, 0);
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(cancelBtn, 1);
        Grid.SetColumn(saveBtn, 2);
        footer.Children.Add(errorText);
        footer.Children.Add(cancelBtn);
        footer.Children.Add(saveBtn);

        // Bo cuc: tieu de - noi dung (cuon duoc) - footer nut.
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(20, 16, 20, 16),
        };
        var header = new TextBlock
        {
            Text = title, FontSize = 17, FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#1F2937")), Margin = new Thickness(0, 0, 0, 12),
        };
        Grid.SetRow(header, 0);
        // Form co cot danh sach/luoi: dat thang vao o giua (chieu cao co han) de no TU cuon ben trong.
        // Form 1 cot: boc ScrollViewer cho cuon ca than form.
        Control body = scrollable
            ? new ScrollViewer
            {
                Content = content,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            }
            : content;
        Grid.SetRow(body, 1);
        Grid.SetRow(footer, 2);
        root.Children.Add(header);
        root.Children.Add(body);
        root.Children.Add(footer);

        var dialog = new Window
        {
            Title = title,
            Width = width,
            Height = height,
            CanResize = true,                                   // keo/thay doi kich thuoc duoc
            MinWidth = 420, MinHeight = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White,
            Content = root,
        };

        cancelBtn.Click += (_, _) => dialog.Close(false);
        saveBtn.Click += async (_, _) =>
        {
            saveBtn.IsEnabled = false; cancelBtn.IsEnabled = false;
            errorText.IsVisible = false;
            try
            {
                var err = await onSave();
                if (err is null) { dialog.Close(true); return; }
                errorText.Text = err; errorText.IsVisible = true;
            }
            catch (Exception ex)
            {
                errorText.Text = "Lỗi: " + ex.Message; errorText.IsVisible = true;
            }
            finally { saveBtn.IsEnabled = true; cancelBtn.IsEnabled = true; }
        };

        // Owner = cua so dang active (khong phai luon MainWindow) — de dialog LONG trong dialog
        // (vd "Thêm công đoạn" mo tu trong "Tạo mẫu quy trình") nam DE LEN cha, khong chui ra sau.
        var owner = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true;
    }
}
