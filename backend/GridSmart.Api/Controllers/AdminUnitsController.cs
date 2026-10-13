using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace GridSmart.Api.Controllers;

/// <summary>Don vi hanh chinh (BE-24).</summary>
[ApiController]
[Route("api/admin-units")]
[Tags("Admin units")]
public sealed class AdminUnitsController(IAdminUnitService units) : ControllerBase
{
    [HttpGet("tree")]
    [HasPermission(Permissions.AdminUnitView)]
    [EndpointSummary("Cây đơn vị hành chính trong phạm vi.")]
    public Task<IReadOnlyList<AdminUnitNode>> Tree(int? rootId, int? depth, bool? includeInactive, CancellationToken ct) =>
        units.GetTreeAsync(rootId, depth, includeInactive ?? false, ct);

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.AdminUnitView)]
    [EndpointSummary("Chi tiết đơn vị kèm ranh giới GeoJSON.")]
    public Task<AdminUnitDetail> Get(int id, CancellationToken ct) => units.GetAsync(id, ct);

    [HttpGet("locate")]
    [HasPermission(Permissions.AdminUnitView)]
    [EndpointSummary("Đơn vị thấp nhất chứa điểm.")]
    public async Task<ActionResult<AdminUnitDetail>> Locate(double lat, double lon, CancellationToken ct) =>
        await units.LocateAsync(lat, lon, ct) is { } unit ? unit : NotFound();

    [HttpPost]
    [HasPermission(Permissions.AdminUnitManage)]
    [ProducesResponseType<CreatedId>(StatusCodes.Status201Created)]
    [EndpointSummary("Tạo đơn vị. Sai cấp hoặc vòng lặp trả 422.")]
    public async Task<IActionResult> Create(AdminUnitUpsertRequest request, CancellationToken ct)
    {
        var id = await units.CreateAsync(request, ct);
        return Created($"/api/admin-units/{id}", new CreatedId(id));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.AdminUnitManage)]
    [EndpointSummary("Sửa đơn vị, đổi đơn vị cha thì cây con tự cập nhật.")]
    public async Task<IActionResult> Update(int id, AdminUnitUpsertRequest request, CancellationToken ct)
    {
        await units.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("merge")]
    [HasPermission(Permissions.AdminUnitManage)]
    [EndpointSummary("Sáp nhập đơn vị (luồng 9.2).")]
    public Task<MergeResult> Merge(MergeRequest request, CancellationToken ct) => units.MergeAsync(request, ct);

    [HttpPost("import")]
    [HasPermission(Permissions.AdminUnitManage)]
    [EndpointSummary("Nhập GeoJSON FeatureCollection (unit_code, name, type_code, parent_code). Mặc định chạy thử.")]
    public Task<ImportResult> Import([FromBody] JsonElement body, bool? dryRun, CancellationToken ct) => units.ImportAsync(body, dryRun ?? true, ct);

    [HttpGet("~/api/admin-levels")]
    [HasPermission(Permissions.AdminUnitView)]
    [EndpointSummary("Các cấp hành chính.")]
    public Task<IReadOnlyList<AdminLevelDto>> Levels(CancellationToken ct) => units.GetLevelsAsync(ct);

    [HttpGet("~/api/admin-unit-types")]
    [HasPermission(Permissions.AdminUnitView)]
    [EndpointSummary("Các loại đơn vị (phường, xã, khu phố, thôn...).")]
    public Task<IReadOnlyList<AdminUnitTypeDto>> Types(CancellationToken ct) => units.GetTypesAsync(ct);
}
