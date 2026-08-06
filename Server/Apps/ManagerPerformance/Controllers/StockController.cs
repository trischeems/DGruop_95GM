using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Services;
using GM95.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace GM95.Server.Apps.ManagerPerformance.Controllers;

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

    /// <summary>Lich su giao dich kho (don gia tung lan nhap/xuat), loc theo NVL + tu/den ngay neu co.</summary>
    [HttpGet("transactions")]
    public async Task<ActionResult<ApiResult<IEnumerable<StockTransactionDto>>>> GetTransactions(
        [FromQuery] long? materialId = null, [FromQuery] int limit = 200,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<StockTransactionDto>>.Success(
            await _receipts.ListTransactionsAsync(materialId, limit, from, to, ct)));

    /// <summary>Sua 1 dong nhap/xuat (so luong + don gia + ghi chu): dong bo lai ton kho & phieu goc.</summary>
    [HttpPut("transactions/{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> UpdateTransaction(
        long id, [FromBody] UpdateStockTransactionRequest req, CancellationToken ct)
    {
        var ok = await _receipts.UpdateTransactionAsync(id, req, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay dong giao dich kho id={id}."));
    }

    /// <summary>Xoa 1 dong nhap/xuat: tra ton ve nhu chua co dong nay + xoa dong trong phieu goc.</summary>
    [HttpDelete("transactions/{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> DeleteTransaction(long id, CancellationToken ct)
    {
        var ok = await _receipts.DeleteTransactionAsync(id, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay dong giao dich kho id={id}."));
    }
}
