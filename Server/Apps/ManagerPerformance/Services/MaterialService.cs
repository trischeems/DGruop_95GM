using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Repositories;
using DGroup.Server.Infrastructure.Data;
using DGroup.Server.Infrastructure.Tenancy;

namespace DGroup.Server.Apps.ManagerPerformance.Services;

/// <summary>Nghiep vu NVL. Moi thao tac chay trong 1 transaction dung schema tenant.</summary>
public sealed class MaterialService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IMaterialRepository _repo;

    public MaterialService(ITenantConnection db, ITenantContext tenant, IMaterialRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    public Task<IEnumerable<MaterialDto>> ListAsync(bool activeOnly, int? year, int? month, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListAsync(s, activeOnly, year, month), ct);

    public Task<MaterialDto?> GetAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.GetByIdAsync(s, id), ct);

    public Task<long> CreateAsync(CreateMaterialRequest req, CancellationToken ct)
    {
        Validate(req);
        return _db.RunAsync(_tenant.Tenant, s => _repo.InsertAsync(s, req), ct);
    }

    public Task<bool> UpdateAsync(long id, UpdateMaterialRequest req, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.UpdateAsync(s, id, req), ct);

    private static void Validate(CreateMaterialRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Sku)) throw new ArgumentException("SKU khong duoc rong.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Ten NVL khong duoc rong.");
        if (req.UomId <= 0) throw new ArgumentException("uomId khong hop le.");
        if (req.ReorderLevel < 0 || req.ReorderQuantity < 0 || req.StandardCost < 0)
            throw new ArgumentException("Cac gia tri so khong duoc am.");
    }

    /// <summary>Anh huong khi xoa NVL (phuc vu popup canh bao truoc khi xoa).</summary>
    public Task<MaterialImpactDto?> GetImpactAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.GetImpactAsync(s, id), ct);

    /// <summary>
    /// Xoa vinh vien NVL. Kiem tra tham chieu TRONG CUNG transaction: neu con noi dang dung
    /// (ton kho / dinh muc / giu cho / phieu / so cai) thi tu choi — app se de xuat ngung hoat dong thay the.
    /// </summary>
    public Task DeleteAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var impact = await _repo.GetImpactAsync(scope, id)
                         ?? throw new ArgumentException($"Khong tim thay NVL id={id}.");
            if (!impact.CanDelete)
                throw new ArgumentException(
                    "NVL dang duoc su dung (ton kho/dinh muc/phieu), khong the xoa vinh vien. " +
                    "Hay chuyen sang ngung hoat dong (isActive=false).");
            await _repo.DeleteAsync(scope, id);
        }, ct);
}
