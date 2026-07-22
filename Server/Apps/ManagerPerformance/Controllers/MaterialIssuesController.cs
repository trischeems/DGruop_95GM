using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Services;
using DGroup.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace DGroup.Server.Apps.ManagerPerformance.Controllers;

/// <summary>Xuat kho NVL cho san xuat (tru ton). Prefix /dgrpi/ (app dau tien).</summary>
[ApiController]
[Route("dgrpi/manager-performance/material-issues")]
public sealed class MaterialIssuesController : ControllerBase
{
    private readonly MaterialIssueService _service;

    public MaterialIssuesController(MaterialIssueService service) => _service = service;

    /// <summary>Danh sach phieu xuat (co the loc theo don san xuat), moi nhat truoc.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<MaterialIssueDto>>>> GetAll(
        [FromQuery] long? orderId = null, CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<MaterialIssueDto>>.Success(await _service.ListAsync(orderId, ct)));

    /// <summary>Tao & POST phieu xuat NVL: tru ton + ghi so cai + tieu thu giu cho trong 1 transaction.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResult<MaterialIssueResultDto>>> Create(
        [FromBody] CreateMaterialIssueRequest req, CancellationToken ct) =>
        Ok(ApiResult<MaterialIssueResultDto>.Success(await _service.CreateAsync(req, ct)));
}
