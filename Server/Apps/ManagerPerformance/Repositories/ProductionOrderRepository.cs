using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

public sealed class ProductionOrderRepository : IProductionOrderRepository
{
    // SELECT khop ProductionOrderDto (Dapper: snake_case <-> PascalCase da bat o DapperConfig).
    // JOIN products de tra kem SKU/ten ma hang -> UI hien thi ten canh ProductId.
    private const string OrderSelect =
        "SELECT po.id, po.order_no, po.product_id, p.sku AS product_sku, p.name AS product_name, " +
        "pu.code AS product_uom_code, pu.name AS product_uom_name, " +
        "po.bom_id, po.quantity, po.status, po.due_date, po.confirmed_at, " +
        "po.routing_id, rt.name AS routing_name " +
        "FROM production_orders po LEFT JOIN products p ON p.id = po.product_id " +
        "LEFT JOIN units_of_measure pu ON pu.id = p.uom_id " +
        "LEFT JOIN production_routings rt ON rt.id = po.routing_id";

    // JOIN materials + units_of_measure + warehouses de tra kem ten NVL/DVT/kho canh MaterialId/WarehouseId.
    private const string ReservationSelect =
        "SELECT r.id, r.production_order_id, r.material_id, m.sku AS material_sku, m.name AS material_name, " +
        "u.code AS material_uom_code, u.name AS material_uom_name, " +
        "r.warehouse_id, w.name AS warehouse_name, r.qty_reserved, r.status " +
        "FROM material_reservations r " +
        "LEFT JOIN materials m ON m.id = r.material_id " +
        "LEFT JOIN units_of_measure u ON u.id = m.uom_id " +
        "LEFT JOIN warehouses w ON w.id = r.warehouse_id";

    public Task<IEnumerable<ProductionOrderDto>> ListAsync(TenantScope scope, string? status, int? year, int? month)
    {
        var sql = $"{OrderSelect} WHERE " + PeriodWhere("po.created_at") +
                  (status is null ? "" : " AND po.status = @status") +
                  " ORDER BY po.created_at DESC";
        return scope.QueryAsync<ProductionOrderDto>(sql, new { status, year, month });
    }
    // Dieu kien loc theo thang/nam tren cot ngay (@year/@month null = khong loc).
    private static string PeriodWhere(string col) =>
        $"(@year IS NULL OR EXTRACT(YEAR FROM {col}) = @year) AND (@month IS NULL OR EXTRACT(MONTH FROM {col}) = @month)";

    public Task<ProductionOrderDto?> GetByIdAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<ProductionOrderDto>(
            $"{OrderSelect} WHERE po.id = @id", new { id });

    public Task<long> InsertAsync(TenantScope scope, CreateProductionOrderRequest req) =>
        scope.QuerySingleAsync<long>(
            """
            INSERT INTO production_orders (order_no, product_id, bom_id, quantity, status, due_date, note, routing_id)
            VALUES (@OrderNo, @ProductId, @BomId, @Quantity, 'DRAFT', @DueDate, @Note,
                    COALESCE(@RoutingId, (SELECT id FROM production_routings WHERE is_default AND is_active LIMIT 1)))
            RETURNING id
            """,
            new { req.OrderNo, req.ProductId, req.BomId, req.Quantity, req.DueDate, req.Note, req.RoutingId });

    public Task<IEnumerable<ReservationDto>> ListReservationsAsync(TenantScope scope, long orderId) =>
        scope.QueryAsync<ReservationDto>(
            $"{ReservationSelect} WHERE r.production_order_id = @orderId ORDER BY r.id",
            new { orderId });

    // ----- Confirm (giu cho) -----

    public Task<OrderLockRow?> LockOrderAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<OrderLockRow>(
            // FOR UPDATE: khoa dong don hang chong race khi confirm/cancel dong thoi.
            "SELECT id, status, quantity, bom_id, product_id FROM production_orders WHERE id = @id FOR UPDATE",
            new { id });

    public Task<long?> FindActiveBomAsync(TenantScope scope, long productId) =>
        scope.QueryFirstOrDefaultAsync<long?>(
            "SELECT id FROM bom WHERE product_id = @productId AND status = 'ACTIVE' LIMIT 1",
            new { productId });

    public Task SetBomAsync(TenantScope scope, long id, long bomId) =>
        scope.ExecuteAsync(
            "UPDATE production_orders SET bom_id = @bomId, updated_at = now() WHERE id = @id",
            new { id, bomId });

    public Task<IEnumerable<BomItemNeed>> ListBomItemsAsync(TenantScope scope, long bomId) =>
        scope.QueryAsync<BomItemNeed>(
            "SELECT material_id, qty_per_unit, waste_pct FROM bom_items WHERE bom_id = @bomId",
            new { bomId });

