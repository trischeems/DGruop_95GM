using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Repositories;
using DGroup.Server.Infrastructure.Data;
using DGroup.Server.Infrastructure.Tenancy;

namespace DGroup.Server.Apps.ManagerPerformance.Services;

/// <summary>Nghiep vu ke hoach san xuat. Moi thao tac chay trong 1 transaction dung schema tenant.</summary>
public sealed class ProductionPlanService
{
    // Trang thai hop le (khop CHECK constraint tren cot status).
    private static readonly HashSet<string> AllowedStatus =
        new(StringComparer.Ordinal) { "PLANNED", "RELEASED", "IN_PROGRESS", "DONE", "CANCELLED" };

    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IProductionPlanRepository _repo;

    public ProductionPlanService(ITenantConnection db, ITenantContext tenant, IProductionPlanRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    public Task<IEnumerable<ProductionPlanDto>> ListAsync(long? orderId, int? year, int? month, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListAsync(s, orderId, year, month), ct);

    public Task<long> CreateAsync(CreateProductionPlanRequest req, CancellationToken ct)
    {
        if (req.ProductionOrderId <= 0) throw new ArgumentException("productionOrderId khong hop le.");
        if (req.PlannedQty <= 0) throw new ArgumentException("plannedQty phai > 0.");
        if (req.PlannedStart.HasValue && req.PlannedEnd.HasValue && req.PlannedEnd < req.PlannedStart)
            throw new ArgumentException("plannedEnd phai >= plannedStart.");

        return _db.RunAsync(_tenant.Tenant, s => _repo.InsertAsync(s, req), ct);
    }

    public Task<bool> UpdateStatusAsync(long id, UpdatePlanStatusRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Status) || !AllowedStatus.Contains(req.Status))
            throw new ArgumentException("status khong hop le.");

        return _db.RunAsync(_tenant.Tenant, s => _repo.UpdateStatusAsync(s, id, req.Status), ct);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.DeleteAsync(s, id), ct);

    /// <summary>Sua ke hoach: so luong / ma chuyen / ghi chu.</summary>
    public Task<bool> UpdateAsync(long id, UpdatePlanRequest req, CancellationToken ct)
    {
        if (req.PlannedQty <= 0) throw new ArgumentException("plannedQty phai > 0.");
        return _db.RunAsync(_tenant.Tenant, s => _repo.UpdateAsync(s, id, req), ct);
    }
}
