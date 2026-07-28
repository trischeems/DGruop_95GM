using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Services;
using GM95.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace GM95.Server.Apps.ManagerPerformance.Controllers;

/// <summary>Quan ly danh muc ma hang thanh pham. Prefix /dgrpi/ (app dau tien).</summary>
[ApiController]
[Route("dgrpi/manager-performance/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly ProductService _service;

    public ProductsController(ProductService service) => _service = service;

    /// <summary>Danh sach ma hang (mac dinh chi hoat dong).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<ProductDto>>>> GetAll(
        [FromQuery] bool activeOnly = true, [FromQuery] int? year = null, [FromQuery] int? month = null,
        CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<ProductDto>>.Success(await _service.ListAsync(activeOnly, year, month, ct)));

    /// <summary>Chi tiet 1 ma hang.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResult<ProductDto>>> GetById(long id, CancellationToken ct)
    {
        var p = await _service.GetAsync(id, ct);
        return p is null
            ? NotFound(ApiResult<ProductDto>.Fail("NOT_FOUND", $"Khong tim thay ma hang id={id}."))
            : Ok(ApiResult<ProductDto>.Success(p));
    }

    /// <summary>Tao ma hang moi.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResult<object>>> Create(
        [FromBody] CreateProductRequest req, CancellationToken ct)
    {
        var id = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id },
            ApiResult<object>.Success(new { id }));
    }

    /// <summary>Cap nhat ma hang.</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> Update(
        long id, [FromBody] UpdateProductRequest req, CancellationToken ct)
    {
        var ok = await _service.UpdateAsync(id, req, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay ma hang id={id}."));
    }

    /// <summary>Anh huong khi xoa ma hang (dem noi tham chieu) — phuc vu popup canh bao o app.</summary>
    [HttpGet("{id:long}/impact")]
    public async Task<ActionResult<ApiResult<ProductImpactDto>>> GetImpact(long id, CancellationToken ct)
    {
        var impact = await _service.GetImpactAsync(id, ct);
        return impact is null
            ? NotFound(ApiResult<ProductImpactDto>.Fail("NOT_FOUND", $"Khong tim thay ma hang id={id}."))
            : Ok(ApiResult<ProductImpactDto>.Success(impact));
    }

    /// <summary>Xoa vinh vien ma hang (tu choi neu dang duoc su dung).</summary>
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(long id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResult.Success());
    }
}
