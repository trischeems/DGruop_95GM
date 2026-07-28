using Avalonia;
using System;
using System.Globalization;
using System.Threading;

namespace GM95.App.ManagerPerformance;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Dinh dang so: dung DAU CHAM lam thap phan, DAU PHAY phan nhom nghin (kieu quoc te).
        // Ly do: nguoi dung go dinh muc kieu "1.25" (dau cham) tren ban phim so; neu de vi-VN
        // mac dinh (thap phan la dau PHAY) thi "1.25" bi hieu la 125 -> "khong nhap duoc thap phan".
        // Clone vi-VN roi ep separator de vua giu ngon ngu VN vua nhap thap phan bang dau cham.
        var vi = (CultureInfo)new CultureInfo("vi-VN").Clone();
        vi.NumberFormat.NumberDecimalSeparator = ".";
        vi.NumberFormat.NumberGroupSeparator = ",";
        vi.NumberFormat.CurrencyDecimalSeparator = ".";
        vi.NumberFormat.CurrencyGroupSeparator = ",";
        vi.NumberFormat.PercentDecimalSeparator = ".";
        vi.NumberFormat.PercentGroupSeparator = ",";
        CultureInfo.DefaultThreadCurrentCulture = vi;
        CultureInfo.DefaultThreadCurrentUICulture = vi;
        Thread.CurrentThread.CurrentCulture = vi;
        Thread.CurrentThread.CurrentUICulture = vi;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
