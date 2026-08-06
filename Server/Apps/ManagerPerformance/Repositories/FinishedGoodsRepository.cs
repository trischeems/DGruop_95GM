using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

public sealed class FinishedGoodsRepository : IFinishedGoodsRepository
{
    public Task<IEnumerable<FinishedGoodsReceiptDto>> ListAsync(TenantScope scope, long? orderId, int? year, int? month)
    {
        // JOIN products/production_orders/warehouses de tra kem ten canh cac cot ID.
        var sql = "SELECT r.id, r.receipt_no, r.production_order_id, po.order_no, " +
                  "r.production_order_item_id, COALESCE(i.line_no, 1) AS line_no, " +
                  "r.product_id, p.sku AS product_sku, p.name AS product_name, " +
                  "pu.code AS product_uom_code, pu.name AS product_uom_name, " +
                  "r.warehouse_id, w.name AS warehouse_name, " +
                  "r.qty_received, r.received_at " +
                  "FROM finished_goods_receipts r " +
                  "LEFT JOIN products p ON p.id = r.product_id " +
                  "LEFT JOIN units_of_measure pu ON pu.id = p.uom_id " +
                  "LEFT JOIN production_orders po ON po.id = r.production_order_id " +
                  "LEFT JOIN production_order_items i ON i.id = r.production_order_item_id " +
                  "LEFT JOIN warehouses w ON w.id = r.warehouse_id " +
                  "WHERE " + PeriodWhere("r.received_at") +
                  (orderId.HasValue ? " AND r.production_order_id = @OrderId" : "") +
                  " ORDER BY r.received_at DESC";
        return scope.QueryAsync<FinishedGoodsReceiptDto>(sql, new { OrderId = orderId, year, month });
    }
    // Dieu kien loc theo thang/nam tren cot ngay (@year/@month null = khong loc).
    private static string PeriodWhere(string col) =>
        $"(@year IS NULL OR EXTRACT(YEAR FROM {col}) = @year) AND (@month IS NULL OR EXTRACT(MONTH FROM {col}) = @month)";

    // Khoa don + tong hop du lieu de kiem tra truoc khi nhap kho TP:
    //   - status/product_id cua don (FOR UPDATE chong race).
    //   - receivable_limit: TRAN duoc phep nhap. COALESCE 3 muc (quy trinh V006 la tu do,
    //     khong bat buoc co cong doan QC nen khong the ep buoc QC lam can cu duy nhat):
    //       1) SL ra cua buoc QC neu buoc do ton tai, khong bo qua va khong bi huy;
    //       2) SL ra LON NHAT trong cac buoc phai lam cua don (buoc cuoi thuong cao nhat);
    //       3) SL dat cua don — khi don chua co buoc nao (MAX tren 0 dong tra NULL).
    //   - already_received: tong qty_received da nhap kho cua don.
    // Nhu tren nhung tinh cho RIENG 1 MAT HANG cua don (V007): moi mat hang co cong doan,
    // han muc nhap va so da nhap RIENG. Khoa ca dong don va dong mat hang (FOR UPDATE).
    public Task<OrderReceiptCheckRow?> LockItemForReceiptAsync(TenantScope scope, long orderItemId) =>
        scope.QueryFirstOrDefaultAsync<OrderReceiptCheckRow>(
            """
            SELECT po.status,
                   i.product_id                                                           AS product_id,
                   COALESCE(
                       (SELECT ps.qty_out FROM production_steps ps
                        JOIN production_stages st ON st.id = ps.stage_id
                        WHERE ps.production_order_item_id = i.id AND st.code = 'QC'
                          AND NOT ps.is_skipped AND ps.status <> 'CANCELLED'),
                       (SELECT MAX(ps.qty_out) FROM production_steps ps
                        WHERE ps.production_order_item_id = i.id
                          AND NOT ps.is_skipped AND ps.status <> 'CANCELLED'),
                       i.quantity)                                                         AS receivable_limit,
                   COALESCE((SELECT SUM(qty_received) FROM finished_goods_receipts
                             WHERE production_order_item_id = i.id), 0)                    AS already_received
            FROM production_order_items i
            JOIN production_orders po ON po.id = i.production_order_id
            WHERE i.id = @orderItemId
            FOR UPDATE OF po, i
            """, new { orderItemId });

