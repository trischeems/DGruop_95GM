using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

public sealed class LossReportRepository : ILossReportRepository
{
    // SELECT khop LossReportDto. JOIN materials + production_orders lay ten NVL/so don.
    private const string LossSelect =
        "SELECT lr.id, lr.production_order_id, po.order_no, lr.material_id, " +
        "m.sku AS material_sku, m.name AS material_name, " +
        "mu.code AS material_uom_code, mu.name AS material_uom_name, " +
        "lr.qty_issued, lr.qty_standard, lr.qty_variance, lr.finished_qty, " +
        "pu.code AS product_uom_code, pu.name AS product_uom_name " +
        "FROM loss_reports lr " +
        "LEFT JOIN materials m ON m.id = lr.material_id " +
        "LEFT JOIN units_of_measure mu ON mu.id = m.uom_id " +
        "LEFT JOIN production_orders po ON po.id = lr.production_order_id " +
        "LEFT JOIN products p ON p.id = po.product_id " +
        "LEFT JOIN units_of_measure pu ON pu.id = p.uom_id";

    public Task<IEnumerable<LossReportDto>> ListAsync(TenantScope scope, long? orderId)
    {
        var sql = LossSelect +
                  (orderId is null ? "" : " WHERE lr.production_order_id = @orderId") +
                  " ORDER BY lr.material_id";
        return scope.QueryAsync<LossReportDto>(sql, new { orderId });
    }

    public Task<decimal> GetFinishedQtyAsync(TenantScope scope, long orderId) =>
        scope.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(qty_received), 0) FROM finished_goods_receipts WHERE production_order_id = @orderId",
            new { orderId });

    public Task<long?> GetBomIdAsync(TenantScope scope, long orderId) =>
        scope.QueryFirstOrDefaultAsync<long?>(
            "SELECT bom_id FROM production_orders WHERE id = @orderId", new { orderId });

    public Task<IEnumerable<BomItemDto>> ListBomItemsAsync(TenantScope scope, long bomId) =>
        scope.QueryAsync<BomItemDto>(
            """
            SELECT bi.id, bi.bom_id, bi.material_id, m.sku AS material_sku, m.name AS material_name,
                   u.code AS material_uom_code, u.name AS material_uom_name,
                   bi.qty_per_unit, bi.waste_pct, bi.note
            FROM bom_items bi LEFT JOIN materials m ON m.id = bi.material_id
            LEFT JOIN units_of_measure u ON u.id = m.uom_id
            WHERE bi.bom_id = @bomId ORDER BY bi.material_id
            """,
            new { bomId });

    public Task<decimal> GetIssuedQtyAsync(TenantScope scope, long orderId, long materialId) =>
        scope.ExecuteScalarAsync<decimal>(
            """
            SELECT COALESCE(SUM(mii.qty_issued), 0)
            FROM material_issue_items mii
            JOIN material_issues mi ON mi.id = mii.material_issue_id
            WHERE mi.production_order_id = @orderId AND mii.material_id = @materialId
            """, new { orderId, materialId });

    public Task UpsertAsync(
        TenantScope scope, long orderId, long materialId,
        decimal qtyIssued, decimal qtyStandard, decimal finishedQty) =>
        // qty_variance la GENERATED column -> KHONG insert.
        scope.ExecuteAsync(
            """
            INSERT INTO loss_reports (production_order_id, material_id, qty_issued, qty_standard, finished_qty)
            VALUES (@orderId, @materialId, @qtyIssued, @qtyStandard, @finishedQty)
            ON CONFLICT (production_order_id, material_id) DO UPDATE SET
                qty_issued = EXCLUDED.qty_issued,
                qty_standard = EXCLUDED.qty_standard,
                finished_qty = EXCLUDED.finished_qty,
                reported_at = now()
            """, new { orderId, materialId, qtyIssued, qtyStandard, finishedQty });
}
