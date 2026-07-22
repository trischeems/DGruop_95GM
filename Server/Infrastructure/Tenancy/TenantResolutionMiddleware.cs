using DGroup.Server.Configuration;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Infrastructure.Tenancy;

/// <summary>
/// Xac dinh tenant cho moi request tu header (mac dinh "X-Tenant").
/// Neu khong co header -> dung default_tenant. Validate theo whitelist; sai -> 400.
/// Bo qua cac endpoint health/system (khong can tenant).
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TenancyOptions _tenancy;

    public TenantResolutionMiddleware(RequestDelegate next, TenancyOptions tenancy)
    {
        _next = next;
        _tenancy = tenancy;
    }

    public async Task InvokeAsync(HttpContext ctx, ITenantContext tenantContext)
    {
        var path = ctx.Request.Path.Value ?? string.Empty;

        // Endpoint he thong khong gan voi tenant nao.
        if (path.StartsWith("/dgrpi/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/dgrpi/system", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        var headerName = string.IsNullOrWhiteSpace(_tenancy.TenantHeader) ? "X-Tenant" : _tenancy.TenantHeader;
        var raw = ctx.Request.Headers[headerName].ToString();
        var candidate = string.IsNullOrWhiteSpace(raw) ? _tenancy.DefaultTenant : raw;

        string tenant;
        try
        {
            tenant = SqlTenantValidator.Validate(candidate, _tenancy.AllowedSet);
        }
        catch (ArgumentException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new
            {
                ok = false,
                error = new { code = "INVALID_TENANT", message = ex.Message },
            });
            return;
        }

        tenantContext.Set(tenant);
        await _next(ctx);
    }
}
