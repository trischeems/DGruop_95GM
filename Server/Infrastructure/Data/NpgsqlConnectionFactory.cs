using Npgsql;

namespace GM95.Server.Infrastructure.Data;

/// <summary>
/// Xay dung NpgsqlDataSource 1 lan (singleton) tu connection string.
/// NpgsqlDataSource quan ly pooling built-in — tai su dung an toan cho 500+ user.
/// </summary>
public sealed class NpgsqlConnectionFactory : IDbConnectionFactory, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = builder.Build();
    }

    public NpgsqlDataSource DataSource => _dataSource;

    public ValueTask<NpgsqlConnection> OpenAsync(CancellationToken ct = default) =>
        _dataSource.OpenConnectionAsync(ct);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