    // TAT CA cac mat hang cua don da nhap du chua (de biet co dong don hay khong).
    // 1 mat hang duoc coi la XONG khi: co san luong (limit > 0) VA da nhap het san luong do.
    // Bat buoc limit > 0: mat hang chua chay cong doan nao co limit = 0, neu khong chan thi
    // "0 >= 0" se coi nhu da xong -> don bi dong som khi mat hang khac chua san xuat.
    public Task<bool> AreAllItemsReceivedAsync(TenantScope scope, long orderId) =>
        scope.ExecuteScalarAsync<bool>(
            """
            SELECT NOT EXISTS (
                SELECT 1
                FROM production_order_items i
                CROSS JOIN LATERAL (
                    SELECT COALESCE(
                        (SELECT ps.qty_out FROM production_steps ps
                         JOIN production_stages st ON st.id = ps.stage_id
                         WHERE ps.production_order_item_id = i.id AND st.code = 'QC'
                           AND NOT ps.is_skipped AND ps.status <> 'CANCELLED'),
                        (SELECT MAX(ps.qty_out) FROM production_steps ps
                         WHERE ps.production_order_item_id = i.id
                           AND NOT ps.is_skipped AND ps.status <> 'CANCELLED'),
                        i.quantity) AS lim,
                    COALESCE((SELECT SUM(f.qty_received) FROM finished_goods_receipts f
                              WHERE f.production_order_item_id = i.id), 0) AS got
                ) x
                WHERE i.production_order_id = @orderId
                  AND (x.lim <= 0 OR x.got < x.lim)
            )
            """, new { orderId });

    public Task<long> InsertAsync(TenantScope scope, CreateFinishedGoodsRequest req) =>
        scope.QuerySingleAsync<long>(
            """
            INSERT INTO finished_goods_receipts
                (receipt_no, production_order_id, production_order_item_id, product_id, warehouse_id,
                 qty_received, note, created_by)
            VALUES
                ('NTP-' || to_char(now(), 'YYYYMMDDHH24MISSMS'),
                 @ProductionOrderId, @ProductionOrderItemId, @ProductId, @WarehouseId, @QtyReceived, @Note, NULL)
            RETURNING id
            """,
            new
            {
                req.ProductionOrderId,
                req.ProductionOrderItemId,
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

    // Lien ket: nhap kho TP -> danh dau cong doan FG_RECEIPT sang DONE (neu chua ket thuc).
    // Chi doi status + finished_at, KHONG dung qty (giu nguyen so lieu san luong da nhap o cac buoc).
    public Task<int> MarkFinishingStepsDoneAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            """
            UPDATE production_steps ps
            SET status = 'DONE', finished_at = now(), updated_at = now()
            FROM production_stages st
            WHERE st.id = ps.stage_id
              AND ps.production_order_id = @OrderId
              AND st.code = 'FG_RECEIPT'
              AND ps.status <> 'DONE'
            """, new { OrderId = orderId });

    // Nhu tren nhung chi cho cong doan FG_RECEIPT cua RIENG 1 mat hang.
    public Task<int> MarkItemFinishingStepsDoneAsync(TenantScope scope, long orderItemId) =>
        scope.ExecuteAsync(
            """
            UPDATE production_steps ps
            SET status = 'DONE', finished_at = now(), updated_at = now()
            FROM production_stages st
            WHERE st.id = ps.stage_id
              AND ps.production_order_item_id = @ItemId
              AND st.code = 'FG_RECEIPT'
              AND ps.status <> 'DONE'
            """, new { ItemId = orderItemId });

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
