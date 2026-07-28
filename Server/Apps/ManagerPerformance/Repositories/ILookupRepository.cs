using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Truy van/ghi cac bang danh muc nen (Dapper raw SQL). Nhan TenantScope (da co tx dung schema).</summary>
public interface ILookupRepository
{
    Task<IEnumerable<UomDto>> ListUomsAsync(TenantScope scope);
    Task<long> InsertUomAsync(TenantScope scope, CreateUomRequest req);

    Task<IEnumerable<WarehouseDto>> ListWarehousesAsync(TenantScope scope, bool activeOnly);
    Task<long> InsertWarehouseAsync(TenantScope scope, CreateWarehouseRequest req);

    Task<IEnumerable<MaterialCategoryDto>> ListCategoriesAsync(TenantScope scope);
    Task<long> InsertCategoryAsync(TenantScope scope, CreateCategoryRequest req);
}
