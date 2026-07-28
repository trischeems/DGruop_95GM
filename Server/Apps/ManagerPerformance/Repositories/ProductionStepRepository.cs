using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

public sealed class ProductionStepRepository : IProductionStepRepository
{
    public Task<int> InitStepsAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            // Sinh buoc theo MAU QUY TRINH cua don (V006): uu tien routing_id cua don,
            // chua chon thi lay mau mac dinh. Buoc da co (UNIQUE order+stage) thi bo qua.
            """
            INSERT INTO production_steps (production_order_id, stage_id, seq, status)
            SELECT @OrderId, rs.stage_id, rs.seq, 'PENDING'
            FROM routing_steps rs
            WHERE rs.routing_id = COALESCE(
                    (SELECT routing_id FROM production_orders WHERE id = @OrderId),
                    (SELECT id FROM production_routings WHERE is_default AND is_active LIMIT 1))
            ON CONFLICT (production_order_id, stage_id) DO NOTHING
            """, new { OrderId = orderId });

    public Task<IEnumerable<ProductionStepDto>> ListByOrderAsync(TenantScope scope, long orderId) =>
        scope.QueryAsync<ProductionStepDto>(
            """
            SELECT ps.id, ps.production_order_id, ps.stage_id,
                   st.code AS stage_code, st.name AS stage_name,
                   COALESCE(ps.seq, st.seq) AS seq,
                   ps.status, ps.qty_in, ps.qty_out, ps.qty_defect,
                   ps.started_at, ps.finished_at, ps.note, ps.is_skipped,
                   pu.code AS product_uom_code, pu.name AS product_uom_name
            FROM production_steps ps
            JOIN production_stages st ON st.id = ps.stage_id
            LEFT JOIN production_orders po ON po.id = ps.production_order_id
            LEFT JOIN products p ON p.id = po.product_id
            LEFT JOIN units_of_measure pu ON pu.id = p.uom_id
            WHERE ps.production_order_id = @OrderId
            ORDER BY COALESCE(ps.seq, st.seq), st.name
            """, new { OrderId = orderId });

    public async Task<(string Status, DateTime? StartedAt)?> LockStepAsync(TenantScope scope, long id)
    {
        // FOR UPDATE: khoa dong buoc chong race khi 500+ user cap nhat dong thoi.
        var row = await scope.QueryFirstOrDefaultAsync<StepLockRow>(
            "SELECT status, started_at FROM production_steps WHERE id = @Id FOR UPDATE",
            new { Id = id });
        return row is null ? null : (row.Status, row.StartedAt);
    }

    public Task<StepContextRow?> LockStepContextAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<StepContextRow>(
            """
            SELECT ps.production_order_id, st.code AS stage_code,
                   COALESCE(ps.seq, st.seq) AS seq, ps.status
            FROM production_steps ps
            JOIN production_stages st ON st.id = ps.stage_id
            WHERE ps.id = @Id
            FOR UPDATE OF ps
            """, new { Id = id });

    // Day don CONFIRMED -> IN_PROGRESS (khi cong doan bat dau chay). Chi doi khi dang CONFIRMED.
    public Task<int> MarkOrderInProgressAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            "UPDATE production_orders SET status = 'IN_PROGRESS', updated_at = now() WHERE id = @orderId AND status = 'CONFIRMED'",
            new { orderId });

    // Day ke hoach PLANNED/RELEASED -> IN_PROGRESS khi bat dau san xuat.
    public Task<int> MarkPlansInProgressAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            """
            UPDATE production_plans SET status = 'IN_PROGRESS', updated_at = now()
            WHERE production_order_id = @orderId AND status IN ('PLANNED', 'RELEASED')
            """, new { orderId });

    // Day ke hoach IN_PROGRESS -> DONE khi cong doan cuoi hoan tat.
    public Task<int> MarkPlansDoneAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            "UPDATE production_plans SET status = 'DONE', updated_at = now() WHERE production_order_id = @orderId AND status = 'IN_PROGRESS'",
            new { orderId });

    public Task<int> UpdateStepAsync(TenantScope scope, long id, UpdateStepRequest req) =>
        scope.ExecuteAsync(
            """
            UPDATE production_steps SET
                status = @Status,
                qty_in = @QtyIn,
                qty_out = @QtyOut,
                qty_defect = @QtyDefect,
                note = @Note,
                started_at = CASE
                    WHEN @Status = 'IN_PROGRESS' AND started_at IS NULL THEN now()
                    ELSE started_at END,
                finished_at = CASE
                    WHEN @Status = 'DONE' THEN now()
                    ELSE finished_at END,
                updated_at = now()
            WHERE id = @Id
            """,
            new { Id = id, req.Status, req.QtyIn, req.QtyOut, req.QtyDefect, req.Note });

    /// <summary>Dong tam de doc khi khoa buoc (FOR UPDATE).</summary>
    private sealed record StepLockRow(string Status, DateTime? StartedAt);
}
