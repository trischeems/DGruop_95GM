using System.Text.RegularExpressions;
using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Repositories;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Tenancy;

namespace GM95.Server.Apps.ManagerPerformance.Services;

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

    /// <summary>Goi y NVL gan giong theo SKU/ten (chong tao trung). Chuoi rong -> danh sach rong.</summary>
    public Task<IEnumerable<MaterialDto>> SearchAsync(string? query, int limit, CancellationToken ct)
    {
        var q = (query ?? "").Trim();
        if (q.Length == 0) return Task.FromResult(Enumerable.Empty<MaterialDto>());
        var lim = Math.Clamp(limit, 1, 50);
        return _db.RunAsync(_tenant.Tenant, s => _repo.SearchAsync(s, q, lim), ct);
    }

    // Quy chuan SKU: bat dau chu/so, sau do chu/so/dau . _ - , dai 2..50 ky tu.
    private static readonly Regex SkuPattern =
        new(@"^[A-Za-z0-9][A-Za-z0-9._-]{1,49}$", RegexOptions.Compiled);

    public Task<long> CreateAsync(CreateMaterialRequest req, CancellationToken ct)
    {
        var normalized = Validate(req);
        return _db.RunAsync(_tenant.Tenant, async s =>
        {
            if (await _repo.SkuExistsAsync(s, normalized.Sku))
                throw new ArgumentException($"SKU '{normalized.Sku}' da ton tai. Vui long dung SKU khac.");
            return await _repo.InsertAsync(s, normalized);
        }, ct);
    }

    public Task<bool> UpdateAsync(long id, UpdateMaterialRequest req, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.UpdateAsync(s, id, req), ct);

    // Validate + chuan hoa (trim, SKU HOA). Tra ve request da chuan hoa.
    private static CreateMaterialRequest Validate(CreateMaterialRequest req)
    {
        var sku = (req.Sku ?? "").Trim().ToUpperInvariant();
        var name = (req.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SKU khong duoc rong.");
        if (!SkuPattern.IsMatch(sku))
            throw new ArgumentException(
                "SKU khong hop le. Chi dung chu, so va cac dau . _ - ; bat dau bang chu/so; dai 2-50 ky tu.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Ten NVL khong duoc rong.");
        if (req.UomId <= 0) throw new ArgumentException("uomId khong hop le.");
        if (req.ReorderLevel < 0 || req.ReorderQuantity < 0 || req.StandardCost < 0)
            throw new ArgumentException("Cac gia tri so khong duoc am.");
        return req with { Sku = sku, Name = name };
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
