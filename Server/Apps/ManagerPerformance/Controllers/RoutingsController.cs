using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Services;
using GM95.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace GM95.Server.Apps.ManagerPerformance.Controllers;

/// <summary>
/// MAU QUY TRINH san xuat: moi loai hang 1 chuoi cong doan rieng.
/// Don chon mau -> sinh buoc; van sua rieng duoc tren tung don (xem OrderStepsController).
/// </summary>
[ApiController]
[Route("dgrpi/manager-performance/routings")]
public sealed class RoutingsController : ControllerBase
{
    private readonly RoutingService _service;

    public RoutingsController(RoutingService service) => _service = service;

    /// <summary>Danh sach mau quy trinh (mac dinh chi lay mau dang dung).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<RoutingDto>>>> List(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<RoutingDto>>.Success(await _service.ListAsync(activeOnly, ct)));

    /// <summary>Chi tiet 1 mau: thong tin + day du cac buoc theo thu tu.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResult<RoutingDetailDto>>> Get(long id, CancellationToken ct)
    {
        var detail = await _service.GetAsync(id, ct);
        return detail is null
            ? NotFound(ApiResult<RoutingDetailDto>.Fail("NOT_FOUND", $"Khong tim thay quy trinh id={id}."))
            : Ok(ApiResult<RoutingDetailDto>.Success(detail));
    }

    /// <summary>Tao mau quy trinh moi (kem chuoi buoc).</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResult<long>>> Create(
        [FromBody] CreateRoutingRequest req, CancellationToken ct) =>
        Ok(ApiResult<long>.Success(await _service.CreateAsync(req, ct)));

    /// <summary>Sua mau quy trinh (ghi de toan bo chuoi buoc).</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> Update(
        long id, [FromBody] UpdateRoutingRequest req, CancellationToken ct)
    {
        var ok = await _service.UpdateAsync(id, req, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay quy trinh id={id}."));
    }

    /// <summary>Xoa mau quy trinh (chi khi chua don nao dung).</summary>
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(long id, CancellationToken ct)
    {
        var ok = await _service.DeleteAsync(id, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay quy trinh id={id}."));
    }

    // ---------------------------------------------------------------------------------
    // DANH MUC CONG DOAN (dung chung cho moi mau)
    // ---------------------------------------------------------------------------------

    /// <summary>Danh muc cong doan (Cat, May, QC, In, Theu, Dong goi...).</summary>
    [HttpGet("/dgrpi/manager-performance/stages")]
    public async Task<ActionResult<ApiResult<IEnumerable<StageDto>>>> ListStages(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<StageDto>>.Success(await _service.ListStagesAsync(activeOnly, ct)));

    /// <summary>Them cong doan moi vao danh muc.</summary>
    [HttpPost("/dgrpi/manager-performance/stages")]
    public async Task<ActionResult<ApiResult<long>>> CreateStage(
        [FromBody] CreateStageRequest req, CancellationToken ct) =>
        Ok(ApiResult<long>.Success(await _service.CreateStageAsync(req, ct)));

    /// <summary>Sua cong doan (ten, thu tu goi y, ngung dung).</summary>
    [HttpPut("/dgrpi/manager-performance/stages/{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> UpdateStage(
        long id, [FromBody] UpdateStageRequest req, CancellationToken ct)
    {
        var ok = await _service.UpdateStageAsync(id, req, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay cong doan id={id}."));
    }
}
