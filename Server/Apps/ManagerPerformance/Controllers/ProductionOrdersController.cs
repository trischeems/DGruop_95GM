using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Services;
using GM95.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace GM95.Server.Apps.ManagerPerformance.Controllers;

/// <summary>Don hang san xuat: tao ban nhap, xac nhan (giu cho NVL), huy (nha giu cho).</summary>
[ApiController]
[Route("dgrpi/manager-performance/production-orders")]
public sealed class ProductionOrdersController : ControllerBase
{
    private readonly ProductionOrderService _service;

    public ProductionOrdersController(ProductionOrderService service) => _service = service;

    /// <summary>Danh sach don hang (loc theo trang thai neu co), moi nhat truoc.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<ProductionOrderDto>>>> GetAll(
        [FromQuery] string? status = null, [FromQuery] int? year = null, [FromQuery] int? month = null,
        CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<ProductionOrderDto>>.Success(await _service.ListAsync(status, year, month, ct)));

    /// <summary>Chi tiet 1 don hang.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResult<ProductionOrderDto>>> GetById(long id, CancellationToken ct)
    {
        var dto = await _service.GetByIdAsync(id, ct);
        return dto is null
            ? NotFound(ApiResult<ProductionOrderDto>.Fail("NOT_FOUND", $"Khong tim thay don hang id={id}."))
            : Ok(ApiResult<ProductionOrderDto>.Success(dto));
    }

    /// <summary>Tao don hang moi (ban nhap DRAFT).</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResult<object>>> Create(
        [FromBody] CreateProductionOrderRequest req, CancellationToken ct)
    {
        var id = await _service.CreateAsync(req, ct);
        return Ok(ApiResult<object>.Success(new { id }));
    }

    /// <summary>Cac MAT HANG (dong) cua don — 1 don co the nhieu ma hang, moi ma hang 1 SL.</summary>
    [HttpGet("{id:long}/items")]
    public async Task<ActionResult<ApiResult<IEnumerable<ProductionOrderItemDto>>>> GetItems(
        long id, CancellationToken ct) =>
        Ok(ApiResult<IEnumerable<ProductionOrderItemDto>>.Success(await _service.ListItemsAsync(id, ct)));

    /// <summary>Them 1 mat hang vao don (chi khi don con DRAFT).</summary>
    [HttpPost("{id:long}/items")]
    public async Task<ActionResult<ApiResult<object>>> AddItem(
        long id, [FromBody] OrderItemInput item, CancellationToken ct)
    {
        var itemId = await _service.AddItemAsync(id, item, ct);
        return Ok(ApiResult<object>.Success(new { id = itemId }));
    }

    /// <summary>Sua 1 mat hang cua don: so luong / BOM / quy trinh / ghi chu (chi khi DRAFT).</summary>
    [HttpPut("items/{itemId:long}")]
    public async Task<ActionResult<ApiResult<object>>> UpdateItem(
        long itemId, [FromBody] UpdateOrderItemRequest req, CancellationToken ct)
    {
        var ok = await _service.UpdateItemAsync(itemId, req, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay mat hang id={itemId} trong don."));
    }

    /// <summary>Xoa 1 mat hang khoi don (chi khi DRAFT, don phai con it nhat 1 mat hang).</summary>
    [HttpDelete("items/{itemId:long}")]
    public async Task<ActionResult<ApiResult<object>>> DeleteItem(long itemId, CancellationToken ct)
    {
        var ok = await _service.DeleteItemAsync(itemId, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay mat hang id={itemId} trong don."));
    }

    /// <summary>Cac phieu giu cho NVL cua don hang.</summary>
    [HttpGet("{id:long}/reservations")]
    public async Task<ActionResult<ApiResult<IEnumerable<ReservationDto>>>> GetReservations(
        long id, CancellationToken ct) =>
        Ok(ApiResult<IEnumerable<ReservationDto>>.Success(await _service.ListReservationsAsync(id, ct)));

    /// <summary>Xac nhan don (chi khi DRAFT) + giu cho NVL theo BOM trong 1 transaction.</summary>
    [HttpPost("{id:long}/confirm")]
    public async Task<ActionResult<ApiResult<object>>> Confirm(long id, CancellationToken ct)
    {
        await _service.ConfirmAsync(id, ct);
        return Ok(ApiResult<object>.Success(new { id }));
    }

    /// <summary>Huy don (DRAFT/CONFIRMED) + nha toan bo giu cho NVL trong 1 transaction.</summary>
    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult<ApiResult<object>>> Cancel(long id, CancellationToken ct)
    {
        await _service.CancelAsync(id, ct);
        return Ok(ApiResult<object>.Success(new { id }));
    }

    /// <summary>Sua don hang (chi DRAFT): so luong, han giao, ghi chu.</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> Update(
        long id, [FromBody] UpdateProductionOrderRequest req, CancellationToken ct)
    {
        await _service.UpdateAsync(id, req, ct);
        return Ok(ApiResult.Success());
    }

    /// <summary>Anh huong khi xoa don (dem ke hoach/cong doan/giu cho/phieu).</summary>
    [HttpGet("{id:long}/impact")]
    public async Task<ActionResult<ApiResult<OrderImpactDto>>> GetImpact(long id, CancellationToken ct)
    {
        var impact = await _service.GetImpactAsync(id, ct);
        return impact is null
            ? NotFound(ApiResult<OrderImpactDto>.Fail("NOT_FOUND", $"Khong tim thay don hang id={id}."))
            : Ok(ApiResult<OrderImpactDto>.Success(impact));
    }

    /// <summary>Xoa don (chi DRAFT/CANCELLED, chua co phieu xuat/nhap TP).</summary>
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(long id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResult.Success());
    }
}
