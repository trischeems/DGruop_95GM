using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

/// <summary>
/// 1 dong so cai kho da khoa (FOR UPDATE) phuc vu SUA/XOA: du thong tin de dong bo lai
/// ton kho + dong chung tu goc (Quantity CO DAU: duong = nhap, am = xuat).
/// </summary>
public sealed record StockTxnLockRow(
    long WarehouseId, long MaterialId, decimal Quantity, string? RefType, long? RefId);

/// <summary>
/// Truy van/ghi phieu NHAP kho nhieu dong (stock_receipts + stock_receipt_items).
/// Phan cap nhat TON (cong on_hand, ghi so cai) dung lai IStockRepository co san.
/// </summary>
public interface IStockReceiptRepository
{
    /// <summary>Danh sach phieu nhap (loc theo kho neu co), moi nhat truoc.</summary>
    Task<IEnumerable<StockReceiptDto>> ListAsync(TenantScope scope, long? warehouseId);

    /// <summary>Tao header phieu nhap (receipt_no sinh trong SQL 'PN-'+timestamp). Tra ve id + so phieu.</summary>
    Task<(long Id, string ReceiptNo)> InsertReceiptAsync(TenantScope scope, long warehouseId, string? note);

    /// <summary>Them 1 dong NVL vao phieu nhap.</summary>
    Task InsertItemAsync(TenantScope scope, long receiptId, long materialId, decimal qtyReceived, decimal unitCost);

    /// <summary>
    /// Lich su giao dich kho (loc theo NVL neu co + khoang thoi gian UTC [fromUtc, toExclusiveUtc)),
    /// moi nhat truoc — de xem don gia tung lan nhap/xuat.
    /// </summary>
    Task<IEnumerable<StockTransactionDto>> ListTransactionsAsync(
        TenantScope scope, long? materialId, int limit, DateTime fromUtc, DateTime toExclusiveUtc);

    // ----- SUA / XOA 1 dong so cai (thu tu khoa: dong so cai -> dong stock) -----

    /// <summary>Khoa dong so cai (SELECT ... FOR UPDATE). Null neu khong co dong id nay.</summary>
    Task<StockTxnLockRow?> LockTransactionAsync(TenantScope scope, long id);

    /// <summary>Khoa dong stock (FOR UPDATE), tra ve (on_hand, reserved). Null neu chua co dong ton.</summary>
    Task<(decimal QtyOnHand, decimal QtyReserved)?> LockStockAsync(
        TenantScope scope, long warehouseId, long materialId);

    /// <summary>Ghi lai ton on_hand sau khi sua/xoa 1 dong so cai.</summary>
    Task SetOnHandAsync(TenantScope scope, long warehouseId, long materialId, decimal newOnHand);

    /// <summary>Sua so luong (CO DAU) / don gia / ghi chu cua 1 dong so cai. Tra ve so dong cap nhat.</summary>
    Task<int> UpdateTransactionAsync(
        TenantScope scope, long id, decimal quantity, decimal unitCost, string? note);

    /// <summary>Xoa 1 dong so cai. Tra ve so dong bi xoa.</summary>
    Task<int> DeleteTransactionAsync(TenantScope scope, long id);

    /// <summary>
    /// Sua dong NVL cua chung tu goc cho khop so cai: RECEIPT -> stock_receipt_items.qty_received,
    /// MATERIAL_ISSUE -> material_issue_items.qty_issued. quantity la so TUYET DOI (> 0).
    /// Bo qua (0 dong) khi ref_type khong thuoc 2 loai tren.
    /// </summary>
    Task<int> UpdateParentLineAsync(
        TenantScope scope, string refType, long refId, long materialId, decimal quantity, decimal unitCost);

    /// <summary>Xoa dong NVL cua chung tu goc theo ref_type. Bo qua (0 dong) khi ref_type khac.</summary>
    Task<int> DeleteParentLineAsync(TenantScope scope, string refType, long refId, long materialId);

    /// <summary>Xoa header chung tu goc khi da het dong NVL. Bo qua (0 dong) khi ref_type khac.</summary>
    Task<int> DeleteParentHeaderIfEmptyAsync(TenantScope scope, string refType, long refId);

    /// <summary>
    /// Tinh lai balance_after cho toan bo so cai cua 1 (kho, NVL) sau khi sua/xoa,
    /// neo dong cuoi cung bang ton hien tai (onHand). Tra ve so dong cap nhat.
    /// </summary>
    Task<int> RecomputeBalancesAsync(TenantScope scope, long warehouseId, long materialId, decimal onHand);
}
