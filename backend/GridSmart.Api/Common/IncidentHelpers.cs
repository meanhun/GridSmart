using System.Globalization;
using GridSmart.Api.Constants;
using GridSmart.Api.Data;
using GridSmart.Api.DataAccess;
using GridSmart.Api.Dtos;
using GridSmart.Api.Exceptions;
using GridSmart.Api.Interfaces;
using GridSmart.Api.Models.Enums;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GridSmart.Api.Common;

/// <summary>Gia tri cot image_type trong tbl_incident_images.</summary>
public static class ImageTypes
{
    public const string Before = "BEFORE";
    public const string After = "AFTER";
}

/// <summary>Ghi nhat ky su co (tbl_incident_logs). Dung trong ProcTransaction khi ghi nhieu buoc.</summary>
public static class IncidentLogWriter
{
    public static Task<long> AddAsync(IncidentLogsDataAccess logs, long incidentId, LogActionType type, string note, DateTime now,
        int? userId = null, IncidentStatus? oldStatus = null, IncidentStatus? newStatus = null)
    {
        var row = new Row
        {
            ["incident_id"] = incidentId,
            ["action_type"] = EnumText.UpperSnake(type),
            ["action_note"] = note.Length > 500 ? note[..500] : note,
            ["action_by_user_id"] = userId,
            ["old_status"] = oldStatus is { } o ? (object)(byte)o : null,
            ["new_status"] = newStatus is { } n ? (object)(byte)n : null,
            ["created_at"] = now,
        };
        return logs.InsertAsync(row);
    }
}

/// <summary>Truy van nhat ky tren Row (thay cho cac truy van EF tren IncidentLog).</summary>
public static class IncidentLogQueries
{
    public static readonly LogActionType[] ReviewTypes = [LogActionType.Reassigned, LogActionType.PipelineError];

    public static async Task<bool> HasAsync(IncidentLogsDataAccess logs, long incidentId, LogActionType type)
    {
        var code = EnumText.UpperSnake(type);
        var rows = await logs.ReadByIncidentAsync(incidentId);
        return rows.Any(r => r.GetStringOrNull("action_type") == code);
    }

    /// <summary>Nhat ky review (REASSIGNED, PIPELINE_ERROR) moi nhat cua tung su co.</summary>
    public static async Task<Dictionary<long, (LogActionType? Type, IncidentStatus? NewStatus)>> LastReviewAsync(
        IncidentLogsDataAccess logs, IEnumerable<long> incidentIds)
    {
        var result = new Dictionary<long, (LogActionType? Type, IncidentStatus? NewStatus)>();
        foreach (var id in incidentIds.Distinct())
        {
            var rows = await logs.ReadByIncidentAsync(id);
            var last = rows
                .Where(r => ReviewTypes.Contains(IncidentRows.ParseLogType(r.GetStringOrNull("action_type")) ?? LogActionType.Created))
                .OrderByDescending(r => r.GetLong("id"))
                .FirstOrDefault();
            if (last is null) continue;
            result[id] = (IncidentRows.ParseLogType(last.GetStringOrNull("action_type")),
                IncidentRows.ToStatus(last.GetIntOrNull("new_status")));
        }
        return result;
    }
}

/// <summary>Tien ich doc/ghi Row cua tbl_incidents va hinh hoc WKT (SRID 4326, "POINT (lon lat)").</summary>
public static class IncidentRows
{
    public static long Id(Row r) => r.GetLong("id");

    public static IncidentStatus Status(Row r) => (IncidentStatus)r.GetByte("status");

    public static void SetStatus(Row r, IncidentStatus status) => r["status"] = (byte)status;

    public static Priority PriorityOf(Row r) => (Priority)r.GetByte("priority");

    public static IncidentStatus? ToStatus(int? value) => value is { } v ? (IncidentStatus)v : null;

    public static LogActionType? ParseLogType(string? code) =>
        code is not null && Enum.TryParse<LogActionType>(code.Replace("_", ""), true, out var t) ? (LogActionType?)t : null;

