using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Truy van/ghi bang products (Dapper raw SQL). Nhan TenantScope (da co tx dung schema).</summary>
public interface IProductRepository
{
    Task<IEnumerable<ProductDto>> ListAsync(TenantScope scope, bool activeOnly, int? year, int? month);
    Task<ProductDto?> GetByIdAsync(TenantScope scope, long id);
    /// <summary>True neu SKU da ton tai (kiem tra trung truoc khi tao). So sanh khong phan biet hoa thuong.</summary>
    Task<bool> SkuExistsAsync(TenantScope scope, string sku);
    Task<long> InsertAsync(TenantScope scope, CreateProductRequest req);
    Task<bool> UpdateAsync(TenantScope scope, long id, UpdateProductRequest req);
    /// <summary>Dem cac noi tham chieu ma hang (phuc vu popup canh bao xoa). Null neu khong ton tai.</summary>
    Task<ProductImpactDto?> GetImpactAsync(TenantScope scope, long id);
    /// <summary>Xoa vinh vien ma hang (chi goi khi CanDelete). Tra ve true neu co dong bi xoa.</summary>
    Task<bool> DeleteAsync(TenantScope scope, long id);
}
