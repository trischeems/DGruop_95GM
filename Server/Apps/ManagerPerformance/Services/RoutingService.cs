using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Repositories;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Tenancy;

namespace GM95.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Nghiep vu QUY TRINH SAN XUAT LINH HOAT (V006):
///   - Quan ly danh muc cong doan (them cong doan moi: In, Theu, Say, Dong goi...).
///   - Quan ly MAU quy trinh (chuoi cong doan) - moi loai hang 1 mau.
///   - Ap mau vao don (co the bat dau tu buoc giua, khong bat buoc tu Cat vai).
///   - Sua rieng buoc cua tung don: them / xoa / bo qua / doi thu tu.
/// Moi thao tac ghi lam trong 1 RunAsync (1 transaction) de nhat quan.
/// </summary>
public sealed class RoutingService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IRoutingRepository _repo;

    public RoutingService(ITenantConnection db, ITenantContext tenant, IRoutingRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    // =================================================================================
    // DANH MUC CONG DOAN
    // =================================================================================

    public Task<IEnumerable<StageDto>> ListStagesAsync(bool activeOnly, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListStagesAsync(s, activeOnly), ct);

    public Task<long> CreateStageAsync(CreateStageRequest req, CancellationToken ct)
    {
        var code = (req.Code ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Ma cong doan khong duoc rong.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Ten cong doan khong duoc rong.");
        if (req.Seq is int sq && sq <= 0) throw new ArgumentException("Thu tu phai > 0.");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            if (await _repo.StageCodeExistsAsync(scope, code, null))
                throw new ArgumentException($"Ma cong doan '{code}' da ton tai.");
            return await _repo.InsertStageAsync(scope, req with { Code = code, Name = req.Name.Trim() });
        }, ct);
    }

    public Task<bool> UpdateStageAsync(long id, UpdateStageRequest req, CancellationToken ct)
    {
        if (id <= 0) throw new ArgumentException("id khong hop le.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Ten cong doan khong duoc rong.");
        if (req.Seq is int sq && sq <= 0) throw new ArgumentException("Thu tu phai > 0.");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // Chan ngung dung cong doan khi con mau quy trinh dang dung -> tranh mau gay.
            if (!req.IsActive)
            {
                var used = await _repo.CountRoutingsUsingStageAsync(scope, id);
                if (used > 0)
                    throw new ArgumentException($"Cong doan dang duoc {used} mau quy trinh su dung, khong ngung dung duoc. Hay go khoi cac mau truoc.");
            }
            return await _repo.UpdateStageAsync(scope, id, req with { Name = req.Name.Trim() }) > 0;
        }, ct);
    }

    // =================================================================================
    // MAU QUY TRINH
    // =================================================================================

    public Task<IEnumerable<RoutingDto>> ListAsync(bool activeOnly, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListRoutingsAsync(s, activeOnly), ct);

    /// <summary>Chi tiet 1 mau: thong tin + day du cac buoc theo thu tu.</summary>
    public Task<RoutingDetailDto?> GetAsync(long id, CancellationToken ct)
    {
        if (id <= 0) throw new ArgumentException("id khong hop le.");
        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var routing = await _repo.GetRoutingAsync(scope, id);
            if (routing is null) return null;
            var steps = await _repo.ListRoutingStepsAsync(scope, id);
            return new RoutingDetailDto(routing, steps);
        }, ct);
    }

    public Task<long> CreateAsync(CreateRoutingRequest req, CancellationToken ct)
    {
        var code = (req.Code ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Ma quy trinh khong duoc rong.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Ten quy trinh khong duoc rong.");
        var steps = NormalizeSteps(req.Steps);

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            if (await _repo.RoutingCodeExistsAsync(scope, code))
                throw new ArgumentException($"Ma quy trinh '{code}' da ton tai.");

            var id = await _repo.InsertRoutingAsync(scope, req with { Code = code, Name = req.Name.Trim() });
            // Chi 1 mau mac dinh: bo co o cac mau khac (unique index uq_routing_one_default).
            if (req.IsDefault) await _repo.ClearDefaultAsync(scope, id);
            foreach (var st in steps) await _repo.InsertRoutingStepAsync(scope, id, st);
            return id;
        }, ct);
    }

    public Task<bool> UpdateAsync(long id, UpdateRoutingRequest req, CancellationToken ct)
    {
        if (id <= 0) throw new ArgumentException("id khong hop le.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Ten quy trinh khong duoc rong.");
        var steps = NormalizeSteps(req.Steps);

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var existing = await _repo.GetRoutingAsync(scope, id);
            if (existing is null) return false;

            // Bo co mac dinh o mau khac TRUOC khi dat mau nay lam mac dinh (tranh dung unique index).
            if (req.IsDefault) await _repo.ClearDefaultAsync(scope, id);
            await _repo.UpdateRoutingAsync(scope, id, req with { Name = req.Name.Trim() });

            // Cac buoc cua MAU sua thoai mai: don da sinh buoc roi thi khong bi anh huong.
            await _repo.DeleteRoutingStepsAsync(scope, id);
            foreach (var st in steps) await _repo.InsertRoutingStepAsync(scope, id, st);
            return true;
        }, ct);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        if (id <= 0) throw new ArgumentException("id khong hop le.");
        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var used = await _repo.CountOrdersUsingRoutingAsync(scope, id);
            if (used > 0)
                throw new ArgumentException($"Quy trinh dang duoc {used} don su dung, khong xoa duoc. Hay chuyen sang 'Ngung dung'.");
            return await _repo.DeleteRoutingAsync(scope, id) > 0;
        }, ct);
    }

    /// <summary>Kiem tra + chuan hoa chuoi buoc: khong rong, khong trung cong doan, seq danh lai 1..n theo thu tu gui len.</summary>
    private static List<RoutingStepInput> NormalizeSteps(IEnumerable<RoutingStepInput>? input)
    {
        var list = (input ?? Enumerable.Empty<RoutingStepInput>()).ToList();
        if (list.Count == 0) throw new ArgumentException("Quy trinh phai co it nhat 1 buoc.");
        if (list.Any(s => s.StageId <= 0)) throw new ArgumentException("Cong doan khong hop le.");
        if (list.Select(s => s.StageId).Distinct().Count() != list.Count)
            throw new ArgumentException("Mot cong doan chi duoc xuat hien 1 lan trong quy trinh.");

        // Danh lai seq lien tuc 1..n theo thu tu client gui (khong de lo hong/trung).
        return list.OrderBy(s => s.Seq)
                   .Select((s, i) => s with { Seq = i + 1 })
                   .ToList();
    }

    // =================================================================================
    // AP MAU VAO DON + SUA BUOC RIENG CHO DON
    // =================================================================================

    /// <summary>
    /// Ap 1 mau quy trinh vao don: gan routing_id + sinh cac buoc.
    /// StartSeq cho phep BAT DAU TU BUOC GIUA (vd hang da cat san -> bat dau tu May).
    /// Replace = true thi xoa cac buoc chua lam truoc khi sinh lai (buoc da chay giu nguyen).
    /// </summary>
    public Task<int> ApplyRoutingAsync(long orderId, ApplyRoutingRequest req, CancellationToken ct)
    {
        if (orderId <= 0) throw new ArgumentException("orderId khong hop le.");
        if (req.RoutingId <= 0) throw new ArgumentException("Chua chon quy trinh.");
        if (req.StartSeq is int ss && ss <= 0) throw new ArgumentException("Buoc bat dau phai > 0.");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var routing = await _repo.GetRoutingAsync(scope, req.RoutingId)
                          ?? throw new ArgumentException($"Khong tim thay quy trinh id={req.RoutingId}.");
            if (!routing.IsActive) throw new ArgumentException("Quy trinh da ngung dung, khong ap duoc.");
            if (routing.StepCount == 0) throw new ArgumentException("Quy trinh chua co buoc nao.");

            await _repo.SetOrderRoutingAsync(scope, orderId, req.RoutingId);
            if (req.Replace) await _repo.DeletePendingStepsAsync(scope, orderId);
            return await _repo.GenerateStepsFromRoutingAsync(scope, orderId, req.RoutingId, req.StartSeq ?? 1);
        }, ct);
    }

    /// <summary>Them 1 cong doan le vao don (ngoai mau). Tra ve id buoc moi, 0 neu don da co cong doan do.</summary>
    public Task<long> AddOrderStepAsync(long orderId, AddOrderStepRequest req, CancellationToken ct)
    {
        if (orderId <= 0) throw new ArgumentException("orderId khong hop le.");
        if (req.StageId <= 0) throw new ArgumentException("Chua chon cong doan.");
        if (req.Seq is int sq && sq <= 0) throw new ArgumentException("Thu tu phai > 0.");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var id = await _repo.AddOrderStepAsync(scope, orderId, req.StageId, req.Seq, req.Note);
            if (id == 0) throw new ArgumentException("Don da co cong doan nay roi.");
            return id;
        }, ct);
    }

    /// <summary>Bat/tat co BO QUA cho 1 buoc cua don (nhay buoc).</summary>
    public Task<bool> SkipStepAsync(long stepId, SkipStepRequest req, CancellationToken ct)
    {
        if (stepId <= 0) throw new ArgumentException("stepId khong hop le.");
        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var step = await _repo.LockOrderStepAsync(scope, stepId);
            if (step is null) return false;
            // Buoc da lam roi thi khong duoc danh bo qua (so lieu da ghi nhan).
            if (req.IsSkipped && (step.Status is "IN_PROGRESS" or "DONE" || step.QtyIn > 0 || step.QtyOut > 0))
                throw new ArgumentException("Buoc da bat dau/hoan tat hoac da co so lieu, khong danh 'bo qua' duoc.");
            return await _repo.SetStepSkippedAsync(scope, stepId, req.IsSkipped, req.Note) > 0;
        }, ct);
    }

    /// <summary>Xoa han 1 buoc khoi don (chi khi chua lam gi).</summary>
    public Task<bool> DeleteOrderStepAsync(long stepId, CancellationToken ct)
    {
        if (stepId <= 0) throw new ArgumentException("stepId khong hop le.");
        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var step = await _repo.LockOrderStepAsync(scope, stepId);
            if (step is null) return false;
            if (step.Status is not "PENDING" || step.QtyIn > 0 || step.QtyOut > 0)
                throw new ArgumentException("Buoc da chay hoac da co so lieu, khong xoa duoc. Hay danh dau 'bo qua'.");
            return await _repo.DeleteOrderStepAsync(scope, stepId) > 0;
        }, ct);
    }

    /// <summary>Doi thu tu cac buoc cua don: client gui danh sach id theo thu tu moi, server danh lai seq 1..n.</summary>
    public Task<bool> ReorderStepsAsync(long orderId, ReorderStepsRequest req, CancellationToken ct)
    {
        if (orderId <= 0) throw new ArgumentException("orderId khong hop le.");
        var ids = (req.StepIdsInOrder ?? Enumerable.Empty<long>()).ToList();
        if (ids.Count == 0) throw new ArgumentException("Danh sach buoc rong.");
        if (ids.Distinct().Count() != ids.Count) throw new ArgumentException("Danh sach buoc bi trung id.");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var actual = (await _repo.ListStepIdsAsync(scope, orderId)).ToHashSet();
            if (actual.Count == 0) return false;
            // Phai gui DU va DUNG cac buoc cua don -> tranh danh seq lech.
            if (ids.Count != actual.Count || ids.Any(id => !actual.Contains(id)))
                throw new ArgumentException("Danh sach buoc khong khop voi cac buoc hien co cua don.");

            // Danh seq am truoc de tranh dung UNIQUE tam thoi (neu sau nay them rang buoc), roi dat seq that.
            for (var i = 0; i < ids.Count; i++)
                await _repo.SetStepSeqAsync(scope, ids[i], i + 1);
            return true;
        }, ct);
    }
}
