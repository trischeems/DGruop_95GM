using System.Text.Json;
using System.Text.Json.Serialization;

namespace GM95.App.ManagerPerformance.Services;

/// <summary>
/// Cau hinh app doc tu config.json (nguon, canh binary). Neu co user-config.json o thu muc
/// du lieu runtime (nguoi dung sua server URL/tenant) thi doc de len tren.
/// </summary>
public sealed class AppConfig
{
    [JsonPropertyName("app")]    public AppInfo App { get; init; } = new();
    [JsonPropertyName("server")] public ServerInfo Server { get; init; } = new();
    [JsonPropertyName("tenant")] public TenantInfo Tenant { get; init; } = new();

    public sealed class AppInfo
    {
        [JsonPropertyName("name")]   public string Name { get; init; } = "Manager_Perfoment";
        [JsonPropertyName("title")]  public string Title { get; init; } = "GM95 - Quản lý sản xuất";
        [JsonPropertyName("vendor")] public string Vendor { get; init; } = "GM95";
    }

    public sealed class ServerInfo
    {
        [JsonPropertyName("base_url")]        public string BaseUrl { get; init; } = "http://localhost:8765";
        [JsonPropertyName("api_prefix")]      public string ApiPrefix { get; init; } = "dgrpi";
        [JsonPropertyName("timeout_seconds")] public string TimeoutSeconds { get; init; } = "30";
        public int TimeoutSecondsValue => int.TryParse(TimeoutSeconds, out var v) ? v : 30;
    }

    public sealed class TenantInfo
    {
        [JsonPropertyName("current")] public string Current { get; init; } = "public";
        [JsonPropertyName("header")]  public string Header { get; init; } = "X-Tenant";
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Nap config: config.json (nguon) + user-config.json (ghi de neu co).</summary>
    public static AppConfig Load()
    {
        var cfg = LoadFrom(Path.Combine(AppContext.BaseDirectory, "config.json")) ?? new AppConfig();

        // Ghi de bang cau hinh nguoi dung (neu ton tai) o thu muc du lieu runtime.
        var userCfg = LoadFrom(AppPaths.UserConfigFile);
        return userCfg is null ? cfg : userCfg;
    }

    private static AppConfig? LoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOpts);
        }
        catch
        {
            return null; // config loi -> dung mac dinh, khong lam sap app
        }
    }
}
