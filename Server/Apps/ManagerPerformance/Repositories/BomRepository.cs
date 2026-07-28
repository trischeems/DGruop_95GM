using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

public sealed class BomRepository : IBomRepository
{
    // Cot header khop BomDto (Dapper: snake_case <-> PascalCase da bat o DapperConfig).
    private const string HeaderCols =
        "id, product_id, version, status, effective_from, note";

    // JOIN materials de tra kem SKU/ten NVL canh MaterialId; JOIN units_of_measure lay DVT.
    private const string ItemSelect =
        "SELECT bi.id, bi.bom_id, bi.material_id, m.sku AS material_sku, m.name AS material_name, " +
        "u.code AS material_uom_code, u.name AS material_uom_name, " +
        "bi.qty_per_unit, bi.waste_pct, bi.note " +
        "FROM bom_items bi LEFT JOIN materials m ON m.id = bi.material_id " +
        "LEFT JOIN units_of_measure u ON u.id = m.uom_id";

    public Task<IEnumerable<BomDto>> ListAsync(TenantScope scope, long? productId)
    {
        var sql = $"SELECT {HeaderCols} FROM bom" +
                  (productId is null ? "" : " WHERE product_id = @productId") +
                  " ORDER BY product_id, version DESC";
        return scope.QueryAsync<BomDto>(sql, new { productId });
    }

    public Task<BomDto?> GetHeaderAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<BomDto>(
            $"SELECT {HeaderCols} FROM bom WHERE id = @id", new { id });

    public Task<IEnumerable<BomItemDto>> ListItemsAsync(TenantScope scope, long bomId) =>
        scope.QueryAsync<BomItemDto>(
            $"{ItemSelect} WHERE bi.bom_id = @bomId ORDER BY bi.id", new { bomId });

    public Task<string?> GetStatusAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<string?>(
            "SELECT status FROM bom WHERE id = @id", new { id });

    public Task<long> InsertBomAsync(TenantScope scope, long productId, string? note) =>
        scope.QuerySingleAsync<long>(
            """
            INSERT INTO bom (product_id, version, status, note)
            VALUES (
                @productId,
                COALESCE((SELECT MAX(version) FROM bom WHERE product_id = @productId), 0) + 1,
                'DRAFT',
                @note)
            RETURNING id
            """, new { productId, note });

    public Task<long> UpsertItemAsync(TenantScope scope, long bomId, UpsertBomItemRequest req) =>
        scope.QuerySingleAsync<long>(
            """
            INSERT INTO bom_items (bom_id, material_id, qty_per_unit, waste_pct, note)
            VALUES (@BomId, @MaterialId, @QtyPerUnit, @WastePct, @Note)
            ON CONFLICT (bom_id, material_id) DO UPDATE SET
                qty_per_unit = @QtyPerUnit,
                waste_pct = @WastePct,
                note = @Note
            RETURNING id
            """, new { BomId = bomId, req.MaterialId, req.QtyPerUnit, req.WastePct, req.Note });

    public Task<int> DeleteItemAsync(TenantScope scope, long bomId, long materialId) =>
        scope.ExecuteAsync(
            "DELETE FROM bom_items WHERE bom_id = @bomId AND material_id = @materialId",
            new { bomId, materialId });

    public Task InsertHistoryAsync(
        TenantScope scope, long bomId, long? bomItemId, long? materialId,
        string changeType, decimal? oldValue, decimal? newValue, string? reason) =>
        scope.ExecuteAsync(
            """
            INSERT INTO bom_change_history
                (bom_id, bom_item_id, material_id, change_type, old_value, new_value, reason)
            VALUES (@bomId, @bomItemId, @materialId, @changeType, @oldValue, @newValue, @reason)
            """, new { bomId, bomItemId, materialId, changeType, oldValue, newValue, reason });

    public Task SetStatusAsync(TenantScope scope, long id, string status) =>
        scope.ExecuteAsync(
            "UPDATE bom SET status = @status, updated_at = now() WHERE id = @id",
            new { id, status });

    public Task ActivateAsync(TenantScope scope, long id) =>
        scope.ExecuteAsync(
            "UPDATE bom SET status = 'ACTIVE', effective_from = now(), updated_at = now() WHERE id = @id",
            new { id });

    public Task ArchiveActiveOfSameProductAsync(TenantScope scope, long id) =>
        scope.ExecuteAsync(
            // Luu kho ban ACTIVE cu (neu co) TRUOC khi kich hoat ban moi:
            // partial unique index chi cho phep 1 ban ACTIVE moi product.
            """
            UPDATE bom SET status = 'ARCHIVED', updated_at = now()
            WHERE product_id = (SELECT product_id FROM bom WHERE id = @id)
              AND status = 'ACTIVE'
            """, new { id });

    public Task InsertPendingApprovalAsync(TenantScope scope, long bomId) =>
        scope.ExecuteAsync(
            "INSERT INTO bom_approvals (bom_id, status) VALUES (@bomId, 'PENDING')",
            new { bomId });

    public Task<int> DecideLatestPendingApprovalAsync(
        TenantScope scope, long bomId, string status, long? decidedBy, string? note) =>
        scope.ExecuteAsync(
            // Chi tac dong phieu PENDING moi nhat cua BOM.
            """
            UPDATE bom_approvals SET
                status = @status,
                decided_by = @decidedBy,
                decided_at = now(),
                decision_note = @note
            WHERE id = (
                SELECT id FROM bom_approvals
                WHERE bom_id = @bomId AND status = 'PENDING'
                ORDER BY requested_at DESC
                LIMIT 1)
            """, new { bomId, status, decidedBy, note });

    public Task<IEnumerable<BomApprovalDto>> ListApprovalsAsync(TenantScope scope, long bomId) =>
        scope.QueryAsync<BomApprovalDto>(
            """
            SELECT id, bom_id, status, requested_by, requested_at, decided_by, decided_at, decision_note
            FROM bom_approvals
            WHERE bom_id = @bomId
            ORDER BY requested_at DESC
            """, new { bomId });

    public Task<IEnumerable<BomChangeHistoryDto>> ListHistoryAsync(TenantScope scope, long bomId) =>
        scope.QueryAsync<BomChangeHistoryDto>(
            """
            SELECT id, bom_id, bom_item_id, material_id, change_type, old_value, new_value, reason, created_at
            FROM bom_change_history
            WHERE bom_id = @bomId
            ORDER BY created_at DESC
            """, new { bomId });

    public Task<BomImpactDto?> GetImpactAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<BomImpactDto>(
            """
            SELECT b.status, b.version,
                   (SELECT count(*) FROM bom_items WHERE bom_id = b.id)         AS item_count,
                   (SELECT count(*) FROM production_orders WHERE bom_id = b.id) AS order_count,
                   ((SELECT count(*) FROM production_orders WHERE bom_id = b.id) = 0
                    AND b.status IN ('DRAFT','REJECTED'))                       AS can_delete
            FROM bom b WHERE b.id = @id
            """, new { id });

    public async Task<bool> DeleteAsync(TenantScope scope, long id) =>
        await scope.ExecuteAsync("DELETE FROM bom WHERE id = @id", new { id }) > 0;
}
