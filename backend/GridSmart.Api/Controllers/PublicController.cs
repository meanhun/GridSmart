using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GridSmart.Api.Controllers;

/// <summary>Form gui phan anh (multipart/form-data).</summary>
public sealed class CreateIncidentForm
{
    public IFormFile? Image { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Description { get; set; }
    public int? CategoryId { get; set; }
    public string? ReporterPhone { get; set; }
    public string? AddressText { get; set; }
}

public sealed class ClassifyPreviewForm
{
    public IFormFile? Image { get; set; }
    public string? Description { get; set; }
}

/// <summary>Cong cong dan khong can dang nhap (BE-10, BE-11, BE-21).</summary>
[ApiController]
[Route("api/public")]
[Tags("Public")]
public sealed class PublicController(IPublicIncidentService incidents, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("incidents")]
    [EnableRateLimiting("report")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<CreateIncidentResult>(StatusCodes.Status202Accepted)]
    [EndpointSummary("Gửi phản ánh hiện trường (UC-01). Trả mã tra cứu ngay, xử lý AI và điều phối chạy nền.")]
    public async Task<IActionResult> CreateIncident([FromForm] CreateIncidentForm form, CancellationToken ct)
    {
        var image = form.Image ?? throw new DomainException(ErrorCodes.ImageInvalid, "Thiếu ảnh hiện trường.");
        await using var stream = image.OpenReadStream();
        // Cong dan da dang nhap (co quyen incident.create) thi gan reporter_user_id
        var reporterId = currentUser.HasPermission(Permissions.IncidentCreate) ? currentUser.UserId : null;
        var result = await incidents.CreateAsync(new CreateIncidentCommand(stream, image.Length, form.Latitude, form.Longitude,
            form.Description, form.CategoryId, form.ReporterPhone, reporterId, form.AddressText), ct);
        return Accepted($"/api/public/track?code={result.TrackingCode}", result);
    }

    [HttpPost("classify-preview")]
    [EnableRateLimiting("classify")]
    [Consumes("multipart/form-data")]
    [EndpointSummary("Gợi ý danh mục từ ảnh và mô tả, không lưu dữ liệu.")]
    public async Task<IReadOnlyList<CategorySuggestion>> ClassifyPreview([FromForm] ClassifyPreviewForm form, CancellationToken ct)
    {
        var image = form.Image ?? throw new DomainException(ErrorCodes.ImageInvalid, "Thiếu ảnh.");
        await using var stream = image.OpenReadStream();
        return await incidents.ClassifyPreviewAsync(stream, image.Length, form.Description, ct);
    }

    [HttpGet("categories")]
    [EndpointSummary("Danh mục sự cố đang hoạt động.")]
    public Task<IReadOnlyList<CategoryDto>> Categories(CancellationToken ct) => incidents.GetCategoriesAsync(ct);

    [HttpGet("locate")]
    [EnableRateLimiting("track")]
    [EndpointSummary("Tên khu phố, phường/xã chứa điểm (để công dân xác nhận vị trí).")]
    public Task<LocateResult> Locate(double lat, double lon, CancellationToken ct) => incidents.LocateAsync(lat, lon, ct);

    [HttpGet("track")]
    [EnableRateLimiting("track")]
    [EndpointSummary("Tra cứu tiến độ bằng mã và số điện thoại (UC-02).")]
    public Task<TrackResult> Track(string code, string? phone, CancellationToken ct) => incidents.TrackAsync(code, phone, currentUser.UserId, ct);

    [HttpPost("incidents/{code}/rating")]
    [EnableRateLimiting("track")]
    [EndpointSummary("Đánh giá 1-5 sao sau khi sự cố được xử lý (UC-03).")]
    public async Task<IActionResult> Rate(string code, RateRequest request, CancellationToken ct)
    {
        await incidents.RateAsync(code, request, currentUser.UserId, ct);
        return NoContent();
    }

    [HttpGet("~/api/me/incidents")]
    [HasPermission(Permissions.IncidentViewOwn)]
    [EndpointSummary("Phản ánh của công dân đang đăng nhập.")]
    public Task<IReadOnlyList<MyIncidentItem>> MyIncidents(CancellationToken ct) => incidents.GetMyIncidentsAsync(currentUser.UserId!.Value, ct);
}
