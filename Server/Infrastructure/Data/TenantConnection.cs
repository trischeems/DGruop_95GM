using GM95.Server.Configuration;
using Npgsql;

namespace GM95.Server.Infrastructure.Data;

/// <summary>
/// Trien khai ITenantConnection. Diem mau chot: dung SET LOCAL search_path trong transaction.
///
/// Vi sao KHONG dung "SET search_path" tran (ngoai transaction)? Voi connection pooling, no
/// dinh lai tren physical connection va ro ri sang request/tenant sau khi connection tai su dung
/// -> sai schema, lo du lieu cheo tenant. SET LOCAL chi song trong transaction hien tai.
/// </summary>
public sealed class TenantConnection : ITenantConnection
{
    private readonly IDbConnectionFactory _factory;
    private readonly IReadOnlySet<string> _allowed;

    public TenantConnection(IDbConnectionFactory factory, TenancyOptions tenancy)
    {
        _factory = factory;
        _allowed = tenancy.AllowedSet;
    }

    public async Task<T> RunAsync<T>(string tenant, Func<TenantScope, Task<T>> work, CancellationToken ct = default)
    {
        // 1) whitelist + regex (chong injection tang cau hinh)
        var t = SqlTenantValidator.Validate(tenant, _allowed);

        await using var conn = await _factory.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // 2) quote identifier an toan (search_path khong nhan tham so bind)
        var ident = new NpgsqlCommandBuilder().QuoteIdentifier(t);
        await using (var cmd = new NpgsqlCommand($"SET LOCAL search_path TO {ident}", conn, tx))
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var scope = new TenantScope(conn, tx, t, ct);
        try
        {
            var result = await work(scope);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct); // search_path tu bien mat khi rollback
            throw;
        }
    }

    public Task RunAsync(string tenant, Func<TenantScope, Task> work, CancellationToken ct = default) =>
        RunAsync<object?>(tenant, async scope =>
        {
            await work(scope);
            return null;
        }, ct);
}
