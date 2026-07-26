using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

public sealed class MaterialRepository : IMaterialRepository
{
    // SELECT khop DTO. JOIN units_of_measure de tra kem ma/ten DVT canh so luong.
    private const string MaterialSelect =
        "SELECT m.id, m.sku, m.name, m.category_id, m.uom_id, " +
        "u.code AS uom_code, u.name AS uom_name, " +
        "m.reorder_level, m.reorder_quantity, m.standard_cost, m.is_active " +
        "FROM materials m LEFT JOIN units_of_measure u ON u.id = m.uom_id";

    public Task<IEnumerable<MaterialDto>> ListAsync(TenantScope scope, bool activeOnly, int? year, int? month)
    {
        var sql = $"{MaterialSelect} WHERE " + PeriodWhere("m.created_at") +
                  (activeOnly ? " AND m.is_active" : "") +
                  " ORDER BY m.name";
        return scope.QueryAsync<MaterialDto>(sql, new { year, month });
    }
    // Dieu kien loc theo thang/nam tren cot ngay (@year/@month null = khong loc).
    private static string PeriodWhere(string col) =>
        $"(@year IS NULL OR EXTRACT(YEAR FROM {col}) = @year) AND (@month IS NULL OR EXTRACT(MONTH FROM {col}) = @month)";

    public Task<MaterialDto?> GetByIdAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<MaterialDto>(
            $"{MaterialSelect} WHERE m.id = @id", new { id });

    // Kiem tra trung SKU (khong phan biet hoa thuong) truoc khi tao.
    public Task<bool> SkuExistsAsync(TenantScope scope, string sku) =>
        scope.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM materials WHERE lower(sku) = lower(@sku))", new { sku });

    public Task<long> InsertAsync(TenantScope scope, CreateMaterialRequest req) =>
        scope.QuerySingleAsync<long>(
            """
            INSERT INTO materials (sku, name, category_id, uom_id, reorder_level, reorder_quantity, standard_cost)
            VALUES (@Sku, @Name, @CategoryId, @UomId, @ReorderLevel, @ReorderQuantity, @StandardCost)
            RETURNING id
            """, req);

    public async Task<bool> UpdateAsync(TenantScope scope, long id, UpdateMaterialRequest req)
    {
        var rows = await scope.ExecuteAsync(
            """
            UPDATE materials SET
                name = @Name, category_id = @CategoryId, reorder_level = @ReorderLevel,
                reorder_quantity = @ReorderQuantity, standard_cost = @StandardCost, is_active = @IsActive
            WHERE id = @id
            """, new { id, req.Name, req.CategoryId, req.ReorderLevel, req.ReorderQuantity, req.StandardCost, req.IsActive });
        return rows > 0;
    }

    public Task<MaterialImpactDto?> GetImpactAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<MaterialImpactDto>(
            """
            SELECT s.*,
                   (s.stock_row_count = 0 AND s.bom_item_count = 0 AND s.reservation_count = 0
                    AND s.issue_line_count = 0 AND s.transaction_count = 0 AND s.loss_report_count = 0) AS can_delete
            FROM (
                SELECT
                    (SELECT count(*) FROM stock WHERE material_id = @id)                      AS stock_row_count,
                    (SELECT COALESCE(SUM(qty_on_hand), 0) FROM stock WHERE material_id = @id) AS total_on_hand,
                    (SELECT count(*) FROM bom_items WHERE material_id = @id)                  AS bom_item_count,
                    (SELECT count(*) FROM material_reservations WHERE material_id = @id)      AS reservation_count,
                    (SELECT count(*) FROM material_issue_items WHERE material_id = @id)       AS issue_line_count,
                    (SELECT count(*) FROM stock_transactions WHERE material_id = @id)         AS transaction_count,
                    (SELECT count(*) FROM loss_reports WHERE material_id = @id)               AS loss_report_count
                WHERE EXISTS (SELECT 1 FROM materials WHERE id = @id)
            ) s
            """, new { id });

    public async Task<bool> DeleteAsync(TenantScope scope, long id) =>
        await scope.ExecuteAsync("DELETE FROM materials WHERE id = @id", new { id }) > 0;
}
