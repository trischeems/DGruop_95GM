using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Repositories;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Tenancy;

namespace GM95.Server.Apps.ManagerPerformance.Services;

/// <summary>Nghiep vu ke hoach san xuat. Moi thao tac chay trong 1 transaction dung schema tenant.</summary>
public sealed class ProductionPlanService
{
    // Trang thai hop le (khop CHECK constraint tren cot status).
    private static readonly HashSet<string> AllowedStatus =
        new(StringComparer.Ordinal) { "PLANNED", "RELEASED", "IN_PROGRESS", "DONE", "CANCELLED" };

    // State machine ke hoach: tu trang thai X duoc chuyen sang nhung trang thai nao.
    // Luong tien: PLANNED -> RELEASED -> IN_PROGRESS -> DONE. Huy duoc tu cac buoc chua xong.
    // DONE cho phep nhay thang tu PLANNED/RELEASED (danh dau "xong" khong bat buoc qua tung buoc).
    // DONE/CANCELLED la trang thai KET THUC -> khong quay lai (chong "Done ve Planned",
    // va chong cong tien do cong doan 2 lan).
    private static readonly Dictionary<string, string[]> AllowedTransitions = new(StringComparer.Ordinal)
    {
        ["PLANNED"]     = new[] { "RELEASED", "IN_PROGRESS", "DONE", "CANCELLED" },
        ["RELEASED"]    = new[] { "IN_PROGRESS", "PLANNED", "DONE", "CANCELLED" },
        ["IN_PROGRESS"] = new[] { "DONE", "CANCELLED" },
        ["DONE"]        = Array.Empty<string>(),   // ket thuc
        ["CANCELLED"]   = Array.Empty<string>(),   // ket thuc
    };

    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IProductionPlanRepository _repo;
    private readonly IProductionStepRepository _steps;

