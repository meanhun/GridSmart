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

/// <summary>Cac thao tac doi trang thai su co cua can bo va dieu phoi vien (BE-18, BE-19).</summary>
public sealed class IncidentWorkflowService(
    IncidentsDataAccess incidents,
    IncidentImagesDataAccess incidentImages,
    IncidentLogsDataAccess incidentLogs,
    IncidentRatingsDataAccess ratings,
    NotificationsDataAccess notificationRows,
    CategoriesDataAccess categories,
    GridsDataAccess grids,
    AdminUnitsDataAccess adminUnits,
    UsersDataAccess users,
    ICurrentUser currentUser,
    IScopeService scope,
    IImageStore imageStore,
    ISpatialRepository spatial,
    IDispatchService dispatch,
    INotificationService notifications,
    IncidentEvents events,
    IOptions<ResolveOptions> resolveOptions,
    TimeProvider clock) : IIncidentWorkflowService
{
    public async Task AcceptAsync(long id, CancellationToken ct)
    {
        var (incident, _) = await LoadAsync(id, Permissions.IncidentAccept);
        EnsureAssignedOfficer(incident);
        var now = Now();
        var old = IncidentRows.Status(incident);
        if (old != IncidentStatus.Assigned)
            throw DomainException.Conflict(ErrorCodes.InvalidStatusTransition, "Chỉ nhận việc khi sự cố ở trạng thái Đã giao.");

        IncidentRows.SetStatus(incident, IncidentStatus.InProgress);
        incident["accepted_at"] = now;
        incident["updated_at"] = now;

        await using (var tx = await ProcTransaction.BeginAsync())
        {
            await incidents.UpdateAsync(incident);
            await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Accepted, "Cán bộ nhận việc.", now,
                currentUser.UserId, old, IncidentStatus.InProgress);
            await tx.CommitAsync();
        }
        await events.UpdatedAsync(incident, ct);
    }

    public async Task ResolveAsync(long id, ResolveCommand command, CancellationToken ct)
    {
        var (incident, _) = await LoadAsync(id, Permissions.IncidentResolve);
        EnsureAssignedOfficer(incident);
        if (IncidentRows.Status(incident) != IncidentStatus.InProgress)
            throw DomainException.Conflict(ErrorCodes.InvalidStatusTransition, "Cần nhận việc trước khi báo đã xử lý.");

        var stored = await imageStore.SaveAsync(command.Image, command.ImageLength, ct);
        var lat = command.Latitude ?? stored.ExifLatitude;
        var lon = command.Longitude ?? stored.ExifLongitude;
        if (lat is null || lon is null)
            throw new DomainException(ErrorCodes.LocationRequired, "Cần vị trí GPS khi chụp ảnh nghiệm thu.");
        GeoJson.EnsureCoordinates(lat.Value, lon.Value);

        var tolerance = resolveOptions.Value.AfterPhotoToleranceMeters;
        var gridId = incident.GetIntOrNull("grid_id");
        bool inside;
        if (gridId is not null)
        {
            inside = await spatial.IsPointInIncidentGridAsync(id, lat.Value, lon.Value, tolerance, ct) ?? false;
        }
        else
        {
            inside = GeoJson.DistanceMeters(IncidentRows.Lat(incident), IncidentRows.Lon(incident), lat.Value, lon.Value) <= tolerance;
        }
        if (!inside)
            throw DomainException.Unprocessable(ErrorCodes.AfterPhotoOutOfGrid,
                "Ảnh nghiệm thu phải chụp tại hiện trường. Vị trí hiện tại cách ô lưới quá xa.");

        var now = Now();
        var old = IncidentRows.Status(incident);
        IncidentRows.SetStatus(incident, IncidentStatus.Resolved);
        incident["resolved_at"] = now;
        incident["updated_at"] = now;
        var note = string.IsNullOrWhiteSpace(command.Note) ? "Đã xử lý xong, có ảnh nghiệm thu." : $"Đã xử lý xong: {command.Note.Trim()}";
        var code = incident.GetString("tracking_code");

        List<Row> children;
        await using (var tx = await ProcTransaction.BeginAsync())
        {
            await incidentImages.InsertAsync(new Row
            {
                ["incident_id"] = id,
                ["image_type"] = ImageTypes.After,
                ["image_url"] = stored.Url,
                ["capture_point_wkt"] = IncidentRows.PointWkt(lat.Value, lon.Value),
                ["uploaded_by_user_id"] = currentUser.UserId,
                ["uploaded_at"] = now,
            });
            await incidents.UpdateAsync(incident);
            await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Resolved, note, now,
                currentUser.UserId, old, IncidentStatus.Resolved);
            children = await CloseChildrenAsync(incident, IncidentStatus.Resolved, LogActionType.Resolved,
                $"Đóng theo phản ánh gốc {code}.", now);
            await tx.CommitAsync();
        }

        await events.PublishAsync(incident, RealtimeEvents.IncidentResolved,
            new { id, trackingCode = code, resolvedAt = now, gridId }, ct);
        foreach (var c in children) await events.UpdatedAsync(c, ct);
        await events.GridStatusChangedAsync(gridId, ct);
    }

    public async Task ReturnAsync(long id, string reason, CancellationToken ct)
    {
        RequireReason(reason);
        var (incident, _) = await LoadAsync(id, Permissions.IncidentAccept);
        EnsureAssignedOfficer(incident);
        var old = IncidentRows.Status(incident);
        IncidentStateMachine.EnsureTransition(old, IncidentStatus.New);

        var now = Now();
        IncidentRows.SetStatus(incident, IncidentStatus.New);
        incident["assigned_officer_id"] = null;
        incident["accepted_at"] = null;
        incident["is_manual_review"] = true;
        incident["updated_at"] = now;

        await using (var tx = await ProcTransaction.BeginAsync())
        {
            await incidents.UpdateAsync(incident);
            await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Reassigned,
                $"Cán bộ trả lại: {reason.Trim()}", now, currentUser.UserId, old, IncidentStatus.New);
            await tx.CommitAsync();
        }

        await dispatch.NotifyManualReviewAsync(incident, $"Cán bộ trả lại sự cố: {reason.Trim()}", ct);
        await events.UpdatedAsync(incident, ct);
        await events.GridStatusChangedAsync(incident.GetIntOrNull("grid_id"), ct);
    }

    public async Task ClassifyAsync(long id, ClassifyRequest request, CancellationToken ct)
    {
        var (incident, _) = await LoadAsync(id, Permissions.IncidentClassify);
        EnsureOpen(incident);
        var category = await categories.ReadByIdAsync(request.CategoryId);
        if (category is null || !category.GetBool("is_active"))
            throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy danh mục.");
        var priority = request.Priority is { } p
            ? (Enum.IsDefined(typeof(Priority), (byte)p) ? (byte)p : throw new DomainException(ErrorCodes.ValidationFailed, "Mức ưu tiên phải từ 1 đến 3."))
            : category.GetByte("default_priority");

        var now = Now();
        var categoryId = category.GetInt("id");
        var categoryName = category.GetString("category_name");
        incident["category_id"] = categoryId;
        incident["priority"] = priority;
        incident["updated_at"] = now;
        var note = $"Phân loại thủ công: {categoryName}, ưu tiên {priority}.";

        // Da du dieu kien thi bo co cho duyet va chay dieu phoi (luong 8)
        var autoDispatch = IncidentRows.Status(incident) == IncidentStatus.New
                           && incident.GetIntOrNull("grid_id") is not null
                           && incident.GetLongOrNull("parent_incident_id") is null;
        if (autoDispatch) incident["is_manual_review"] = false;

        await using (var tx = await ProcTransaction.BeginAsync())
        {
            await incidents.UpdateAsync(incident);
            await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Classified, note, now, currentUser.UserId);
            await tx.CommitAsync();
        }

        if (autoDispatch)
        {
            await dispatch.DispatchAsync(incident, null, currentUser.UserId, ct);
            return;
        }
        await events.UpdatedAsync(incident, ct);
    }

    public async Task DispatchAsync(long id, DispatchRequest request, CancellationToken ct)
    {
        var (incident, _) = await LoadAsync(id, Permissions.IncidentDispatch);
        if (IncidentRows.Status(incident) != IncidentStatus.New)
            throw DomainException.Conflict(ErrorCodes.InvalidStatusTransition, "Chỉ điều phối sự cố đang chờ giao. Dùng chức năng giao lại cho sự cố đã giao.");
        if (incident.GetLongOrNull("parent_incident_id") is not null)
            throw DomainException.Conflict(ErrorCodes.Conflict, "Phản ánh này đã gộp vào phản ánh khác, hãy điều phối phản ánh gốc.");
        if (request.GridId is null && request.OfficerId is null)
            throw new DomainException(ErrorCodes.ValidationFailed, "Cần chọn ô lưới hoặc cán bộ.");

        var now = Now();
        var oldGridId = incident.GetIntOrNull("grid_id");
        int? newGridId = null;
        int? newAdminUnitId = null;
        string? gridCode = null;
        if (request.GridId is { } gridId)
        {
            var grid = await grids.ReadDetailAsync(gridId);
            if (grid is null || !grid.GetBool("is_active"))
                throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy ô lưới.");
            scope.EnsureAccess(Permissions.IncidentDispatch, grid.GetStringOrNull("unit_path"), $"grid:{gridId}");
            newGridId = grid.GetInt("id");
            newAdminUnitId = grid.GetInt("admin_unit_id");
            gridCode = grid.GetString("grid_code");
        }
        if (incident.GetIntOrNull("category_id") is null && incident.GetIntOrNull("category_id") is null)
            throw DomainException.Unprocessable(ErrorCodes.ValidationFailed, "Cần phân loại sự cố trước khi điều phối.");

        await using (var tx = await ProcTransaction.BeginAsync())
        {
            if (newGridId is { } ng)
            {
                incident["grid_id"] = ng;
                incident["admin_unit_id"] = newAdminUnitId;
                await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.GridMatched,
                    $"Điều phối viên chọn ô lưới {gridCode}.", now, currentUser.UserId);
            }
            incident["is_manual_review"] = false;
            incident["updated_at"] = now;
            await incidents.UpdateAsync(incident);
            await tx.CommitAsync();
        }

        if (newGridId is not null && oldGridId != newGridId) await events.GridStatusChangedAsync(oldGridId, ct);

        var ok = await dispatch.DispatchAsync(incident, request.OfficerId, currentUser.UserId, ct);
        if (!ok)
            throw DomainException.Unprocessable(ErrorCodes.ValidationFailed, "Ô lưới chưa có cán bộ hợp lệ, hãy chọn cán bộ cụ thể.");
    }

    public async Task ReassignAsync(long id, ReassignRequest request, CancellationToken ct)
    {
        RequireReason(request.Reason);
        var (incident, path) = await LoadAsync(id, Permissions.IncidentReassign);
        var old = IncidentRows.Status(incident);
        if (old is not (IncidentStatus.Assigned or IncidentStatus.InProgress))
            throw DomainException.Conflict(ErrorCodes.InvalidStatusTransition, "Chỉ giao lại sự cố đã giao hoặc đang xử lý.");

        var valid = await IncidentRows.ValidOfficerIdsAsync(users, path);
        if (!valid.Contains(request.OfficerId))
            throw DomainException.Unprocessable(ErrorCodes.OutOfScope, "Cán bộ được chọn không phụ trách địa bàn của sự cố.");
        var officer = await users.ReadByIdAsync(request.OfficerId)
                      ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy cán bộ.");
        var officerName = officer.GetString("full_name");

        var now = Now();
        var reason = request.Reason.Trim();
        IncidentRows.SetStatus(incident, IncidentStatus.Assigned);
        incident["assigned_officer_id"] = request.OfficerId;
        incident["accepted_at"] = null;
        incident["updated_at"] = now;

        await using (var tx = await ProcTransaction.BeginAsync())
        {
            await incidents.UpdateAsync(incident);
            await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Reassigned,
                $"Giao lại cho cán bộ {officerName}. Lý do: {reason}", now, currentUser.UserId, old, IncidentStatus.Assigned);
            await tx.CommitAsync();
        }

        var trackingCode = incident.GetString("tracking_code");
        var gridId = incident.GetIntOrNull("grid_id");
        await notifications.NotifyAsync([request.OfficerId], NotificationType.NewAssignment,
            $"Việc được giao lại: {trackingCode}", reason, id, ct);
        await events.PublishAsync(incident, RealtimeEvents.IncidentAssigned,
            new { id, trackingCode, officerId = request.OfficerId, slaDeadline = incident.GetDateTimeOrNull("sla_deadline"), gridId }, ct);
        await events.UpdatedAsync(incident, ct);
    }

    public async Task CancelAsync(long id, string reason, CancellationToken ct)
    {
        RequireReason(reason);
        var (incident, _) = await LoadAsync(id, Permissions.IncidentCancel);
        var old = IncidentRows.Status(incident);
        IncidentStateMachine.EnsureTransition(old, IncidentStatus.Cancelled);

        var now = Now();
        var code = incident.GetString("tracking_code");
        IncidentRows.SetStatus(incident, IncidentStatus.Cancelled);
        incident["is_manual_review"] = false;
        incident["updated_at"] = now;

        List<Row> children = [];
        await using (var tx = await ProcTransaction.BeginAsync())
        {
            await incidents.UpdateAsync(incident);
            await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Cancelled, $"Hủy: {reason.Trim()}", now,
                currentUser.UserId, old, IncidentStatus.Cancelled);
            children = await CloseChildrenAsync(incident, IncidentStatus.Cancelled, LogActionType.Cancelled,
                $"Hủy theo phản ánh gốc {code}.", now);
            await tx.CommitAsync();
        }

        await events.UpdatedAsync(incident, ct);
        foreach (var c in children) await events.UpdatedAsync(c, ct);
        await events.GridStatusChangedAsync(incident.GetIntOrNull("grid_id"), ct);
    }

    /// <summary>
    /// Xoa han phan anh (khong can ly do). Xoa ca thong bao, nhat ky, anh, danh gia va cac phan anh trung
    /// da gop vao no (ticket con). Thao tac trong mot giao dich. Anh tren dia khong bi xoa.
    /// </summary>
    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var (incident, _) = await LoadAsync(id, Permissions.IncidentDelete);
        var children = await incidents.ReadChildrenAsync(id);
        var targets = new List<long> { id };
        targets.AddRange(children.Select(IncidentRows.Id));

        await using (var tx = await ProcTransaction.BeginAsync())
        {
            foreach (var targetId in targets)
            {
                foreach (var row in await notificationRows.ReadByIncidentAsync(targetId)) await notificationRows.DeleteAsync(row.GetLong("id"));
                foreach (var row in await incidentLogs.ReadByIncidentAsync(targetId)) await incidentLogs.DeleteAsync(row.GetLong("id"));
                foreach (var row in await incidentImages.ReadByIncidentAsync(targetId)) await incidentImages.DeleteAsync(row.GetLong("id"));
                foreach (var row in await ratings.ReadByIncidentAsync(targetId)) await ratings.DeleteAsync(row.GetLong("id"));
            }
            // Ticket con tham chieu phan anh goc qua parent_incident_id nen xoa truoc, phan anh goc xoa sau.
            foreach (var child in children) await incidents.DeleteAsync(IncidentRows.Id(child));
            await incidents.DeleteAsync(id);
            await tx.CommitAsync();
        }

        await events.GridStatusChangedAsync(incident.GetIntOrNull("grid_id"), ct);
    }

    /// <summary>Dong cac phan anh con dang mo cua mot su co (chay trong ProcTransaction cua nguoi goi).</summary>
    private async Task<List<Row>> CloseChildrenAsync(Row parent, IncidentStatus target, LogActionType type, string note, DateTime now)
    {
        var children = await incidents.ReadOpenChildrenAsync(IncidentRows.Id(parent));
        foreach (var c in children)
        {
            var old = IncidentRows.Status(c);
            IncidentRows.SetStatus(c, target);
            if (target == IncidentStatus.Resolved) c["resolved_at"] = now;
            c["is_manual_review"] = false;
            c["updated_at"] = now;
            await incidents.UpdateAsync(c);
            await IncidentLogWriter.AddAsync(incidentLogs, IncidentRows.Id(c), type, note, now, currentUser.UserId, old, target);
        }
        return children;
    }

    /// <summary>Nap su co va kiem tra lop 2 (pham vi) cho quyen.</summary>
    private async Task<(Row Incident, string? UnitPath)> LoadAsync(long id, string permission)
    {
        var incident = await incidents.ReadByIdAsync(id)
                       ?? throw DomainException.NotFound(ErrorCodes.IncidentNotFound, "Không tìm thấy sự cố.");
        var path = await IncidentRows.UnitPathAsync(adminUnits, incident.GetIntOrNull("admin_unit_id"));
        scope.EnsureAccess(permission, path, $"incident:{id}");
        return (incident, path);
    }

    private void EnsureAssignedOfficer(Row incident)
    {
        if (incident.GetIntOrNull("assigned_officer_id") != currentUser.UserId)
            throw DomainException.Forbidden(ErrorCodes.NotAssignedOfficer, "Bạn không phải cán bộ được giao xử lý sự cố này.");
    }

    private static void EnsureOpen(Row incident)
    {
        if (!IncidentStateMachine.IsOpen(IncidentRows.Status(incident)))
            throw DomainException.Conflict(ErrorCodes.InvalidStatusTransition, "Sự cố đã đóng.");
    }

    private static void RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException(ErrorCodes.ValidationFailed, "Cần nhập lý do.");
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
