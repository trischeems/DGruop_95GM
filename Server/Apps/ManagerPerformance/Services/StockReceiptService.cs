using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Repositories;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Tenancy;

namespace GM95.Server.Apps.ManagerPerformance.Services;

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

    /// <summary>Lich su giao dich kho (don gia tung lan nhap/xuat), loc theo NVL + tu ngay/den ngay neu co.</summary>
    public Task<IEnumerable<StockTransactionDto>> ListTransactionsAsync(
        long? materialId, int limit, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var cap = limit is <= 0 or > 500 ? 200 : limit;   // gioi han so dong tra ve
        return _db.RunAsync(_tenant.Tenant,
            s => _repo.ListTransactionsAsync(s, materialId, cap, PeriodUtil.FromUtc(from), PeriodUtil.ToExclusiveUtc(to)), ct);
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

    /// <summary>
    /// SUA 1 dong so cai kho (so luong + don gia + ghi chu) trong 1 transaction:
    ///   khoa dong so cai -> khoa dong stock -> kiem tra ton moi -> cap nhat ton, so cai,
    ///   dong chung tu goc -> tinh lai balance_after.
    /// Chieu nhap/xuat GIU NGUYEN theo dau cu; Quantity gui len la so tuyet doi.
    /// Tra ve false neu khong co dong so cai id nay.
    /// </summary>
    public Task<bool> UpdateTransactionAsync(long id, UpdateStockTransactionRequest req, CancellationToken ct)
    {
        if (req.Quantity <= 0) throw new ArgumentException("So luong phai > 0.");
        if (req.UnitCost < 0) throw new ArgumentException("Don gia khong duoc am.");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var txn = await _repo.LockTransactionAsync(scope, id);
            if (txn is null) return false;

            var st = await _repo.LockStockAsync(scope, txn.WarehouseId, txn.MaterialId)
                     ?? throw new ArgumentException(
                         $"Khong tim thay dong ton kho cua NVL id={txn.MaterialId} tai kho id={txn.WarehouseId}.");

            // Giu nguyen CHIEU cua dong: dong nhap van duong, dong xuat van am.
            var signed = txn.Quantity < 0 ? -req.Quantity : req.Quantity;
            var delta = signed - txn.Quantity;
            var newOnHand = st.QtyOnHand + delta;
            EnsureStockValid(newOnHand, st.QtyOnHand, st.QtyReserved, "sua");

            await _repo.SetOnHandAsync(scope, txn.WarehouseId, txn.MaterialId, newOnHand);
            await _repo.UpdateTransactionAsync(scope, id, signed, req.UnitCost, req.Note);

            // Dong chung tu goc (phieu nhap / phieu xuat) phai khop lai voi so cai.
            if (txn.RefType is not null && txn.RefId is not null)
                await _repo.UpdateParentLineAsync(
                    scope, txn.RefType, txn.RefId.Value, txn.MaterialId, req.Quantity, req.UnitCost);

            await _repo.RecomputeBalancesAsync(scope, txn.WarehouseId, txn.MaterialId, newOnHand);
            return true;
        }, ct);
    }

    /// <summary>
    /// XOA 1 dong so cai kho trong 1 transaction: khoa dong so cai -> khoa dong stock ->
    /// tra ton ve nhu chua co dong nay (tru quantity CO DAU) -> xoa dong chung tu goc
    /// (va header khi phieu khong con dong nao) -> xoa dong so cai -> tinh lai balance_after.
    /// Tra ve false neu khong co dong so cai id nay.
    /// </summary>
    public Task<bool> DeleteTransactionAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var txn = await _repo.LockTransactionAsync(scope, id);
            if (txn is null) return false;

            var st = await _repo.LockStockAsync(scope, txn.WarehouseId, txn.MaterialId)
                     ?? throw new ArgumentException(
                         $"Khong tim thay dong ton kho cua NVL id={txn.MaterialId} tai kho id={txn.WarehouseId}.");

            // quantity CO DAU -> xoa dong NHAP thi tru ton, xoa dong XUAT thi cong ton tra lai.
            var newOnHand = st.QtyOnHand - txn.Quantity;
            EnsureStockValid(newOnHand, st.QtyOnHand, st.QtyReserved, "xoa");

            await _repo.SetOnHandAsync(scope, txn.WarehouseId, txn.MaterialId, newOnHand);

            // Xoa dong NVL tuong ung trong phieu goc; phieu rong (het dong) thi xoa luon header.
            if (txn.RefType is not null && txn.RefId is not null)
            {
                await _repo.DeleteParentLineAsync(scope, txn.RefType, txn.RefId.Value, txn.MaterialId);
                await _repo.DeleteParentHeaderIfEmptyAsync(scope, txn.RefType, txn.RefId.Value);
            }

            await _repo.DeleteTransactionAsync(scope, id);
            await _repo.RecomputeBalancesAsync(scope, txn.WarehouseId, txn.MaterialId, newOnHand);
            return true;
        }, ct);

    /// <summary>Chan ton am / ton it hon so da giu cho khi sua-xoa (bao kem con so cho de hieu).</summary>
    private static void EnsureStockValid(decimal newOnHand, decimal oldOnHand, decimal reserved, string action)
    {
        if (newOnHand < 0)
            throw new ArgumentException(
                $"Khong the {action} dong nay: ton kho se bi am " +
                $"(ton hien tai {oldOnHand:0.####} -> {newOnHand:0.####}).");
        if (newOnHand < reserved)
            throw new ArgumentException(
                $"Khong the {action} dong nay: ton con lai {newOnHand:0.####} nho hon so da giu cho " +
                $"{reserved:0.####} (ton hien tai {oldOnHand:0.####}).");
    }
}
