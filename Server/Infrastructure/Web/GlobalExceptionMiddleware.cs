using System.Text.Json;
using Npgsql;

namespace DGroup.Server.Infrastructure.Web;

/// <summary>
/// Bat exception chua xu ly -> tra JSON chuan { ok:false, error }.
/// ArgumentException -> 400 (loi dau vao / tenant); con lai -> 500.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request tai {Path}", ctx.Request.Path);
            await WriteError(ctx, StatusCodes.Status400BadRequest, "BAD_REQUEST", ex.Message);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Vi pham rang buoc duy nhat (vd SKU/order_no trung) -> 409, thong bao ro thay vi 500.
            // Phong truong hop race qua duoc pre-check o service.
            _logger.LogWarning(ex, "Trung du lieu (unique) tai {Path}: {Constraint}",
                ctx.Request.Path, ex.ConstraintName);
            await WriteError(ctx, StatusCodes.Status409Conflict, "DUPLICATE",
                "Du lieu bi trung (ma da ton tai). Vui long kiem tra lai.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loi khong xu ly tai {Path}", ctx.Request.Path);
            await WriteError(ctx, StatusCodes.Status500InternalServerError, "INTERNAL_ERROR",
                "Loi he thong. Vui long thu lai.");
        }
    }

    private static async Task WriteError(HttpContext ctx, int status, string code, string message)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var payload = JsonSerializer.Serialize(new
        {
            ok = false,
            error = new { code, message },
        });
        await ctx.Response.WriteAsync(payload);
    }
}
