using Dapper;
using Npgsql;

namespace GM95.Server.Infrastructure.Data;

/// <summary>
/// Boc connection + transaction dang mo DUNG schema cua mot tenant.
/// Repository nhan TenantScope va chi viec query — khong tu mo connection, khong prefix schema.
/// Moi lenh Dapper deu di kem transaction cua scope.
/// </summary>
public sealed class TenantScope
{
    private readonly NpgsqlConnection _conn;
    private readonly NpgsqlTransaction _tx;
    private readonly CancellationToken _ct;

    public TenantScope(NpgsqlConnection conn, NpgsqlTransaction tx, string tenant, CancellationToken ct)
    {
        _conn = conn;
        _tx = tx;
        Tenant = tenant;
        _ct = ct;
    }

    /// <summary>Ten tenant (schema) hien tai cua scope.</summary>
    public string Tenant { get; }

    public NpgsqlConnection Connection => _conn;
    public NpgsqlTransaction Transaction => _tx;

    private CommandDefinition Cmd(string sql, object? p) =>
        new(sql, p, _tx, cancellationToken: _ct);

    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? p = null) =>
        _conn.QueryAsync<T>(Cmd(sql, p));

    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? p = null) =>
        _conn.QueryFirstOrDefaultAsync<T?>(Cmd(sql, p));

    public Task<T> QuerySingleAsync<T>(string sql, object? p = null) =>
        _conn.QuerySingleAsync<T>(Cmd(sql, p));

    public Task<T> ExecuteScalarAsync<T>(string sql, object? p = null) =>
        _conn.ExecuteScalarAsync<T>(Cmd(sql, p))!;

    public Task<int> ExecuteAsync(string sql, object? p = null) =>
        _conn.ExecuteAsync(Cmd(sql, p));
}
