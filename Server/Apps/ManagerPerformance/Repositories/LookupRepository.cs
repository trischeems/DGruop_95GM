using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

public sealed class LookupRepository : ILookupRepository
{
    // Don vi tinh (units_of_measure).
    public Task<IEnumerable<UomDto>> ListUomsAsync(TenantScope scope) =>
        scope.QueryAsync<UomDto>(
            "SELECT id, code, name FROM units_of_measure ORDER BY code");

    public Task<long> InsertUomAsync(TenantScope scope, CreateUomRequest req) =>
        scope.QuerySingleAsync<long>(
            """
            INSERT INTO units_of_measure (code, name)
            VALUES (@Code, @Name)
            RETURNING id
            """, req);

    // Kho (warehouses).
    public Task<IEnumerable<WarehouseDto>> ListWarehousesAsync(TenantScope scope, bool activeOnly)
    {
        var sql = "SELECT id, code, name, is_active FROM warehouses" +
                  (activeOnly ? " WHERE is_active" : "") +
                  " ORDER BY code";
        return scope.QueryAsync<WarehouseDto>(sql);
    }

    public Task<long> InsertWarehouseAsync(TenantScope scope, CreateWarehouseRequest req) =>
        scope.QuerySingleAsync<long>(
            """
            INSERT INTO warehouses (code, name)
            VALUES (@Code, @Name)
            RETURNING id
            """, req);

    // Nhom NVL (material_categories).
    public Task<IEnumerable<MaterialCategoryDto>> ListCategoriesAsync(TenantScope scope) =>
        scope.QueryAsync<MaterialCategoryDto>(
            "SELECT id, code, name FROM material_categories ORDER BY code");

    public Task<long> InsertCategoryAsync(TenantScope scope, CreateCategoryRequest req) =>
        scope.QuerySingleAsync<long>(
            """
            INSERT INTO material_categories (code, name)
            VALUES (@Code, @Name)
            RETURNING id
            """, req);
}
