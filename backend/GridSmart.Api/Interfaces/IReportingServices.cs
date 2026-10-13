namespace GridSmart.Api.Interfaces;

/// <summary>So lieu trung tam dieu hanh (UC-08, BE-27).</summary>
public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(int? unitId, CancellationToken ct);
    Task<IReadOnlyList<HeatPoint>> GetHeatmapAsync(int? unitId, DateTime? from, DateTime? to, int? categoryId, CancellationToken ct);
    Task<IReadOnlyList<TrendPoint>> GetTrendAsync(int? unitId, int days, CancellationToken ct);
}

/// <summary>Bao cao va KPI (BE-28).</summary>
public interface IReportService
{
    Task<IReadOnlyList<UnitReportRow>> ByUnitAsync(int? parentUnitId, DateTime? from, DateTime? to, CancellationToken ct);
    Task<IReadOnlyList<CategoryReportRow>> ByCategoryAsync(int? unitId, DateTime? from, DateTime? to, CancellationToken ct);
    Task<IReadOnlyList<OfficerKpiRow>> OfficerKpiAsync(int? unitId, DateTime? from, DateTime? to, CancellationToken ct);
    Task<ExportFile> ExportAsync(string type, string format, int? unitId, DateTime? from, DateTime? to, CancellationToken ct);
}
