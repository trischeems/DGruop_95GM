namespace DGroup.Server.Infrastructure.Tenancy;

/// <summary>
/// Tenant hien tai cua request (scoped). Middleware gan gia tri; controller/service doc.
/// </summary>
public interface ITenantContext
{
    /// <summary>Ten tenant (= ten schema) da duoc validate cho request nay.</summary>
    string Tenant { get; }

    bool IsResolved { get; }

    void Set(string tenant);
}
