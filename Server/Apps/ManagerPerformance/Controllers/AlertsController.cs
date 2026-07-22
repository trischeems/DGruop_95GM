using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Services;
using DGroup.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace DGroup.Server.Apps.ManagerPerformance.Controllers;

/// <summary>Canh bao ton thap & don thieu NVL: liet ke, sinh canh bao, ghi nhan/dong.</summary>
[ApiController]
[Route("dgrpi/manager-performance/alerts")]
public sealed class AlertsController : ControllerBase
{
    private readonly AlertService _service;

    public AlertsController(AlertService service) => _service = service;

    /// <summary>Danh sach canh bao (mac dinh loc status = 'OPEN'), moi nhat truoc.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<AlertDto>>>> GetAll(
        [FromQuery] string? status = null, [FromQuery] int? year = null, [FromQuery] int? month = null,
        CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<AlertDto>>.Success(await _service.ListAsync(status, year, month, ct)));

    /// <summary>Quet & tao canh bao (ton thap + don thieu NVL). Tra ve so canh bao da tao.</summary>
    [HttpPost("generate")]
    public async Task<ActionResult<ApiResult<object>>> Generate(CancellationToken ct)
    {
        var created = await _service.GenerateAsync(ct);
        return Ok(ApiResult<object>.Success(new { created }));
    }

    /// <summary>Ghi nhan canh bao (ACKNOWLEDGED).</summary>
    [HttpPut("{id:long}/acknowledge")]
    public async Task<ActionResult<ApiResult<object>>> Acknowledge(long id, CancellationToken ct)
    {
        var ok = await _service.AcknowledgeAsync(id, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay canh bao id={id}."));
    }

    /// <summary>Dong canh bao (RESOLVED) + ghi resolved_at.</summary>
    [HttpPut("{id:long}/resolve")]
    public async Task<ActionResult<ApiResult<object>>> Resolve(long id, CancellationToken ct)
    {
        var ok = await _service.ResolveAsync(id, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay canh bao id={id}."));
    }
}
