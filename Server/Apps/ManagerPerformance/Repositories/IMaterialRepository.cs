using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Truy van/ghi bang materials (Dapper raw SQL). Nhan TenantScope (da co tx dung schema).</summary>
public interface IMaterialRepository
{
    Task<IEnumerable<MaterialDto>> ListAsync(TenantScope scope, bool activeOnly, int? year, int? month);
    Task<MaterialDto?> GetByIdAsync(TenantScope scope, long id);
    /// <summary>True neu SKU da ton tai (kiem tra trung truoc khi tao). Khong phan biet hoa thuong.</summary>
    Task<bool> SkuExistsAsync(TenantScope scope, string sku);
    Task<long> InsertAsync(TenantScope scope, CreateMaterialRequest req);
    Task<bool> UpdateAsync(TenantScope scope, long id, UpdateMaterialRequest req);
    /// <summary>Dem cac noi tham chieu NVL (phuc vu popup canh bao xoa). Null neu NVL khong ton tai.</summary>
    Task<MaterialImpactDto?> GetImpactAsync(TenantScope scope, long id);
    /// <summary>Xoa vinh vien NVL (chi goi khi CanDelete). Tra ve true neu co dong bi xoa.</summary>
    Task<bool> DeleteAsync(TenantScope scope, long id);
}
