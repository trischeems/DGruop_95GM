using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Repositories;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Tenancy;

namespace GM95.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Nghiep vu canh bao (ton thap / don thieu NVL). Sinh canh bao quet cac view v_material_stock
/// va v_order_material_requirement, chong trung canh bao OPEN cung entity. Tat ca lam trong 1
/// transaction (1 RunAsync) de nhat quan.
/// </summary>
public sealed class AlertService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IAlertRepository _repo;

    public AlertService(ITenantConnection db, ITenantContext tenant, IAlertRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    /// <summary>Danh sach canh bao (mac dinh loc status = 'OPEN' khi khong truyen).</summary>
    public Task<IEnumerable<AlertDto>> ListAsync(string? status, int? year, int? month, CancellationToken ct)
    {
        var filter = string.IsNullOrWhiteSpace(status) ? "OPEN" : status.Trim();
        return _db.RunAsync(_tenant.Tenant, s => _repo.ListAsync(s, filter, year, month), ct);
    }

    /// <summary>Quet & tao canh bao (LOW_STOCK + ORDER_SHORTAGE) trong 1 transaction. Tra ve so canh bao da tao.</summary>
    public Task<int> GenerateAsync(CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var lowStock = await _repo.GenerateLowStockAsync(scope);
            var shortage = await _repo.GenerateOrderShortageAsync(scope);
            return lowStock + shortage;
        }, ct);

    /// <summary>Ghi nhan canh bao. Tra ve false neu khong tim thay id.</summary>
    public Task<bool> AcknowledgeAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async s => await _repo.AcknowledgeAsync(s, id) > 0, ct);

    /// <summary>Dong canh bao (RESOLVED). Tra ve false neu khong tim thay id.</summary>
    public Task<bool> ResolveAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async s => await _repo.ResolveAsync(s, id) > 0, ct);
}
