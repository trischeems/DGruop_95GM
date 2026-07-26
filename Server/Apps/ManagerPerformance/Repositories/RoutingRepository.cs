using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Dapper raw SQL cho danh muc cong doan + mau quy trinh (V006). Khong prefix schema.</summary>
public sealed class RoutingRepository : IRoutingRepository
{
    // =================================================================================
    // DANH MUC CONG DOAN
    // =================================================================================

    public Task<IEnumerable<StageDto>> ListStagesAsync(TenantScope scope, bool activeOnly) =>
        scope.QueryAsync<StageDto>(
            "SELECT id, code, name, seq, is_active FROM production_stages" +
            (activeOnly ? " WHERE is_active" : "") +
            " ORDER BY seq, name");

    public Task<bool> StageCodeExistsAsync(TenantScope scope, string code, long? exceptId) =>
        scope.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM production_stages WHERE upper(code) = upper(@code) AND (@exceptId IS NULL OR id <> @exceptId))",
            new { code, exceptId });

    public Task<long> InsertStageAsync(TenantScope scope, CreateStageRequest req) =>
        scope.ExecuteScalarAsync<long>(
            """
            INSERT INTO production_stages (code, name, seq)
            VALUES (@Code, @Name, COALESCE(@Seq, (SELECT COALESCE(max(seq), 0) + 1 FROM production_stages)))
            RETURNING id
            """, new { req.Code, req.Name, req.Seq });

    public Task<int> UpdateStageAsync(TenantScope scope, long id, UpdateStageRequest req) =>
        scope.ExecuteAsync(
            "UPDATE production_stages SET name = @Name, seq = COALESCE(@Seq, seq), is_active = @IsActive WHERE id = @id",
            new { id, req.Name, req.Seq, req.IsActive });

    public Task<int> CountRoutingsUsingStageAsync(TenantScope scope, long stageId) =>
        scope.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM routing_steps rs JOIN production_routings r ON r.id = rs.routing_id WHERE rs.stage_id = @stageId AND r.is_active",
            new { stageId });

    // =================================================================================
    // MAU QUY TRINH
    // =================================================================================

    public Task<IEnumerable<RoutingDto>> ListRoutingsAsync(TenantScope scope, bool activeOnly) =>
        scope.QueryAsync<RoutingDto>(
            """
            SELECT r.id, r.code, r.name, r.note, r.is_active, r.is_default,
                   (SELECT count(*) FROM routing_steps rs WHERE rs.routing_id = r.id)::int AS step_count,
                   r.created_at
            FROM production_routings r
            """ + (activeOnly ? " WHERE r.is_active" : "") +
            " ORDER BY r.is_default DESC, r.name");

    public Task<RoutingDto?> GetRoutingAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<RoutingDto>(
            """
            SELECT r.id, r.code, r.name, r.note, r.is_active, r.is_default,
                   (SELECT count(*) FROM routing_steps rs WHERE rs.routing_id = r.id)::int AS step_count,
                   r.created_at
            FROM production_routings r WHERE r.id = @id
            """, new { id });

    public Task<IEnumerable<RoutingStepDto>> ListRoutingStepsAsync(TenantScope scope, long routingId) =>
        scope.QueryAsync<RoutingStepDto>(
            """
            SELECT rs.id, rs.routing_id, rs.stage_id,
                   st.code AS stage_code, st.name AS stage_name,
                   rs.seq, rs.is_optional, rs.note
            FROM routing_steps rs
            JOIN production_stages st ON st.id = rs.stage_id
            WHERE rs.routing_id = @routingId
            ORDER BY rs.seq
            """, new { routingId });

    public Task<bool> RoutingCodeExistsAsync(TenantScope scope, string code) =>
        scope.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM production_routings WHERE upper(code) = upper(@code))",
            new { code });

    public Task<long> InsertRoutingAsync(TenantScope scope, CreateRoutingRequest req) =>
        scope.ExecuteScalarAsync<long>(
            """
            INSERT INTO production_routings (code, name, note, is_default)
            VALUES (@Code, @Name, @Note, @IsDefault)
            RETURNING id
            """, new { req.Code, req.Name, req.Note, req.IsDefault });

    public Task<int> UpdateRoutingAsync(TenantScope scope, long id, UpdateRoutingRequest req) =>
        scope.ExecuteAsync(
            """
            UPDATE production_routings
            SET name = @Name, note = @Note, is_active = @IsActive, is_default = @IsDefault, updated_at = now()
            WHERE id = @id
            """, new { id, req.Name, req.Note, req.IsActive, req.IsDefault });

    public Task<int> ClearDefaultAsync(TenantScope scope, long exceptId) =>
        scope.ExecuteAsync(
            "UPDATE production_routings SET is_default = false, updated_at = now() WHERE is_default AND id <> @exceptId",
            new { exceptId });

    public Task<int> DeleteRoutingStepsAsync(TenantScope scope, long routingId) =>
        scope.ExecuteAsync("DELETE FROM routing_steps WHERE routing_id = @routingId", new { routingId });

    public Task<int> InsertRoutingStepAsync(TenantScope scope, long routingId, RoutingStepInput step) =>
        scope.ExecuteAsync(
            """
            INSERT INTO routing_steps (routing_id, stage_id, seq, is_optional, note)
            VALUES (@routingId, @StageId, @Seq, @IsOptional, @Note)
            """, new { routingId, step.StageId, step.Seq, step.IsOptional, step.Note });

    public Task<int> CountOrdersUsingRoutingAsync(TenantScope scope, long routingId) =>
        scope.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM production_orders WHERE routing_id = @routingId", new { routingId });

    public Task<int> DeleteRoutingAsync(TenantScope scope, long id) =>
        scope.ExecuteAsync("DELETE FROM production_routings WHERE id = @id", new { id });

    public Task<long?> FindDefaultRoutingIdAsync(TenantScope scope) =>
        scope.QueryFirstOrDefaultAsync<long?>(
            "SELECT id FROM production_routings WHERE is_default AND is_active LIMIT 1");

    // =================================================================================
    // BUOC CUA TUNG DON
    // =================================================================================

    public Task<int> SetOrderRoutingAsync(TenantScope scope, long orderId, long routingId) =>
        scope.ExecuteAsync(
            "UPDATE production_orders SET routing_id = @routingId, updated_at = now() WHERE id = @orderId",
            new { orderId, routingId });

    public Task<int> DeletePendingStepsAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            // Chi xoa buoc CHUA dong den: PENDING va khong co so lieu. Buoc da chay giu nguyen.
            """
            DELETE FROM production_steps
            WHERE production_order_id = @orderId
              AND status = 'PENDING' AND qty_in = 0 AND qty_out = 0 AND qty_defect = 0
            """, new { orderId });

    public Task<int> GenerateStepsFromRoutingAsync(TenantScope scope, long orderId, long routingId, int startSeq) =>
        scope.ExecuteAsync(
            // Sinh buoc theo mau; buoc da co (UNIQUE order+stage) thi bo qua, khong de len so lieu cu.
            """
            INSERT INTO production_steps (production_order_id, stage_id, seq, status)
            SELECT @orderId, rs.stage_id, rs.seq, 'PENDING'
            FROM routing_steps rs
            WHERE rs.routing_id = @routingId AND rs.seq >= @startSeq
            ON CONFLICT (production_order_id, stage_id) DO NOTHING
            """, new { orderId, routingId, startSeq });

    public Task<long> AddOrderStepAsync(TenantScope scope, long orderId, long stageId, int? seq, string? note) =>
        scope.ExecuteScalarAsync<long>(
            """
            INSERT INTO production_steps (production_order_id, stage_id, seq, status, note)
            VALUES (@orderId, @stageId, COALESCE(@seq,
                    (SELECT COALESCE(max(seq), 0) + 1 FROM production_steps WHERE production_order_id = @orderId)),
                    'PENDING', @note)
            ON CONFLICT (production_order_id, stage_id) DO NOTHING
            RETURNING id
            """, new { orderId, stageId, seq, note });

    public Task<int> SetStepSkippedAsync(TenantScope scope, long stepId, bool isSkipped, string? note) =>
        scope.ExecuteAsync(
            """
            UPDATE production_steps
            SET is_skipped = @isSkipped, note = COALESCE(@note, note), updated_at = now()
            WHERE id = @stepId
            """, new { stepId, isSkipped, note });

    public Task<int> SetStepSeqAsync(TenantScope scope, long stepId, int seq) =>
        scope.ExecuteAsync(
            "UPDATE production_steps SET seq = @seq, updated_at = now() WHERE id = @stepId",
            new { stepId, seq });

    public Task<OrderStepGuardRow?> LockOrderStepAsync(TenantScope scope, long stepId) =>
        scope.QueryFirstOrDefaultAsync<OrderStepGuardRow>(
            """
            SELECT id, production_order_id, status, qty_in, qty_out, is_skipped
            FROM production_steps WHERE id = @stepId FOR UPDATE
            """, new { stepId });

    public Task<int> DeleteOrderStepAsync(TenantScope scope, long stepId) =>
        scope.ExecuteAsync("DELETE FROM production_steps WHERE id = @stepId", new { stepId });

    public Task<IEnumerable<long>> ListStepIdsAsync(TenantScope scope, long orderId) =>
        scope.QueryAsync<long>(
            "SELECT id FROM production_steps WHERE production_order_id = @orderId", new { orderId });
}
