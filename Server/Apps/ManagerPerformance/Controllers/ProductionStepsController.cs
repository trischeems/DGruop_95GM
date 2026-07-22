using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Services;
using DGroup.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace DGroup.Server.Apps.ManagerPerformance.Controllers;

/// <summary>Quy trinh san xuat (Cat/May/QC/Nhap TP) theo don, dang state machine.</summary>
[ApiController]
[Route("dgrpi/manager-performance/production-steps")]
public sealed class ProductionStepsController : ControllerBase
{
    private readonly ProductionStepService _service;

    public ProductionStepsController(ProductionStepService service) => _service = service;

    /// <summary>Khoi tao 4 dong cong doan cho 1 don (bo qua neu da co).</summary>
    [HttpPost("init/{orderId:long}")]
    public async Task<ActionResult<ApiResult<object>>> Init(long orderId, CancellationToken ct)
    {
        await _service.InitAsync(orderId, ct);
        return Ok(ApiResult.Success());
    }

    /// <summary>Danh sach buoc quy trinh cua 1 don, sap xep theo seq.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<ProductionStepDto>>>> GetByOrder(
        [FromQuery] long orderId, CancellationToken ct) =>
        Ok(ApiResult<IEnumerable<ProductionStepDto>>.Success(await _service.ListByOrderAsync(orderId, ct)));

    /// <summary>Cap nhat 1 buoc quy trinh (trang thai + so luong vao/ra/loi).</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> Update(
        long id, [FromBody] UpdateStepRequest req, CancellationToken ct)
    {
        var ok = await _service.UpdateAsync(id, req, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay buoc quy trinh id={id}."));
    }
}