    public ProductionPlanService(
        ITenantConnection db, ITenantContext tenant,
        IProductionPlanRepository repo, IProductionStepRepository steps)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
        _steps = steps;
    }

    public Task<IEnumerable<ProductionPlanDto>> ListAsync(long? orderId, int? year, int? month, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListAsync(s, orderId, year, month), ct);

    public Task<long> CreateAsync(CreateProductionPlanRequest req, CancellationToken ct)
    {
        if (req.ProductionOrderId <= 0) throw new ArgumentException("productionOrderId khong hop le.");
        if (req.PlannedQty <= 0) throw new ArgumentException("plannedQty phai > 0.");
        if (req.PlannedStart.HasValue && req.PlannedEnd.HasValue && req.PlannedEnd < req.PlannedStart)
            throw new ArgumentException("plannedEnd phai >= plannedStart.");

        return _db.RunAsync(_tenant.Tenant, async s =>
        {
            // RANG BUOC: tong SL cac ke hoach cua don KHONG duoc vuot SL dat cua don.
            // Khoa dong don (FOR UPDATE) chong race khi nhieu user cung them ke hoach.
            var orderQty = await _repo.LockOrderQuantityAsync(s, req.ProductionOrderId)
                ?? throw new ArgumentException($"Khong tim thay don san xuat id={req.ProductionOrderId}.");
            var existing = await _repo.SumPlannedQtyAsync(s, req.ProductionOrderId, null);
            if (existing + req.PlannedQty > orderQty)
                throw new ArgumentException(
                    $"Tong SL ke hoach ({existing + req.PlannedQty:0.####}) vuot SL dat cua don ({orderQty:0.####}). " +
                    $"Con lai co the lap: {orderQty - existing:0.####}.");

            return await _repo.InsertAsync(s, req);
        }, ct);
    }

    public Task<bool> UpdateStatusAsync(long id, UpdatePlanStatusRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Status) || !AllowedStatus.Contains(req.Status))
            throw new ArgumentException("status khong hop le.");

        return _db.RunAsync(_tenant.Tenant, async s =>
        {
            // THU TU KHOA TOAN CUC: don -> ke hoach -> cong doan (khop ProductionStepService)
            // — chong deadlock khi nguoi khac dang luu cong doan cua cung don.
            var orderId = await _repo.GetPlanOrderIdAsync(s, id);
            if (orderId is null) return false;
            var orderQty = await _repo.LockOrderQuantityAsync(s, orderId.Value);

            // Doc trang thai hien tai (khoa dong) roi kiem tra chuyen trang thai co hop le khong.
            var cur = await _repo.LockPlanAsync(s, id);
            if (cur is null) return false;
            if (cur.Status == req.Status) return true; // giu nguyen -> khong lam gi

            var allowed = AllowedTransitions.TryGetValue(cur.Status, out var next) ? next : Array.Empty<string>();
            if (!allowed.Contains(req.Status))
                throw new ArgumentException(
                    $"Khong the chuyen ke hoach tu '{cur.Status}' sang '{req.Status}'. " +
                    (allowed.Length == 0
                        ? "Day la trang thai ket thuc, khong doi duoc."
                        : $"Chi duoc chuyen sang: {string.Join(", ", allowed)}."));

            var ok = await _repo.UpdateStatusAsync(s, id, req.Status);

            // ----- LIEN KET MODULE (cung transaction) -----
            // Ke hoach DONE -> tu dong keo tien do cac cong doan cua don len TONG SL ke hoach da DONE
            // (du SL don thi cong doan DONE) — khong can nhap tay nua.
            if (ok && req.Status == "DONE" && orderQty.HasValue)
            {
                // Don chua bam "Khoi tao cong doan" thi tu sinh truoc (idempotent) —
                // neu khong, tien do cua ke hoach DONE se mat khong ghi lai duoc (DONE la ket thuc).
                await _steps.InitStepsAsync(s, cur.ProductionOrderId);
                var doneQty = await _steps.SumDonePlannedQtyAsync(s, cur.ProductionOrderId);
                await _steps.AutoProgressStepsAsync(s, cur.ProductionOrderId, doneQty, orderQty.Value);
                await _steps.MarkOrderInProgressAsync(s, cur.ProductionOrderId);
            }
            // Ke hoach bat dau chay -> keo don CONFIRMED sang IN_PROGRESS cho khop.
            else if (ok && req.Status == "IN_PROGRESS")
            {
                await _steps.MarkOrderInProgressAsync(s, cur.ProductionOrderId);
            }

            return ok;
        }, ct);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.DeleteAsync(s, id), ct);

    /// <summary>Sua ke hoach: so luong / ma chuyen / ghi chu.</summary>
    public Task<bool> UpdateAsync(long id, UpdatePlanRequest req, CancellationToken ct)
    {
        if (req.PlannedQty <= 0) throw new ArgumentException("plannedQty phai > 0.");
        return _db.RunAsync(_tenant.Tenant, async s =>
        {
            // Thu tu khoa toan cuc: don -> ke hoach (khop UpdateStatusAsync/ProductionStepService).
            var orderId = await _repo.GetPlanOrderIdAsync(s, id);
            if (orderId is null) return false;
            var orderQty = await _repo.LockOrderQuantityAsync(s, orderId.Value)
                ?? throw new ArgumentException("Khong tim thay don san xuat cua ke hoach.");

            var cur = await _repo.LockPlanAsync(s, id);
            if (cur is null) return false;

            // Ke hoach da KET THUC: SL cua no da duoc cong vao tien do cong doan (DONE) —
            // sua SL luc nay se lech so lieu khong bao gio dong bo lai duoc.
            if (cur.Status is "DONE" or "CANCELLED")
                throw new ArgumentException(
                    $"Ke hoach da o trang thai ket thuc ({cur.Status}), khong sua duoc SL/chuyen.");

            // Khi sua SL 1 ke hoach cung phai dam bao tong (khong tinh chinh no) + SL moi <= SL don.
            var others = await _repo.SumPlannedQtyAsync(s, cur.ProductionOrderId, id);
            if (others + req.PlannedQty > orderQty)
                throw new ArgumentException(
                    $"Tong SL ke hoach ({others + req.PlannedQty:0.####}) vuot SL dat cua don ({orderQty:0.####}).");

            return await _repo.UpdateAsync(s, id, req);
        }, ct);
    }
}
