using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>Bao cao va dashboard (SP_rpt_*). Khong gan voi mot bang.</summary>
public sealed class ReportsDataAccess
{
    public Task<Row?> DashboardSummaryAsync(string? scopePaths, string? unitPath, DateTime todayStartUtc, DateTime sinceUtc) =>
        ProcExecutor.QuerySingleAsync("SP_rpt_dashboard_Summary",
            ProcExecutor.P("@scope_paths", scopePaths),
            ProcExecutor.P("@unit_path", unitPath),
            ProcExecutor.P("@today_start_utc", todayStartUtc),
            ProcExecutor.P("@since_utc", sinceUtc));

    public Task<List<Row>> HeatmapAsync(string? scopePaths, string? unitPath, DateTime? fromUtc, DateTime? toUtc, int? categoryId, int take) =>
        ProcExecutor.QueryAsync("SP_rpt_dashboard_Heatmap",
            ProcExecutor.P("@scope_paths", scopePaths),
            ProcExecutor.P("@unit_path", unitPath),
            ProcExecutor.P("@from_utc", fromUtc),
            ProcExecutor.P("@to_utc", toUtc),
            ProcExecutor.P("@category_id", categoryId),
            ProcExecutor.P("@take", take));

    public Task<List<Row>> TrendAsync(string? scopePaths, string? unitPath, DateTime startUtc, int days) =>
        ProcExecutor.QueryAsync("SP_rpt_dashboard_Trend",
            ProcExecutor.P("@scope_paths", scopePaths),
            ProcExecutor.P("@unit_path", unitPath),
            ProcExecutor.P("@start_utc", startUtc),
            ProcExecutor.P("@days", days));

    public Task<List<Row>> ByChildUnitsAsync(int unitId, DateTime? fromUtc, DateTime? toUtc) =>
        ProcExecutor.QueryAsync("SP_rpt_by_child_units",
            ProcExecutor.P("@unit_id", unitId),
            ProcExecutor.P("@from_utc", fromUtc),
            ProcExecutor.P("@to_utc", toUtc));

    public Task<List<Row>> ByCategoryAsync(string? scopePaths, string? unitPath, DateTime? fromUtc, DateTime? toUtc) =>
        ProcExecutor.QueryAsync("SP_rpt_by_category",
            ProcExecutor.P("@scope_paths", scopePaths),
            ProcExecutor.P("@unit_path", unitPath),
            ProcExecutor.P("@from_utc", fromUtc),
            ProcExecutor.P("@to_utc", toUtc));

    public Task<List<Row>> OfficerKpiAsync(string? scopePaths, string? unitPath, DateTime? fromUtc, DateTime? toUtc) =>
        ProcExecutor.QueryAsync("SP_rpt_officer_kpi",
            ProcExecutor.P("@scope_paths", scopePaths),
            ProcExecutor.P("@unit_path", unitPath),
            ProcExecutor.P("@from_utc", fromUtc),
            ProcExecutor.P("@to_utc", toUtc));
}
