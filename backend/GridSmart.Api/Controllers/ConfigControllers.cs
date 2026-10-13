using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GridSmart.Api.Controllers;

/// <summary>Danh muc su co (BE-26).</summary>
[ApiController]
[Route("api/categories")]
[Tags("Config")]
[HasPermission(Permissions.CategoryManage)]
public sealed class CategoriesController(IConfigService config) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Danh mục sự cố, kể cả danh mục đã tắt.")]
    public Task<IReadOnlyList<CategoryDto>> List(bool? includeInactive, CancellationToken ct) => config.ListCategoriesAsync(includeInactive ?? true, ct);

    [HttpPost]
    [ProducesResponseType<CreatedId>(StatusCodes.Status201Created)]
    [EndpointSummary("Thêm danh mục.")]
    public async Task<IActionResult> Create(CategoryUpsertRequest request, CancellationToken ct) =>
        Created("/api/categories", new CreatedId(await config.CreateCategoryAsync(request, ct)));

    [HttpPut("{id:int}")]
    [EndpointSummary("Sửa hoặc bật tắt danh mục.")]
    public async Task<IActionResult> Update(int id, CategoryUpsertRequest request, CancellationToken ct)
    {
        await config.UpdateCategoryAsync(id, request, ct);
        return NoContent();
    }
}

/// <summary>Chinh sach SLA (BE-26).</summary>
[ApiController]
[Route("api/sla-policies")]
[Tags("Config")]
public sealed class SlaPoliciesController(IConfigService config) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [EndpointSummary("Chính sách SLA theo mức ưu tiên.")]
    public Task<IReadOnlyList<SlaPolicyDto>> List(CancellationToken ct) => config.ListSlaPoliciesAsync(ct);

    [HttpPut]
    [HasPermission(Permissions.SlaManage)]
    [EndpointSummary("Cập nhật SLA. Chỉ áp dụng cho sự cố điều phối sau thời điểm lưu.")]
    public async Task<IActionResult> Update(List<SlaPolicyUpdate> request, CancellationToken ct)
    {
        await config.UpdateSlaPoliciesAsync(request, ct);
        return NoContent();
    }
}
