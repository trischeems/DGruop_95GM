using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace DGroup.App.ManagerPerformance.Services;

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

        var result = await dialog.ShowDialog<bool?>(desktop.MainWindow);
        return result == true;
    }

    /// <summary>Popup chi thong bao (1 nut "Đã hiểu") — dung khi hanh dong bi chan han.</summary>
    public static Task InfoAsync(string title, string message) =>
        ConfirmAsync(title, message, "Đã hiểu", danger: false);
}
