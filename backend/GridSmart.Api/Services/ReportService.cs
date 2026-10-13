using System.Text;
using GridSmart.Api.Common;
using GridSmart.Api.Constants;
using GridSmart.Api.Data;
using GridSmart.Api.DataAccess;
using GridSmart.Api.Dtos;
using GridSmart.Api.Exceptions;
using GridSmart.Api.Interfaces;
using GridSmart.Api.Models.Enums;

namespace GridSmart.Api.Services;

/// <summary>Bao cao va KPI (BE-28).</summary>
public sealed class ReportService(
    IScopeService scope,
    AdminUnitsDataAccess adminUnits,
    IncidentsDataAccess incidents,
    ReportsDataAccess reports,
    IExcelExporter excel,
    TimeProvider clock) : IReportService
{
    /// <summary>Gioi han so dong xuat (proc page_size toi da 200, nen lap theo trang).</summary>
    private const int ExportLimit = 50000;

    public async Task<IReadOnlyList<UnitReportRow>> ByUnitAsync(int? parentUnitId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        int unitId;
        if (parentUnitId is { } pid)
        {
            unitId = pid;
        }
        else
        {
            // Mac dinh: don vi cao nhat trong pham vi nguoi dung (hoac goc cua cay neu toan he thong)
            var paths = scope.ScopePaths(Permissions.ReportView);
            if (paths == "") return new List<UnitReportRow>();
            if (paths is null)
            {
                var tree = await adminUnits.ReadTreeAsync(false);
                var root = tree
                    .Where(r => r.GetIntOrNull("parent_id") is null && r.GetBool("is_active"))
                    .OrderBy(r => r.GetInt("id"))
                    .FirstOrDefault()
                    ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị.");
                unitId = root.GetInt("id");
            }
            else
            {
                unitId = paths.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .OrderBy(p => p.Length)
                    .Select(p => IncidentRows.UnitIdFromPath(p))
                    .First() ?? 0;
            }
        }
        var unit = await adminUnits.ReadByIdAsync(unitId)
                   ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị.");
        var path = unit.GetStringOrNull("unit_path")
                   ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị.");
        scope.EnsureAccess(Permissions.ReportView, path, $"admin_unit:{unitId}");

        var rows = await reports.ByChildUnitsAsync(unitId, from, to);
        return rows.Select(r => new UnitReportRow(r.GetInt("unit_id"), r.GetString("unit_code"), r.GetString("name"),
            r.GetInt("total_incidents"), r.GetInt("open_incidents"), r.GetInt("resolved_incidents"), r.GetInt("overdue_incidents"))).ToList();
    }

    public async Task<IReadOnlyList<CategoryReportRow>> ByCategoryAsync(int? unitId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var q = await IncidentScope.ResolveAsync(scope, adminUnits, Permissions.ReportView, unitId);
        if (q is null) return new List<CategoryReportRow>();
        var rows = await reports.ByCategoryAsync(q.ScopePaths, q.UnitPath, from, to);
        return rows
            .Select(r => new CategoryReportRow(r.GetIntOrNull("category_id"),
                r.GetStringOrNull("category_name") ?? "Chưa phân loại",
                r.GetInt("total"), r.GetInt("open"), r.GetInt("resolved"), r.GetInt("overdue")))
            .OrderByDescending(r => r.Total)
            .ToList();
    }

    public async Task<IReadOnlyList<OfficerKpiRow>> OfficerKpiAsync(int? unitId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var q = await IncidentScope.ResolveAsync(scope, adminUnits, Permissions.KpiView, unitId);
        if (q is null) return new List<OfficerKpiRow>();
        var rows = await reports.OfficerKpiAsync(q.ScopePaths, q.UnitPath, from, to);
        return rows.Select(r =>
        {
            var withSla = r.GetIntOrNull("resolved_with_sla") ?? 0;
            var onTime = r.GetIntOrNull("on_time_count") ?? 0;
            return new OfficerKpiRow(
                r.GetInt("officer_id"), r.GetString("full_name"), r.GetInt("assigned"), r.GetInt("resolved"),
                withSla == 0 ? null : Math.Round((double)onTime / withSla, 3),
                Round(r.GetDoubleOrNull("avg_accept_minutes"), 1),
                Round(r.GetDoubleOrNull("avg_resolve_hours"), 1),
                Round(r.GetDoubleOrNull("avg_rating"), 2),
                r.GetInt("escalated_count"), r.GetInt("open_now"));
        }).OrderBy(r => r.FullName).ToList();
    }

    public async Task<ExportFile> ExportAsync(string type, string format, int? unitId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (headers, rows, name) = type.ToLowerInvariant() switch
        {
            "by-unit" => await UnitTableAsync(unitId, from, to, ct),
            "officer-kpi" => await KpiTableAsync(unitId, from, to, ct),
            "incidents" => await IncidentTableAsync(unitId, from, to, ct),
            _ => throw new DomainException(ErrorCodes.ValidationFailed, "Loại báo cáo phải là by-unit, officer-kpi hoặc incidents."),
        };
        var stamp = clock.GetUtcNow().UtcDateTime.AddHours(7).ToString("yyyyMMdd-HHmm");
        return format.ToLowerInvariant() switch
        {
            "xlsx" => new ExportFile(excel.Export(name, headers, rows), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{name}-{stamp}.xlsx"),
            "csv" => new ExportFile(Csv(headers, rows), "text/csv; charset=utf-8", $"{name}-{stamp}.csv"),
            _ => throw new DomainException(ErrorCodes.ValidationFailed, "Định dạng phải là xlsx hoặc csv."),
        };
    }

    private async Task<(string[], List<object?[]>, string)> UnitTableAsync(int? unitId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var data = await ByUnitAsync(unitId, from, to, ct);
        return (new[] { "Mã đơn vị", "Tên đơn vị", "Tổng", "Đang mở", "Đã xử lý", "Quá hạn" },
            data.Select(r => new object?[] { r.UnitCode, r.Name, r.TotalIncidents, r.OpenIncidents, r.ResolvedIncidents, r.OverdueIncidents }).ToList(),
            "bao-cao-theo-don-vi");
    }

    private async Task<(string[], List<object?[]>, string)> KpiTableAsync(int? unitId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var data = await OfficerKpiAsync(unitId, from, to, ct);
        return (new[] { "Cán bộ", "Số việc", "Đã xử lý", "Tỷ lệ đúng hạn", "Nhận việc TB (phút)", "Xử lý TB (giờ)", "Đánh giá TB", "Số lần leo thang", "Đang mở" },
            data.Select(r => new object?[] { r.FullName, r.Assigned, r.Resolved, r.OnTimeRate, r.AvgAcceptMinutes, r.AvgResolveHours, r.AvgRating, r.EscalatedCount, r.OpenNow }).ToList(),
            "kpi-can-bo");
    }

    private async Task<(string[], List<object?[]>, string)> IncidentTableAsync(int? unitId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var headers = new[] { "Mã", "Trạng thái", "Danh mục", "Ưu tiên", "Đơn vị", "Ô lưới", "Cán bộ", "Địa chỉ", "Tạo lúc (UTC)", "Hạn SLA (UTC)", "Xử lý lúc (UTC)", "Quá hạn", "Số trùng", "Gộp vào (ID gốc)" };
        var q = await IncidentScope.ResolveAsync(scope, adminUnits, Permissions.ReportExport, unitId);
        var source = new List<Row>();
        if (q is not null)
        {
            // Proc gioi han page_size 200: lap theo trang den khi du ExportLimit
            var pageIndex = 1;
            while (source.Count < ExportLimit)
            {
                var (items, total) = await incidents.ListAsync(new IncidentListFilter(
                    PageIndex: pageIndex, PageSize: 200, ScopePaths: q.ScopePaths, StatusList: null, UnitPath: q.UnitPath,
                    GridId: null, CategoryId: null, Priority: null, IsOverdue: null, IsManualReview: null,
                    AssignedOfficerId: null, FromUtc: from, ToUtc: to, Search: null, IncludeChildren: true));
                source.AddRange(items);
                if (items.Count == 0 || source.Count >= total) break;
                pageIndex++;
            }
            if (source.Count > ExportLimit) source.RemoveRange(ExportLimit, source.Count - ExportLimit);
        }

        var data = source.Select(r => new object?[]
        {
            r.GetString("tracking_code"),
            IncidentText.StatusCode(IncidentRows.Status(r)),
            r.GetStringOrNull("category_name"),
            r.GetInt("priority"),
            r.GetStringOrNull("unit_name"),
            r.GetStringOrNull("grid_code"),
            r.GetStringOrNull("officer_name"),
            r.GetStringOrNull("address_text"),
            r.GetDateTime("created_at"),
            r.GetDateTimeOrNull("sla_deadline"),
            r.GetDateTimeOrNull("resolved_at"),
            r.GetBool("is_overdue"),
            r.GetInt("duplicate_count"),
            r.GetLongOrNull("parent_incident_id"),
        }).ToList();
        return (headers, data, "danh-sach-su-co");
    }

    private static double? Round(double? value, int digits) => value is null ? null : Math.Round(value.Value, digits);

    private static byte[] Csv(IReadOnlyList<string> headers, IEnumerable<object?[]> rows)
    {
        static string Cell(object? v)
        {
            var s = v switch
            {
                null => "",
                DateTime d => d.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                double x => x.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => v.ToString() ?? "",
            };
            return s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        }
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', headers.Select(Cell)));
        foreach (var r in rows) sb.AppendLine(string.Join(',', r.Select(Cell)));
        // BOM de Excel doc dung tieng Viet
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }
}
