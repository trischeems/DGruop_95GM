using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

public sealed class ProductRepository : IProductRepository
{
    // SELECT khop DTO. JOIN units_of_measure de tra kem ma/ten DVT canh UomId.
    private const string ProductSelect =
        "SELECT p.id, p.sku, p.name, p.uom_id, u.code AS uom_code, u.name AS uom_name, p.is_active " +
        "FROM products p LEFT JOIN units_of_measure u ON u.id = p.uom_id";

    public Task<IEnumerable<ProductDto>> ListAsync(TenantScope scope, bool activeOnly, int? year, int? month)
    {
        var sql = $"{ProductSelect} WHERE " + PeriodWhere("p.created_at") +
                  (activeOnly ? " AND p.is_active" : "") +
                  " ORDER BY p.name";
        return scope.QueryAsync<ProductDto>(sql, new { year, month });
    }
    // Dieu kien loc theo thang/nam tren cot ngay (@year/@month null = khong loc).
    private static string PeriodWhere(string col) =>
        $"(@year IS NULL OR EXTRACT(YEAR FROM {col}) = @year) AND (@month IS NULL OR EXTRACT(MONTH FROM {col}) = @month)";

    public Task<ProductDto?> GetByIdAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<ProductDto>(
            $"{ProductSelect} WHERE p.id = @id", new { id });

    // Kiem tra trung SKU (khong phan biet hoa thuong) truoc khi tao -> loi 400 co nghia
    // thay vi de DB nem 23505 -> 500 kho hieu.
    public Task<bool> SkuExistsAsync(TenantScope scope, string sku) =>
        scope.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM products WHERE lower(sku) = lower(@sku))", new { sku });

    public Task<long> InsertAsync(TenantScope scope, CreateProductRequest req) =>
        scope.QuerySingleAsync<long>(
            """
            INSERT INTO products (sku, name, uom_id)
            VALUES (@Sku, @Name, @UomId)
            RETURNING id
            """, req);

    public async Task<bool> UpdateAsync(TenantScope scope, long id, UpdateProductRequest req)
    {
        var rows = await scope.ExecuteAsync(
            """
            UPDATE products SET
                name = @Name, uom_id = @UomId, is_active = @IsActive
            WHERE id = @id
            """, new { id, req.Name, req.UomId, req.IsActive });
        return rows > 0;
    }

    public Task<ProductImpactDto?> GetImpactAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<ProductImpactDto>(
            """
            SELECT s.*, (s.bom_count = 0 AND s.order_count = 0 AND s.receipt_count = 0) AS can_delete
            FROM (
                SELECT
                    (SELECT count(*) FROM bom WHERE product_id = @id)                     AS bom_count,
                    (SELECT count(*) FROM production_orders WHERE product_id = @id)       AS order_count,
                    (SELECT count(*) FROM finished_goods_receipts WHERE product_id = @id) AS receipt_count
                WHERE EXISTS (SELECT 1 FROM products WHERE id = @id)
            ) s
            """, new { id });

    public async Task<bool> DeleteAsync(TenantScope scope, long id) =>
        await scope.ExecuteAsync("DELETE FROM products WHERE id = @id", new { id }) > 0;
}
