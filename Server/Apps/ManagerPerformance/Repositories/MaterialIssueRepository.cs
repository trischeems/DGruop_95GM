using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

public sealed class MaterialIssueRepository : IMaterialIssueRepository
{
    public Task<IEnumerable<MaterialIssueDto>> ListAsync(TenantScope scope, long? orderId)
    {
        var sql = "SELECT id, issue_no, production_order_id, warehouse_id, status, issued_at " +
                  "FROM material_issues" +
                  (orderId.HasValue ? " WHERE production_order_id = @orderId" : "") +
                  " ORDER BY issued_at DESC";
        return scope.QueryAsync<MaterialIssueDto>(sql, new { orderId });
    }

    public async Task<(long Id, string IssueNo)> InsertIssueAsync(
        TenantScope scope, long productionOrderId, long warehouseId, string? note)
    {
        // issue_no sinh trong SQL: 'PX-' + moc thoi gian (den mili giay).
        var row = await scope.QuerySingleAsync<IssueRow>(
            """
            INSERT INTO material_issues (issue_no, production_order_id, warehouse_id, status, note)
            VALUES ('PX-' || to_char(now(), 'YYYYMMDDHH24MISSMS'), @productionOrderId, @warehouseId, 'POSTED', @note)
            RETURNING id, issue_no
            """, new { productionOrderId, warehouseId, note });
        return (row.Id, row.IssueNo);
    }

    public async Task<(decimal QtyOnHand, decimal QtyReserved)?> LockStockAsync(
        TenantScope scope, long warehouseId, long materialId)
    {
        // FOR UPDATE: khoa dong stock chong race khi 500+ user xuat dong thoi.
        var row = await scope.QueryFirstOrDefaultAsync<StockRow?>(
            "SELECT qty_on_hand, qty_reserved FROM stock WHERE warehouse_id = @warehouseId AND material_id = @materialId FOR UPDATE",
            new { warehouseId, materialId });
        return row is null ? null : (row.QtyOnHand, row.QtyReserved);
    }

    public Task SetStockAsync(
        TenantScope scope, long warehouseId, long materialId, decimal newOnHand, decimal newReserved) =>
        scope.ExecuteAsync(
            """
            UPDATE stock SET qty_on_hand = @newOnHand, qty_reserved = @newReserved
            WHERE warehouse_id = @warehouseId AND material_id = @materialId
            """, new { warehouseId, materialId, newOnHand, newReserved });

    public Task InsertItemAsync(
        TenantScope scope, long materialIssueId, long materialId, decimal qtyIssued, decimal unitCost) =>
        scope.ExecuteAsync(
            """
            INSERT INTO material_issue_items (material_issue_id, material_id, qty_issued, unit_cost)
            VALUES (@materialIssueId, @materialId, @qtyIssued, @unitCost)
            """, new { materialIssueId, materialId, qtyIssued, unitCost });

    public Task<long> InsertTransactionAsync(
        TenantScope scope, long warehouseId, long materialId, string txnType,
        decimal quantity, decimal unitCost, decimal balanceAfter, string? refType, long? refId, string? note) =>
        scope.QuerySingleAsync<long>(
            """
            INSERT INTO stock_transactions
                (warehouse_id, material_id, txn_type, quantity, unit_cost, balance_after, ref_type, ref_id, note)
            VALUES (@warehouseId, @materialId, @txnType, @quantity, @unitCost, @balanceAfter, @refType, @refId, @note)
            RETURNING id
            """, new { warehouseId, materialId, txnType, quantity, unitCost, balanceAfter, refType, refId, note });

    public Task<int> ConsumeReservationsAsync(TenantScope scope, long productionOrderId) =>
        scope.ExecuteAsync(
            """
            UPDATE material_reservations SET status = 'CONSUMED', updated_at = now()
            WHERE production_order_id = @productionOrderId AND status = 'ACTIVE'
            """, new { productionOrderId });

    public Task<int> MarkOrderInProgressAsync(TenantScope scope, long productionOrderId) =>
        scope.ExecuteAsync(
            "UPDATE production_orders SET status = 'IN_PROGRESS' WHERE id = @productionOrderId AND status = 'CONFIRMED'",
            new { productionOrderId });

    /// <summary>Dong ton kho doc len de tru (khop cot snake_case cua bang stock).</summary>
    private sealed record StockRow(decimal QtyOnHand, decimal QtyReserved);

    /// <summary>Dong tra ve khi tao phieu (khop cot id, issue_no).</summary>
    private sealed record IssueRow(long Id, string IssueNo);
}
