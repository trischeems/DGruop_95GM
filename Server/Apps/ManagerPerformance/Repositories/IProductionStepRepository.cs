using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Truy van/ghi cac buoc quy trinh san xuat. Ghi lam trong 1 transaction cua scope.</summary>
public interface IProductionStepRepository
{
    /// <summary>Tao du 4 dong cong doan cho don (bo qua neu da co). Tra ve so dong them moi.</summary>
    Task<int> InitStepsAsync(TenantScope scope, long orderId);

    /// <summary>Danh sach buoc quy trinh cua 1 don, JOIN cong doan, sap xep theo seq.</summary>
    Task<IEnumerable<ProductionStepDto>> ListByOrderAsync(TenantScope scope, long orderId);

    /// <summary>Khoa dong buoc (SELECT ... FOR UPDATE), tra ve trang thai + started_at (null neu khong co).</summary>
    Task<(string Status, DateTime? StartedAt)?> LockStepAsync(TenantScope scope, long id);

    /// <summary>
    /// Cap nhat 1 buoc: status, qty_in, qty_out, qty_defect, note, updated_at.
    /// started_at = now() khi chuyen sang IN_PROGRESS ma started_at IS NULL.
    /// finished_at = now() khi status = 'DONE'. Tra ve so dong cap nhat.
    /// </summary>
    Task<int> UpdateStepAsync(TenantScope scope, long id, UpdateStepRequest req);
}
