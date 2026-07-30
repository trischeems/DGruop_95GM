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
        // CONG THUC: SL ra + SL loi khong bao gio vuot SL vao (ke ca khi vao = 0 —
        // truoc day vao=0 la lo hong cho ra/loi tuy y).
        if (req.QtyOut + req.QtyDefect > req.QtyIn)
            throw new ArgumentException(
                $"SL ra ({req.QtyOut:0.####}) + SL loi ({req.QtyDefect:0.####}) khong duoc vuot SL vao ({req.QtyIn:0.####}).");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // THU TU KHOA TOAN CUC: don -> ke hoach -> cong doan (khop ProductionPlanService)
            // de 2 nguoi cung thao tac plan + step tren 1 don khong deadlock cheo nhau.
            var orderId = await _repo.GetStepOrderIdAsync(scope, id);
            if (orderId is null) return false;
            await _repo.LockOrderAsync(scope, orderId.Value);

            // Khoa dong buoc + lay ngu canh (don, ma cong doan); khong ton tai -> NOT_FOUND.
            var ctx = await _repo.LockStepContextAsync(scope, id);
            if (ctx is null) return false;

            // QC la can cu nhap kho TP: khong cho ha SL ra xuong duoi tong TP DA nhap kho
            // (giu bat bien SUM(nhap TP) <= SL ra QC ma module Kho TP dua vao).
            if (ctx.StageCode == "QC")
            {
                var received = await _repo.SumFinishedGoodsAsync(scope, ctx.ProductionOrderId);
                if (req.QtyOut < received)
                    throw new ArgumentException(
                        $"SL ra QC moi ({req.QtyOut:0.####}) thap hon tong TP da nhap kho ({received:0.####}). " +
                        "Xoa/dieu chinh phieu nhap TP truoc khi giam SL ra cua QC.");
            }

            // Cong doan DONE/CANCELLED van sua duoc (mo lai / dieu chinh so lieu) —
            // truoc day bi khoa cung khien "bam Luu khong doi duoc trang thai".
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
