using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Truy van/ghi cac buoc quy trinh san xuat. Ghi lam trong 1 transaction cua scope.</summary>
public interface IProductionStepRepository
{
    /// <summary>Tao du 4 dong cong doan cho don (bo qua neu da co). Tra ve so dong them moi.</summary>
    Task<int> InitStepsAsync(TenantScope scope, long orderId);

    /// <summary>Sinh cong doan cho RIENG 1 mat hang cua don (theo mau quy trinh cua dong do).</summary>
    Task<int> InitStepsForItemAsync(TenantScope scope, long orderItemId);

    /// <summary>Danh sach buoc quy trinh cua 1 don (moi mat hang mot bo), sap theo dong roi seq.</summary>
    Task<IEnumerable<ProductionStepDto>> ListByOrderAsync(TenantScope scope, long orderId);

    /// <summary>Danh sach buoc quy trinh cua RIENG 1 mat hang trong don.</summary>
    Task<IEnumerable<ProductionStepDto>> ListByItemAsync(TenantScope scope, long orderItemId);

    /// <summary>Khoa dong buoc (SELECT ... FOR UPDATE), tra ve trang thai + started_at (null neu khong co).</summary>
    Task<(string Status, DateTime? StartedAt)?> LockStepAsync(TenantScope scope, long id);

    /// <summary>Khoa dong buoc + tra kem production_order_id va ma cong doan (de lien ket module). Null neu khong co.</summary>
    Task<StepContextRow?> LockStepContextAsync(TenantScope scope, long id);

    /// <summary>Doc order id cua buoc KHONG khoa (phuc vu khoa don truoc — thu tu khoa toan cuc).</summary>
    Task<long?> GetStepOrderIdAsync(TenantScope scope, long id);

    /// <summary>Khoa dong don (FOR UPDATE). Goi TRUOC moi khoa ke hoach/cong doan de chong deadlock.</summary>
    Task<int> LockOrderAsync(TenantScope scope, long orderId);

    /// <summary>Tong TP da nhap kho cua don (SUM finished_goods_receipts.qty_received).</summary>
    Task<decimal> SumFinishedGoodsForItemAsync(TenantScope scope, long orderItemId);

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
    /// <summary>
    /// Day MOI ke hoach chua ket thuc cua don (PLANNED/RELEASED/IN_PROGRESS) sang DONE
    /// khi don da chay xong. Ke hoach DONE/CANCELLED giu nguyen -> goi lai nhieu lan van an toan.
    /// Tra ve so dong doi.
    /// </summary>
    Task<int> MarkPlansDoneAsync(TenantScope scope, long orderId);

    /// <summary>
    /// True khi don da chay xong het: co IT NHAT 1 cong doan phai lam (khong bo qua, khong CANCELLED)
    /// VA khong con cong doan phai lam nao chua ket thuc (DONE/CANCELLED).
    /// Don chua co cong doan nao / bo qua het -> false (khong duoc coi la xong).
    /// </summary>
    Task<bool> AreAllStepsDoneAsync(TenantScope scope, long orderId);

    /// <summary>
    /// Tu dong keo tien do MOI cong doan cua don (tru CANCELLED/da bo qua) len muc doneQty
    /// (tong SL cac ke hoach da DONE) bang GREATEST — khong cong don nen khong dem trung voi nhap tay.
    /// Du SL dat cua don thi cong doan DONE, chua du thi IN_PROGRESS. Tra ve so cong doan da cap nhat.
    /// </summary>
    Task<int> AutoProgressStepsAsync(TenantScope scope, long orderId, decimal doneQty, decimal orderQty);

    /// <summary>Tong planned_qty cac ke hoach DONE cua don (thuoc do tien do tu dong).</summary>
    Task<decimal> SumDonePlannedQtyAsync(TenantScope scope, long orderId);

    /// <summary>
    /// Nhu AutoProgressStepsAsync nhung cho RIENG 1 MAT HANG cua don (V007):
    /// keo cong doan cua mat hang do len muc doneQty; du SL mat hang thi DONE.
    /// </summary>
    Task<int> AutoProgressItemStepsAsync(TenantScope scope, long orderItemId, decimal doneQty, decimal itemQty);

    /// <summary>Tong planned_qty cac ke hoach DONE cua RIENG 1 mat hang.</summary>
    Task<decimal> SumDonePlannedQtyForItemAsync(TenantScope scope, long orderItemId);
}

/// <summary>Ngu canh 1 buoc (da khoa FOR UPDATE) phuc vu lien ket module.</summary>
public sealed record StepContextRow(
    long ProductionOrderId, long ProductionOrderItemId, string StageCode, int Seq, string Status);
