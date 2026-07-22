using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Services;
using DGroup.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace DGroup.Server.Apps.ManagerPerformance.Controllers;

/// <summary>Ton kho & nhap NVL vao kho.</summary>
[ApiController]
[Route("dgrpi/manager-performance/stock")]
public sealed class StockController : ControllerBase
{
    private readonly StockService _service;

    public StockController(StockService service) => _service = service;

    /// <summary>Ton kha dung gop moi kho theo NVL (co the loc chi ton thap).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<MaterialStockDto>>>> GetStock(
        [FromQuery] bool lowStockOnly = false, [FromQuery] int? year = null, [FromQuery] int? month = null,
        CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<MaterialStockDto>>.Success(await _service.ListStockAsync(lowStockOnly, year, month, ct)));

    /// <summary>Nhap NVL vao kho (RECEIPT). Cong ton + ghi so cai trong 1 transaction.</summary>
    [HttpPost("receive")]
    public async Task<ActionResult<ApiResult<StockTransactionResult>>> Receive(
        [FromBody] ReceiveStockRequest req, CancellationToken ct) =>
        Ok(ApiResult<StockTransactionResult>.Success(await _service.ReceiveAsync(req, ct)));
}
