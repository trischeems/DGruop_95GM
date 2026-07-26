using System.Text.RegularExpressions;
using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Repositories;
using DGroup.Server.Infrastructure.Data;
using DGroup.Server.Infrastructure.Tenancy;

namespace DGroup.Server.Apps.ManagerPerformance.Services;

/// <summary>Nghiep vu ma hang thanh pham. Moi thao tac chay trong 1 transaction dung schema tenant.</summary>
public sealed class ProductService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly IProductRepository _repo;

    public ProductService(ITenantConnection db, ITenantContext tenant, IProductRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    public Task<IEnumerable<ProductDto>> ListAsync(bool activeOnly, int? year, int? month, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListAsync(s, activeOnly, year, month), ct);

    public Task<ProductDto?> GetAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.GetByIdAsync(s, id), ct);

    // Quy chuan SKU: bat dau bang chu/so, sau do chu/so/dau . _ - , dai 2..50.
    // Chan ky tu dac biet (khoang trang, @#$%...) va SKU qua ngan.
    private static readonly Regex SkuPattern =
        new(@"^[A-Za-z0-9][A-Za-z0-9._-]{1,49}$", RegexOptions.Compiled);

    public Task<long> CreateAsync(CreateProductRequest req, CancellationToken ct)
    {
        var normalized = Validate(req);
        return _db.RunAsync(_tenant.Tenant, async s =>
        {
            // Chan trung SKU (khong phan biet hoa thuong) -> loi 400 ro rang thay vi 500.
            if (await _repo.SkuExistsAsync(s, normalized.Sku))
                throw new ArgumentException($"SKU '{normalized.Sku}' da ton tai. Vui long dung SKU khac.");
            return await _repo.InsertAsync(s, normalized);
        }, ct);
    }

    public Task<bool> UpdateAsync(long id, UpdateProductRequest req, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.UpdateAsync(s, id, req), ct);

    // Validate + chuan hoa: trim, SKU viet HOA. Tra ve request da chuan hoa de dung nhat quan.
    private static CreateProductRequest Validate(CreateProductRequest req)
    {
        var sku = (req.Sku ?? "").Trim().ToUpperInvariant();
        var name = (req.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SKU khong duoc rong.");
        if (!SkuPattern.IsMatch(sku))
            throw new ArgumentException(
                "SKU khong hop le. Chi dung chu, so va cac dau . _ - ; bat dau bang chu/so; dai 2-50 ky tu.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Ten ma hang khong duoc rong.");
        if (req.UomId <= 0) throw new ArgumentException("uomId khong hop le.");
        return req with { Sku = sku, Name = name };
    }

    /// <summary>Anh huong khi xoa ma hang (phuc vu popup canh bao truoc khi xoa).</summary>
    public Task<ProductImpactDto?> GetImpactAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.GetImpactAsync(s, id), ct);

    /// <summary>Xoa vinh vien ma hang; tu choi neu con BOM/don/phieu TP tham chieu.</summary>
    public Task DeleteAsync(long id, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, async scope =>
        {
            var impact = await _repo.GetImpactAsync(scope, id)
                         ?? throw new ArgumentException($"Khong tim thay ma hang id={id}.");
            if (!impact.CanDelete)
                throw new ArgumentException(
                    "Ma hang dang duoc su dung (BOM/don san xuat/phieu nhap TP), khong the xoa vinh vien. " +
                    "Hay chuyen sang ngung hoat dong (isActive=false).");
            await _repo.DeleteAsync(scope, id);
        }, ct);
}