    public static Point? ParsePoint(string? wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt)) return null;
        return new WKTReader(GeoJson.Factory).Read(wkt) as Point;
    }

    /// <summary>Vi tri su co: Y = vi do (lat), X = kinh do (lon).</summary>
    public static double Lat(Row r) => ParsePoint(r.GetStringOrNull("location_point_wkt"))?.Y ?? 0;

    public static double Lon(Row r) => ParsePoint(r.GetStringOrNull("location_point_wkt"))?.X ?? 0;

    public static string PointWkt(double latitude, double longitude) =>
        $"POINT ({longitude.ToString("R", CultureInfo.InvariantCulture)} {latitude.ToString("R", CultureInfo.InvariantCulture)})";

    public static System.Text.Json.JsonElement? BoundaryElement(string? wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt)) return null;
        return GeoJson.ToElement(new WKTReader(GeoJson.Factory).Read(wkt));
    }

    /// <summary>Con lai (giay) den han SLA, chi khi su co con mo.</summary>
    public static long? RemainingSeconds(DateTime? deadline, IncidentStatus status, DateTime now) =>
        deadline is { } d && IncidentStateMachine.IsOpen(status) ? (long)(d - now).TotalSeconds : null;

    /// <summary>So sanh duong dan: pham vi null = toan he thong.</summary>
    public static bool PathMatches(string? scopePath, string? unitPath) =>
        scopePath is null || (unitPath is not null && unitPath.StartsWith(scopePath, StringComparison.Ordinal));

    /// <summary>Id don vi tu duong dan '/1/2/5/' -> 5.</summary>
    public static int? UnitIdFromPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var parts = path.Trim('/').Split('/');
        return int.TryParse(parts[^1], out var id) ? id : null;
    }

    public static async Task<string?> UnitPathAsync(AdminUnitsDataAccess units, int? unitId)
    {
        if (unitId is null) return null;
        var row = await units.ReadByIdAsync(unitId.Value);
        return row?.GetStringOrNull("unit_path");
    }

    /// <summary>Duong dan don vi "Tinh > Phuong > Khu pho" (gom ca chinh no).</summary>
    public static async Task<List<UnitRef>> BreadcrumbAsync(AdminUnitsDataAccess units, int? unitId)
    {
        if (unitId is null) return [];
        var rows = await units.ReadAncestorsAsync(unitId.Value);
        return rows.Select(r => new UnitRef(r.GetInt("ancestor_id"), r.GetString("ancestor_code"),
            r.GetString("ancestor_name"), r.GetString("ancestor_level_code"))).ToList();
    }

    /// <summary>Id cua don vi va cac to tien (dung de phat su kien realtime).</summary>
    public static async Task<List<int>> AncestorIdsAsync(AdminUnitsDataAccess units, int? unitId)
    {
        if (unitId is null) return [];
        var rows = await units.ReadAncestorsAsync(unitId.Value);
        return rows.Select(r => r.GetInt("ancestor_id")).ToList();
    }

    /// <summary>Cac cán bộ GRID_OFFICER hop le cho duong dan don vi (scope khop va chua don vi).</summary>
    public static async Task<HashSet<int>> ValidOfficerIdsAsync(UsersDataAccess users, string? unitPath)
    {
        var rows = await users.FindOfficersForUnitAsync(unitPath);
        return rows
            .Where(r => PathMatches(r.GetStringOrNull("scope_path"), unitPath))
            .Select(r => r.GetInt("user_id"))
            .ToHashSet();
    }

    /// <summary>So viec dang mo (ASSIGNED, IN_PROGRESS) cua tung can bo.</summary>
    public static async Task<Dictionary<int, int>> OpenCountsAsync(IncidentsDataAccess incidents, IEnumerable<int> officerIds)
    {
        var ids = officerIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, int>();
        var rows = await incidents.CountOpenByOfficersAsync(string.Join(",", ids));
        return rows.ToDictionary(r => r.GetInt("officer_id"), r => r.GetInt("open_count"));
    }
}

/// <summary>Pham vi truy van: chuoi unit_path (null = toan he thong) va don vi loc them (neu co).</summary>
public sealed record ScopeQuery(string? ScopePaths, string? UnitPath);

