namespace GM95.App.ManagerPerformance.Services;

/// <summary>
/// Duong dan cua app, TU CHUYEN HOA theo he dieu hanh (Windows/macOS/Linux).
/// KHONG hard-code path, KHONG hard-code username.
///
/// Du lieu runtime (cache, log, cau hinh nguoi dung) tach khoi file nguon (chi doc):
///   Windows: %LOCALAPPDATA%\GM95\App\Manager_performent
///   macOS  : ~/Library/Application Support/GM95/App/Manager_performent
///   Linux  : ~/.local/share/GM95/App/Manager_performent
/// (.NET SpecialFolder.LocalApplicationData tu tra dung thu muc moi HDH.)
/// </summary>
public static class AppPaths
{
    private const string Vendor = "GM95";
    private const string AppSegment = "App";
    private const string AppName = "Manager_performent";

    /// <summary>Thu muc du lieu runtime (ghi duoc). Tu tao neu chua co.</summary>
    public static string DataDir
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(root, Vendor, AppSegment, AppName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Thu muc log.</summary>
    public static string LogsDir
    {
        get
        {
            var dir = Path.Combine(DataDir, "logs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Thu muc cache.</summary>
    public static string CacheDir
    {
        get
        {
            var dir = Path.Combine(DataDir, "cache");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>File cau hinh nguoi dung ghi de (nam o data dir, tach khoi config.json nguon).</summary>
    public static string UserConfigFile => Path.Combine(DataDir, "user-config.json");
}
