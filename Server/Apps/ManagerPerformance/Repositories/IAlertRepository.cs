using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Truy van/ghi canh bao. Sinh canh bao (scan) lam trong 1 transaction cua scope.</summary>
public interface IAlertRepository
{
    /// <summary>Danh sach canh bao theo status (moi nhat truoc).</summary>
    Task<IEnumerable<AlertDto>> ListAsync(TenantScope scope, string status, int? year, int? month);

    /// <summary>
    /// Sinh canh bao LOW_STOCK tu v_material_stock (is_low_stock), chong trung: chi INSERT khi
    /// chua co canh bao OPEN cung (MATERIAL, material_id, LOW_STOCK). Tra ve so dong da tao.
    /// </summary>
    Task<int> GenerateLowStockAsync(TenantScope scope);

    /// <summary>
    /// Sinh canh bao ORDER_SHORTAGE tu v_order_material_requirement (shortage_qty > 0), chong trung:
    /// chi INSERT khi chua co canh bao OPEN cung (PRODUCTION_ORDER, production_order_id, ORDER_SHORTAGE).
    /// Tra ve so dong da tao.
    /// </summary>
    Task<int> GenerateOrderShortageAsync(TenantScope scope);

    /// <summary>Ghi nhan canh bao (ACKNOWLEDGED). Tra ve so dong bi tac dong.</summary>
    Task<int> AcknowledgeAsync(TenantScope scope, long id);

    /// <summary>Dong canh bao (RESOLVED) + dat resolved_at. Tra ve so dong bi tac dong.</summary>
    Task<int> ResolveAsync(TenantScope scope, long id);
}
