using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Repositories;
using DGroup.Server.Infrastructure.Data;
using DGroup.Server.Infrastructure.Tenancy;

namespace DGroup.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Nghiep vu quy trinh san xuat (Cat -> May -> QC -> Nhap TP), dang state machine.
/// Cap nhat 1 buoc lam trong 1 transaction: khoa dong (FOR UPDATE) -> cap nhat.
/// Chong race khi 500+ user cap nhat dong thoi.
/// </summary>
public sealed class ProductionStepService
{
    // Khop CHECK constraint tren cot status.
    private static readonly string[] AllowedStatus =
        { "PENDING", "IN_PROGRESS", "DONE", "ON_HOLD", "CANCELLED" };

    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IProductionStepRepository _repo;

    public ProductionStepService(ITenantConnection db, ITenantContext tenant, IProductionStepRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    /// <summary>Khoi tao 4 dong cong doan cho don (bo qua neu da co).</summary>
    public Task InitAsync(long orderId, CancellationToken ct)
    {
        if (orderId <= 0) throw new ArgumentException("orderId khong hop le.");
        return _db.RunAsync(_tenant.Tenant, s => _repo.InitStepsAsync(s, orderId), ct);
    }

    /// <summary>Danh sach buoc quy trinh cua 1 don, sap xep theo seq.</summary>
    public Task<IEnumerable<ProductionStepDto>> ListByOrderAsync(long orderId, CancellationToken ct)
    {
        if (orderId <= 0) throw new ArgumentException("orderId khong hop le.");
        return _db.RunAsync(_tenant.Tenant, s => _repo.ListByOrderAsync(s, orderId), ct);
    }

    /// <summary>
    /// Cap nhat 1 buoc quy trinh. Tra ve false neu khong tim thay dong.
    /// Khoa dong (FOR UPDATE) truoc khi cap nhat trong cung 1 transaction.
    /// </summary>
    public Task<bool> UpdateAsync(long id, UpdateStepRequest req, CancellationToken ct)
    {
        if (id <= 0) throw new ArgumentException("id khong hop le.");
        if (!AllowedStatus.Contains(req.Status)) throw new ArgumentException("Trang thai khong hop le.");
        if (req.QtyIn < 0 || req.QtyOut < 0 || req.QtyDefect < 0)
            throw new ArgumentException("So luong khong duoc am.");
        // Khop CHECK qty_out+qty_defect<=qty_in (tru khi qty_in=0).
        if (req.QtyIn > 0 && req.QtyOut + req.QtyDefect > req.QtyIn)
            throw new ArgumentException("qty_out + qty_defect khong duoc vuot qua qty_in.");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // Khoa dong buoc; khong ton tai -> bao NOT_FOUND cho controller.
            var locked = await _repo.LockStepAsync(scope, id);
            if (locked is null) return false;

            await _repo.UpdateStepAsync(scope, id, req);
            return true;
        }, ct);
    }
}
