using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Apps.ManagerPerformance.Services;
using GM95.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace GM95.Server.Apps.ManagerPerformance.Controllers;

/// <summary>Danh muc nen: don vi tinh, kho, nhom NVL. Prefix /dgrpi/ (app dau tien).</summary>
[ApiController]
[Route("dgrpi/manager-performance/lookups")]
public sealed class LookupsController : ControllerBase
{
    private readonly LookupService _service;

    public LookupsController(LookupService service) => _service = service;

    // --- Don vi tinh ---

    /// <summary>Danh sach don vi tinh (sap xep theo code).</summary>
    [HttpGet("units-of-measure")]
    public async Task<ActionResult<ApiResult<IEnumerable<UomDto>>>> GetUoms(CancellationToken ct) =>
        Ok(ApiResult<IEnumerable<UomDto>>.Success(await _service.ListUomsAsync(ct)));

    /// <summary>Tao don vi tinh moi.</summary>
    [HttpPost("units-of-measure")]
    public async Task<ActionResult<ApiResult<object>>> CreateUom(
        [FromBody] CreateUomRequest req, CancellationToken ct)
    {
        var id = await _service.CreateUomAsync(req, ct);
        return Ok(ApiResult<object>.Success(new { id }));
    }

    // --- Kho ---

    /// <summary>Danh sach kho (activeOnly loc kho dang hoat dong), sap xep theo code.</summary>
    [HttpGet("warehouses")]
    public async Task<ActionResult<ApiResult<IEnumerable<WarehouseDto>>>> GetWarehouses(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<WarehouseDto>>.Success(await _service.ListWarehousesAsync(activeOnly, ct)));

    /// <summary>Tao kho moi.</summary>
    [HttpPost("warehouses")]
    public async Task<ActionResult<ApiResult<object>>> CreateWarehouse(
        [FromBody] CreateWarehouseRequest req, CancellationToken ct)
    {
        var id = await _service.CreateWarehouseAsync(req, ct);
        return Ok(ApiResult<object>.Success(new { id }));
    }

    // --- Nhom NVL ---

    /// <summary>Danh sach nhom NVL (sap xep theo code).</summary>
    [HttpGet("material-categories")]
    public async Task<ActionResult<ApiResult<IEnumerable<MaterialCategoryDto>>>> GetCategories(CancellationToken ct) =>
        Ok(ApiResult<IEnumerable<MaterialCategoryDto>>.Success(await _service.ListCategoriesAsync(ct)));

    /// <summary>Tao nhom NVL moi.</summary>
    [HttpPost("material-categories")]
    public async Task<ActionResult<ApiResult<object>>> CreateCategory(
        [FromBody] CreateCategoryRequest req, CancellationToken ct)
    {
        var id = await _service.CreateCategoryAsync(req, ct);
        return Ok(ApiResult<object>.Success(new { id }));
    }
}
