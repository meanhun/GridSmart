using GridSmart.Api.Common;
using GridSmart.Api.Constants;
using GridSmart.Api.Data;
using GridSmart.Api.DataAccess;
using GridSmart.Api.Dtos;
using GridSmart.Api.Interfaces;

namespace GridSmart.Api.Services;

/// <summary>So lieu trung tam dieu hanh (UC-08, BE-27).</summary>
public sealed class DashboardService(IScopeService scope, AdminUnitsDataAccess adminUnits, ReportsDataAccess reports, TimeProvider clock) : IDashboardService
{
    /// <summary>Gio Viet Nam (UTC+7) de tinh "hom nay".</summary>
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    public async Task<DashboardSummary> GetSummaryAsync(int? unitId, CancellationToken ct)
    {
        var q = await IncidentScope.ResolveAsync(scope, adminUnits, Permissions.DashboardView, unitId);
        if (q is null) return new DashboardSummary(0, 0, 0, 0, 0, null, null, 0, null);

        var now = Now();
        var row = await reports.DashboardSummaryAsync(q.ScopePaths, q.UnitPath, TodayStartUtc(now), now.AddDays(-30));
        if (row is null) return new DashboardSummary(0, 0, 0, 0, 0, null, null, 0, null);

        return new DashboardSummary(
            row.GetInt("open_count"), row.GetInt("overdue_count"), row.GetInt("pending_review_count"),
            row.GetInt("resolved_today_count"), row.GetInt("created_today_count"),
            Round(row.GetDoubleOrNull("avg_dispatch_minutes"), 1),
            Round(row.GetDoubleOrNull("avg_resolution_hours"), 1),
            Math.Round(row.GetDoubleOrNull("duplicate_rate") ?? 0, 3),
            Round(row.GetDoubleOrNull("on_time_rate"), 3));
    }

    public async Task<IReadOnlyList<HeatPoint>> GetHeatmapAsync(int? unitId, DateTime? from, DateTime? to, int? categoryId, CancellationToken ct)
    {
        var q = await IncidentScope.ResolveAsync(scope, adminUnits, Permissions.DashboardView, unitId);
        if (q is null) return new List<HeatPoint>();
        var start = from ?? Now().AddDays(-30);
        var rows = await reports.HeatmapAsync(q.ScopePaths, q.UnitPath, start, to, categoryId, 5000);
        return rows.Select(r => new HeatPoint(
            r.GetDoubleOrNull("latitude") ?? 0,
            r.GetDoubleOrNull("longitude") ?? 0,
            1 + r.GetInt("duplicate_count"))).ToList();
    }

    public async Task<IReadOnlyList<TrendPoint>> GetTrendAsync(int? unitId, int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 365);
        var q = await IncidentScope.ResolveAsync(scope, adminUnits, Permissions.DashboardView, unitId);
        var now = Now();
        var startUtc = TodayStartUtc(now).AddDays(-(days - 1));

        var byDay = new Dictionary<DateOnly, (int Created, int Resolved)>();
        if (q is not null)
        {
            foreach (var r in await reports.TrendAsync(q.ScopePaths, q.UnitPath, startUtc, days))
                byDay[DateOnly.FromDateTime(r.GetDateTime("day_vn"))] = (r.GetInt("created_count"), r.GetInt("resolved_count"));
        }

        var firstDay = DateOnly.FromDateTime(startUtc + VnOffset);
        return Enumerable.Range(0, days)
            .Select(d => firstDay.AddDays(d))
            .Select(d =>
            {
                var v = byDay.GetValueOrDefault(d);
                return new TrendPoint(d, v.Created, v.Resolved);
            })
            .ToList();
    }

    private static double? Round(double? value, int digits) => value is null ? null : Math.Round(value.Value, digits);

    private static DateTime TodayStartUtc(DateTime nowUtc)
    {
        var local = nowUtc + VnOffset;
        return DateTime.SpecifyKind(local.Date - VnOffset, DateTimeKind.Utc);
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
