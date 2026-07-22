using System.Text.Json.Serialization;

namespace DGroup.Server.Configuration;

/// <summary>
/// Khoi "dgroup_postgress" trong config.json (giu nguyen typo lam nguon su that).
/// Moi app se co the co khoi Postgres rieng (DB rieng) sau nay; app dau tien dung khoi nay.
/// </summary>
public sealed class PostgresOptions
{
    [JsonPropertyName("host")]
    public string Host { get; init; } = "localhost";

    [JsonPropertyName("port")]
    public string Port { get; init; } = "5678";

    [JsonPropertyName("dbname")]
    public string DbName { get; init; } = "dgroup_db";

    [JsonPropertyName("user")]
    public string User { get; init; } = "dgroup";

    [JsonPropertyName("pass")]
    public string Pass { get; init; } = "";

    /// <summary>
    /// Connection string cho NpgsqlDataSource (pool built-in).
    /// Max Pool Size 100 cho 500+ user (khi can hon -> PgBouncer transaction-mode phia truoc).
    /// </summary>
    public string ConnectionString =>
        $"Host={Host};Port={Port};Database={DbName};Username={User};Password={Pass};" +
        "Pooling=true;Minimum Pool Size=5;Maximum Pool Size=100;" +
        "Timeout=15;Command Timeout=30";

    /// <summary>Chuoi ket noi da an mat khau (dung de log).</summary>
    public string SafeConnectionString =>
        $"Host={Host};Port={Port};Database={DbName};Username={User};Password=***";
}
