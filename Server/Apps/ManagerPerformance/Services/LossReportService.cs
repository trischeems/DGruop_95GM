using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Repositories;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Tenancy;

namespace GM95.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Nghiep vu bao cao hao hut: doi chieu cap phat thuc te (qty_issued) vs dinh muc chuan (qty_standard).
/// generate lam trong 1 transaction (1 RunAsync): doc thanh pham + BOM, tinh & UPSERT tung dong, roi tra ve.
/// </summary>
public sealed class LossReportService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly ILossReportRepository _repo;

    public LossReportService(ITenantConnection db, ITenantContext tenant, ILossReportRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    public Task<IEnumerable<LossReportDto>> ListAsync(long? orderId, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListAsync(s, orderId), ct);

    /// <summary>
    /// Tinh & UPSERT bao cao hao hut cho 1 don, tat ca trong 1 transaction:
    ///   finishedQty (tong thanh pham) -> lay BOM -> voi moi dong NVL trong BOM:
    ///     qtyStandard = finishedQty * qty_per_unit * (1 + waste_pct/100), qtyIssued = tong cap phat -> UPSERT.
    /// Tra ve danh sach bao cao cua don sau khi upsert.
    /// </summary>
    public Task<IEnumerable<LossReportDto>> GenerateAsync(long orderId, CancellationToken ct)
    {
        if (orderId <= 0) throw new ArgumentException("orderId khong hop le.");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var finishedQty = await _repo.GetFinishedQtyAsync(scope, orderId);

            var bomId = await _repo.GetBomIdAsync(scope, orderId);
            if (bomId is null) throw new ArgumentException("Don chua chot BOM.");

            var items = await _repo.ListBomItemsAsync(scope, bomId.Value);
            foreach (var item in items)
            {
                var qtyStandard = finishedQty * item.QtyPerUnit * (1 + item.WastePct / 100m);
                var qtyIssued = await _repo.GetIssuedQtyAsync(scope, orderId, item.MaterialId);
                await _repo.UpsertAsync(scope, orderId, item.MaterialId, qtyIssued, qtyStandard, finishedQty);
            }

            return await _repo.ListAsync(scope, orderId);
        }, ct);
    }
}
