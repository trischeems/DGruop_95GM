using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DGroup.Server.Configuration;
using DGroup.Server.Infrastructure.Data;
using DGroup.Server.Infrastructure.Web;
using Npgsql;

namespace DGroup.Server.Infrastructure.Migrations;

/// <summary>
/// Chay migration SQL thuan theo tung schema tenant.
/// Voi moi tenant, trong 1 transaction:
///   1) CREATE SCHEMA IF NOT EXISTS "&lt;tenant&gt;"
///   2) SET LOCAL search_path toi tenant
///   3) tao bang __migration_history
///   4) chay cac file V*.sql chua co trong history (sort theo version)
/// Checksum khac -> FAIL (cam sua file da ap dung).
/// </summary>
public sealed class MigrationRunner : IMigrationRunner
{
    private static readonly Regex VersionRx = new(
        @"^(V\d+)__", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IDbConnectionFactory _factory;
    private readonly TenancyOptions _tenancy;
    private readonly IReadOnlyList<IAppModule> _modules;
    private readonly ILogger<MigrationRunner> _logger;
    private readonly string _historySql;

    public MigrationRunner(
        IDbConnectionFactory factory,
        TenancyOptions tenancy,
        IEnumerable<IAppModule> modules,
        ILogger<MigrationRunner> logger)
    {
        _factory = factory;
        _tenancy = tenancy;
        _modules = modules.ToList();
        _logger = logger;

        var historyPath = Path.Combine(
            AppContext.BaseDirectory, "Infrastructure", "Migrations", "MigrationHistory.sql");
        _historySql = File.Exists(historyPath)
            ? File.ReadAllText(historyPath)
            // fallback: nhung truc tiep neu file khong duoc copy-to-output
            : """
              CREATE TABLE IF NOT EXISTS __migration_history (
                  version text PRIMARY KEY, filename text NOT NULL,
                  checksum text NOT NULL, applied_at timestamptz NOT NULL DEFAULT now());
              """;
    }

    public async Task MigrateAllAsync(CancellationToken ct = default)
    {
        foreach (var tenant in _tenancy.AllowedSet)
            await MigrateTenantAsync(tenant, ct);
    }

    public async Task MigrateTenantAsync(string tenant, CancellationToken ct = default)
    {
        if (!SqlTenantValidator.IsWellFormed(tenant))
            throw new ArgumentException($"Ten tenant khong hop le: '{tenant}'.");

        var files = CollectMigrationFiles();
        if (files.Count == 0)
        {
            _logger.LogWarning("Khong tim thay file migration nao cho tenant '{Tenant}'.", tenant);
            return;
        }

        await using var conn = await _factory.OpenAsync(ct);

        // Tao schema (ngoai search_path) - quote an toan.
        var ident = new NpgsqlCommandBuilder().QuoteIdentifier(tenant);
        await ExecAsync(conn, null, $"CREATE SCHEMA IF NOT EXISTS {ident}", ct);

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await ExecAsync(conn, tx, $"SET LOCAL search_path TO {ident}", ct);
            await ExecAsync(conn, tx, _historySql, ct);

            var applied = await GetAppliedAsync(conn, tx, ct);

            foreach (var f in files)
            {
                if (applied.TryGetValue(f.Version, out var existingChecksum))
                {
                    if (!string.Equals(existingChecksum, f.Checksum, StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            $"Migration {f.Version} ({f.FileName}) da doi noi dung so voi ban da ap dung " +
                            $"cho tenant '{tenant}'. Cam sua file migration cu — tao file V moi thay the.");
                    continue; // da ap dung, checksum khop -> bo qua
                }

                _logger.LogInformation("Tenant '{Tenant}': ap dung {Version} ({File})", tenant, f.Version, f.FileName);
                await ExecAsync(conn, tx, f.Sql, ct);
                await ExecAsync(conn, tx,
                    "INSERT INTO __migration_history(version, filename, checksum) VALUES (@v, @f, @c)",
                    ct, new { v = f.Version, f = f.FileName, c = f.Checksum });
            }

            await tx.CommitAsync(ct);
            _logger.LogInformation("Tenant '{Tenant}': migration hoan tat ({Count} file kiem tra).", tenant, files.Count);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private List<MigrationFile> CollectMigrationFiles()
    {
        var result = new List<MigrationFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in _modules.SelectMany(m => m.MigrationDirectories).Distinct())
        {
            if (!Directory.Exists(dir))
            {
                _logger.LogWarning("Thu muc migration khong ton tai: {Dir}", dir);
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(dir, "V*.sql"))
            {
                var name = Path.GetFileName(path);
                var m = VersionRx.Match(name);
                if (!m.Success)
                {
                    _logger.LogWarning("Bo qua file khong dung dinh dang V<so>__ten.sql: {File}", name);
                    continue;
                }

                var version = m.Groups[1].Value.ToUpperInvariant();
                if (!seen.Add(version))
                    throw new InvalidOperationException($"Trung version migration '{version}' o {name}.");

                var sql = File.ReadAllText(path);
                result.Add(new MigrationFile(version, name, sql, Sha256(sql)));
            }
        }

        // Sort theo so version (V1, V2, ... V10 dung thu tu).
        return result.OrderBy(f => int.Parse(f.Version[1..])).ToList();
    }

    private static async Task<Dictionary<string, string>> GetAppliedAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var cmd = new NpgsqlCommand("SELECT version, checksum FROM __migration_history", conn, tx);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            map[reader.GetString(0)] = reader.GetString(1);
        return map;
    }

    private static async Task ExecAsync(
        NpgsqlConnection conn, NpgsqlTransaction? tx, string sql, CancellationToken ct, object? p = null)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        if (p is not null)
            foreach (var prop in p.GetType().GetProperties())
                cmd.Parameters.AddWithValue(prop.Name, prop.GetValue(p) ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string Sha256(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes);
    }

    private sealed record MigrationFile(string Version, string FileName, string Sql, string Checksum);
}
