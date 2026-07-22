namespace DGroup.Server.Infrastructure.Tenancy;

/// <summary>Trien khai scoped cua ITenantContext (mot instance / request).</summary>
public sealed class TenantContext : ITenantContext
{
    private string? _tenant;

    public string Tenant =>
        _tenant ?? throw new InvalidOperationException("Tenant chua duoc xac dinh cho request nay.");

    public bool IsResolved => _tenant is not null;

    public void Set(string tenant) => _tenant = tenant;
}
