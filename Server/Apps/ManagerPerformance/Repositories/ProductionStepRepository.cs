using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

public sealed class ProductionStepRepository : IProductionStepRepository
{
    public Task<int> InitStepsAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            // Tao du 4 dong cong doan cho don; da co (UNIQUE order+stage) thi bo qua.
            """
            INSERT INTO production_steps (production_order_id, stage_id, status)
            SELECT @OrderId, id, 'PENDING' FROM production_stages
            ON CONFLICT (production_order_id, stage_id) DO NOTHING
            """, new { OrderId = orderId });

    public Task<IEnumerable<ProductionStepDto>> ListByOrderAsync(TenantScope scope, long orderId) =>
        scope.QueryAsync<ProductionStepDto>(
            """
            SELECT ps.id, ps.production_order_id, ps.stage_id,
                   st.code AS stage_code, st.name AS stage_name, st.seq,
                   ps.status, ps.qty_in, ps.qty_out, ps.qty_defect,
                   ps.started_at, ps.finished_at, ps.note
            FROM production_steps ps
            JOIN production_stages st ON st.id = ps.stage_id
            WHERE ps.production_order_id = @OrderId
            ORDER BY st.seq
            """, new { OrderId = orderId });

    public async Task<(string Status, DateTime? StartedAt)?> LockStepAsync(TenantScope scope, long id)
    {
        // FOR UPDATE: khoa dong buoc chong race khi 500+ user cap nhat dong thoi.
        var row = await scope.QueryFirstOrDefaultAsync<StepLockRow>(
            "SELECT status, started_at FROM production_steps WHERE id = @Id FOR UPDATE",
            new { Id = id });
        return row is null ? null : (row.Status, row.StartedAt);
    }

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
