using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Repositories;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Tenancy;

namespace GM95.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Nghiep vu nhap kho thanh pham. Tao phieu nhap + dong lenh san xuat (COMPLETED)
/// trong CUNG 1 transaction (1 RunAsync) de dam bao nhat quan.
/// </summary>
public sealed class FinishedGoodsService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IFinishedGoodsRepository _repo;

    public FinishedGoodsService(ITenantConnection db, ITenantContext tenant, IFinishedGoodsRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    public Task<IEnumerable<FinishedGoodsReceiptDto>> ListAsync(long? orderId, int? year, int? month, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListAsync(s, orderId, year, month), ct);

    public Task<long> CreateAsync(CreateFinishedGoodsRequest req, CancellationToken ct)
    {
        if (req.QtyReceived <= 0) throw new ArgumentException("So luong nhap phai > 0.");
        if (req.ProductionOrderId <= 0) throw new ArgumentException("Lenh san xuat khong hop le.");
        if (req.ProductId <= 0 || req.WarehouseId <= 0) throw new ArgumentException("Ma hang/kho khong hop le.");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // 0) Khoa don + kiem tra dieu kien nhap kho TP (chong nhap khi don chua san xuat).
            var chk = await _repo.LockOrderForReceiptAsync(scope, req.ProductionOrderId)
                ?? throw new ArgumentException($"Khong tim thay don san xuat id={req.ProductionOrderId}.");

            // (a) Don phai dang CONFIRMED hoac IN_PROGRESS (da xac nhan/dang lam). Chan DRAFT/CANCELLED/COMPLETED.
            if (chk.Status is not ("CONFIRMED" or "IN_PROGRESS"))
                throw new ArgumentException(
                    $"Chi nhap kho TP cho don da xac nhan / dang san xuat. Trang thai don hien tai: {chk.Status}.");

            // (b) Ma hang nhap phai dung ma hang cua don (chong nhap nham mat hang).
            if (req.ProductId != chk.ProductId)
                throw new ArgumentException(
                    $"Ma hang nhap (id={req.ProductId}) khong khop ma hang cua don (id={chk.ProductId}).");

            // (c) Phai da qua QC moi cho nhap: cong doan QC phai ton tai va co san luong dat > 0.
            if (chk.QcOutput is null)
                throw new ArgumentException(
                    "Don chua co cong doan QC (chua khoi tao/san xuat). Khong the nhap kho thanh pham.");
            if (chk.QcOutput.Value <= 0)
                throw new ArgumentException("Cong doan QC chua co san luong dat. Khong the nhap kho thanh pham.");

            // (d) Tong da nhap + lan nay khong duoc vuot san luong dat cua QC.
            if (chk.AlreadyReceived + req.QtyReceived > chk.QcOutput.Value)
                throw new ArgumentException(
                    $"Nhap vuot san luong dat: da nhap {chk.AlreadyReceived:0.####} + lan nay {req.QtyReceived:0.####} " +
                    $"> san luong QC {chk.QcOutput.Value:0.####}. Con lai co the nhap: {chk.QcOutput.Value - chk.AlreadyReceived:0.####}.");

            // 1) Tao phieu nhap kho thanh pham (receipt_no sinh trong SQL).
            var id = await _repo.InsertAsync(scope, req);

            // 2) Dong lenh san xuat -> COMPLETED (chi khi dang CONFIRMED/IN_PROGRESS). Cung transaction.
            await _repo.MarkOrderCompletedAsync(scope, req.ProductionOrderId);

            // 3) LIEN KET: danh dau cong doan FG_RECEIPT (nhap kho TP) sang DONE. Cung transaction.
            await _repo.MarkFinishingStepsDoneAsync(scope, req.ProductionOrderId);

            return id;
        }, ct);
    }

    /// <summary>Anh huong khi xoa phieu nhap TP (phuc vu popup canh bao truoc khi xoa).</summary>
    public Task<FinishedGoodsImpactDto?> GetImpactAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.GetImpactAsync(s, id), ct);

    /// <summary>
    /// Xoa phieu nhap TP trong 1 transaction: xoa phieu; neu don khong con phieu nao va dang
    /// COMPLETED thi tra ve IN_PROGRESS. Hao hut cua don can duoc tinh lai sau khi xoa.
    /// </summary>
    public Task DeleteAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var orderId = await _repo.GetOrderIdAsync(scope, id)
                          ?? throw new ArgumentException($"Khong tim thay phieu nhap TP id={id}.");
            await _repo.DeleteAsync(scope, id);
            var remaining = await _repo.CountByOrderAsync(scope, orderId);
            if (remaining == 0)
                await _repo.RevertOrderToInProgressAsync(scope, orderId);
        }, ct);
}
