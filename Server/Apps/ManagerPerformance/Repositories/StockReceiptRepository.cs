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

    // ----- SUA / XOA 1 dong so cai (thu tu khoa: dong so cai -> dong stock) -----

    public Task<StockTxnLockRow?> LockTransactionAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<StockTxnLockRow?>(
            // FOR UPDATE: khoa dong so cai truoc, roi moi khoa dong stock (thu tu co dinh, chong deadlock).
            "SELECT warehouse_id, material_id, quantity, ref_type, ref_id " +
            "FROM stock_transactions WHERE id = @id FOR UPDATE",
            new { id });

    public async Task<(decimal QtyOnHand, decimal QtyReserved)?> LockStockAsync(
        TenantScope scope, long warehouseId, long materialId)
    {
        // FOR UPDATE: khoa dong stock chong race khi 500+ user nhap/xuat/sua dong thoi.
        var row = await scope.QueryFirstOrDefaultAsync<StockRow?>(
            "SELECT qty_on_hand, qty_reserved FROM stock WHERE warehouse_id = @warehouseId AND material_id = @materialId FOR UPDATE",
            new { warehouseId, materialId });
        return row is null ? null : (row.QtyOnHand, row.QtyReserved);
    }

    public Task SetOnHandAsync(TenantScope scope, long warehouseId, long materialId, decimal newOnHand) =>
        scope.ExecuteAsync(
            """
            UPDATE stock SET qty_on_hand = @newOnHand, updated_at = now()
            WHERE warehouse_id = @warehouseId AND material_id = @materialId
            """, new { warehouseId, materialId, newOnHand });

    public Task<int> UpdateTransactionAsync(
        TenantScope scope, long id, decimal quantity, decimal unitCost, string? note) =>
        scope.ExecuteAsync(
            """
            UPDATE stock_transactions
            SET quantity = @quantity, unit_cost = @unitCost, note = COALESCE(@note, note)
            WHERE id = @id
            """, new { id, quantity, unitCost, note });

    public Task<int> DeleteTransactionAsync(TenantScope scope, long id) =>
        scope.ExecuteAsync("DELETE FROM stock_transactions WHERE id = @id", new { id });

    public Task<int> UpdateParentLineAsync(
        TenantScope scope, string refType, long refId, long materialId, decimal quantity, decimal unitCost)
    {
        // Chung tu goc luu so luong TUYET DOI (qty_received / qty_issued deu > 0).
        string? sql = refType switch
        {
            "RECEIPT" =>
                "UPDATE stock_receipt_items SET qty_received = @quantity, unit_cost = @unitCost " +
                "WHERE stock_receipt_id = @refId AND material_id = @materialId",
            "MATERIAL_ISSUE" =>
                "UPDATE material_issue_items SET qty_issued = @quantity, unit_cost = @unitCost " +
                "WHERE material_issue_id = @refId AND material_id = @materialId",
            _ => null,
        };
        return sql is null ? Task.FromResult(0) : scope.ExecuteAsync(sql, new { refId, materialId, quantity, unitCost });
    }

    public Task<int> DeleteParentLineAsync(TenantScope scope, string refType, long refId, long materialId)
    {
        string? sql = refType switch
        {
            "RECEIPT" =>
                "DELETE FROM stock_receipt_items WHERE stock_receipt_id = @refId AND material_id = @materialId",
            "MATERIAL_ISSUE" =>
                "DELETE FROM material_issue_items WHERE material_issue_id = @refId AND material_id = @materialId",
            _ => null,
        };
        return sql is null ? Task.FromResult(0) : scope.ExecuteAsync(sql, new { refId, materialId });
    }

    public Task<int> DeleteParentHeaderIfEmptyAsync(TenantScope scope, string refType, long refId)
    {
        // Chi xoa header khi khong con dong NVL nao (phieu rong thi khong con y nghia).
        string? sql = refType switch
        {
            "RECEIPT" =>
                "DELETE FROM stock_receipts WHERE id = @refId " +
                "AND NOT EXISTS (SELECT 1 FROM stock_receipt_items WHERE stock_receipt_id = @refId)",
            "MATERIAL_ISSUE" =>
                "DELETE FROM material_issues WHERE id = @refId " +
                "AND NOT EXISTS (SELECT 1 FROM material_issue_items WHERE material_issue_id = @refId)",
            _ => null,
        };
        return sql is null ? Task.FromResult(0) : scope.ExecuteAsync(sql, new { refId });
    }

    public Task<int> RecomputeBalancesAsync(TenantScope scope, long warehouseId, long materialId, decimal onHand) =>
        scope.ExecuteAsync(
            // Cong don quantity theo (created_at, id) de balance_after chay lai dung sau khi sua/xoa.
            // NEO theo ton hien tai: cong (onHand - tong quantity) de dong CUOI luon = ton thuc te
            // (so cai co the khong bat dau tu 0 neu ton mo dau duoc nap san). GREATEST(...,0) giu
            // rang buoc ck balance_after >= 0. Chi dung 1 (kho, NVL) -> dung index ix_stxn_material_time.
            """
            WITH running AS (
                SELECT id,
                       GREATEST(SUM(quantity) OVER (ORDER BY created_at, id ROWS UNBOUNDED PRECEDING)
                                + (@onHand - SUM(quantity) OVER ()), 0) AS balance
                FROM stock_transactions
                WHERE warehouse_id = @warehouseId AND material_id = @materialId
            )
            UPDATE stock_transactions t SET balance_after = r.balance
            FROM running r
            WHERE t.id = r.id AND t.balance_after <> r.balance
            """, new { warehouseId, materialId, onHand });

    private sealed record ReceiptRow(long Id, string ReceiptNo);

    /// <summary>Dong ton kho doc len khi sua/xoa so cai (khop cot snake_case cua bang stock).</summary>
    private sealed record StockRow(decimal QtyOnHand, decimal QtyReserved);
}
