using System.Text.Json.Serialization;

namespace DGroup.Server.Configuration;

/// <summary>
/// Khoi "tenancy" (tuy chon) trong config.json. Neu vang mat -> dung mac dinh an toan.
/// Multi-tenant kieu schema-per-tenant: moi tenant = 1 PostgreSQL schema rieng trong DB cua app.
/// </summary>
public sealed class TenancyOptions
{
    /// <summary>Tenant mac dinh khi request khong chi ro (schema "public").</summary>
    [JsonPropertyName("default_tenant")]
    public string DefaultTenant { get; init; } = "public";

    /// <summary>Whitelist ten schema hop le (chong SQL injection o tang cau hinh).</summary>
    [JsonPropertyName("allowed_tenants")]
    public List<string> AllowedTenants { get; init; } = new() { "public" };

    /// <summary>Dev = true: tu tao schema + chay migration khi gap tenant moi. Prod nen false.</summary>
    [JsonPropertyName("auto_create_schema")]
    public bool AutoCreateSchema { get; init; } = true;

    /// <summary>Header HTTP dung de chon tenant.</summary>
    [JsonPropertyName("tenant_header")]
    public string TenantHeader { get; init; } = "X-Tenant";

    /// <summary>Tap tenant hop le (chuan hoa lowercase), luon bao gom default_tenant.</summary>
    public IReadOnlySet<string> AllowedSet =>
        new HashSet<string>(
            AllowedTenants.Append(DefaultTenant).Select(t => t.Trim().ToLowerInvariant()),
            StringComparer.Ordinal);
}
