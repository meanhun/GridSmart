using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace GridSmart.Api.Controllers;

/// <summary>O luoi (BE-25).</summary>
[ApiController]
[Route("api/grids")]
[Tags("Grids")]
public sealed class GridsController(IGridService grids) : ControllerBase
{
    public const string GeoJsonContentType = "application/geo+json";

    [HttpGet]
    [HasPermission(Permissions.GridView)]
    [Produces(GeoJsonContentType)]
    [ProducesResponseType<JsonElement>(StatusCodes.Status200OK, GeoJsonContentType)]
    [EndpointSummary("Ô lưới dạng GeoJSON FeatureCollection, kèm statusColor và số sự cố.")]
    public async Task<IActionResult> List(int? unitId, CancellationToken ct) =>
        Content(await grids.GetFeatureCollectionAsync(unitId, ct), GeoJsonContentType);

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.GridView)]
    [EndpointSummary("Chi tiết ô lưới, ranh giới và cán bộ phụ trách.")]
    public Task<GridDetail> Get(int id, CancellationToken ct) => grids.GetAsync(id, ct);

    [HttpPost]
    [HasPermission(Permissions.GridManage)]
    [EndpointSummary("Tạo ô lưới (GeoJSON hoặc WKT). Trả cảnh báo chồng lấn nếu có.")]
    public Task<GridSaveResult> Create(GridUpsertRequest request, CancellationToken ct) => grids.CreateAsync(request, ct);

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.GridManage)]
    [EndpointSummary("Sửa ô lưới. Bỏ trống ranh giới thì giữ ranh giới cũ.")]
    public Task<GridSaveResult> Update(int id, GridUpsertRequest request, CancellationToken ct) => grids.UpdateAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.GridManage)]
    [EndpointSummary("Ngừng sử dụng ô lưới (xóa mềm).")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        await grids.DeactivateAsync(id, ct);
        return NoContent();
    }

    [HttpPost("import")]
    [HasPermission(Permissions.GridManage)]
    [EndpointSummary("Nhập GeoJSON (grid_code, name, unit_code). Mặc định chạy thử.")]
    public Task<ImportResult> Import([FromBody] JsonElement body, bool? dryRun, CancellationToken ct) => grids.ImportAsync(body, dryRun ?? true, ct);

    [HttpPut("{id:int}/officers")]
    [HasPermission(Permissions.GridAssignOfficer)]
    [EndpointSummary("Phân công cán bộ ô lưới.")]
    public async Task<IActionResult> AssignOfficers(int id, AssignOfficersRequest request, CancellationToken ct)
    {
        await grids.AssignOfficersAsync(id, request, ct);
        return NoContent();
    }
}
