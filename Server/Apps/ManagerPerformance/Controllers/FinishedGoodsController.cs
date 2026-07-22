using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Services;
using DGroup.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace DGroup.Server.Apps.ManagerPerformance.Controllers;

/// <summary>Nhap kho thanh pham (finished goods receipts). Prefix /dgrpi/ (app dau tien).</summary>
[ApiController]
[Route("dgrpi/manager-performance/finished-goods")]
public sealed class FinishedGoodsController : ControllerBase
{
    private readonly FinishedGoodsService _service;

    public FinishedGoodsController(FinishedGoodsService service) => _service = service;

    /// <summary>Danh sach phieu nhap thanh pham (co the loc theo lenh san xuat), moi nhat truoc.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<FinishedGoodsReceiptDto>>>> GetAll(
        [FromQuery] long? orderId = null, [FromQuery] int? year = null, [FromQuery] int? month = null,
        CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<FinishedGoodsReceiptDto>>.Success(await _service.ListAsync(orderId, year, month, ct)));

    /// <summary>Tao phieu nhap thanh pham + dong lenh san xuat trong 1 transaction.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResult<object>>> Create(
        [FromBody] CreateFinishedGoodsRequest req, CancellationToken ct)
    {
        var id = await _service.CreateAsync(req, ct);
        return Ok(ApiResult<object>.Success(new { id }));
    }

    /// <summary>Anh huong khi xoa phieu nhap TP (trang thai don + hao hut se lech).</summary>
    [HttpGet("{id:long}/impact")]
    public async Task<ActionResult<ApiResult<FinishedGoodsImpactDto>>> GetImpact(long id, CancellationToken ct)
    {
        var impact = await _service.GetImpactAsync(id, ct);
        return impact is null
            ? NotFound(ApiResult<FinishedGoodsImpactDto>.Fail("NOT_FOUND", $"Khong tim thay phieu id={id}."))
            : Ok(ApiResult<FinishedGoodsImpactDto>.Success(impact));
    }

    /// <summary>Xoa phieu nhap TP (don tro lai IN_PROGRESS neu het phieu; can tinh lai hao hut).</summary>
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(long id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResult.Success());
    }
}
