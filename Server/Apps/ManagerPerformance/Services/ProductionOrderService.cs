using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Repositories;
using DGroup.Server.Infrastructure.Data;
using DGroup.Server.Infrastructure.Tenancy;

namespace DGroup.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Nghiep vu don hang san xuat + giu cho NVL.
/// Vong doi: DRAFT -> CONFIRMED (giu cho NVL) -> ... ; huy -> nha giu cho.
/// Confirm/cancel dung ghi ton -> lam trong 1 RunAsync + FOR UPDATE (chong race 500+ user).
/// </summary>
public sealed class ProductionOrderService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IProductionOrderRepository _repo;

    public ProductionOrderService(ITenantConnection db, ITenantContext tenant, IProductionOrderRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    public Task<IEnumerable<ProductionOrderDto>> ListAsync(string? status, int? year, int? month, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListAsync(s, status, year, month), ct);

    public Task<ProductionOrderDto?> GetByIdAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.GetByIdAsync(s, id), ct);

    public Task<IEnumerable<ReservationDto>> ListReservationsAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListReservationsAsync(s, id), ct);

    public Task<long> CreateAsync(CreateProductionOrderRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.OrderNo)) throw new ArgumentException("orderNo khong duoc rong.");
        if (req.ProductId <= 0) throw new ArgumentException("productId khong hop le.");
        if (req.Quantity <= 0) throw new ArgumentException("quantity phai > 0.");

        return _db.RunAsync(_tenant.Tenant, s => _repo.InsertAsync(s, req), ct);
    }

    /// <summary>
    /// Xac nhan don + GIU CHO NVL. Tat ca trong 1 RunAsync (1 transaction) de cung 1 tenant tx:
    ///   khoa don (FOR UPDATE) -> chot BOM -> voi moi NVL: khoa cac dong stock kha dung (FOR UPDATE),
    ///   giu cho theo thu tu kho nhieu ton nhat -> cong reserved + upsert phieu giu cho -> doi trang thai CONFIRMED.
    /// Thieu hut (remaining > 0) KHONG phai loi: de nguyen, bao cao se hien.
    /// </summary>
    public Task<long> ConfirmAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // 1) Khoa dong don hang.
            var order = await _repo.LockOrderAsync(scope, id)
                        ?? throw new ArgumentException($"Khong tim thay don hang id={id}.");
            if (order.Status != "DRAFT")
                throw new ArgumentException("Chi xac nhan duoc don o trang thai DRAFT.");

            // 2) Chot BOM: uu tien bom_id cua don, khong co thi lay BOM ACTIVE cua san pham.
            long bomId;
            if (order.BomId is long ob)
            {
                bomId = ob;
            }
            else
            {
                var activeBom = await _repo.FindActiveBomAsync(scope, order.ProductId)
                    ?? throw new ArgumentException("Chua co BOM active cho ma hang.");
                bomId = activeBom;
                await _repo.SetBomAsync(scope, id, bomId);
            }

            // 3) Cac dong NVL cua BOM.
            var items = await _repo.ListBomItemsAsync(scope, bomId);

            // 4) Voi moi NVL: tinh nhu cau (co hao hut) -> giu cho tren cac dong stock kha dung.
            foreach (var item in items)
            {
                var need = order.Quantity * item.QtyPerUnit * (1 + item.WastePct / 100m);
                var remaining = need;

                var rows = await _repo.LockAvailableStockAsync(scope, item.MaterialId);
                foreach (var row in rows)
                {
                    var available = row.QtyOnHand - row.QtyReserved;
                    var take = Math.Min(remaining, available);
                    if (take > 0)
                    {
                        await _repo.AddReservedAsync(scope, row.Id, take);
                        await _repo.UpsertReservationAsync(scope, id, item.MaterialId, row.WarehouseId, take);
                        remaining -= take;
                    }
                    if (remaining <= 0) break;
                }
                // remaining > 0: thieu hut -> khong bao loi (bao cao xu ly).
            }

            // 5) Doi trang thai don.
            await _repo.MarkConfirmedAsync(scope, id);
            return id;
        }, ct);

    /// <summary>
    /// Huy don + NHA giu cho. Trong 1 RunAsync: khoa cac dong stock cua phieu giu cho ACTIVE (FOR UPDATE) ->
    /// tru reserved + danh dau phieu RELEASED -> doi trang thai CANCELLED.
    /// Chi huy khi don o DRAFT hoac CONFIRMED.
    /// </summary>
    public Task CancelAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var order = await _repo.LockOrderAsync(scope, id)
                        ?? throw new ArgumentException($"Khong tim thay don hang id={id}.");
            if (order.Status is not ("DRAFT" or "CONFIRMED"))
                throw new ArgumentException("Chi huy duoc don o trang thai DRAFT hoac CONFIRMED.");

            // Khoa dong stock tuong ung roi nha tung phieu giu cho ACTIVE.
            var reservations = await _repo.LockActiveReservationsAsync(scope, id);
            foreach (var r in reservations)
            {
                await _repo.ReleaseStockAsync(scope, r.WarehouseId, r.MaterialId, r.QtyReserved);
                await _repo.MarkReservationReleasedAsync(scope, r.Id);
            }

            await _repo.MarkCancelledAsync(scope, id);
        }, ct);

    /// <summary>Sua don hang: chi don DRAFT (don da xac nhan dinh giu cho, phai huy truoc).</summary>
    public Task UpdateAsync(long id, UpdateProductionOrderRequest req, CancellationToken ct)
    {
        if (req.Quantity <= 0) throw new ArgumentException("quantity phai > 0.");
        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var order = await _repo.LockOrderAsync(scope, id)
                        ?? throw new ArgumentException($"Khong tim thay don hang id={id}.");
            if (order.Status != "DRAFT")
                throw new ArgumentException(
                    $"Chi sua duoc don o trang thai DRAFT (hien tai: {order.Status}). Don da xac nhan can huy de nha giu cho truoc.");
            await _repo.UpdateAsync(scope, id, req);
        }, ct);
    }

    /// <summary>Anh huong khi xoa don (phuc vu popup canh bao truoc khi xoa).</summary>
    public Task<OrderImpactDto?> GetImpactAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.GetImpactAsync(s, id), ct);

    /// <summary>
    /// Xoa don: chi DRAFT/CANCELLED va chua co phieu xuat NVL / phieu nhap TP / hao hut.
    /// Ke hoach + cong doan + giu cho xoa theo (cascade); giu cho ACTIVE (neu con) duoc nha ton truoc.
    /// </summary>
    public Task DeleteAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var order = await _repo.LockOrderAsync(scope, id)
                        ?? throw new ArgumentException($"Khong tim thay don hang id={id}.");
            var impact = await _repo.GetImpactAsync(scope, id)
                         ?? throw new ArgumentException($"Khong tim thay don hang id={id}.");
            if (!impact.CanDelete)
                throw new ArgumentException(
                    impact.IssueCount > 0 || impact.ReceiptCount > 0 || impact.LossReportCount > 0
                        ? "Don da co phieu xuat NVL/nhap TP/hao hut — khong the xoa (chi huy)."
                        : $"Chi xoa duoc don DRAFT/CANCELLED (hien tai: {order.Status}). Hay huy don truoc.");
            // Nha giu cho ACTIVE con sot (neu co) de tra ton kho truoc khi cascade xoa phieu giu cho.
            var reservations = await _repo.LockActiveReservationsAsync(scope, id);
            foreach (var r in reservations)
            {
                await _repo.ReleaseStockAsync(scope, r.WarehouseId, r.MaterialId, r.QtyReserved);
                await _repo.MarkReservationReleasedAsync(scope, r.Id);
            }
            await _repo.DeleteAsync(scope, id);
        }, ct);
}
