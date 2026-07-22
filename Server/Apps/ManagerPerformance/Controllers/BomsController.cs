using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Services;
using DGroup.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace DGroup.Server.Apps.ManagerPerformance.Controllers;

/// <summary>Dinh muc nguyen vat lieu (BOM): tao ban nhap, sua dong NVL, trinh/duyet/tu choi.</summary>
[ApiController]
[Route("dgrpi/manager-performance/boms")]
public sealed class BomsController : ControllerBase
{
    private readonly BomService _service;

    public BomsController(BomService service) => _service = service;

    /// <summary>Danh sach BOM (loc theo product neu co), sap xep product_id, version desc.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<BomDto>>>> GetAll(
        [FromQuery] long? productId = null, CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<BomDto>>.Success(await _service.ListAsync(productId, ct)));

    /// <summary>Chi tiet 1 BOM (header + cac dong NVL).</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResult<BomDetailDto>>> GetById(long id, CancellationToken ct)
    {
        var detail = await _service.GetDetailAsync(id, ct);
        return detail is null
            ? NotFound(ApiResult<BomDetailDto>.Fail("NOT_FOUND", $"Khong tim thay dinh muc id={id}."))
            : Ok(ApiResult<BomDetailDto>.Success(detail));
    }

    /// <summary>Tao BOM moi (ban nhap DRAFT). version tu tang theo product.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResult<object>>> Create(
        [FromBody] CreateBomRequest req, CancellationToken ct)
    {
        var id = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id },
            ApiResult<object>.Success(new { id }));
    }

    /// <summary>Them/cap nhat 1 dong NVL trong BOM (upsert theo material_id) + ghi lich su.</summary>
    [HttpPut("{id:long}/items")]
    public async Task<ActionResult<ApiResult<object>>> UpsertItem(
        long id, [FromBody] UpsertBomItemRequest req, CancellationToken ct)
    {
        var itemId = await _service.UpsertItemAsync(id, req, ct);
        return Ok(ApiResult<object>.Success(new { id = itemId }));
    }

    /// <summary>Xoa 1 dong NVL khoi BOM + ghi lich su REMOVE_ITEM.</summary>
    [HttpDelete("{id:long}/items/{materialId:long}")]
    public async Task<ActionResult<ApiResult<object>>> DeleteItem(
        long id, long materialId, CancellationToken ct)
    {
        await _service.DeleteItemAsync(id, materialId, ct);
        return Ok(ApiResult.Success());
    }

    /// <summary>Trinh duyet BOM (chi khi dang DRAFT).</summary>
    [HttpPost("{id:long}/submit")]
    public async Task<ActionResult<ApiResult<object>>> Submit(long id, CancellationToken ct)
    {
        await _service.SubmitAsync(id, ct);
        return Ok(ApiResult.Success());
    }

    /// <summary>Duyet BOM: kich hoat ban nay, luu kho ban ACTIVE cu cung product.</summary>
    [HttpPost("{id:long}/approve")]
    public async Task<ActionResult<ApiResult<object>>> Approve(
        long id, [FromBody] DecisionRequest req, CancellationToken ct)
    {
        await _service.ApproveAsync(id, req, ct);
        return Ok(ApiResult.Success());
    }

    /// <summary>Tu choi BOM.</summary>
    [HttpPost("{id:long}/reject")]
    public async Task<ActionResult<ApiResult<object>>> Reject(
        long id, [FromBody] DecisionRequest req, CancellationToken ct)
    {
        await _service.RejectAsync(id, req, ct);
        return Ok(ApiResult.Success());
    }

    /// <summary>Lich su thay doi cua BOM (moi nhat truoc).</summary>
    [HttpGet("{id:long}/history")]
    public async Task<ActionResult<ApiResult<IEnumerable<BomChangeHistoryDto>>>> GetHistory(
        long id, CancellationToken ct) =>
        Ok(ApiResult<IEnumerable<BomChangeHistoryDto>>.Success(await _service.ListHistoryAsync(id, ct)));

    /// <summary>Cac phieu trinh duyet cua BOM (moi nhat truoc).</summary>
    [HttpGet("{id:long}/approvals")]
    public async Task<ActionResult<ApiResult<IEnumerable<BomApprovalDto>>>> GetApprovals(
        long id, CancellationToken ct) =>
        Ok(ApiResult<IEnumerable<BomApprovalDto>>.Success(await _service.ListApprovalsAsync(id, ct)));

    /// <summary>Anh huong khi xoa BOM (trang thai + so dong + so don dang dung).</summary>
    [HttpGet("{id:long}/impact")]
    public async Task<ActionResult<ApiResult<BomImpactDto>>> GetImpact(long id, CancellationToken ct)
    {
        var impact = await _service.GetImpactAsync(id, ct);
        return impact is null
            ? NotFound(ApiResult<BomImpactDto>.Fail("NOT_FOUND", $"Khong tim thay dinh muc id={id}."))
            : Ok(ApiResult<BomImpactDto>.Success(impact));
    }

    /// <summary>Xoa BOM (chi DRAFT/REJECTED, chua co don su dung).</summary>
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(long id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResult.Success());
    }
}
