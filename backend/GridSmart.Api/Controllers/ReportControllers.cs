using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace GridSmart.Api.Controllers;

/// <summary>So lieu trung tam dieu hanh (BE-27).</summary>
[ApiController]
[Route("api/dashboard")]
[Tags("Dashboard")]
[HasPermission(Permissions.DashboardView)]
public sealed class DashboardController(IDashboardService dashboard, IGridService grids) : ControllerBase
{
    [HttpGet("summary")]
    [EndpointSummary("Số liệu tổng quan: mở, quá hạn, chờ duyệt, thời gian điều phối và xử lý, tỷ lệ gom trùng.")]
    public Task<DashboardSummary> Summary(int? unitId, CancellationToken ct) => dashboard.GetSummaryAsync(unitId, ct);

    [HttpGet("heatmap")]
    [EndpointSummary("Điểm nóng [lat, lon, weight], tối đa 5000 điểm.")]
    public Task<IReadOnlyList<HeatPoint>> Heatmap(int? unitId, DateTime? from, DateTime? to, int? categoryId, CancellationToken ct) =>
        dashboard.GetHeatmapAsync(unitId, from, to, categoryId, ct);

    [HttpGet("grid-status")]
    [Produces(GridsController.GeoJsonContentType)]
    [ProducesResponseType<JsonElement>(StatusCodes.Status200OK, GridsController.GeoJsonContentType)]
    [EndpointSummary("Màu trạng thái ô lưới (GeoJSON), cùng định dạng /api/grids.")]
    public async Task<IActionResult> GridStatus(int? unitId, CancellationToken ct) =>
        Content(await grids.GetFeatureCollectionAsync(unitId, ct), GridsController.GeoJsonContentType);

    [HttpGet("trend")]
    [EndpointSummary("Số sự cố mới và đã xử lý theo ngày (giờ Việt Nam).")]
    public Task<IReadOnlyList<TrendPoint>> Trend(int? unitId, int? days, CancellationToken ct) => dashboard.GetTrendAsync(unitId, days ?? 30, ct);
}

/// <summary>Bao cao va KPI (BE-28).</summary>
[ApiController]
[Route("api/reports")]
[Tags("Reports")]
public sealed class ReportsController(IReportService reports) : ControllerBase
{
    [HttpGet("by-unit")]
    [HasPermission(Permissions.ReportView)]
    [EndpointSummary("Thống kê theo đơn vị con. Bỏ trống parentUnitId thì lấy đơn vị cao nhất trong phạm vi.")]
    public Task<IReadOnlyList<UnitReportRow>> ByUnit(int? parentUnitId, DateTime? from, DateTime? to, CancellationToken ct) =>
        reports.ByUnitAsync(parentUnitId, from, to, ct);

    [HttpGet("by-category")]
    [HasPermission(Permissions.ReportView)]
    [EndpointSummary("Thống kê theo danh mục.")]
    public Task<IReadOnlyList<CategoryReportRow>> ByCategory(int? unitId, DateTime? from, DateTime? to, CancellationToken ct) =>
        reports.ByCategoryAsync(unitId, from, to, ct);

    [HttpGet("officer-kpi")]
    [HasPermission(Permissions.KpiView)]
    [EndpointSummary("KPI cán bộ: đúng hạn, thời gian nhận việc và xử lý, đánh giá, số lần leo thang.")]
    public Task<IReadOnlyList<OfficerKpiRow>> OfficerKpi(int? unitId, DateTime? from, DateTime? to, CancellationToken ct) =>
        reports.OfficerKpiAsync(unitId, from, to, ct);

    [HttpGet("export")]
    [HasPermission(Permissions.ReportExport)]
    [EndpointSummary("Xuất báo cáo: type = by-unit | officer-kpi | incidents, format = xlsx | csv.")]
    public async Task<IActionResult> Export(string type, string? format, int? unitId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var file = await reports.ExportAsync(type, format ?? "xlsx", unitId, from, to, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
