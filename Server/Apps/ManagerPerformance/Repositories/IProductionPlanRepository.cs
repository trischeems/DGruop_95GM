using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Truy van/ghi bang production_plans (Dapper raw SQL). Nhan TenantScope (da co tx dung schema).</summary>
public interface IProductionPlanRepository
{
    Task<IEnumerable<ProductionPlanDto>> ListAsync(TenantScope scope, long? orderId, int? year, int? month);
    Task<long> InsertAsync(TenantScope scope, CreateProductionPlanRequest req);
    Task<bool> UpdateStatusAsync(TenantScope scope, long id, string status);
    /// <summary>Sua so luong ke hoach / ma chuyen / ghi chu.</summary>
    Task<bool> UpdateAsync(TenantScope scope, long id, UpdatePlanRequest req);
    Task<bool> DeleteAsync(TenantScope scope, long id);
}
