using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Truy van/ghi cac buoc quy trinh san xuat. Ghi lam trong 1 transaction cua scope.</summary>
public interface IProductionStepRepository
{
    /// <summary>Tao du 4 dong cong doan cho don (bo qua neu da co). Tra ve so dong them moi.</summary>
    Task<int> InitStepsAsync(TenantScope scope, long orderId);

    /// <summary>Danh sach buoc quy trinh cua 1 don, JOIN cong doan, sap xep theo seq.</summary>
    Task<IEnumerable<ProductionStepDto>> ListByOrderAsync(TenantScope scope, long orderId);

    /// <summary>Khoa dong buoc (SELECT ... FOR UPDATE), tra ve trang thai + started_at (null neu khong co).</summary>
    Task<(string Status, DateTime? StartedAt)?> LockStepAsync(TenantScope scope, long id);

    /// <summary>Khoa dong buoc + tra kem production_order_id va ma cong doan (de lien ket module). Null neu khong co.</summary>
    Task<StepContextRow?> LockStepContextAsync(TenantScope scope, long id);

    /// <summary>Doc order id cua buoc KHONG khoa (phuc vu khoa don truoc — thu tu khoa toan cuc).</summary>
    Task<long?> GetStepOrderIdAsync(TenantScope scope, long id);

    /// <summary>Khoa dong don (FOR UPDATE). Goi TRUOC moi khoa ke hoach/cong doan de chong deadlock.</summary>
    Task<int> LockOrderAsync(TenantScope scope, long orderId);

    /// <summary>Tong TP da nhap kho cua don (SUM finished_goods_receipts.qty_received).</summary>
    Task<decimal> SumFinishedGoodsAsync(TenantScope scope, long orderId);

    /// <summary>
    /// Cap nhat 1 buoc: status, qty_in, qty_out, qty_defect, note, updated_at.
    /// started_at = now() khi chuyen sang IN_PROGRESS ma started_at IS NULL.
    /// finished_at = now() khi status = 'DONE'. Tra ve so dong cap nhat.
    /// </summary>
    Task<int> UpdateStepAsync(TenantScope scope, long id, UpdateStepRequest req);

    // ----- Lien ket trang thai module (goi trong cung transaction cap nhat buoc) -----

    /// <summary>Day don sang IN_PROGRESS neu dang CONFIRMED (khi bat dau san xuat cong doan). Tra ve so dong doi.</summary>
    Task<int> MarkOrderInProgressAsync(TenantScope scope, long orderId);
    /// <summary>Day cac ke hoach PLANNED/RELEASED cua don sang IN_PROGRESS (khi bat dau san xuat). Tra ve so dong doi.</summary>
    Task<int> MarkPlansInProgressAsync(TenantScope scope, long orderId);
    /// <summary>Day cac ke hoach IN_PROGRESS cua don sang DONE (khi cong doan cuoi hoan tat). Tra ve so dong doi.</summary>
    Task<int> MarkPlansDoneAsync(TenantScope scope, long orderId);

    /// <summary>
    /// Tu dong keo tien do MOI cong doan cua don (tru CANCELLED/da bo qua) len muc doneQty
    /// (tong SL cac ke hoach da DONE) bang GREATEST — khong cong don nen khong dem trung voi nhap tay.
    /// Du SL dat cua don thi cong doan DONE, chua du thi IN_PROGRESS. Tra ve so cong doan da cap nhat.
    /// </summary>
    Task<int> AutoProgressStepsAsync(TenantScope scope, long orderId, decimal doneQty, decimal orderQty);

    /// <summary>Tong planned_qty cac ke hoach DONE cua don (thuoc do tien do tu dong).</summary>
    Task<decimal> SumDonePlannedQtyAsync(TenantScope scope, long orderId);
}

/// <summary>Ngu canh 1 buoc (da khoa FOR UPDATE) phuc vu lien ket module.</summary>
public sealed record StepContextRow(long ProductionOrderId, string StageCode, int Seq, string Status);
