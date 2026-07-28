using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Repositories;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Tenancy;

namespace GM95.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Nghiep vu dinh muc (BOM). Vong doi: DRAFT -> PENDING_APPROVAL -> ACTIVE/REJECTED (-> ARCHIVED).
/// Cac buoc ghi nhieu cau lam trong 1 transaction (1 RunAsync) de dam bao nhat quan.
/// </summary>
public sealed class BomService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IBomRepository _repo;

    public BomService(ITenantConnection db, ITenantContext tenant, IBomRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    public Task<IEnumerable<BomDto>> ListAsync(long? productId, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListAsync(s, productId), ct);

    /// <summary>Chi tiet: header + items trong cung 1 transaction (null neu khong co header).</summary>
    public Task<BomDetailDto?> GetDetailAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var header = await _repo.GetHeaderAsync(scope, id);
            if (header is null) return (BomDetailDto?)null;
            var items = await _repo.ListItemsAsync(scope, id);
            return new BomDetailDto(header, items);
        }, ct);

    public Task<long> CreateAsync(CreateBomRequest req, CancellationToken ct)
    {
        if (req.ProductId <= 0) throw new ArgumentException("productId khong hop le.");
        // version = MAX(version)+1 cho product; lam trong 1 RunAsync.
        return _db.RunAsync(_tenant.Tenant, s => _repo.InsertBomAsync(s, req.ProductId, req.Note), ct);
    }

    /// <summary>Them/cap nhat 1 dong NVL + ghi lich su trong 1 transaction. Tra ve id dong item.</summary>
    public Task<long> UpsertItemAsync(long bomId, UpsertBomItemRequest req, CancellationToken ct)
    {
        if (req.MaterialId <= 0) throw new ArgumentException("materialId khong hop le.");
        if (req.QtyPerUnit <= 0) throw new ArgumentException("qtyPerUnit phai > 0.");
        if (req.WastePct < 0 || req.WastePct >= 100) throw new ArgumentException("wastePct phai trong [0, 100).");

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // GUARD: chi sua dinh muc khi BOM dang DRAFT. Ban da trinh duyet/ACTIVE/ARCHIVED
            // la ban da CHOT -> khong cho sua (tao ban moi neu can dieu chinh).
            var st = await _repo.GetStatusAsync(scope, bomId);
            if (st is null) throw new ArgumentException($"Khong tim thay dinh muc id={bomId}.");
            if (st != "DRAFT")
                throw new ArgumentException(
                    $"Chi sua duoc dinh muc o trang thai DRAFT (hien tai: {st}). Hay tao ban dinh muc moi de dieu chinh.");

            // Da co dong NVL nay chua? -> quyet dinh loai lich su ADD_ITEM / UPDATE_QTY.
            var existing = await _repo.ListItemsAsync(scope, bomId);
            var isNew = !existing.Any(i => i.MaterialId == req.MaterialId);

            var itemId = await _repo.UpsertItemAsync(scope, bomId, req);
            await _repo.InsertHistoryAsync(
                scope, bomId, itemId, req.MaterialId,
                isNew ? "ADD_ITEM" : "UPDATE_QTY", null, req.QtyPerUnit, null);

            return itemId;
        }, ct);
    }

    /// <summary>Xoa 1 dong NVL + ghi lich su REMOVE_ITEM trong 1 transaction.</summary>
    public Task DeleteItemAsync(long bomId, long materialId, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // GUARD: chi xoa dong dinh muc khi BOM dang DRAFT (ban chua chot).
            var st = await _repo.GetStatusAsync(scope, bomId);
            if (st is null) throw new ArgumentException($"Khong tim thay dinh muc id={bomId}.");
            if (st != "DRAFT")
                throw new ArgumentException(
                    $"Chi xoa dong dinh muc khi BOM o trang thai DRAFT (hien tai: {st}).");

            await _repo.DeleteItemAsync(scope, bomId, materialId);
            await _repo.InsertHistoryAsync(
                scope, bomId, null, materialId, "REMOVE_ITEM", null, null, null);
        }, ct);

    /// <summary>Trinh duyet: chi khi dang DRAFT. Doi sang PENDING_APPROVAL + tao phieu PENDING.</summary>
    public Task SubmitAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var status = await _repo.GetStatusAsync(scope, id);
            if (status is null) throw new ArgumentException($"Khong tim thay dinh muc id={id}.");
            if (status != "DRAFT") throw new ArgumentException("Chi trinh duyet duoc dinh muc o trang thai DRAFT.");

            await _repo.SetStatusAsync(scope, id, "PENDING_APPROVAL");
            await _repo.InsertPendingApprovalAsync(scope, id);
        }, ct);

    /// <summary>
    /// Duyet: ra quyet dinh phieu PENDING -> ARCHIVE ban ACTIVE cu cung product (TRUOC) ->
    /// kich hoat ban nay -> ghi lich su STATUS_CHANGE. Tat ca trong 1 transaction.
    /// </summary>
    public Task ApproveAsync(long id, DecisionRequest req, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // GUARD: chi duyet duoc BOM dang cho duyet. Chan duyet lai ban da ACTIVE/REJECTED/DRAFT.
            var status = await _repo.GetStatusAsync(scope, id);
            if (status is null) throw new ArgumentException($"Khong tim thay dinh muc id={id}.");
            if (status != "PENDING_APPROVAL")
                throw new ArgumentException(
                    $"Chi duyet duoc dinh muc dang cho duyet (PENDING_APPROVAL). Hien tai: {status}.");

            await _repo.DecideLatestPendingApprovalAsync(scope, id, "APPROVED", req.DecidedBy, req.Note);
            await _repo.ArchiveActiveOfSameProductAsync(scope, id); // phai truoc khi kich hoat (partial unique)
            await _repo.ActivateAsync(scope, id);
            await _repo.InsertHistoryAsync(
                scope, id, null, null, "STATUS_CHANGE", null, null, "APPROVED");
        }, ct);

    /// <summary>Tu choi: ra quyet dinh phieu PENDING -> BOM ve REJECTED -> ghi lich su. 1 transaction.</summary>
    public Task RejectAsync(long id, DecisionRequest req, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // GUARD: chi tu choi duoc BOM dang cho duyet.
            var status = await _repo.GetStatusAsync(scope, id);
            if (status is null) throw new ArgumentException($"Khong tim thay dinh muc id={id}.");
            if (status != "PENDING_APPROVAL")
                throw new ArgumentException(
                    $"Chi tu choi duoc dinh muc dang cho duyet (PENDING_APPROVAL). Hien tai: {status}.");

            await _repo.DecideLatestPendingApprovalAsync(scope, id, "REJECTED", req.DecidedBy, req.Note);
            await _repo.SetStatusAsync(scope, id, "REJECTED");
            await _repo.InsertHistoryAsync(
                scope, id, null, null, "STATUS_CHANGE", null, null, "REJECTED");
        }, ct);

    public Task<IEnumerable<BomChangeHistoryDto>> ListHistoryAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListHistoryAsync(s, id), ct);

    public Task<IEnumerable<BomApprovalDto>> ListApprovalsAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListApprovalsAsync(s, id), ct);

    /// <summary>Anh huong khi xoa BOM (phuc vu popup canh bao truoc khi xoa).</summary>
    public Task<BomImpactDto?> GetImpactAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.GetImpactAsync(s, id), ct);

    /// <summary>
    /// Xoa BOM: chi cho phep khi DRAFT/REJECTED va chua co don nao chot BOM nay.
    /// items/history/approvals xoa theo (FK cascade).
    /// </summary>
    public Task DeleteAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var impact = await _repo.GetImpactAsync(scope, id)
                         ?? throw new ArgumentException($"Khong tim thay dinh muc id={id}.");
            if (!impact.CanDelete)
                throw new ArgumentException(
                    impact.OrderCount > 0
                        ? $"BOM dang duoc {impact.OrderCount} don san xuat su dung, khong the xoa."
                        : $"Chi xoa duoc BOM o trang thai DRAFT/REJECTED (hien tai: {impact.Status}).");
            await _repo.DeleteAsync(scope, id);
        }, ct);
}