    public Task<IEnumerable<StockLockRow>> LockAvailableStockAsync(TenantScope scope, long materialId) =>
        scope.QueryAsync<StockLockRow>(
            // FOR UPDATE: khoa cac dong stock kha dung chong race khi 500+ user giu cho dong thoi.
            // Sap kha dung (on_hand - reserved) giam dan -> uu tien kho nhieu ton.
            """
            SELECT id, warehouse_id, qty_on_hand, qty_reserved
            FROM stock
            WHERE material_id = @materialId AND qty_on_hand > qty_reserved
            ORDER BY (qty_on_hand - qty_reserved) DESC
            FOR UPDATE
            """,
            new { materialId });

    public Task AddReservedAsync(TenantScope scope, long stockId, decimal take) =>
        scope.ExecuteAsync(
            "UPDATE stock SET qty_reserved = qty_reserved + @take WHERE id = @stockId",
            new { stockId, take });

    public Task UpsertReservationAsync(TenantScope scope, long orderId, long materialId, long warehouseId, decimal take) =>
        scope.ExecuteAsync(
            """
            INSERT INTO material_reservations
                (production_order_id, material_id, warehouse_id, qty_reserved, status)
            VALUES (@orderId, @materialId, @warehouseId, @take, 'ACTIVE')
            ON CONFLICT (production_order_id, material_id, warehouse_id)
            DO UPDATE SET
                qty_reserved = material_reservations.qty_reserved + EXCLUDED.qty_reserved,
                status = 'ACTIVE',
                updated_at = now()
            """,
            new { orderId, materialId, warehouseId, take });

    public Task MarkConfirmedAsync(TenantScope scope, long id) =>
        scope.ExecuteAsync(
            "UPDATE production_orders SET status = 'CONFIRMED', confirmed_at = now(), updated_at = now() WHERE id = @id",
            new { id });

    // ----- Cancel (nha giu cho) -----

    public Task<IEnumerable<ActiveReservationRow>> LockActiveReservationsAsync(TenantScope scope, long orderId) =>
        scope.QueryAsync<ActiveReservationRow>(
            // Khoa dong stock cua tung phieu giu cho (FOR UPDATE) truoc khi tru reserved.
            """
            SELECT r.id, r.material_id, r.warehouse_id, r.qty_reserved
            FROM material_reservations r
            JOIN stock s ON s.warehouse_id = r.warehouse_id AND s.material_id = r.material_id
            WHERE r.production_order_id = @orderId AND r.status = 'ACTIVE'
            FOR UPDATE OF s
            """,
            new { orderId });

    public Task ReleaseStockAsync(TenantScope scope, long warehouseId, long materialId, decimal qty) =>
        scope.ExecuteAsync(
            "UPDATE stock SET qty_reserved = qty_reserved - @qty WHERE warehouse_id = @warehouseId AND material_id = @materialId",
            new { warehouseId, materialId, qty });

    public Task MarkReservationReleasedAsync(TenantScope scope, long reservationId) =>
        scope.ExecuteAsync(
            "UPDATE material_reservations SET status = 'RELEASED', updated_at = now() WHERE id = @reservationId",
            new { reservationId });

    public Task MarkCancelledAsync(TenantScope scope, long id) =>
        scope.ExecuteAsync(
            "UPDATE production_orders SET status = 'CANCELLED', updated_at = now() WHERE id = @id",
            new { id });

    public async Task<bool> UpdateAsync(TenantScope scope, long id, UpdateProductionOrderRequest req) =>
        await scope.ExecuteAsync(
            "UPDATE production_orders SET quantity = @Quantity, due_date = @DueDate, note = COALESCE(@Note, note) WHERE id = @id",
            new { id, req.Quantity, req.DueDate, req.Note }) > 0;

    public Task<OrderImpactDto?> GetImpactAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<OrderImpactDto>(
            """
            SELECT po.status, po.order_no,
                   (SELECT count(*) FROM production_plans WHERE production_order_id = po.id)        AS plan_count,
                   (SELECT count(*) FROM production_steps WHERE production_order_id = po.id)        AS step_count,
                   (SELECT count(*) FROM material_reservations WHERE production_order_id = po.id)   AS reservation_count,
                   (SELECT count(*) FROM material_issues WHERE production_order_id = po.id)         AS issue_count,
                   (SELECT count(*) FROM finished_goods_receipts WHERE production_order_id = po.id) AS receipt_count,
                   (SELECT count(*) FROM loss_reports WHERE production_order_id = po.id)            AS loss_report_count,
                   (po.status IN ('DRAFT','CANCELLED')
                    AND (SELECT count(*) FROM material_issues WHERE production_order_id = po.id) = 0
                    AND (SELECT count(*) FROM finished_goods_receipts WHERE production_order_id = po.id) = 0
                    AND (SELECT count(*) FROM loss_reports WHERE production_order_id = po.id) = 0)  AS can_delete
            FROM production_orders po WHERE po.id = @id
            """, new { id });

    public async Task<bool> DeleteAsync(TenantScope scope, long id) =>
        await scope.ExecuteAsync("DELETE FROM production_orders WHERE id = @id", new { id }) > 0;
}
