using System.Text.Json;

namespace GM95.Server.Configuration;

/// <summary>
/// Nap Server/config.json thanh AppConfig. Fail-fast neu thieu file hoac thieu khoi Postgres.
/// Uu tien doc file canh binary (da copy khi build); fallback ve ContentRoot.
/// </summary>
public static class ConfigLoader
{
    private const string FileName = "config.json";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static AppConfig Load(string contentRootPath)
    {
        var path = ResolvePath(contentRootPath);
        if (path is null)
            throw new FileNotFoundException(
                $"Khong tim thay {FileName}. Da tim o thu muc binary va ContentRoot='{contentRootPath}'.");

        var json = File.ReadAllText(path);
        AppConfig? cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{FileName} sai dinh dang JSON: {ex.Message}", ex);
        }

        if (cfg is null)
            throw new InvalidOperationException($"{FileName} rong hoac khong parse duoc.");

        // Bat buoc phai co cau hinh Postgres de server hoat dong.
        if (string.IsNullOrWhiteSpace(cfg.Gm95Postgres.DbName) ||
            string.IsNullOrWhiteSpace(cfg.Gm95Postgres.User))
            throw new InvalidOperationException(
                $"{FileName} thieu khoi 'gm95_postgress' (dbname/user).");

        return cfg;
    }

    /// <summary>Tra ve duong dan config.json dau tien tim thay, hoac null.</summary>
    public static string? ResolvePath(string contentRootPath)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, FileName), // canh binary (copy-to-output)
            Path.Combine(contentRootPath, FileName),          // ContentRoot (khi dotnet run)
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
