using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Repositories;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Tenancy;

namespace GM95.Server.Apps.ManagerPerformance.Services;

/// <summary>
/// Nghiep vu xuat kho NVL cho san xuat (tru ton). Tao & POST phieu trong 1 transaction:
///   INSERT phieu -> voi moi NVL: khoa dong stock (FOR UPDATE) -> kiem tra du ton -> tru on_hand/reserved
///   -> ghi dong phieu + so cai (ISSUE, quantity am) -> tieu thu giu cho -> chuyen don sang IN_PROGRESS.
/// Chong race khi 500+ user xuat dong thoi.
/// </summary>
public sealed class MaterialIssueService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IMaterialIssueRepository _repo;

    public MaterialIssueService(ITenantConnection db, ITenantContext tenant, IMaterialIssueRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    public Task<IEnumerable<MaterialIssueDto>> ListAsync(long? orderId, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListAsync(s, orderId), ct);

    /// <summary>Danh sach DONG NVL cua cac phieu xuat (kem ma + ten NVL) — phuc vu bang chi tiet o app.</summary>
    public Task<IEnumerable<MaterialIssueItemDto>> ListItemsAsync(long? orderId, int limit, CancellationToken ct)
    {
        var cap = limit is <= 0 or > 500 ? 200 : limit;
        return _db.RunAsync(_tenant.Tenant, s => _repo.ListItemsAsync(s, orderId, cap), ct);
    }

    public Task<MaterialIssueResultDto> CreateAsync(CreateMaterialIssueRequest req, CancellationToken ct)
    {
        if (req.ProductionOrderId <= 0 || req.WarehouseId <= 0) throw new ArgumentException("Don/kho khong hop le.");
        var items = req.Items?.ToList() ?? new List<MaterialIssueItemInput>();
        if (items.Count == 0) throw new ArgumentException("Phieu xuat phai co it nhat 1 dong NVL.");
        foreach (var it in items)
        {
            if (it.MaterialId <= 0) throw new ArgumentException("NVL khong hop le.");
            if (it.QtyIssued <= 0) throw new ArgumentException("So luong xuat phai > 0.");
            if (it.UnitCost < 0) throw new ArgumentException("Don gia khong duoc am.");
        }

        return _db.RunAsync(_tenant.Tenant, async scope =>
        {
            // 1) Tao phieu xuat (issue_no sinh trong SQL, status POSTED).
            var (issueId, issueNo) = await _repo.InsertIssueAsync(scope, req.ProductionOrderId, req.WarehouseId, req.Note);

            // 2) Voi moi NVL: khoa dong stock -> kiem tra du ton -> tru -> ghi dong phieu + so cai.
            foreach (var it in items)
            {
                var stock = await _repo.LockStockAsync(scope, req.WarehouseId, it.MaterialId);
                if (stock is null || stock.Value.QtyOnHand < it.QtyIssued)
                {
                    // Bao loi de doc: SKU — ten + con bao nhieu so voi yeu cau.
                    var label = await _repo.GetMaterialLabelAsync(scope, it.MaterialId) ?? $"id={it.MaterialId}";
                    var onHand = stock?.QtyOnHand ?? 0m;
                    throw new ArgumentException(
                        $"Ton kho khong du cho NVL {label}: con {onHand:0.####}, can xuat {it.QtyIssued:0.####}.");
                }

                var newOnHand = stock.Value.QtyOnHand - it.QtyIssued;
                var newReserved = Math.Max(0m, stock.Value.QtyReserved - it.QtyIssued);

                await _repo.SetStockAsync(scope, req.WarehouseId, it.MaterialId, newOnHand, newReserved);
                await _repo.InsertItemAsync(scope, issueId, it.MaterialId, it.QtyIssued, it.UnitCost);

                // So cai xuat kho: quantity am, balance_after = ton on_hand sau khi tru.
                await _repo.InsertTransactionAsync(
                    scope, req.WarehouseId, it.MaterialId, "ISSUE",
                    -it.QtyIssued, it.UnitCost, newOnHand, "MATERIAL_ISSUE", issueId, req.Note);
            }

            // 3) Tieu thu giu cho cua don + chuyen don da xac nhan sang dang san xuat.
            await _repo.ConsumeReservationsAsync(scope, req.ProductionOrderId);
            await _repo.MarkOrderInProgressAsync(scope, req.ProductionOrderId);

            return new MaterialIssueResultDto(issueId, issueNo, items.Count);
        }, ct);
    }
}
