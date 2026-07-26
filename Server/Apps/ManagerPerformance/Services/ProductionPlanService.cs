using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Repositories;
using DGroup.Server.Infrastructure.Data;
using DGroup.Server.Infrastructure.Tenancy;

namespace DGroup.Server.Apps.ManagerPerformance.Services;

/// <summary>Nghiep vu ke hoach san xuat. Moi thao tac chay trong 1 transaction dung schema tenant.</summary>
public sealed class ProductionPlanService
{
    // Trang thai hop le (khop CHECK constraint tren cot status).
    private static readonly HashSet<string> AllowedStatus =
        new(StringComparer.Ordinal) { "PLANNED", "RELEASED", "IN_PROGRESS", "DONE", "CANCELLED" };

    // State machine ke hoach: tu trang thai X duoc chuyen sang nhung trang thai nao.
    // Luong tien: PLANNED -> RELEASED -> IN_PROGRESS -> DONE. Huy duoc tu cac buoc chua xong.
    // DONE/CANCELLED la trang thai KET THUC -> khong quay lai (chong "Done ve Planned").
    private static readonly Dictionary<string, string[]> AllowedTransitions = new(StringComparer.Ordinal)
    {
        ["PLANNED"]     = new[] { "RELEASED", "IN_PROGRESS", "CANCELLED" },
        ["RELEASED"]    = new[] { "IN_PROGRESS", "PLANNED", "CANCELLED" },
        ["IN_PROGRESS"] = new[] { "DONE", "CANCELLED" },
        ["DONE"]        = Array.Empty<string>(),   // ket thuc
        ["CANCELLED"]   = Array.Empty<string>(),   // ket thuc
    };

    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IProductionPlanRepository _repo;

    public ProductionPlanService(ITenantConnection db, ITenantContext tenant, IProductionPlanRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
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

            return await _repo.UpdateStatusAsync(s, id, req.Status);
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
            // Khi sua SL 1 ke hoach cung phai dam bao tong (khong tinh chinh no) + SL moi <= SL don.
            var cur = await _repo.LockPlanAsync(s, id);
            if (cur is null) return false;
            var orderQty = await _repo.LockOrderQuantityAsync(s, cur.ProductionOrderId)
                ?? throw new ArgumentException("Khong tim thay don san xuat cua ke hoach.");
            var others = await _repo.SumPlannedQtyAsync(s, cur.ProductionOrderId, id);
            if (others + req.PlannedQty > orderQty)
                throw new ArgumentException(
                    $"Tong SL ke hoach ({others + req.PlannedQty:0.####}) vuot SL dat cua don ({orderQty:0.####}).");

            return await _repo.UpdateAsync(s, id, req);
        }, ct);
    }
}
