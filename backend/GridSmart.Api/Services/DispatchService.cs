using GridSmart.Api.Common;
using GridSmart.Api.Constants;
using GridSmart.Api.Data;
using GridSmart.Api.DataAccess;
using GridSmart.Api.Dtos;
using GridSmart.Api.Exceptions;
using GridSmart.Api.Interfaces;
using GridSmart.Api.Models.Enums;

namespace GridSmart.Api.Services;

/// <summary>Dieu phoi su co cho can bo va dat han SLA (UC-06, BE-16).</summary>
public sealed class DispatchService(
    IncidentsDataAccess incidents,
    AdminUnitsDataAccess adminUnits,
    GridsDataAccess grids,
    GridOfficersDataAccess gridOfficers,
    UsersDataAccess users,
    SlaPoliciesDataAccess slaPolicies,
    IncidentLogsDataAccess incidentLogs,
    INotificationService notifications,
    IncidentEvents events,
    TimeProvider clock) : IDispatchService
{
    public async Task<bool> DispatchAsync(Row incident, int? officerId, int? actorUserId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var incidentId = IncidentRows.Id(incident);
        var unitPath = await IncidentRows.UnitPathAsync(adminUnits, incident.GetIntOrNull("admin_unit_id"));

        var chosen = officerId ?? await ChooseOfficerAsync(incident, unitPath, ct);
        if (chosen is null)
        {
            incident["is_manual_review"] = true;
            incident["updated_at"] = now;
            await incidents.UpdateAsync(incident);
            await NotifyManualReviewAsync(incident, "Ô lưới chưa có cán bộ phụ trách hợp lệ.", ct);
            await events.UpdatedAsync(incident, ct);
            return false;
        }

        if (officerId is not null)
        {
            var valid = await IncidentRows.ValidOfficerIdsAsync(users, unitPath);
            if (!valid.Contains(officerId.Value))
                throw DomainException.Unprocessable(ErrorCodes.OutOfScope, "Cán bộ được chọn không phụ trách địa bàn của sự cố.");
        }

        var priority = (int)IncidentRows.PriorityOf(incident);
        var policy = (await slaPolicies.ReadAllAsync()).FirstOrDefault(p => p.GetInt("priority") == priority)
                     ?? throw new InvalidOperationException($"Thiếu chính sách SLA cho mức ưu tiên {priority}.");
        var officer = await users.ReadByIdAsync(chosen.Value)
                      ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy cán bộ.");
        var officerUserId = chosen.Value;
        var officerName = officer.GetString("full_name");
        var slaHours = policy.GetInt("sla_hours");

        var old = IncidentRows.Status(incident);
        IncidentStateMachine.EnsureTransition(old, IncidentStatus.Assigned);
        IncidentRows.SetStatus(incident, IncidentStatus.Assigned);
        incident["assigned_officer_id"] = officerUserId;
        incident["accepted_at"] = null;
        incident["is_manual_review"] = false;
        incident["sla_policy_id"] = policy.GetInt("id");
        if (incident.GetDateTimeOrNull("sla_deadline") is null)
            incident["sla_deadline"] = SlaRules.ComputeDeadline(now, slaHours);
        incident["updated_at"] = now;

        var note = actorUserId is null
            ? $"Tự động giao cho cán bộ {officerName}. Hạn xử lý {slaHours} giờ."
            : $"Điều phối viên giao cho cán bộ {officerName}. Hạn xử lý {slaHours} giờ.";

        await using (var tx = await ProcTransaction.BeginAsync())
        {
            await incidents.UpdateAsync(incident);
            await IncidentLogWriter.AddAsync(incidentLogs, incidentId, LogActionType.Dispatched, note, now, actorUserId, old, IncidentStatus.Assigned);
            await tx.CommitAsync();
        }

        var trackingCode = incident.GetString("tracking_code");
        var gridId = incident.GetIntOrNull("grid_id");
        await notifications.NotifyAsync([officerUserId], NotificationType.NewAssignment,
            $"Việc mới: {trackingCode}", incident.GetStringOrNull("address_text") ?? incident.GetStringOrNull("description"), incidentId, ct);
        await events.PublishAsync(incident, RealtimeEvents.IncidentAssigned,
            new { id = incidentId, trackingCode, officerId = officerUserId, slaDeadline = incident.GetDateTimeOrNull("sla_deadline"), gridId }, ct);
        await events.UpdatedAsync(incident, ct);
        await events.GridStatusChangedAsync(gridId, ct);
        return true;
    }

    public async Task NotifyManualReviewAsync(Row incident, string reason, CancellationToken ct)
    {
        var receivers = await notifications.FindUsersWithPermissionOverUnitAsync(
            Permissions.IncidentDispatch, incident.GetIntOrNull("admin_unit_id"), ct);
        await notifications.NotifyAsync(receivers, NotificationType.ManualReview,
            $"Cần duyệt: {incident.GetString("tracking_code")}", reason, IncidentRows.Id(incident), ct);
    }

    /// <summary>Can bo chinh cua o luoi neu hop le, neu khong thi can bo it viec nhat trong tbl_grid_officers.</summary>
    private async Task<int?> ChooseOfficerAsync(Row incident, string? unitPath, CancellationToken ct)
    {
        var gridId = incident.GetIntOrNull("grid_id");
        if (gridId is null) return null;
        var grid = await grids.ReadByIdAsync(gridId.Value)
                   ?? throw new InvalidOperationException("Không tìm thấy ô lưới của sự cố.");

        var valid = await IncidentRows.ValidOfficerIdsAsync(users, unitPath);
        if (grid.GetIntOrNull("assigned_officer_id") is { } primary && valid.Contains(primary)) return primary;

        var candidates = (await gridOfficers.ReadByGridAsync(gridId.Value))
            .Select(r => r.GetInt("user_id"))
            .Distinct()
            .Where(o => valid.Contains(o))
            .ToList();
        if (candidates.Count == 0) return null;

        var counts = await IncidentRows.OpenCountsAsync(incidents, candidates);
        return candidates.OrderBy(c => counts.GetValueOrDefault(c)).ThenBy(c => c).First();
    }
}
