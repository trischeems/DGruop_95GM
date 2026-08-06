using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Repositories;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Tenancy;

namespace GM95.Server.Apps.ManagerPerformance.Services;

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

    /// <summary>Cac dong (ma hang + SL) cua don.</summary>
    public Task<IEnumerable<ProductionOrderItemDto>> ListItemsAsync(long orderId, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListItemsAsync(s, orderId), ct);

    /// <summary>Tao don MOI voi 1 hoac nhieu dong ma hang (ban nhap DRAFT).</summary>
    public Task<long> CreateAsync(CreateProductionOrderRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.OrderNo)) throw new ArgumentException("orderNo khong duoc rong.");
        var items = req.Items?.ToList() ?? new List<OrderItemInput>();
        if (items.Count == 0) throw new ArgumentException("Don phai co it nhat 1 mat hang.");
        foreach (var it in items) ValidateItem(it.ProductId, it.Quantity);
        // Chan trung ma hang trong cung don (moi ma hang 1 dong, sua SL thay vi them dong moi).
        var dup = items.GroupBy(i => i.ProductId).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null)
            throw new ArgumentException($"Ma hang id={dup.Key} bi lap trong don. Moi ma hang chi 1 dong.");

        return _db.RunAsync(_tenant.Tenant, async s =>
        {
            var orderId = await _repo.InsertAsync(s, req, items[0]);
            foreach (var it in items) await _repo.InsertItemAsync(s, orderId, it);
            return orderId;
        }, ct);
    }

    /// <summary>Them 1 mat hang vao don (chi khi don con DRAFT).</summary>
    public Task<long> AddItemAsync(long orderId, OrderItemInput item, CancellationToken ct)
    {
        ValidateItem(item.ProductId, item.Quantity);
        return _db.RunAsync(_tenant.Tenant, async s =>
        {
            await EnsureDraftAsync(s, orderId);
            return await _repo.InsertItemAsync(s, orderId, item);
        }, ct);
    }

    /// <summary>Sua 1 dong cua don (SL/BOM/quy trinh/ghi chu) — chi khi don con DRAFT.</summary>
    public Task<bool> UpdateItemAsync(long itemId, UpdateOrderItemRequest req, CancellationToken ct)
    {
        if (req.Quantity <= 0) throw new ArgumentException("So luong phai > 0.");
        return _db.RunAsync(_tenant.Tenant, async s =>
        {
            var orderId = await _repo.GetItemOrderIdAsync(s, itemId);
            if (orderId is null) return false;
            await EnsureDraftAsync(s, orderId.Value);
            return await _repo.UpdateItemAsync(s, itemId, req);
        }, ct);
    }

    /// <summary>Xoa 1 mat hang khoi don — chi khi don con DRAFT va con it nhat 1 dong khac.</summary>
    public Task<bool> DeleteItemAsync(long itemId, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async s =>
        {
            var orderId = await _repo.GetItemOrderIdAsync(s, itemId);
            if (orderId is null) return false;
            await EnsureDraftAsync(s, orderId.Value);
            if (await _repo.CountItemsAsync(s, orderId.Value) <= 1)
                throw new ArgumentException("Don phai con it nhat 1 mat hang. Muon bo han thi xoa ca don.");
            return await _repo.DeleteItemAsync(s, itemId);
        }, ct);

    private static void ValidateItem(long productId, decimal quantity)
    {
        if (productId <= 0) throw new ArgumentException("productId khong hop le.");
        if (quantity <= 0) throw new ArgumentException("So luong moi mat hang phai > 0.");
    }

    // Sua dong don sau khi da xac nhan se lam lech giu cho NVL -> bat huy don truoc.
    private async Task EnsureDraftAsync(TenantScope scope, long orderId)
    {
        var order = await _repo.LockOrderAsync(scope, orderId)
                    ?? throw new ArgumentException($"Khong tim thay don hang id={orderId}.");
        if (order.Status != "DRAFT")
            throw new ArgumentException(
                $"Chi sua duoc mat hang cua don o trang thai DRAFT (hien tai: {order.Status}). " +
                "Don da xac nhan can huy de nha giu cho truoc.");
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

            // 2) Duyet TUNG DONG (mat hang) cua don — moi dong co BOM + SL rieng.
            var lines = (await _repo.ListItemsForConfirmAsync(scope, id)).ToList();
            if (lines.Count == 0) throw new ArgumentException("Don chua co mat hang nao de xac nhan.");

            foreach (var line in lines)
            {
                // 2a) Chot BOM cua dong: uu tien bom_id da chon, khong co thi lay BOM ACTIVE cua ma hang.
                long bomId;
                if (line.BomId is long ob)
                {
                    bomId = ob;
                }
                else
                {
                    bomId = await _repo.FindActiveBomAsync(scope, line.ProductId)
                        ?? throw new ArgumentException(
                            $"Ma hang id={line.ProductId} chua co BOM active — khong the giu cho NVL cho don.");
                    await _repo.SetItemBomAsync(scope, line.Id, bomId);
                }

                // 2b) Voi moi NVL cua BOM: tinh nhu cau (co hao hut) -> giu cho tren cac dong stock kha dung.
                var bomItems = await _repo.ListBomItemsAsync(scope, bomId);
                foreach (var item in bomItems)
                {
                    var remaining = line.Quantity * item.QtyPerUnit * (1 + item.WastePct / 100m);

                    var rows = await _repo.LockAvailableStockAsync(scope, item.MaterialId);
                    foreach (var row in rows)
                    {
                        var available = row.QtyOnHand - row.QtyReserved;
                        var take = Math.Min(remaining, available);
                        if (take > 0)
                        {
                            await _repo.AddReservedAsync(scope, row.Id, take);
                            await _repo.UpsertReservationAsync(scope, id, line.Id, item.MaterialId, row.WarehouseId, take);
                            remaining -= take;
                        }
                        if (remaining <= 0) break;
                    }
                    // remaining > 0: thieu hut -> khong bao loi (bao cao xu ly).
                }
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

    /// <summary>Sua HEADER don (han giao/ghi chu): chi don DRAFT. SL nam o tung mat hang.</summary>
    public Task UpdateAsync(long id, UpdateProductionOrderRequest req, CancellationToken ct)
    {
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
