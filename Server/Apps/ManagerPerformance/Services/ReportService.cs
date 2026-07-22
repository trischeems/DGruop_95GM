using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Repositories;
using DGroup.Server.Infrastructure.Data;
using DGroup.Server.Infrastructure.Tenancy;

namespace DGroup.Server.Apps.ManagerPerformance.Services;

/// <summary>Bao cao doc tu cac view nghiep vu (san luong toi da, thieu hut NVL, ton thap).</summary>
public sealed class ReportService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IReportRepository _repo;

    public ReportService(ITenantConnection db, ITenantContext tenant, IReportRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    public Task<IEnumerable<MaxOutputByProductDto>> MaxOutputAsync(CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.MaxOutputByProductAsync(s), ct);

    public Task<MaxOutputByProductDto?> MaxOutputForProductAsync(long productId, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.MaxOutputForProductAsync(s, productId), ct);

    public Task<IEnumerable<OrderMaterialRequirementDto>> OrderRequirementsAsync(long? orderId, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.OrderRequirementsAsync(s, orderId), ct);

    public Task<IEnumerable<MaterialStockDto>> LowStockAsync(int? year, int? month, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.LowStockAsync(s, year, month), ct);

    /// <summary>So lieu tong hop 12 thang (phuc vu so sanh thang).</summary>
    public Task<IEnumerable<MonthlyStatsDto>> MonthlyStatsAsync(int year, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.MonthlyStatsAsync(s, year), ct);
}
