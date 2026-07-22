using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Services;
using DGroup.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace DGroup.Server.Apps.ManagerPerformance.Controllers;

/// <summary>Quan ly ke hoach san xuat. Prefix /dgrpi/ (app dau tien).</summary>
[ApiController]
[Route("dgrpi/manager-performance/production-plans")]
public sealed class ProductionPlansController : ControllerBase
{
    private readonly ProductionPlanService _service;

    public ProductionPlansController(ProductionPlanService service) => _service = service;

    /// <summary>Danh sach ke hoach (loc theo don san xuat neu co orderId).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<ProductionPlanDto>>>> GetAll(
        [FromQuery] long? orderId = null, [FromQuery] int? year = null, [FromQuery] int? month = null,
        CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<ProductionPlanDto>>.Success(await _service.ListAsync(orderId, year, month, ct)));

    /// <summary>Tao ke hoach san xuat moi.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResult<object>>> Create(
        [FromBody] CreateProductionPlanRequest req, CancellationToken ct)
    {
        var id = await _service.CreateAsync(req, ct);
        return Ok(ApiResult<object>.Success(new { id }));
    }

    /// <summary>Cap nhat trang thai ke hoach.</summary>
    [HttpPut("{id:long}/status")]
    public async Task<ActionResult<ApiResult<object>>> UpdateStatus(
        long id, [FromBody] UpdatePlanStatusRequest req, CancellationToken ct)
    {
        var ok = await _service.UpdateStatusAsync(id, req, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay ke hoach id={id}."));
    }

    /// <summary>Xoa ke hoach.</summary>
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(long id, CancellationToken ct)
    {
        var ok = await _service.DeleteAsync(id, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay ke hoach id={id}."));
    }

    /// <summary>Sua ke hoach: so luong, ma chuyen, ghi chu.</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> Update(
        long id, [FromBody] UpdatePlanRequest req, CancellationToken ct)
    {
        var ok = await _service.UpdateAsync(id, req, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay ke hoach id={id}."));
    }
}
