using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Apps.ManagerPerformance.Services;
using DGroup.Server.Infrastructure.Web;
using Microsoft.AspNetCore.Mvc;

namespace DGroup.Server.Apps.ManagerPerformance.Controllers;

/// <summary>Quan ly danh muc nguyen vat lieu. Prefix /dgrpi/ (app dau tien).</summary>
[ApiController]
[Route("dgrpi/manager-performance/materials")]
public sealed class MaterialsController : ControllerBase
{
    private readonly MaterialService _service;

    public MaterialsController(MaterialService service) => _service = service;

    /// <summary>Danh sach NVL (mac dinh chi hoat dong).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult<IEnumerable<MaterialDto>>>> GetAll(
        [FromQuery] bool activeOnly = true, [FromQuery] int? year = null, [FromQuery] int? month = null,
        CancellationToken ct = default) =>
        Ok(ApiResult<IEnumerable<MaterialDto>>.Success(await _service.ListAsync(activeOnly, year, month, ct)));

    /// <summary>Chi tiet 1 NVL.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResult<MaterialDto>>> GetById(long id, CancellationToken ct)
    {
        var m = await _service.GetAsync(id, ct);
        return m is null
            ? NotFound(ApiResult<MaterialDto>.Fail("NOT_FOUND", $"Khong tim thay NVL id={id}."))
            : Ok(ApiResult<MaterialDto>.Success(m));
    }

    /// <summary>Tao NVL moi.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResult<object>>> Create(
        [FromBody] CreateMaterialRequest req, CancellationToken ct)
    {
        var id = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id },
            ApiResult<object>.Success(new { id }));
    }

    /// <summary>Cap nhat NVL.</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> Update(
        long id, [FromBody] UpdateMaterialRequest req, CancellationToken ct)
    {
        var ok = await _service.UpdateAsync(id, req, ct);
        return ok
            ? Ok(ApiResult.Success())
            : NotFound(ApiResult<object>.Fail("NOT_FOUND", $"Khong tim thay NVL id={id}."));
    }

    /// <summary>Anh huong khi xoa NVL (dem noi tham chieu) — phuc vu popup canh bao o app.</summary>
    [HttpGet("{id:long}/impact")]
    public async Task<ActionResult<ApiResult<MaterialImpactDto>>> GetImpact(long id, CancellationToken ct)
    {
        var impact = await _service.GetImpactAsync(id, ct);
        return impact is null
            ? NotFound(ApiResult<MaterialImpactDto>.Fail("NOT_FOUND", $"Khong tim thay NVL id={id}."))
            : Ok(ApiResult<MaterialImpactDto>.Success(impact));
    }

    /// <summary>Xoa vinh vien NVL (tu choi neu dang duoc su dung — app se de xuat ngung hoat dong).</summary>
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(long id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResult.Success());
    }
}
