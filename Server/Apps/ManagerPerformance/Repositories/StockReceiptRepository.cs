using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

public sealed class StockReceiptRepository : IStockReceiptRepository
{
    public Task<IEnumerable<StockReceiptDto>> ListAsync(TenantScope scope, long? warehouseId)
    {
        // JOIN warehouses de tra kem ten kho canh WarehouseId.
        var sql =
            "SELECT r.id, r.receipt_no, r.warehouse_id, w.name AS warehouse_name, r.status, r.received_at, r.note " +
            "FROM stock_receipts r LEFT JOIN warehouses w ON w.id = r.warehouse_id" +
            (warehouseId.HasValue ? " WHERE r.warehouse_id = @warehouseId" : "") +
            " ORDER BY r.received_at DESC";
        return scope.QueryAsync<StockReceiptDto>(sql, new { warehouseId });
    }

    public async Task<(long Id, string ReceiptNo)> InsertReceiptAsync(TenantScope scope, long warehouseId, string? note)
    {
        // receipt_no sinh trong SQL: 'PN-' + moc thoi gian (den mili giay).
        var row = await scope.QuerySingleAsync<ReceiptRow>(
            """
            INSERT INTO stock_receipts (receipt_no, warehouse_id, status, note)
            VALUES ('PN-' || to_char(now(), 'YYYYMMDDHH24MISSMS'), @warehouseId, 'POSTED', @note)
            RETURNING id, receipt_no
            """, new { warehouseId, note });
        return (row.Id, row.ReceiptNo);
    }

    public Task InsertItemAsync(TenantScope scope, long receiptId, long materialId, decimal qtyReceived, decimal unitCost) =>
        scope.ExecuteAsync(
            """
            INSERT INTO stock_receipt_items (stock_receipt_id, material_id, qty_received, unit_cost)
            VALUES (@receiptId, @materialId, @qtyReceived, @unitCost)
            """, new { receiptId, materialId, qtyReceived, unitCost });

    public Task<IEnumerable<StockTransactionDto>> ListTransactionsAsync(
        TenantScope scope, long? materialId, int limit, DateTime fromUtc, DateTime toExclusiveUtc)
    {
        // Lich su so cai + ten NVL/kho join san. Dung index ix_stxn_material_time.
        // Loc thoi gian dang range [from, to) de sargable.
        var sql =
            "SELECT t.id, t.material_id, m.sku AS material_sku, m.name AS material_name, " +
            "u.code AS material_uom_code, u.name AS material_uom_name, " +
            "t.warehouse_id, w.name AS warehouse_name, t.txn_type, t.quantity, t.unit_cost, " +
            "t.balance_after, t.ref_type, t.ref_id, t.note, t.created_at " +
            "FROM stock_transactions t " +
            "LEFT JOIN materials m ON m.id = t.material_id " +
            "LEFT JOIN units_of_measure u ON u.id = m.uom_id " +
            "LEFT JOIN warehouses w ON w.id = t.warehouse_id" +
            " WHERE t.created_at >= @fromUtc AND t.created_at < @toExclusiveUtc" +
            (materialId.HasValue ? " AND t.material_id = @materialId" : "") +
            " ORDER BY t.created_at DESC, t.id DESC LIMIT @limit";
        return scope.QueryAsync<StockTransactionDto>(sql, new { materialId, limit, fromUtc, toExclusiveUtc });
    }

    private sealed record ReceiptRow(long Id, string ReceiptNo);
}
