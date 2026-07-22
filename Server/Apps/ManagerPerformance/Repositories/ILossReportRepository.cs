using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Truy van/ghi bao cao hao hut (doi chieu cap phat vs dinh muc). Ghi lam trong 1 transaction cua scope.</summary>
public interface ILossReportRepository
{
    /// <summary>Danh sach bao cao (loc theo don neu co), sap xep material_id.</summary>
    Task<IEnumerable<LossReportDto>> ListAsync(TenantScope scope, long? orderId);

    /// <summary>Tong thanh pham da nhap kho cua 1 don (COALESCE SUM(qty_received), 0).</summary>
    Task<decimal> GetFinishedQtyAsync(TenantScope scope, long orderId);

    /// <summary>bom_id cua 1 don san xuat (null neu chua chot BOM / khong co don).</summary>
    Task<long?> GetBomIdAsync(TenantScope scope, long orderId);

    /// <summary>Cac dong NVL trong dinh muc (bom_id) de tinh dinh muc chuan.</summary>
    Task<IEnumerable<BomItemDto>> ListBomItemsAsync(TenantScope scope, long bomId);

    /// <summary>Tong da cap phat cho 1 NVL cua 1 don (COALESCE SUM(mii.qty_issued), 0).</summary>
    Task<decimal> GetIssuedQtyAsync(TenantScope scope, long orderId, long materialId);

    /// <summary>UPSERT 1 dong bao cao theo (production_order_id, material_id). qty_variance do DB tu tinh.</summary>
    Task UpsertAsync(
        TenantScope scope, long orderId, long materialId,
        decimal qtyIssued, decimal qtyStandard, decimal finishedQty);
}
