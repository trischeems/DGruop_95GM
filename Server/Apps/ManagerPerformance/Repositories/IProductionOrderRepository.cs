using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Dong don hang khoa FOR UPDATE khi confirm/cancel.</summary>
public sealed record OrderLockRow(long Id, string Status, decimal Quantity, long? BomId, long ProductId);

/// <summary>1 DONG don (ma hang + SL + BOM) doc khi confirm de giu cho theo tung dong.</summary>
public sealed record OrderItemNeedRow(long Id, long ProductId, long? BomId, decimal Quantity);

/// <summary>1 dong NVL trong BOM (chi cot can cho tinh nhu cau giu cho).</summary>
public sealed record BomItemNeed(long MaterialId, decimal QtyPerUnit, decimal WastePct);

/// <summary>1 dong stock kha dung, da khoa FOR UPDATE, sap theo kha dung giam dan.</summary>
public sealed record StockLockRow(long Id, long WarehouseId, decimal QtyOnHand, decimal QtyReserved);

/// <summary>1 phieu giu cho ACTIVE can nha khi huy don (khoa dong stock tuong ung).</summary>
public sealed record ActiveReservationRow(long Id, long MaterialId, long WarehouseId, decimal QtyReserved);

/// <summary>
/// Truy van/ghi don hang san xuat + giu cho NVL. Ghi ton PHAI trong 1 transaction cua scope + FOR UPDATE.
/// </summary>
public interface IProductionOrderRepository
{
    Task<IEnumerable<ProductionOrderDto>> ListAsync(TenantScope scope, string? status, int? year, int? month);

    Task<ProductionOrderDto?> GetByIdAsync(TenantScope scope, long id);

    /// <summary>Tao HEADER don hang moi (DRAFT) — cac cot dai dien lay tu dong dau. Tra ve id.</summary>
    Task<long> InsertAsync(TenantScope scope, CreateProductionOrderRequest req, OrderItemInput first);

    Task<IEnumerable<ReservationDto>> ListReservationsAsync(TenantScope scope, long orderId);

    // ----- Dong don (nhieu mat hang trong 1 don, V007) -----

    /// <summary>Cac dong (ma hang + SL) cua don, kem ten/DVT/BOM/quy trinh + tien do.</summary>
    Task<IEnumerable<ProductionOrderItemDto>> ListItemsAsync(TenantScope scope, long orderId);

    /// <summary>1 dong don theo id (null neu khong co).</summary>
    Task<ProductionOrderItemDto?> GetItemAsync(TenantScope scope, long itemId);

    /// <summary>Them 1 dong ma hang vao don (line_no tu tang). Tra ve id dong.</summary>
    Task<long> InsertItemAsync(TenantScope scope, long orderId, OrderItemInput item);

    /// <summary>Sua 1 dong (SL/BOM/quy trinh/ghi chu). Tra ve true neu co dong bi sua.</summary>
    Task<bool> UpdateItemAsync(TenantScope scope, long itemId, UpdateOrderItemRequest req);

    /// <summary>Xoa 1 dong khoi don (cong doan/giu cho/ke hoach cua dong cascade theo).</summary>
    Task<bool> DeleteItemAsync(TenantScope scope, long itemId);

    /// <summary>Don cua 1 dong (de kiem tra trang thai truoc khi sua/xoa dong). Null neu khong co.</summary>
    Task<long?> GetItemOrderIdAsync(TenantScope scope, long itemId);

    /// <summary>So dong con lai cua don (chan xoa het dong cuoi cung).</summary>
    Task<int> CountItemsAsync(TenantScope scope, long orderId);

    /// <summary>Cac dong cua don kem BOM/SL — doc khi confirm de giu cho theo tung dong.</summary>
    Task<IEnumerable<OrderItemNeedRow>> ListItemsForConfirmAsync(TenantScope scope, long orderId);

    /// <summary>Gan bom_id cho 1 DONG don (khi dong chua chi dinh BOM).</summary>
    Task SetItemBomAsync(TenantScope scope, long itemId, long bomId);

    // ----- Confirm (giu cho) -----

    /// <summary>Khoa dong don hang (FOR UPDATE). Null neu khong co.</summary>
    Task<OrderLockRow?> LockOrderAsync(TenantScope scope, long id);

    /// <summary>Tim BOM ACTIVE cua san pham (null neu khong co).</summary>
    Task<long?> FindActiveBomAsync(TenantScope scope, long productId);

    /// <summary>Gan bom_id cho don hang (khi don chua chi dinh BOM).</summary>
    Task SetBomAsync(TenantScope scope, long id, long bomId);

    /// <summary>Cac dong NVL cua BOM.</summary>
    Task<IEnumerable<BomItemNeed>> ListBomItemsAsync(TenantScope scope, long bomId);

    /// <summary>Cac dong stock con kha dung cho 1 NVL, khoa FOR UPDATE, sap kha dung giam dan.</summary>
    Task<IEnumerable<StockLockRow>> LockAvailableStockAsync(TenantScope scope, long materialId);

    /// <summary>Cong qty_reserved cho 1 dong stock (da khoa).</summary>
    Task AddReservedAsync(TenantScope scope, long stockId, decimal take);

    /// <summary>Ghi/cong phieu giu cho ACTIVE (upsert theo DONG DON + NVL + kho).</summary>
    Task UpsertReservationAsync(TenantScope scope, long orderId, long orderItemId, long materialId, long warehouseId, decimal take);

    /// <summary>Doi trang thai don sang CONFIRMED + set confirmed_at.</summary>
    Task MarkConfirmedAsync(TenantScope scope, long id);

    // ----- Cancel (nha giu cho) -----

    /// <summary>Cac phieu giu cho ACTIVE cua don + khoa dong stock tuong ung (FOR UPDATE).</summary>
    Task<IEnumerable<ActiveReservationRow>> LockActiveReservationsAsync(TenantScope scope, long orderId);

    /// <summary>Tru qty_reserved cua dong stock (theo kho+NVL).</summary>
    Task ReleaseStockAsync(TenantScope scope, long warehouseId, long materialId, decimal qty);

    /// <summary>Danh dau 1 phieu giu cho la RELEASED.</summary>
    Task MarkReservationReleasedAsync(TenantScope scope, long reservationId);

    /// <summary>Doi trang thai don sang CANCELLED.</summary>
    Task MarkCancelledAsync(TenantScope scope, long id);
    // ----- Sua / Xoa don -----
    /// <summary>Sua so luong/han giao/ghi chu cua don (service da kiem tra DRAFT).</summary>
    Task<bool> UpdateAsync(TenantScope scope, long id, UpdateProductionOrderRequest req);
    /// <summary>Anh huong khi xoa don: dem ke hoach/cong doan/giu cho/phieu xuat/phieu TP/hao hut.</summary>
    Task<OrderImpactDto?> GetImpactAsync(TenantScope scope, long id);
    /// <summary>Xoa don (plans/steps/reservations cascade; service da chan khi co phieu xuat/TP).</summary>
    Task<bool> DeleteAsync(TenantScope scope, long id);
}
