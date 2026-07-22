namespace DGroup.Server.Infrastructure.Migrations;

/// <summary>
/// Chay migration SQL thuan (khong EF) theo tung schema tenant.
/// </summary>
public interface IMigrationRunner
{
    /// <summary>Migrate 1 tenant: tao schema neu can + chay cac file V*.sql chua ap dung.</summary>
    Task MigrateTenantAsync(string tenant, CancellationToken ct = default);

    /// <summary>Migrate tat ca tenant trong allowed_tenants.</summary>
    Task MigrateAllAsync(CancellationToken ct = default);
}