public static class IncidentScope
{
    /// <summary>
    /// Doc pham vi cua quyen. Tra null khi nguoi dung khong co quyen nao (service tra ket qua rong, khong goi DB).
    /// Neu co unitId thi kiem tra quyen tren don vi do.
    /// </summary>
    public static async Task<ScopeQuery?> ResolveAsync(IScopeService scope, AdminUnitsDataAccess units, string permission, int? unitId)
    {
        var paths = scope.ScopePaths(permission);
        if (paths == "") return null;
        string? unitPath = null;
        if (unitId is { } uid)
        {
            unitPath = await IncidentRows.UnitPathAsync(units, uid)
                       ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị.");
            scope.EnsureAccess(permission, unitPath, $"admin_unit:{uid}");
        }
        return new ScopeQuery(paths, unitPath);
    }
}

/// <summary>Chuoi hien thi va ma trang thai.</summary>
public static class IncidentText
{
    public static string StatusCode(IncidentStatus s) => s switch
    {
        IncidentStatus.New => "NEW",
        IncidentStatus.Assigned => "ASSIGNED",
        IncidentStatus.InProgress => "IN_PROGRESS",
        IncidentStatus.Resolved => "RESOLVED",
        IncidentStatus.Cancelled => "CANCELLED",
        _ => s.ToString().ToUpperInvariant(),
    };

    public static IncidentStatus ParseStatus(string code) => code.ToUpperInvariant() switch
    {
        "NEW" => IncidentStatus.New,
        "ASSIGNED" => IncidentStatus.Assigned,
        "IN_PROGRESS" => IncidentStatus.InProgress,
        "RESOLVED" => IncidentStatus.Resolved,
        "CANCELLED" => IncidentStatus.Cancelled,
        _ => throw new DomainException(ErrorCodes.ValidationFailed, $"Trạng thái không hợp lệ: {code}"),
    };

    /// <summary>Trang thai hien thi cho cong dan: Da tiep nhan -> Dang xu ly -> Da hoan thanh.</summary>
    public static string PublicStatusText(IncidentStatus s) => s switch
    {
        IncidentStatus.New => "Đã tiếp nhận",
        IncidentStatus.Assigned or IncidentStatus.InProgress => "Đang xử lý",
        IncidentStatus.Resolved => "Đã hoàn thành",
        IncidentStatus.Cancelled => "Đã hủy",
        _ => s.ToString(),
    };

    /// <summary>Nhat ky cong khai voi cau chu chuan hoa.</summary>
    public static string? PublicMessage(LogActionType type) => type switch
    {
        LogActionType.Created => "Hệ thống đã tiếp nhận phản ánh.",
        LogActionType.Dispatched => "Phản ánh đã được chuyển tới cán bộ phụ trách địa bàn.",
        LogActionType.Accepted => "Cán bộ đã nhận việc và đang xử lý tại hiện trường.",
        LogActionType.Resolved => "Sự cố đã được xử lý xong.",
        LogActionType.Cancelled => "Phản ánh đã bị hủy.",
        _ => null,
    };

    /// <summary>Ly do nam trong hang cho duyet, suy tu trang thai va nhat ky gan nhat.</summary>
    public static string? ReviewReason(bool isManualReview, int? gridId, int? categoryId, decimal? aiConfidence, double threshold,
        LogActionType? lastReviewLogType, IncidentStatus? lastReviewLogNewStatus)
    {
        if (!isManualReview) return null;
        Models.Enums.ReviewReason reason;
        if (lastReviewLogType == LogActionType.PipelineError) reason = Models.Enums.ReviewReason.PipelineError;
        else if (lastReviewLogType == LogActionType.Reassigned && lastReviewLogNewStatus == IncidentStatus.New) reason = Models.Enums.ReviewReason.Returned;
        else if (gridId is null) reason = Models.Enums.ReviewReason.NoGrid;
        else if (categoryId is null || aiConfidence is null || (double)aiConfidence < threshold) reason = Models.Enums.ReviewReason.AiLowConfidence;
        else reason = Models.Enums.ReviewReason.NoOfficer;
        return EnumText.UpperSnake(reason);
    }
}
