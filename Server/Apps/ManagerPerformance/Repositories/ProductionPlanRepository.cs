using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

public sealed class ProductionPlanRepository : IProductionPlanRepository
{
    // SELECT khop DTO (Dapper: snake_case <-> PascalCase). JOIN production_orders lay so don.
    // JOIN dong don (V007): ke hoach lap cho TUNG MAT HANG, khong phai ca don.
    private const string PlanSelect =
        "SELECT pp.id, pp.production_order_id, po.order_no, " +
        "pp.production_order_item_id, i.line_no, " +
        "p.sku AS line_product_sku, p.name AS line_product_name, i.quantity AS line_quantity, " +
        "pp.planned_qty, pp.planned_start, " +
        "pp.planned_end, pp.line_code, pp.status, pp.note, " +
        "pu.code AS product_uom_code, pu.name AS product_uom_name " +
        "FROM production_plans pp LEFT JOIN production_orders po ON po.id = pp.production_order_id " +
        "LEFT JOIN production_order_items i ON i.id = pp.production_order_item_id " +
        "LEFT JOIN products p ON p.id = i.product_id " +
        "LEFT JOIN units_of_measure pu ON pu.id = p.uom_id";

    public Task<IEnumerable<ProductionPlanDto>> ListAsync(TenantScope scope, long? orderId, int? year, int? month)
    {
        // Loc theo don san xuat + thang/nam tao; sap theo mat hang roi ngay bat dau (null xuong cuoi).
        var sql = $"{PlanSelect} WHERE " + PeriodWhere("pp.created_at") +
                  (orderId.HasValue ? " AND pp.production_order_id = @orderId" : "") +
                  " ORDER BY i.line_no, pp.planned_start ASC NULLS LAST, pp.id";
        return scope.QueryAsync<ProductionPlanDto>(sql, new { orderId, year, month });
    }
    // Dieu kien loc theo thang/nam tren cot ngay (@year/@month null = khong loc).
    private static string PeriodWhere(string col) =>
        $"(@year IS NULL OR EXTRACT(YEAR FROM {col}) = @year) AND (@month IS NULL OR EXTRACT(MONTH FROM {col}) = @month)";

    public Task<long> InsertAsync(TenantScope scope, CreateProductionPlanRequest req) =>
        scope.QuerySingleAsync<long>(
            """
            INSERT INTO production_plans
                (production_order_id, production_order_item_id, planned_qty, planned_start, planned_end, line_code, note)
            VALUES
                (@ProductionOrderId, @ProductionOrderItemId, @PlannedQty, @PlannedStart, @PlannedEnd, @LineCode, @Note)
            RETURNING id
            """, req);

    public async Task<bool> UpdateStatusAsync(TenantScope scope, long id, string status)
    {
        var rows = await scope.ExecuteAsync(
            """
            UPDATE production_plans SET
                status = @status, updated_at = now()
            WHERE id = @id
            """, new { id, status });
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(TenantScope scope, long id)
    {
        var rows = await scope.ExecuteAsync(
            "DELETE FROM production_plans WHERE id = @id", new { id });
        return rows > 0;
    }

    public async Task<bool> UpdateAsync(TenantScope scope, long id, UpdatePlanRequest req) =>
        await scope.ExecuteAsync(
            "UPDATE production_plans SET planned_qty = @PlannedQty, line_code = @LineCode, note = COALESCE(@Note, note) WHERE id = @id",
            new { id, req.PlannedQty, req.LineCode, req.Note }) > 0;

    // Khoa dong don (FOR UPDATE) khi kiem tra tong ke hoach, chong race khi 2 user cung them plan.
    public Task<decimal?> LockOrderQuantityAsync(TenantScope scope, long orderId) =>
        scope.QueryFirstOrDefaultAsync<decimal?>(
            "SELECT quantity FROM production_orders WHERE id = @orderId FOR UPDATE", new { orderId });

    // SL dat cua RIENG 1 mat hang trong don (rang buoc ke hoach tinh theo tung mat hang, V007).
    public Task<decimal?> LockItemQuantityAsync(TenantScope scope, long orderItemId) =>
        scope.QueryFirstOrDefaultAsync<decimal?>(
            "SELECT quantity FROM production_order_items WHERE id = @orderItemId FOR UPDATE",
            new { orderItemId });

    // Tong planned_qty hien co cua 1 MAT HANG (khong tinh CANCELLED), tru 1 plan dang sua neu co.
    public Task<decimal> SumPlannedQtyAsync(TenantScope scope, long orderItemId, long? excludePlanId) =>
        scope.ExecuteScalarAsync<decimal>(
            """
            SELECT COALESCE(SUM(planned_qty), 0)
            FROM production_plans
            WHERE production_order_item_id = @orderItemId
              AND status <> 'CANCELLED'
              AND (@excludePlanId IS NULL OR id <> @excludePlanId)
            """, new { orderItemId, excludePlanId });

    public Task<PlanLockRow?> LockPlanAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<PlanLockRow>(
            "SELECT production_order_id, production_order_item_id, status, planned_qty " +
            "FROM production_plans WHERE id = @id FOR UPDATE",
            new { id });

    // Doc order id cua plan KHONG khoa — de khoa dong DON truoc roi moi khoa plan (chong deadlock).
    public Task<long?> GetPlanOrderIdAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<long?>(
            "SELECT production_order_id FROM production_plans WHERE id = @id", new { id });
}
