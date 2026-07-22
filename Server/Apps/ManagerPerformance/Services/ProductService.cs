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

    public Task<long> CreateAsync(CreateProductRequest req, CancellationToken ct)
    {
        Validate(req);
        return _db.RunAsync(_tenant.Tenant, s => _repo.InsertAsync(s, req), ct);
    }

    public Task<bool> UpdateAsync(long id, UpdateProductRequest req, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.UpdateAsync(s, id, req), ct);

    private static void Validate(CreateProductRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Sku)) throw new ArgumentException("SKU khong duoc rong.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Ten ma hang khong duoc rong.");
        if (req.UomId <= 0) throw new ArgumentException("uomId khong hop le.");
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
