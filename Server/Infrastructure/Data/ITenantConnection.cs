namespace DGroup.Server.Infrastructure.Data;

/// <summary>
/// CACH DUY NHAT de truy cap DB theo tenant.
/// Mo connection + BEGIN + "SET LOCAL search_path" toi schema cua tenant, chay work trong transaction,
/// commit neu khong loi. Vi dung SET LOCAL, search_path tu reset khi transaction ket thuc
/// -> KHONG ro ri sang request/tenant khac khi connection duoc tra ve pool (an toan tuyet doi).
/// </summary>
public interface ITenantConnection
{
    Task<T> RunAsync<T>(string tenant, Func<TenantScope, Task<T>> work, CancellationToken ct = default);
    Task RunAsync(string tenant, Func<TenantScope, Task> work, CancellationToken ct = default);
}
