using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

/// <summary>1 dong plan da khoa (FOR UPDATE) phuc vu kiem tra chuyen trang thai.</summary>
public sealed record PlanLockRow(long ProductionOrderId, string Status, decimal PlannedQty);

/// <summary>Truy van/ghi bang production_plans (Dapper raw SQL). Nhan TenantScope (da co tx dung schema).</summary>
public interface IProductionPlanRepository
{
    Task<IEnumerable<ProductionPlanDto>> ListAsync(TenantScope scope, long? orderId, int? year, int? month);
    Task<long> InsertAsync(TenantScope scope, CreateProductionPlanRequest req);
    Task<bool> UpdateStatusAsync(TenantScope scope, long id, string status);
    /// <summary>Sua so luong ke hoach / ma chuyen / ghi chu.</summary>
    Task<bool> UpdateAsync(TenantScope scope, long id, UpdatePlanRequest req);
    Task<bool> DeleteAsync(TenantScope scope, long id);

    /// <summary>SL dat cua don (khoa dong FOR UPDATE de chong race khi kiem tra tong ke hoach). Null neu khong co don.</summary>
    Task<decimal?> LockOrderQuantityAsync(TenantScope scope, long orderId);
    /// <summary>Tong planned_qty hien co cua don, tru 1 plan (excludePlanId) khi dang sua. 0 neu chua co.</summary>
    Task<decimal> SumPlannedQtyAsync(TenantScope scope, long orderId, long? excludePlanId);
    /// <summary>Doc (production_order_id, status, planned_qty) cua 1 plan, khoa dong FOR UPDATE. Null neu khong co.</summary>
    Task<PlanLockRow?> LockPlanAsync(TenantScope scope, long id);
    /// <summary>Doc order id cua plan KHONG khoa (phuc vu khoa don truoc — thu tu khoa toan cuc).</summary>
    Task<long?> GetPlanOrderIdAsync(TenantScope scope, long id);
}
