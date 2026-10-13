using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GridSmart.Api.Controllers;

public sealed class ResolveForm
{
    public IFormFile? Image { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Note { get; set; }
}

/// <summary>Su co cho can bo, dieu phoi vien, lanh dao (BE-17, BE-18, BE-19).</summary>
[ApiController]
[Route("api/incidents")]
[Tags("Incidents")]
public sealed class IncidentsController(IIncidentQueryService queries, IIncidentWorkflowService workflow) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.IncidentView)]
    [EndpointSummary("Danh sách sự cố trong phạm vi. status nhận nhiều giá trị cách nhau dấu phẩy.")]
    public Task<PagedResult<IncidentListItem>> List([FromQuery] IncidentListQuery query, CancellationToken ct) => queries.ListAsync(query, ct);

    [HttpGet("{id:long}")]
    [Authorize]
    [EndpointSummary("Chi tiết sự cố.")]
    public Task<IncidentDetail> Get(long id, CancellationToken ct) => queries.GetAsync(id, ct);

    [HttpGet("{id:long}/logs")]
    [Authorize]
    [EndpointSummary("Nhật ký xử lý.")]
    public Task<IReadOnlyList<IncidentLogDto>> Logs(long id, CancellationToken ct) => queries.GetLogsAsync(id, ct);

    [HttpGet("{id:long}/children")]
    [Authorize]
    [EndpointSummary("Các phản ánh trùng đã gộp vào sự cố này.")]
    public Task<IReadOnlyList<IncidentListItem>> Children(long id, CancellationToken ct) => queries.GetChildrenAsync(id, ct);

    [HttpPost("{id:long}/accept")]
    [HasPermission(Permissions.IncidentAccept)]
    [EndpointSummary("Cán bộ nhận việc.")]
    public async Task<IActionResult> Accept(long id, CancellationToken ct)
    {
        await workflow.AcceptAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/resolve")]
    [HasPermission(Permissions.IncidentResolve)]
    [Consumes("multipart/form-data")]
    [EndpointSummary("Nghiệm thu bằng ảnh After, kiểm tra vị trí chụp (UC-07).")]
    public async Task<IActionResult> Resolve(long id, [FromForm] ResolveForm form, CancellationToken ct)
    {
        var image = form.Image ?? throw new DomainException(ErrorCodes.ImageInvalid, "Thiếu ảnh nghiệm thu.");
        await using var stream = image.OpenReadStream();
        await workflow.ResolveAsync(id, new ResolveCommand(stream, image.Length, form.Latitude, form.Longitude, form.Note), ct);
        return NoContent();
    }

    [HttpPost("{id:long}/return")]
    [HasPermission(Permissions.IncidentAccept)]
    [EndpointSummary("Cán bộ trả lại sự cố ngoài thẩm quyền.")]
    public async Task<IActionResult> Return(long id, ReasonRequest request, CancellationToken ct)
    {
        await workflow.ReturnAsync(id, request.Reason, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/classify")]
    [HasPermission(Permissions.IncidentClassify)]
    [EndpointSummary("Phân loại thủ công.")]
    public async Task<IActionResult> Classify(long id, ClassifyRequest request, CancellationToken ct)
    {
        await workflow.ClassifyAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/dispatch")]
    [HasPermission(Permissions.IncidentDispatch)]
    [EndpointSummary("Điều phối thủ công tới ô lưới và/hoặc cán bộ.")]
    public async Task<IActionResult> Dispatch(long id, DispatchRequest request, CancellationToken ct)
    {
        await workflow.DispatchAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/reassign")]
    [HasPermission(Permissions.IncidentReassign)]
    [EndpointSummary("Giao lại cho cán bộ khác, giữ nguyên hạn SLA.")]
    public async Task<IActionResult> Reassign(long id, ReassignRequest request, CancellationToken ct)
    {
        await workflow.ReassignAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    [HasPermission(Permissions.IncidentCancel)]
    [EndpointSummary("Hủy sự cố và các phản ánh trùng.")]
    public async Task<IActionResult> Cancel(long id, ReasonRequest request, CancellationToken ct)
    {
        await workflow.CancelAsync(id, request.Reason, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.IncidentDelete)]
    [EndpointSummary("Xóa hẳn phản ánh và các phản ánh trùng đã gộp vào nó. Không cần lý do.")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await workflow.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("~/api/officer/incidents")]
    [HasPermission(Permissions.IncidentAccept)]
    [EndpointSummary("Việc của cán bộ đang đăng nhập. tab = assigned | in_progress | done.")]
    public Task<IReadOnlyList<IncidentListItem>> MyAssignments(string? tab, CancellationToken ct) => queries.GetMyAssignmentsAsync(tab, ct);

    [HttpGet("~/api/officers")]
    [Authorize]
    [EndpointSummary("Cán bộ ô lưới trong phạm vi, kèm số việc đang mở (để điều phối, giao lại, phân công).")]
    public Task<IReadOnlyList<OfficerDto>> Officers(int? unitId, int? gridId, CancellationToken ct) => queries.ListOfficersAsync(unitId, gridId, ct);
}
