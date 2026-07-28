using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Repositories;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Tenancy;

namespace GM95.Server.Apps.ManagerPerformance.Services;

/// <summary>Nghiep vu danh muc nen. Moi thao tac chay trong 1 transaction dung schema tenant.</summary>
public sealed class LookupService
{
    private readonly ITenantConnection _db;
    private readonly ITenantContext _tenant;
    private readonly ILookupRepository _repo;

    public LookupService(ITenantConnection db, ITenantContext tenant, ILookupRepository repo)
    {
        _db = db;
        _tenant = tenant;
        _repo = repo;
    }

    // --- Don vi tinh ---

    public Task<IEnumerable<UomDto>> ListUomsAsync(CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListUomsAsync(s), ct);

    public Task<long> CreateUomAsync(CreateUomRequest req, CancellationToken ct)
    {
        ValidateCodeName(req.Code, req.Name);
        return _db.RunAsync(_tenant.Tenant, s => _repo.InsertUomAsync(s, req), ct);
    }

    // --- Kho ---

    public Task<IEnumerable<WarehouseDto>> ListWarehousesAsync(bool activeOnly, CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListWarehousesAsync(s, activeOnly), ct);

    public Task<long> CreateWarehouseAsync(CreateWarehouseRequest req, CancellationToken ct)
    {
        ValidateCodeName(req.Code, req.Name);
        return _db.RunAsync(_tenant.Tenant, s => _repo.InsertWarehouseAsync(s, req), ct);
    }

    // --- Nhom NVL ---

    public Task<IEnumerable<MaterialCategoryDto>> ListCategoriesAsync(CancellationToken ct) =>
        _db.RunAsync(_tenant.Tenant, s => _repo.ListCategoriesAsync(s), ct);

    public Task<long> CreateCategoryAsync(CreateCategoryRequest req, CancellationToken ct)
    {
        ValidateCodeName(req.Code, req.Name);
        return _db.RunAsync(_tenant.Tenant, s => _repo.InsertCategoryAsync(s, req), ct);
    }

    private static void ValidateCodeName(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Ma (code) khong duoc rong.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Ten (name) khong duoc rong.");
    }
}
