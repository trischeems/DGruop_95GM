using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Repositories;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Tenancy;

namespace GM95.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Nghiep vu nhap kho thanh pham. Tao phieu nhap; khi da nhap DU thi dong lenh san xuat
/// (COMPLETED) + dong cong doan + dong ke hoach — tat ca trong CUNG 1 transaction (1 RunAsync).
/// Nhap tung phan thi don van IN_PROGRESS de nhap tiep duoc.
/// </summary>
public sealed class FinishedGoodsService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IFinishedGoodsRepository _repo;
    private readonly IProductionStepRepository _steps;

    public FinishedGoodsService(
        ITenantConnection db, ITenantContext tenant,
        IFinishedGoodsRepository repo, IProductionStepRepository steps)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
        _steps = steps;
    }

    public Task<IEnumerable<FinishedGoodsReceiptDto>> ListAsync(long? orderId, int? year, int? month, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListAsync(s, orderId, year, month), ct);

    public Task<long> CreateAsync(CreateFinishedGoodsRequest req, CancellationToken ct)
    {
        if (req.QtyReceived <= 0) throw new ArgumentException("So luong nhap phai > 0.");
        if (req.ProductionOrderId <= 0) throw new ArgumentException("Lenh san xuat khong hop le.");
        if (req.ProductionOrderItemId <= 0) throw new ArgumentException("Chua chon mat hang de nhap kho.");
        if (req.ProductId <= 0 || req.WarehouseId <= 0) throw new ArgumentException("Ma hang/kho khong hop le.");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // 0) Khoa don + MAT HANG + kiem tra dieu kien nhap kho TP cua rieng mat hang do.
            var chk = await _repo.LockItemForReceiptAsync(scope, req.ProductionOrderItemId)
                ?? throw new ArgumentException($"Khong tim thay mat hang id={req.ProductionOrderItemId} trong don.");

            // (a) Don phai dang CONFIRMED hoac IN_PROGRESS (da xac nhan/dang lam). Chan DRAFT/CANCELLED/COMPLETED.
            if (chk.Status is not ("CONFIRMED" or "IN_PROGRESS"))
                throw new ArgumentException(
                    $"Chi nhap kho TP cho don da xac nhan / dang san xuat. Trang thai don hien tai: {chk.Status}.");

            // (b) Ma hang nhap phai dung ma hang CUA DONG do (chong nhap nham mat hang).
            if (req.ProductId != chk.ProductId)
                throw new ArgumentException(
                    $"Ma hang nhap (id={req.ProductId}) khong khop ma hang cua dong don (id={chk.ProductId}).");

            // (c) Phai co san luong lam can cu nhap (SL ra cua QC, hoac cua cong doan cao nhat).
            //     Khong ep buoc phai co cong doan QC — quy trinh V006 tu do, QC co the khong co.
            if (chk.ReceivableLimit <= 0)
                throw new ArgumentException(
                    "Mat hang nay chua co san luong de nhap kho. Hay vao tab Cong doan, cap nhat 'SL ra' " +
                    "cua cong doan cuoi (hoac QC) cua mat hang truoc khi nhap kho.");

            // (d) Tong da nhap + lan nay khong duoc vuot san luong cho phep CUA MAT HANG.
            if (chk.AlreadyReceived + req.QtyReceived > chk.ReceivableLimit)
                throw new ArgumentException(
                    $"Nhap vuot san luong: da nhap {chk.AlreadyReceived:0.####} + lan nay {req.QtyReceived:0.####} " +
                    $"> san luong cho phep {chk.ReceivableLimit:0.####}. Con lai co the nhap: {chk.ReceivableLimit - chk.AlreadyReceived:0.####}.");

            // 1) Tao phieu nhap kho thanh pham (receipt_no sinh trong SQL).
            var id = await _repo.InsertAsync(scope, req);

            // 2) Nhap DU cho MAT HANG nay -> dong cong doan FG_RECEIPT cua rieng mat hang do.
            if (chk.AlreadyReceived + req.QtyReceived >= chk.ReceivableLimit)
            {
                await _repo.MarkItemFinishingStepsDoneAsync(scope, req.ProductionOrderItemId);

                // 3) Chi dong CA DON khi MOI mat hang trong don deu da nhap du.
                //    Nhap tung phan / con mat hang khac dang lam thi don giu IN_PROGRESS de nhap tiep.
                if (await _repo.AreAllItemsReceivedAsync(scope, req.ProductionOrderId))
                {
                    await _repo.MarkOrderCompletedAsync(scope, req.ProductionOrderId);
                    await _repo.MarkFinishingStepsDoneAsync(scope, req.ProductionOrderId);

                    // Moi cong doan phai lam cua don da xong -> dong luon cac ke hoach cua don.
                    if (await _steps.AreAllStepsDoneAsync(scope, req.ProductionOrderId))
                        await _steps.MarkPlansDoneAsync(scope, req.ProductionOrderId);
                }
            }

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
