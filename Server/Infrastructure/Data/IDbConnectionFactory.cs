using Npgsql;

namespace GM95.Server.Infrastructure.Data;

/// <summary>
/// Cung cap connection toi DB cua mot app. Moi app 1 DataSource (1 pool) rieng.
/// Hien tai app dau tien (ManagerPerformance) dung DB "gm95_postgress".
/// Dung 1 DataSource dung chung (KHONG tao pool moi cho tung tenant) — chon schema o muc transaction.
/// </summary>
public interface IDbConnectionFactory
{
    NpgsqlDataSource DataSource { get; }

    /// <summary>Mo mot connection tu pool (nho dispose).</summary>
    ValueTask<NpgsqlConnection> OpenAsync(CancellationToken ct = default);
}
