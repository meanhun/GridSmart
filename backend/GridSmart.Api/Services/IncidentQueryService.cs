using GridSmart.Api.Common;
using GridSmart.Api.Constants;
using GridSmart.Api.Data;
using GridSmart.Api.DataAccess;
using GridSmart.Api.Dtos;
using GridSmart.Api.Exceptions;
using GridSmart.Api.Interfaces;
using GridSmart.Api.Models.Enums;
using Microsoft.Extensions.Options;

namespace GridSmart.Api.Services;

/// <summary>Truy van su co cho can bo, dieu phoi vien, lanh dao (BE-17).</summary>
public sealed class IncidentQueryService(
    IncidentsDataAccess incidents,
    IncidentImagesDataAccess incidentImages,
    IncidentLogsDataAccess incidentLogs,
    IncidentRatingsDataAccess ratings,
    AdminUnitsDataAccess adminUnits,
    GridsDataAccess grids,
    UsersDataAccess users,
    GridOfficersDataAccess gridOfficers,
    ICurrentUser currentUser,
    IScopeService scope,
    IOptions<AiOptions> aiOptions,
    TimeProvider clock) : IIncidentQueryService
{
    public async Task<PagedResult<IncidentListItem>> ListAsync(IncidentListQuery query, CancellationToken ct)
    {
        var (page, size) = Paging.Normalize(query.Page, query.PageSize);
        var scopePaths = scope.ScopePaths(Permissions.IncidentView);
        if (scopePaths == "") return new PagedResult<IncidentListItem>([], page, size, 0);

        string? statusList = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            statusList = string.Join(",", query.Status
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => (int)IncidentText.ParseStatus(s)));
        }

        string? unitPath = null;
        if (query.UnitId is { } unitId)
        {
            unitPath = await IncidentRows.UnitPathAsync(adminUnits, unitId)
                       ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị.");
        }

        var filter = new IncidentListFilter(
            PageIndex: page,
            PageSize: size,
            ScopePaths: scopePaths,
            StatusList: statusList,
            UnitPath: unitPath,
            GridId: query.GridId,
            CategoryId: query.CategoryId,
            Priority: query.Priority is { } p ? (byte?)(byte)p : null,
            IsOverdue: query.IsOverdue,
            IsManualReview: query.IsManualReview,
            AssignedOfficerId: query.AssignedToMe == true ? currentUser.UserId : null,
            FromUtc: query.From,
            ToUtc: query.To,
            Search: string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim(),
            IncludeChildren: query.IncludeChildren == true);

        var (rows, total) = await incidents.ListAsync(filter);
        var items = await ToListItemsAsync(rows);
        // Hang cho duyet: cu nhat truoc (chi sap xep trong trang hien tai)
        if (query.IsManualReview == true) items = items.OrderBy(i => i.CreatedAt).ToList();
        return new PagedResult<IncidentListItem>(items, page, size, total);
    }

    public async Task<IncidentDetail> GetAsync(long id, CancellationToken ct)
    {
        var i = await incidents.ReadDetailAsync(id)
                ?? throw DomainException.NotFound(ErrorCodes.IncidentNotFound, "Không tìm thấy sự cố.");
        EnsureView(i);

        var now = Now();
        var lastReview = await IncidentLogQueries.LastReviewAsync(incidentLogs, [id]);
        var breadcrumb = await IncidentRows.BreadcrumbAsync(adminUnits, i.GetIntOrNull("admin_unit_id"));
        var images = await incidentImages.ReadByIncidentAsync(id);
        var rating = (await ratings.ReadByIncidentAsync(id)).FirstOrDefault();
        lastReview.TryGetValue(id, out var lr);

        var status = IncidentRows.Status(i);
        var isManualReview = i.GetBool("is_manual_review");
        var categoryId = i.GetIntOrNull("category_id");
        var aiConfidence = i.GetDecimalOrNull("ai_confidence");
        var point = IncidentRows.ParsePoint(i.GetStringOrNull("location_point_wkt"));
        var slaDeadline = i.GetDateTimeOrNull("sla_deadline");

        return new IncidentDetail(
            IncidentRows.Id(i), i.GetString("tracking_code"), IncidentText.StatusCode(status), i.GetInt("priority"),
            categoryId, i.GetStringOrNull("category_name"),
            i.GetStringOrNull("description"), i.GetStringOrNull("ai_label"), aiConfidence, isManualReview,
            IncidentText.ReviewReason(isManualReview, i.GetIntOrNull("grid_id"), categoryId, aiConfidence,
                aiOptions.Value.ConfidenceThreshold, lr.Type, lr.NewStatus),
            point?.Y ?? 0, point?.X ?? 0, i.GetStringOrNull("address_text"),
            i.GetIntOrNull("grid_id"), i.GetStringOrNull("grid_code"), i.GetStringOrNull("grid_name"),
            IncidentRows.BoundaryElement(i.GetStringOrNull("grid_boundary_wkt")),
            i.GetIntOrNull("admin_unit_id"), breadcrumb,
            i.GetIntOrNull("assigned_officer_id"), i.GetStringOrNull("officer_name"),
            i.GetStringOrNull("reporter_phone"),
            i.GetIntOrNull("sla_policy_id"), slaDeadline, IncidentRows.RemainingSeconds(slaDeadline, status, now),
            i.GetDateTimeOrNull("accepted_at"),
            i.GetBool("is_overdue"), i.GetDateTimeOrNull("escalated_at"), i.GetDateTimeOrNull("resolved_at"),
            i.GetLongOrNull("parent_incident_id"), i.GetStringOrNull("parent_tracking_code"),
            i.GetInt("duplicate_count"), i.GetInt("child_count"),
            i.GetDateTime("created_at"), i.GetDateTimeOrNull("updated_at"), i.GetDateTimeOrNull("processed_at"),
            images.Select(ImageUrls.ToDto).ToList(),
            rating?.GetIntOrNull("rating"), rating?.GetStringOrNull("feedback"));
    }

    public async Task<IReadOnlyList<IncidentLogDto>> GetLogsAsync(long id, CancellationToken ct)
    {
        await EnsureViewByIdAsync(id);
        var rows = await incidentLogs.ReadByIncidentAsync(id);
        return rows.Select(l => new IncidentLogDto(
            l.GetLong("id"),
            l.GetStringOrNull("action_type") ?? string.Empty,
            l.GetStringOrNull("action_note") ?? string.Empty,
            l.GetIntOrNull("old_status") is { } o ? IncidentText.StatusCode((IncidentStatus)o) : null,
            l.GetIntOrNull("new_status") is { } n ? IncidentText.StatusCode((IncidentStatus)n) : null,
            l.GetIntOrNull("action_by_user_id"),
            l.GetStringOrNull("action_by_name"),
            l.GetDateTime("created_at")))
            .ToList();
    }

    public async Task<IReadOnlyList<IncidentListItem>> GetChildrenAsync(long id, CancellationToken ct)
    {
        await EnsureViewByIdAsync(id);
        var rows = await incidents.ReadChildrenAsync(id);
        return await ToListItemsAsync(rows);
    }

    public async Task<IReadOnlyList<IncidentListItem>> GetMyAssignmentsAsync(string? tab, CancellationToken ct)
    {
        var me = currentUser.UserId;
        if (me is null) return [];

        var statusList = (tab ?? "").ToLowerInvariant() switch
        {
            "assigned" => "1",
            "in_progress" => "2",
            "done" => "3",
            _ => "1,2",
        };
        var doneMode = string.Equals(tab, "done", StringComparison.OrdinalIgnoreCase);
        var since = Now().AddDays(-7);

        var result = await incidents.ListAsync(new IncidentListFilter(
            PageIndex: 1, PageSize: 200, ScopePaths: null, StatusList: statusList, UnitPath: null,
            GridId: null, CategoryId: null, Priority: null, IsOverdue: null, IsManualReview: null,
            AssignedOfficerId: me, FromUtc: null, ToUtc: null, Search: null, IncludeChildren: false));

        // Sap xep theo han SLA (khong co han thi xuong cuoi). Loc "done" theo resolved_at trong C#.
        var rows = result.Items
            .Where(r => !doneMode || (r.GetDateTimeOrNull("resolved_at") is { } resolvedAt && resolvedAt >= since))
            .OrderBy(r => r.GetDateTimeOrNull("sla_deadline") is null)
            .ThenBy(r => r.GetDateTimeOrNull("sla_deadline"))
            .Take(200)
            .ToList();
        return await ToListItemsAsync(rows);
    }

    public async Task<IReadOnlyList<OfficerDto>> ListOfficersAsync(int? unitId, int? gridId, CancellationToken ct)
    {
        if (!currentUser.HasPermission(Permissions.IncidentDispatch) && !currentUser.HasPermission(Permissions.IncidentReassign)
            && !currentUser.HasPermission(Permissions.GridAssignOfficer))
            throw DomainException.Forbidden(ErrorCodes.OutOfScope, "Bạn không có quyền xem danh sách cán bộ.");

        string? targetPath = null;
        if (gridId is { } g)
        {
            var grid = await grids.ReadDetailAsync(g)
                       ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy ô lưới.");
            targetPath = grid.GetStringOrNull("unit_path")
                         ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy ô lưới.");
        }
        else if (unitId is { } u)
        {
            targetPath = await IncidentRows.UnitPathAsync(adminUnits, u)
                         ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị.");
        }

        var canSee = scope.CanAccess(Permissions.IncidentDispatch, targetPath) || scope.CanAccess(Permissions.IncidentReassign, targetPath)
                     || scope.CanAccess(Permissions.GridAssignOfficer, targetPath);
        if (targetPath is not null && !canSee)
            throw DomainException.Forbidden(ErrorCodes.OutOfScope, "Đơn vị nằm ngoài phạm vi quản lý của bạn.");

        // Lay moi can bo GRID_OFFICER roi loc theo ca hai chieu: pham vi can bo khop dia ban dich,
        // hoac nam trong dia ban dich; va dia ban do phai nam trong pham vi nguoi xem.
        var rows = await users.FindOfficersForUnitAsync(null);
        var filtered = rows.Where(r =>
            {
                var path = r.GetStringOrNull("scope_path");
                return (targetPath is null || IncidentRows.PathMatches(path, targetPath)
                        || (path is not null && path.StartsWith(targetPath, StringComparison.Ordinal)))
                       && (scope.CanAccess(Permissions.IncidentDispatch, path) || scope.CanAccess(Permissions.IncidentReassign, path)
                           || scope.CanAccess(Permissions.GridAssignOfficer, path));
            })
            .GroupBy(r => r.GetInt("user_id"))
            .Select(gr => gr.First())
            .ToList();

        var ids = filtered.Select(f => f.GetInt("user_id")).ToList();
        var counts = await IncidentRows.OpenCountsAsync(incidents, ids);
        var gridRows = await gridOfficers.ReadAllAsync();
        var gridsByUser = gridRows
            .GroupBy(r => r.GetInt("user_id"))
            .ToDictionary(gr => gr.Key, gr => gr.Select(x => x.GetInt("grid_id")).ToList());

        return filtered
            .Select(f =>
            {
                var userId = f.GetInt("user_id");
                return new OfficerDto(userId, f.GetString("full_name"), f.GetStringOrNull("phone"),
                    counts.GetValueOrDefault(userId),
                    gridsByUser.TryGetValue(userId, out var gl) ? gl : new List<int>());
            })
            .OrderBy(o => o.OpenIncidentCount).ThenBy(o => o.FullName)
            .ToList();
    }

    /// <summary>Su co duoc xem khi la can bo duoc giao, hoac co quyen incident.view tren don vi cua su co.</summary>
    private void EnsureView(Row i)
    {
        var me = currentUser.UserId;
        if (me is not null && i.GetIntOrNull("assigned_officer_id") == me) return;
        scope.EnsureAccess(Permissions.IncidentView, i.GetStringOrNull("unit_path"), $"incident:{IncidentRows.Id(i)}");
    }

    private async Task EnsureViewByIdAsync(long id)
    {
        var i = await incidents.ReadDetailAsync(id)
                ?? throw DomainException.NotFound(ErrorCodes.IncidentNotFound, "Không tìm thấy sự cố.");
        EnsureView(i);
    }

    /// <summary>Chuyen cac dong danh sach (cung cot voi SP_tbl_incidents_List) thanh DTO.</summary>
    private async Task<List<IncidentListItem>> ToListItemsAsync(IReadOnlyList<Row> rows)
    {
        if (rows.Count == 0) return new List<IncidentListItem>();

        var reviewIds = rows.Where(r => r.GetBool("is_manual_review")).Select(IncidentRows.Id).ToList();
        var lastReview = await IncidentLogQueries.LastReviewAsync(incidentLogs, reviewIds);
        var threshold = aiOptions.Value.ConfidenceThreshold;
        var now = Now();

        return rows.Select(r =>
        {
            var id = IncidentRows.Id(r);
            lastReview.TryGetValue(id, out var lr);
            var status = IncidentRows.Status(r);
            var deadline = r.GetDateTimeOrNull("sla_deadline");
            var point = IncidentRows.ParsePoint(r.GetStringOrNull("location_point_wkt"));
            var thumb = r.GetStringOrNull("thumbnail_url");
            var categoryId = r.GetIntOrNull("category_id");
            var gridId = r.GetIntOrNull("grid_id");
            var aiConfidence = r.GetDecimalOrNull("ai_confidence");
            var isManualReview = r.GetBool("is_manual_review");

            return new IncidentListItem(
                id, r.GetString("tracking_code"), IncidentText.StatusCode(status), r.GetInt("priority"),
                r.GetStringOrNull("category_name"), categoryId,
                r.GetStringOrNull("address_text"), point?.Y ?? 0, point?.X ?? 0,
                gridId, r.GetStringOrNull("grid_code"), r.GetIntOrNull("admin_unit_id"), r.GetStringOrNull("unit_name"),
                r.GetIntOrNull("assigned_officer_id"), r.GetStringOrNull("officer_name"),
                deadline, IncidentRows.RemainingSeconds(deadline, status, now),
                r.GetBool("is_overdue"), isManualReview,
                IncidentText.ReviewReason(isManualReview, gridId, categoryId, aiConfidence, threshold, lr.Type, lr.NewStatus),
                r.GetInt("duplicate_count"), r.GetLongOrNull("parent_incident_id"), r.GetDateTime("created_at"),
                thumb is null ? null : ImageUrls.Thumbnail(thumb));
        }).ToList();
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
