using System.Text.Json.Serialization;

namespace GM95.Server.Configuration;

/// <summary>
/// Khoi "r2_backup" trong config.json. Cac bien dat san cho backup Cloudflare R2 (lam sau).
/// </summary>
public sealed class R2BackupOptions
{
    [JsonPropertyName("account_id")]
    public string AccountId { get; init; } = "";

    [JsonPropertyName("access_key_id")]
    public string AccessKeyId { get; init; } = "";

    [JsonPropertyName("secret_access_key")]
    public string SecretAccessKey { get; init; } = "";

    [JsonPropertyName("bucket_name")]
    public string BucketName { get; init; } = "";

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; init; } = "";

    [JsonPropertyName("region")]
    public string Region { get; init; } = "";
}
