using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

/// <summary>
/// Thong tin don phuc vu kiem tra khi nhap kho thanh pham (da khoa dong don FOR UPDATE):
/// - Status/ProductId: chan nhap sai trang thai / sai ma hang.
/// - QcOutput: san luong dat cua cong doan QC (qty_out) — tran duoc phep nhap.
///   Neu chua co cong doan QC thi null (chua san xuat/khoi tao) -> chan nhap.
/// - AlreadyReceived: tong da nhap kho cua don (chong nhap vuot).
/// </summary>
public sealed record OrderReceiptCheckRow(
    string Status, long ProductId, decimal? QcOutput, decimal AlreadyReceived);

/// <summary>Truy van/ghi phieu nhap kho thanh pham. Ghi PHAI trong 1 transaction cua scope.</summary>
public interface IFinishedGoodsRepository
{
    /// <summary>Khoa don (FOR UPDATE) + lay status/product/san luong QC/tong da nhap. Null neu don khong ton tai.</summary>
    Task<OrderReceiptCheckRow?> LockOrderForReceiptAsync(TenantScope scope, long orderId);

    /// <summary>Danh sach phieu nhap (loc theo lenh san xuat neu co), moi nhat truoc.</summary>
    Task<IEnumerable<FinishedGoodsReceiptDto>> ListAsync(TenantScope scope, long? orderId, int? year, int? month);

    /// <summary>Tao phieu nhap thanh pham (receipt_no sinh trong SQL 'NTP-'+timestamp). Tra ve id.</summary>
    Task<long> InsertAsync(TenantScope scope, CreateFinishedGoodsRequest req);

    /// <summary>Danh dau lenh san xuat da hoan thanh (COMPLETED) neu dang CONFIRMED/IN_PROGRESS. Tra ve so dong cap nhat.</summary>
    Task<int> MarkOrderCompletedAsync(TenantScope scope, long orderId);

    /// <summary>
    /// Lien ket: khi nhap kho TP, danh dau cong doan FG_RECEIPT (va QC neu con do dang) -> DONE.
    /// Chi cap nhat cong doan chua ket thuc. Tra ve so dong cap nhat.
    /// </summary>
    Task<int> MarkFinishingStepsDoneAsync(TenantScope scope, long orderId);
    // ----- Xoa phieu nhap TP -----
    /// <summary>Anh huong khi xoa phieu: trang thai don, so phieu con lai, hao hut. Null neu khong ton tai.</summary>
    Task<FinishedGoodsImpactDto?> GetImpactAsync(TenantScope scope, long id);
    /// <summary>Id don cua 1 phieu (null neu phieu khong ton tai).</summary>
    Task<long?> GetOrderIdAsync(TenantScope scope, long id);
    /// <summary>Xoa phieu nhap TP. Tra ve true neu co dong bi xoa.</summary>
    Task<bool> DeleteAsync(TenantScope scope, long id);
    /// <summary>Dem so phieu nhap TP con lai cua don.</summary>
    Task<long> CountByOrderAsync(TenantScope scope, long orderId);
    /// <summary>Tra don ve IN_PROGRESS neu dang COMPLETED (khi xoa het phieu nhap TP).</summary>
    Task<int> RevertOrderToInProgressAsync(TenantScope scope, long orderId);
}
