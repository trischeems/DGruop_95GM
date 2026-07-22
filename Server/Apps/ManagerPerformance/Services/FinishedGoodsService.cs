using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Repositories;
using DGroup.Server.Infrastructure.Data;
using DGroup.Server.Infrastructure.Tenancy;

namespace DGroup.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Nghiep vu nhap kho thanh pham. Tao phieu nhap + dong lenh san xuat (COMPLETED)
/// trong CUNG 1 transaction (1 RunAsync) de dam bao nhat quan.
/// </summary>
public sealed class FinishedGoodsService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IFinishedGoodsRepository _repo;

    public FinishedGoodsService(ITenantConnection db, ITenantContext tenant, IFinishedGoodsRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    public Task<IEnumerable<FinishedGoodsReceiptDto>> ListAsync(long? orderId, int? year, int? month, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListAsync(s, orderId, year, month), ct);

    public Task<long> CreateAsync(CreateFinishedGoodsRequest req, CancellationToken ct)
    {
        if (req.QtyReceived <= 0) throw new ArgumentException("So luong nhap phai > 0.");
        if (req.ProductionOrderId <= 0) throw new ArgumentException("Lenh san xuat khong hop le.");
        if (req.ProductId <= 0 || req.WarehouseId <= 0) throw new ArgumentException("Ma hang/kho khong hop le.");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // 1) Tao phieu nhap kho thanh pham (receipt_no sinh trong SQL).
            var id = await _repo.InsertAsync(scope, req);

            // 2) Dong lenh san xuat -> COMPLETED (chi khi dang CONFIRMED/IN_PROGRESS). Cung transaction.
            await _repo.MarkOrderCompletedAsync(scope, req.ProductionOrderId);

            return id;
        }, ct);
    }

    /// <summary>Anh huong khi xoa phieu nhap TP (phuc vu popup canh bao truoc khi xoa).</summary>
    public Task<FinishedGoodsImpactDto?> GetImpactAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.GetImpactAsync(s, id), ct);

    /// <summary>
    /// Xoa phieu nhap TP trong 1 transaction: xoa phieu; neu don khong con phieu nao va dang
    /// COMPLETED thi tra ve IN_PROGRESS. Hao hut cua don can duoc tinh lai sau khi xoa.
    /// </summary>
    public Task DeleteAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var orderId = await _repo.GetOrderIdAsync(scope, id)
                          ?? throw new ArgumentException($"Khong tim thay phieu nhap TP id={id}.");
            await _repo.DeleteAsync(scope, id);
            var remaining = await _repo.CountByOrderAsync(scope, orderId);
            if (remaining == 0)
                await _repo.RevertOrderToInProgressAsync(scope, orderId);
        }, ct);
}
