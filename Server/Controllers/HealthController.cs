using GM95.Server.Configuration;
using GM95.Server.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace GM95.Server.Controllers;

/// <summary>Endpoint he thong (khong gan tenant). Kiem tra server & ket noi DB.</summary>
[ApiController]
[Route("dgrpi/health")]
public sealed class HealthController : ControllerBase
{
    private readonly AppConfig _config;
    private readonly IDbConnectionFactory _db;

    public HealthController(AppConfig config, IDbConnectionFactory db)
    {
        _config = config;
        _db = db;
    }

    /// <summary>Server song chua.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        ok = true,
        service = _config.Server.Title,
        time = DateTimeOffset.UtcNow,
    });

    /// <summary>Kiem tra ket noi PostgreSQL (SELECT 1).</summary>
    [HttpGet("db")]
    public async Task<IActionResult> Db(CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            var r = await cmd.ExecuteScalarAsync(ct);
            return Ok(new { ok = true, db = "up", result = r });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { ok = false, db = "down", error = ex.Message });
        }
    }
}
