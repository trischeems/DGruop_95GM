using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

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

    /// <summary>Lich su giao dich kho (loc theo NVL neu co), moi nhat truoc — de xem don gia tung lan nhap/xuat.</summary>
    Task<IEnumerable<StockTransactionDto>> ListTransactionsAsync(TenantScope scope, long? materialId, int limit);
}
