using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Services;
using DGroup.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace DGroup.Server.Apps.ManagerPerformance.Controllers;

/// <summary>
/// Bao cao hao hut: doi chieu cap phat thuc te vs dinh muc chuan cho tung don san xuat.
/// </summary>
[ApiController]
[Route("dgrpi/manager-performance/loss-reports")]
public sealed class LossReportsController : ControllerBase
{
    private readonly LossReportService _service;

    public LossReportsController(LossReportService service) => _service = service;

    /// <summary>Danh sach bao cao hao hut (loc theo don neu co), sap xep material_id.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<LossReportDto>>>> GetAll(
        [FromQuery] long? orderId = null, CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<LossReportDto>>.Success(await _service.ListAsync(orderId, ct)));

    /// <summary>Tinh & luu (UPSERT) bao cao hao hut cho 1 don, tra ve ket qua sau khi tinh.</summary>
    [HttpPost("generate/{orderId:long}")]
    public async Task<ActionResult<ApiResult<IEnumerable<LossReportDto>>>> Generate(
        long orderId, CancellationToken ct) =>
        Ok(ApiResult<IEnumerable<LossReportDto>>.Success(await _service.GenerateAsync(orderId, ct)));
}
