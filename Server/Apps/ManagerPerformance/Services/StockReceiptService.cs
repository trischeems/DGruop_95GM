using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Repositories;
using DGroup.Server.Infrastructure.Data;
using DGroup.Server.Infrastructure.Tenancy;

namespace DGroup.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Nghiep vu NHAP kho NVL nhieu dong. Tao phieu + cong ton cho TAT CA dong trong 1 transaction:
///   INSERT header -> voi moi NVL: bao dam co dong stock -> khoa FOR UPDATE -> CONG on_hand
///   -> ghi dong phieu + so cai (RECEIPT, quantity duong, ref tro ve phieu).
/// KHONG dung qty_reserved, KHONG gan don san xuat. Chong race khi 500+ user nhap dong thoi.
/// </summary>
public sealed class StockReceiptService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IStockReceiptRepository _repo;
    private readonly IStockRepository _stock;   // dung lai ensure/lock/add/insert-txn co san

    public StockReceiptService(
        ITenantConnection db, ITenantContext tenant,
        IStockReceiptRepository repo, IStockRepository stock)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
        _stock = stock;
    }

    public Task<IEnumerable<StockReceiptDto>> ListAsync(long? warehouseId, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListAsync(s, warehouseId), ct);

    /// <summary>Lich su giao dich kho (don gia tung lan nhap/xuat), loc theo NVL neu co.</summary>
    public Task<IEnumerable<StockTransactionDto>> ListTransactionsAsync(long? materialId, int limit, CancellationToken ct)
    {
        var cap = limit is <= 0 or > 500 ? 200 : limit;   // gioi han so dong tra ve
        return _db.RunAsync(_tenant.Tenant, s => _repo.ListTransactionsAsync(s, materialId, cap), ct);
    }

    public Task<StockReceiptResultDto> CreateAsync(CreateStockReceiptRequest req, CancellationToken ct)
    {
        if (req.WarehouseId <= 0) throw new ArgumentException("Kho khong hop le.");
        var items = req.Items?.ToList() ?? new List<StockReceiptItemInput>();
        if (items.Count == 0) throw new ArgumentException("Phieu nhap phai co it nhat 1 dong NVL.");
        foreach (var it in items)
        {
            if (it.MaterialId <= 0) throw new ArgumentException("NVL khong hop le.");
            if (it.QtyReceived <= 0) throw new ArgumentException("So luong nhap phai > 0.");
            if (it.UnitCost < 0) throw new ArgumentException("Don gia khong duoc am.");
        }
        // Chan trung NVL trong cung phieu (uq_stock_receipt_item se chan, nhung bao som cho ro).
        var dup = items.GroupBy(i => i.MaterialId).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null)
            throw new ArgumentException($"NVL id={dup.Key} bi lap trong phieu. Moi NVL chi 1 dong.");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // 1) Tao header phieu nhap (receipt_no sinh trong SQL, status POSTED).
            var (receiptId, receiptNo) = await _repo.InsertReceiptAsync(scope, req.WarehouseId, req.Note);

            // 2) Voi moi NVL: bao dam co dong stock -> khoa -> CONG on_hand -> ghi dong phieu + so cai.
            foreach (var it in items)
            {
                await _stock.EnsureStockRowAsync(scope, req.WarehouseId, it.MaterialId);
                await _stock.LockOnHandAsync(scope, req.WarehouseId, it.MaterialId);  // FOR UPDATE
                var balanceAfter = await _stock.AddOnHandAsync(scope, req.WarehouseId, it.MaterialId, it.QtyReceived);

                await _repo.InsertItemAsync(scope, receiptId, it.MaterialId, it.QtyReceived, it.UnitCost);

                // So cai nhap kho: quantity DUONG, ref tro ve phieu nhap.
                await _stock.InsertTransactionAsync(
                    scope, req.WarehouseId, it.MaterialId, "RECEIPT",
                    it.QtyReceived, it.UnitCost, balanceAfter, "RECEIPT", receiptId, req.Note);
            }

            return new StockReceiptResultDto(receiptId, receiptNo, items.Count);
        }, ct);
    }
}
