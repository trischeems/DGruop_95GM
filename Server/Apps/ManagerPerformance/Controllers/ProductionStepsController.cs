using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Services;
using GM95.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace GM95.Server.Apps.ManagerPerformance.Controllers;

/// <summary>Quy trinh san xuat (Cat/May/QC/Nhap TP) theo don, dang state machine.</summary>
[ApiController]
[Route("dgrpi/manager-performance/production-steps")]
public sealed class ProductionStepsController : ControllerBase
{
    private readonly ProductionStepService _service;
    private readonly RoutingService _routing;

    public ProductionStepsController(ProductionStepService service, RoutingService routing)
    {
        _service = service;
        _routing = routing;
    }

    /// <summary>Khoi tao 4 dong cong doan cho 1 don (bo qua neu da co).</summary>
    [HttpPost("init/{orderId:long}")]
    public async Task<ActionResult<ApiResult<object>>> Init(long orderId, CancellationToken ct)
    {
        await _service.InitAsync(orderId, ct);
        return Ok(ApiResult.Success());
    }

    /// <summary>
    /// Danh sach buoc quy trinh: theo DON (moi mat hang mot bo, sap theo dong) hoac
    /// theo RIENG 1 mat hang khi truyen itemId.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<ProductionStepDto>>>> GetByOrder(
        [FromQuery] long orderId, [FromQuery] long? itemId = null, CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<ProductionStepDto>>.Success(
            itemId.HasValue
                ? await _service.ListByItemAsync(itemId.Value, ct)
                : await _service.ListByOrderAsync(orderId, ct)));

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

    // =================================================================================
    // QUY TRINH LINH HOAT (V006): ap mau + sua buoc rieng cho tung don
    // =================================================================================

    /// <summary>Ap 1 mau quy trinh vao don (co the bat dau tu buoc giua, bo cac buoc dau).</summary>
    [HttpPost("apply-routing/{orderId:long}")]
    public async Task<ActionResult<ApiResult<int>>> ApplyRouting(
        long orderId, [FromBody] ApplyRoutingRequest req, CancellationToken ct) =>
        Ok(ApiResult<int>.Success(await _routing.ApplyRoutingAsync(orderId, req, ct)));

    /// <summary>Them 1 cong doan le vao don (ngoai mau).</summary>
    [HttpPost("order/{orderId:long}")]
    public async Task<ActionResult<ApiResult<long>>> AddStep(
        long orderId, [FromBody] AddOrderStepRequest req, CancellationToken ct) =>
        Ok(ApiResult<long>.Success(await _routing.AddOrderStepAsync(orderId, req, ct)));

    /// <summary>Bat/tat co BO QUA cho 1 buoc (nhay buoc, khong phai lam).</summary>
    [HttpPut("{id:long}/skip")]
    public async Task<ActionResult<ApiResult<object>>> Skip(
        long id, [FromBody] SkipStepRequest req, CancellationToken ct)
    {
        var ok = await _routing.SkipStepAsync(id, req, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay buoc quy trinh id={id}."));
    }

    /// <summary>Xoa han 1 buoc khoi don (chi khi buoc chua chay).</summary>
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> DeleteStep(long id, CancellationToken ct)
    {
        var ok = await _routing.DeleteOrderStepAsync(id, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay buoc quy trinh id={id}."));
    }

    /// <summary>Doi thu tu cac buoc cua don (gui danh sach id theo thu tu moi).</summary>
    [HttpPut("order/{orderId:long}/reorder")]
    public async Task<ActionResult<ApiResult<object>>> Reorder(
        long orderId, [FromBody] ReorderStepsRequest req, CancellationToken ct)
    {
        var ok = await _routing.ReorderStepsAsync(orderId, req, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Don id={orderId} chua co buoc quy trinh nao."));
    }
}
