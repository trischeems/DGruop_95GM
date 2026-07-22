using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Services;
using DGroup.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace DGroup.Server.Apps.ManagerPerformance.Controllers;

/// <summary>
/// Bao cao nghiep vu: san luong toi da theo ma hang, nhu cau/thieu hut NVL theo don,
/// canh bao ton thap. Doc tu cac view v_* (cong thuc dong goi san trong DB).
/// </summary>
[ApiController]
[Route("dgrpi/manager-performance/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly ReportService _service;

    public ReportsController(ReportService service) => _service = service;

    /// <summary>San luong toi da co the san xuat cua moi ma hang + NVL nut that.</summary>
    [HttpGet("max-output")]
    public async Task<ActionResult<ApiResult<IEnumerable<MaxOutputByProductDto>>>> MaxOutput(
        CancellationToken ct) =>
        Ok(ApiResult<IEnumerable<MaxOutputByProductDto>>.Success(await _service.MaxOutputAsync(ct)));

    /// <summary>San luong toi da cua 1 ma hang cu the.</summary>
    [HttpGet("max-output/{productId:long}")]
    public async Task<ActionResult<ApiResult<MaxOutputByProductDto>>> MaxOutputForProduct(
        long productId, CancellationToken ct)
    {
        var r = await _service.MaxOutputForProductAsync(productId, ct);
        return r is null
            ? NotFound(ApiResult<MaxOutputByProductDto>.Fail("NOT_FOUND",
                $"Khong co du lieu san luong cho ma hang id={productId} (can BOM active + ton kho)."))
            : Ok(ApiResult<MaxOutputByProductDto>.Success(r));
    }

    /// <summary>Nhu cau NVL vs ton kha dung theo don: thieu hut + de xuat mua them.</summary>
    [HttpGet("order-requirements")]
    public async Task<ActionResult<ApiResult<IEnumerable<OrderMaterialRequirementDto>>>> OrderRequirements(
        [FromQuery] long? orderId = null, CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<OrderMaterialRequirementDto>>.Success(
            await _service.OrderRequirementsAsync(orderId, ct)));

    /// <summary>Danh sach NVL ton thap (canh bao).</summary>
    [HttpGet("low-stock")]
    public async Task<ActionResult<ApiResult<IEnumerable<MaterialStockDto>>>> LowStock(
        [FromQuery] int? year = null, [FromQuery] int? month = null, CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<MaterialStockDto>>.Success(await _service.LowStockAsync(year, month, ct)));

    /// <summary>So lieu tong hop 12 thang cua 1 nam (don, nhap/xuat NVL, TP, hao hut, canh bao).</summary>
    [HttpGet("monthly-stats")]
    public async Task<ActionResult<ApiResult<IEnumerable<MonthlyStatsDto>>>> MonthlyStats(
        [FromQuery] int year, CancellationToken ct) =>
        Ok(ApiResult<IEnumerable<MonthlyStatsDto>>.Success(await _service.MonthlyStatsAsync(year, ct)));
}
