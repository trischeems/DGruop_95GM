using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Truy van/ghi ton kho. Ghi ton PHAI trong 1 transaction cua scope + FOR UPDATE.</summary>
public interface IStockRepository
{
    Task<IEnumerable<MaterialStockDto>> ListStockAsync(TenantScope scope, bool lowStockOnly, int? year, int? month);

    /// <summary>Khoa dong stock (SELECT ... FOR UPDATE), tra ve ton on_hand hien tai (null neu chua co dong).</summary>
    Task<decimal?> LockOnHandAsync(TenantScope scope, long warehouseId, long materialId);

    /// <summary>Tao dong stock rong (0/0) khi chua ton tai, roi tra ve on_hand = 0.</summary>
    Task EnsureStockRowAsync(TenantScope scope, long warehouseId, long materialId);

    /// <summary>Cong ton on_hand (nhap kho). Tra ve on_hand sau khi cong.</summary>
    Task<decimal> AddOnHandAsync(TenantScope scope, long warehouseId, long materialId, decimal delta);

    /// <summary>Ghi 1 dong so cai giao dich kho. Tra ve id.</summary>
    Task<long> InsertTransactionAsync(
        TenantScope scope, long warehouseId, long materialId, string txnType,
        decimal quantity, decimal unitCost, decimal balanceAfter, string? refType, long? refId, string? note);
}
