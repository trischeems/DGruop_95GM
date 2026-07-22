using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Repositories;
using DGroup.Server.Infrastructure.Data;
using DGroup.Server.Infrastructure.Tenancy;

namespace DGroup.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Nghiep vu ton kho. Nhap kho (RECEIPT) lam trong 1 transaction:
///   khoa dong stock (FOR UPDATE) -> cong on_hand -> ghi so cai stock_transactions.
/// Chong race khi 500+ user nhap dong thoi.
/// </summary>
public sealed class StockService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IStockRepository _repo;

    public StockService(ITenantConnection db, ITenantContext tenant, IStockRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    public Task<IEnumerable<MaterialStockDto>> ListStockAsync(bool lowStockOnly, int? year, int? month, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListStockAsync(s, lowStockOnly, year, month), ct);

    public Task<StockTransactionResult> ReceiveAsync(ReceiveStockRequest req, CancellationToken ct)
    {
        if (req.Quantity <= 0) throw new ArgumentException("So luong nhap phai > 0.");
        if (req.UnitCost < 0) throw new ArgumentException("Don gia khong duoc am.");
        if (req.WarehouseId <= 0 || req.MaterialId <= 0) throw new ArgumentException("Kho/NVL khong hop le.");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // 1) Bao dam co dong stock, roi khoa dong (FOR UPDATE) trong transaction nay.
            await _repo.EnsureStockRowAsync(scope, req.WarehouseId, req.MaterialId);
            await _repo.LockOnHandAsync(scope, req.WarehouseId, req.MaterialId);

            // 2) Cong ton on_hand -> lay ton sau khi cong.
            var balanceAfter = await _repo.AddOnHandAsync(scope, req.WarehouseId, req.MaterialId, req.Quantity);

            // 3) Ghi so cai giao dich kho (quantity duong khi nhap).
            var txnId = await _repo.InsertTransactionAsync(
                scope, req.WarehouseId, req.MaterialId, "RECEIPT",
                req.Quantity, req.UnitCost, balanceAfter, "RECEIPT", null, req.Note);

            return new StockTransactionResult(txnId, req.MaterialId, req.WarehouseId, balanceAfter);
        }, ct);
    }
}
