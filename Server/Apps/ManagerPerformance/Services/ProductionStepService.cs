using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Repositories;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Tenancy;

namespace GM95.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Nghiep vu quy trinh san xuat (Cat -> May -> QC -> Nhap TP), dang state machine.
/// Cap nhat 1 buoc lam trong 1 transaction: khoa dong (FOR UPDATE) -> cap nhat.
/// Chong race khi 500+ user cap nhat dong thoi.
/// </summary>
public sealed class ProductionStepService
{
    // Khop CHECK constraint tren cot status.
    private static readonly string[] AllowedStatus =
        { "PENDING", "IN_PROGRESS", "DONE", "ON_HOLD", "CANCELLED" };

    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IProductionStepRepository _repo;

    public ProductionStepService(ITenantConnection db, ITenantContext tenant, IProductionStepRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    /// <summary>Khoi tao 4 dong cong doan cho don (bo qua neu da co).</summary>
    public Task InitAsync(long orderId, CancellationToken ct)
    {
        if (orderId <= 0) throw new ArgumentException("orderId khong hop le.");
        return _db.RunAsync(_tenant.Tenant, s => _repo.InitStepsAsync(s, orderId), ct);
    }

    /// <summary>Danh sach buoc quy trinh cua 1 don, sap xep theo seq.</summary>
    public Task<IEnumerable<ProductionStepDto>> ListByOrderAsync(long orderId, CancellationToken ct)
    {
        if (orderId <= 0) throw new ArgumentException("orderId khong hop le.");
        return _db.RunAsync(_tenant.Tenant, s => _repo.ListByOrderAsync(s, orderId), ct);
    }

    /// <summary>
    /// Cap nhat 1 buoc quy trinh. Tra ve false neu khong tim thay dong.
    /// Khoa dong (FOR UPDATE) truoc khi cap nhat trong cung 1 transaction.
    /// </summary>
    public Task<bool> UpdateAsync(long id, UpdateStepRequest req, CancellationToken ct)
    {
        if (id <= 0) throw new ArgumentException("id khong hop le.");
        if (!AllowedStatus.Contains(req.Status)) throw new ArgumentException("Trang thai khong hop le.");
        if (req.QtyIn < 0 || req.QtyOut < 0 || req.QtyDefect < 0)
            throw new ArgumentException("So luong khong duoc am.");
        // Khop CHECK qty_out+qty_defect<=qty_in (tru khi qty_in=0).
        if (req.QtyIn > 0 && req.QtyOut + req.QtyDefect > req.QtyIn)
            throw new ArgumentException("qty_out + qty_defect khong duoc vuot qua qty_in.");
        // ROT: khi DONE thi ra + loi PHAI bang vao (khong that thoat san luong khong ro).
        //      Dang lam do (chua DONE) van cho phep ra + loi < vao.
        if (req.Status == "DONE" && req.QtyIn > 0 && req.QtyOut + req.QtyDefect != req.QtyIn)
            throw new ArgumentException(
                $"Cong doan DONE: SL ra ({req.QtyOut:0.####}) + SL loi ({req.QtyDefect:0.####}) phai bang SL vao ({req.QtyIn:0.####}).");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // Khoa dong buoc + lay ngu canh (don, ma cong doan); khong ton tai -> NOT_FOUND.
            var ctx = await _repo.LockStepContextAsync(scope, id);
            if (ctx is null) return false;

            // CHAN sua khi cong doan da KET THUC (DONE/CANCELLED) -> khong sua so lieu nguoc.
            if (ctx.Status is "DONE" or "CANCELLED")
                throw new ArgumentException(
                    $"Cong doan da o trang thai {ctx.Status}, khong sua duoc so lieu. " +
                    "Neu can dieu chinh, hay mo lai cong doan truoc.");

            await _repo.UpdateStepAsync(scope, id, req);

            // ----- LIEN KET TRANG THAI MODULE (cung transaction) -----
            // Khi bat dau san xuat 1 cong doan (IN_PROGRESS): keo don + ke hoach sang IN_PROGRESS.
            if (req.Status == "IN_PROGRESS")
            {
                await _repo.MarkOrderInProgressAsync(scope, ctx.ProductionOrderId);
                await _repo.MarkPlansInProgressAsync(scope, ctx.ProductionOrderId);
            }
            // Khi cong doan CUOI (FG_RECEIPT - nhap kho TP) DONE: keo ke hoach sang DONE.
            // (Don se COMPLETED khi nhap kho thanh pham o module Kho TP.)
            else if (req.Status == "DONE" && ctx.StageCode == "FG_RECEIPT")
            {
                await _repo.MarkPlansDoneAsync(scope, ctx.ProductionOrderId);
            }

            return true;
        }, ct);
    }
}
