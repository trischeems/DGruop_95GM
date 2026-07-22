using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

public sealed class FinishedGoodsRepository : IFinishedGoodsRepository
{
    public Task<IEnumerable<FinishedGoodsReceiptDto>> ListAsync(TenantScope scope, long? orderId, int? year, int? month)
    {
        // JOIN products/production_orders/warehouses de tra kem ten canh cac cot ID.
        var sql = "SELECT r.id, r.receipt_no, r.production_order_id, po.order_no, " +
                  "r.product_id, p.sku AS product_sku, p.name AS product_name, " +
                  "r.warehouse_id, w.name AS warehouse_name, " +
                  "r.qty_received, r.received_at " +
                  "FROM finished_goods_receipts r " +
                  "LEFT JOIN products p ON p.id = r.product_id " +
                  "LEFT JOIN production_orders po ON po.id = r.production_order_id " +
                  "LEFT JOIN warehouses w ON w.id = r.warehouse_id " +
                  "WHERE " + PeriodWhere("r.received_at") +
                  (orderId.HasValue ? " AND r.production_order_id = @OrderId" : "") +
                  " ORDER BY r.received_at DESC";
        return scope.QueryAsync<FinishedGoodsReceiptDto>(sql, new { OrderId = orderId, year, month });
    }
    // Dieu kien loc theo thang/nam tren cot ngay (@year/@month null = khong loc).
    private static string PeriodWhere(string col) =>
        $"(@year IS NULL OR EXTRACT(YEAR FROM {col}) = @year) AND (@month IS NULL OR EXTRACT(MONTH FROM {col}) = @month)";

    public Task<long> InsertAsync(TenantScope scope, CreateFinishedGoodsRequest req) =>
        scope.QuerySingleAsync<long>(
            """
            INSERT INTO finished_goods_receipts
                (receipt_no, production_order_id, product_id, warehouse_id, qty_received, note, created_by)
            VALUES
                ('NTP-' || to_char(now(), 'YYYYMMDDHH24MISSMS'),
                 @ProductionOrderId, @ProductId, @WarehouseId, @QtyReceived, @Note, NULL)
            RETURNING id
            """,
            new
            {
                req.ProductionOrderId,
                req.ProductId,
                req.WarehouseId,
                req.QtyReceived,
                req.Note,
            });

    public Task<int> MarkOrderCompletedAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            """
            UPDATE production_orders
            SET status = 'COMPLETED', completed_at = now()
            WHERE id = @OrderId AND status IN ('CONFIRMED', 'IN_PROGRESS')
            """,
            new { OrderId = orderId });

    public Task<FinishedGoodsImpactDto?> GetImpactAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<FinishedGoodsImpactDto>(
            """
            SELECT r.receipt_no, r.qty_received, po.status AS order_status,
                   (SELECT count(*) FROM finished_goods_receipts WHERE production_order_id = po.id) AS order_receipt_count,
                   (SELECT count(*) FROM loss_reports WHERE production_order_id = po.id)            AS loss_report_count,
                   (po.status = 'COMPLETED'
                    AND (SELECT count(*) FROM finished_goods_receipts WHERE production_order_id = po.id) = 1) AS will_revert_order
            FROM finished_goods_receipts r
            JOIN production_orders po ON po.id = r.production_order_id
            WHERE r.id = @id
            """, new { id });

    public Task<long?> GetOrderIdAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<long?>(
            "SELECT production_order_id FROM finished_goods_receipts WHERE id = @id", new { id });

    public async Task<bool> DeleteAsync(TenantScope scope, long id) =>
        await scope.ExecuteAsync("DELETE FROM finished_goods_receipts WHERE id = @id", new { id }) > 0;

    public Task<long> CountByOrderAsync(TenantScope scope, long orderId) =>
        scope.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM finished_goods_receipts WHERE production_order_id = @orderId", new { orderId });

    public Task<int> RevertOrderToInProgressAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            "UPDATE production_orders SET status = 'IN_PROGRESS' WHERE id = @orderId AND status = 'COMPLETED'",
            new { orderId });
}
