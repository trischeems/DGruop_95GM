using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Truy van/ghi bao cao hao hut (doi chieu cap phat vs dinh muc). Ghi lam trong 1 transaction cua scope.</summary>
public interface ILossReportRepository
{
    /// <summary>Danh sach bao cao (loc theo don neu co), sap xep material_id.</summary>
    Task<IEnumerable<LossReportDto>> ListAsync(TenantScope scope, long? orderId);

    /// <summary>Cac MAT HANG cua don kem BOM + TP da nhap kho rieng (V007 — hao hut tinh theo tung mat hang).</summary>
    Task<IEnumerable<LossItemRow>> ListItemsForLossAsync(TenantScope scope, long orderId);

    /// <summary>Cac dong NVL trong dinh muc (bom_id) de tinh dinh muc chuan.</summary>
    Task<IEnumerable<BomItemDto>> ListBomItemsAsync(TenantScope scope, long bomId);

    /// <summary>Tong da cap phat cho 1 NVL cua RIENG 1 mat hang trong don.</summary>
    Task<decimal> GetIssuedQtyForItemAsync(TenantScope scope, long orderId, long orderItemId, long materialId);

    /// <summary>UPSERT 1 dong bao cao theo (production_order_item_id, material_id). qty_variance do DB tu tinh.</summary>
    Task UpsertAsync(
        TenantScope scope, long orderId, long orderItemId, long materialId,
        decimal qtyIssued, decimal qtyStandard, decimal finishedQty);
}

/// <summary>1 MAT HANG cua don khi tinh hao hut: BOM chot + TP da nhap kho cua rieng no.</summary>
public sealed record LossItemRow(long Id, long ProductId, long? BomId, decimal FinishedQty);
