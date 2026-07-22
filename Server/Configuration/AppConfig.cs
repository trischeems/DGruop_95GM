using System.Text.Json.Serialization;

namespace DGroup.Server.Configuration;

/// <summary>
/// Ban do 1-1 voi Server/config.json (nguon su that moi cau hinh).
/// Giu nguyen ten khoa cu (ke ca typo "dgroup_postgress"); chi THEM khoa moi co default.
/// </summary>
public sealed class AppConfig
{
    [JsonPropertyName("server")]
    public ServerOptions Server { get; init; } = new();

    [JsonPropertyName("r2_backup")]
    public R2BackupOptions R2Backup { get; init; } = new();

    // Typo "dgroup_postgress" giu nguyen o JSON; property C# dat ten chuan.
    [JsonPropertyName("dgroup_postgress")]
    public PostgresOptions DgroupPostgres { get; init; } = new();

    [JsonPropertyName("https")]
    public bool Https { get; init; } = true;

    // Khoi tuy chon; vang mat -> mac dinh an toan.
    [JsonPropertyName("tenancy")]
    public TenancyOptions Tenancy { get; init; } = new();
}
