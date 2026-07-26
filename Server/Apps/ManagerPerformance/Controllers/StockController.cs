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
    private readonly StockReceiptService _receipts;

    public StockController(StockService service, StockReceiptService receipts)
    {
        _service = service;
        _receipts = receipts;
    }

    /// <summary>Ton kha dung gop moi kho theo NVL (co the loc chi ton thap). Kem cot gia (last/avg/value).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<MaterialStockDto>>>> GetStock(
        [FromQuery] bool lowStockOnly = false, [FromQuery] int? year = null, [FromQuery] int? month = null,
        CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<MaterialStockDto>>.Success(await _service.ListStockAsync(lowStockOnly, year, month, ct)));

    /// <summary>Nhap NVL vao kho 1 dong (RECEIPT). Cong ton + ghi so cai trong 1 transaction. (Tuong thich cu.)</summary>
    [HttpPost("receive")]
    public async Task<ActionResult<ApiResult<StockTransactionResult>>> Receive(
        [FromBody] ReceiveStockRequest req, CancellationToken ct) =>
        Ok(ApiResult<StockTransactionResult>.Success(await _service.ReceiveAsync(req, ct)));

    /// <summary>Tao phieu NHAP kho NHIEU DONG (cong ton mọi dong trong 1 transaction).</summary>
    [HttpPost("receipts")]
    public async Task<ActionResult<ApiResult<StockReceiptResultDto>>> CreateReceipt(
        [FromBody] CreateStockReceiptRequest req, CancellationToken ct) =>
        Ok(ApiResult<StockReceiptResultDto>.Success(await _receipts.CreateAsync(req, ct)));

    /// <summary>Danh sach phieu nhap kho (loc theo kho neu co), moi nhat truoc.</summary>
    [HttpGet("receipts")]
    public async Task<ActionResult<ApiResult<IEnumerable<StockReceiptDto>>>> GetReceipts(
        [FromQuery] long? warehouseId = null, CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<StockReceiptDto>>.Success(await _receipts.ListAsync(warehouseId, ct)));

    /// <summary>Lich su giao dich kho (don gia tung lan nhap/xuat), loc theo NVL neu co.</summary>
    [HttpGet("transactions")]
    public async Task<ActionResult<ApiResult<IEnumerable<StockTransactionDto>>>> GetTransactions(
        [FromQuery] long? materialId = null, [FromQuery] int limit = 200, CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<StockTransactionDto>>.Success(await _receipts.ListTransactionsAsync(materialId, limit, ct)));
}
