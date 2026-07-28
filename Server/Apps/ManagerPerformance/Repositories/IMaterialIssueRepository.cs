using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

/// <summary>
/// Truy van/ghi phieu xuat NVL. Ghi (xuat kho tru ton) PHAI trong 1 transaction cua scope + FOR UPDATE.
/// </summary>
public interface IMaterialIssueRepository
{
    /// <summary>Danh sach phieu xuat, loc theo don san xuat (null = tat ca), moi nhat truoc.</summary>
    Task<IEnumerable<MaterialIssueDto>> ListAsync(TenantScope scope, long? orderId);

    /// <summary>Tao phieu xuat (issue_no sinh trong SQL, status POSTED). Tra ve (id, issue_no).</summary>
    Task<(long Id, string IssueNo)> InsertIssueAsync(
        TenantScope scope, long productionOrderId, long warehouseId, string? note);

    /// <summary>Khoa dong stock (SELECT ... FOR UPDATE), tra ve (on_hand, reserved) hoac null neu chua co dong.</summary>
    Task<(decimal QtyOnHand, decimal QtyReserved)?> LockStockAsync(
        TenantScope scope, long warehouseId, long materialId);

    /// <summary>Ghi lai ton on_hand/reserved sau khi tru.</summary>
    Task SetStockAsync(
        TenantScope scope, long warehouseId, long materialId, decimal newOnHand, decimal newReserved);

    /// <summary>Them 1 dong NVL vao phieu xuat.</summary>
    Task InsertItemAsync(
        TenantScope scope, long materialIssueId, long materialId, decimal qtyIssued, decimal unitCost);

    /// <summary>Ghi 1 dong so cai giao dich kho (xuat: quantity am). Tra ve id.</summary>
    Task<long> InsertTransactionAsync(
        TenantScope scope, long warehouseId, long materialId, string txnType,
        decimal quantity, decimal unitCost, decimal balanceAfter, string? refType, long? refId, string? note);

    /// <summary>Danh dau cac giu cho ACTIVE cua don thanh CONSUMED. Tra ve so dong cap nhat.</summary>
    Task<int> ConsumeReservationsAsync(TenantScope scope, long productionOrderId);

    /// <summary>Chuyen don CONFIRMED -> IN_PROGRESS. Tra ve so dong cap nhat.</summary>
    Task<int> MarkOrderInProgressAsync(TenantScope scope, long productionOrderId);
}
