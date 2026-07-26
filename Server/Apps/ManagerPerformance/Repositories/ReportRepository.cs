using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

public sealed class ReportRepository : IReportRepository
{
    // JOIN products + materials tu view de tra kem ten ma hang / NVL nut that canh cac cot ID.
    private const string MaxOutSelect =
        "SELECT v.product_id, p.sku AS product_sku, p.name AS product_name, v.bom_id, " +
        "v.max_producible_qty, v.bottleneck_material_id, " +
        "bm.sku AS bottleneck_material_sku, bm.name AS bottleneck_material_name, " +
        "v.bottleneck_material_output " +
        "FROM v_max_output_by_product v " +
        "LEFT JOIN products p ON p.id = v.product_id " +
        "LEFT JOIN materials bm ON bm.id = v.bottleneck_material_id";

    // JOIN materials tu view de tra kem SKU/ten NVL canh MaterialId.
    private const string ReqSelect =
        "SELECT v.production_order_id, v.order_no, v.order_status, v.material_id, " +
        "m.sku AS material_sku, m.name AS material_name, " +
        "u.code AS material_uom_code, u.name AS material_uom_name, " +
        "v.required_qty, v.total_available, v.shortage_qty, v.suggested_purchase_qty " +
        "FROM v_order_material_requirement v " +
        "LEFT JOIN materials m ON m.id = v.material_id " +
        "LEFT JOIN units_of_measure u ON u.id = m.uom_id";

    public Task<IEnumerable<MaxOutputByProductDto>> MaxOutputByProductAsync(TenantScope scope) =>
        scope.QueryAsync<MaxOutputByProductDto>(
            $"{MaxOutSelect} ORDER BY v.product_id");

    public Task<MaxOutputByProductDto?> MaxOutputForProductAsync(TenantScope scope, long productId) =>
        scope.QueryFirstOrDefaultAsync<MaxOutputByProductDto>(
            $"{MaxOutSelect} WHERE v.product_id = @productId",
            new { productId });

    public Task<IEnumerable<OrderMaterialRequirementDto>> OrderRequirementsAsync(TenantScope scope, long? orderId)
    {
        var sql = ReqSelect +
                  (orderId.HasValue ? " WHERE v.production_order_id = @orderId" : "") +
                  " ORDER BY v.production_order_id, v.shortage_qty DESC";
        return scope.QueryAsync<OrderMaterialRequirementDto>(sql, new { orderId });
    }

    public Task<IEnumerable<MaterialStockDto>> LowStockAsync(TenantScope scope, int? year, int? month) =>
        scope.QueryAsync<MaterialStockDto>(
            """
            SELECT vs.material_id, vs.sku, vs.name, vs.reorder_level, vs.reorder_quantity,
                   vs.total_on_hand, vs.total_reserved, vs.total_available, vs.is_low_stock,
                   vs.last_unit_cost, vs.avg_unit_cost, vs.stock_value,
                   vs.uom_code, vs.uom_name
            FROM v_material_stock vs
            JOIN materials m ON m.id = vs.material_id
            WHERE vs.is_low_stock
              AND (@year IS NULL OR EXTRACT(YEAR FROM m.created_at) = @year)
              AND (@month IS NULL OR EXTRACT(MONTH FROM m.created_at) = @month)
            ORDER BY vs.name
            """, new { year, month });

    /// <summary>Tong hop so lieu 12 thang cua 1 nam (moi thang 1 dong, thang trong = 0).</summary>
    public Task<IEnumerable<MonthlyStatsDto>> MonthlyStatsAsync(TenantScope scope, int year) =>
        scope.QueryAsync<MonthlyStatsDto>(
            """
            WITH months AS (SELECT generate_series(1, 12) AS m)
            SELECT months.m                                   AS month,
                   COALESCE(po.cnt, 0)                        AS order_count,
                   COALESCE(po.qty, 0)                        AS order_qty,
                   COALESCE(si.qty, 0)                        AS stock_in_qty,
                   COALESCE(si.val, 0)                        AS stock_in_value,
                   COALESCE(so.qty, 0)                        AS stock_out_qty,
                   COALESCE(so.val, 0)                        AS stock_out_value,
                   COALESCE(fg.cnt, 0)                        AS fg_receipt_count,
                   COALESCE(fg.qty, 0)                        AS fg_qty,
                   COALESCE(ls.variance, 0)                   AS loss_variance,
                   COALESCE(al.cnt, 0)                        AS alert_count
            FROM months
            LEFT JOIN (SELECT EXTRACT(MONTH FROM created_at)::int m, count(*) cnt, SUM(quantity) qty
                       FROM production_orders WHERE EXTRACT(YEAR FROM created_at) = @year
                       GROUP BY 1) po ON po.m = months.m
            LEFT JOIN (SELECT EXTRACT(MONTH FROM created_at)::int m,
                              SUM(quantity) qty, SUM(quantity * COALESCE(unit_cost, 0)) val
                       FROM stock_transactions
                       WHERE txn_type = 'RECEIPT' AND EXTRACT(YEAR FROM created_at) = @year
                       GROUP BY 1) si ON si.m = months.m
            LEFT JOIN (SELECT EXTRACT(MONTH FROM created_at)::int m,
                              SUM(-quantity) qty, SUM(-quantity * COALESCE(unit_cost, 0)) val
                       FROM stock_transactions
                       WHERE txn_type = 'ISSUE' AND EXTRACT(YEAR FROM created_at) = @year
                       GROUP BY 1) so ON so.m = months.m
            LEFT JOIN (SELECT EXTRACT(MONTH FROM received_at)::int m, count(*) cnt, SUM(qty_received) qty
                       FROM finished_goods_receipts WHERE EXTRACT(YEAR FROM received_at) = @year
                       GROUP BY 1) fg ON fg.m = months.m
            LEFT JOIN (SELECT EXTRACT(MONTH FROM reported_at)::int m, SUM(qty_variance) variance
                       FROM loss_reports WHERE EXTRACT(YEAR FROM reported_at) = @year
                       GROUP BY 1) ls ON ls.m = months.m
            LEFT JOIN (SELECT EXTRACT(MONTH FROM created_at)::int m, count(*) cnt
                       FROM alerts WHERE EXTRACT(YEAR FROM created_at) = @year
                       GROUP BY 1) al ON al.m = months.m
            ORDER BY months.m
            """, new { year });
}
