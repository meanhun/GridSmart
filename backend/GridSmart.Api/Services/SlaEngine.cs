using GridSmart.Api.Common;
using GridSmart.Api.Data;
using GridSmart.Api.DataAccess;
using GridSmart.Api.Interfaces;
using GridSmart.Api.Models.Enums;
using Microsoft.Extensions.Logging;

namespace GridSmart.Api.Services;

/// <summary>
/// Quet SLA (UC-06, BE-20). Moi moc duoc "chiem" boi proc UPDATE ... OUTPUT
/// nen chay lai hoac chay song song nhieu instance cung khong gui trung.
/// </summary>
public sealed class SlaEngine(
    IncidentsDataAccess incidents,
    IncidentLogsDataAccess incidentLogs,
    ISpatialRepository spatial,
    INotificationService notifications,
    IncidentEvents events,
    TimeProvider clock,
    ILogger<SlaEngine> logger) : ISlaEngine
{
    public async Task<SlaRunResult> RunOnceAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        // Moc leo thang: start + sla_hours * escalation_percent% ; start = sla_deadline - sla_hours
        var escalated = await incidents.MarkEscalatedAsync(now);
        foreach (var row in escalated) await HandleEscalationAsync(row.GetLong("id"), now, ct);

        var overdue = await incidents.MarkOverdueAsync(now);
        foreach (var row in overdue) await HandleOverdueAsync(row.GetLong("id"), now, ct);

        if (escalated.Count + overdue.Count > 0)
            logger.LogInformation("SLA: leo thang {Escalated}, qua han {Overdue}", escalated.Count, overdue.Count);
        return new SlaRunResult(escalated.Count, overdue.Count);
    }

    private async Task HandleEscalationAsync(long id, DateTime now, CancellationToken ct)
    {
        var incident = await incidents.ReadByIdAsync(id);
        if (incident is null) return;
        var trackingCode = incident.GetString("tracking_code");
        var deadline = incident.GetDateTimeOrNull("sla_deadline");

        var targets = await NearestTargetsAsync(id, ct);
        var names = string.Join(", ", targets.Select(t => t.FullName).Distinct());
        await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Escalated,
            targets.Count > 0 ? $"Quá {EscalationText()} thời hạn chưa nhận việc. Leo thang tới: {names}." : "Quá hạn mức cảnh báo nhưng không tìm thấy người nhận leo thang.",
            now);

        await notifications.NotifyAsync(targets.Select(t => t.UserId), NotificationType.Escalation,
            $"Cảnh báo SLA: {trackingCode}", $"Sự cố chưa được nhận việc, hạn xử lý {deadline:yyyy-MM-ddTHH:mm:ssZ}.", id, ct);
        await events.PublishAsync(incident, RealtimeEvents.IncidentEscalated,
            new { id, trackingCode, escalatedAt = now, slaDeadline = deadline }, ct);
    }

    private async Task HandleOverdueAsync(long id, DateTime now, CancellationToken ct)
    {
        var incident = await incidents.ReadByIdAsync(id);
        if (incident is null) return;
        var trackingCode = incident.GetString("tracking_code");

        var targets = await NearestTargetsAsync(id, ct);
        await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Overdue, "Sự cố đã quá hạn xử lý theo SLA.", now);

        var receivers = targets.Select(t => t.UserId).ToList();
        if (incident.GetIntOrNull("assigned_officer_id") is { } officer) receivers.Add(officer);
        await notifications.NotifyAsync(receivers, NotificationType.Overdue,
            $"Quá hạn: {trackingCode}", "Sự cố đã quá thời hạn xử lý cam kết.", id, ct);
        await events.PublishAsync(incident, RealtimeEvents.IncidentOverdue,
            new { id, trackingCode, gridId = incident.GetIntOrNull("grid_id"), slaDeadline = incident.GetDateTimeOrNull("sla_deadline") }, ct);
        await events.UpdatedAsync(incident, ct);
        await events.GridStatusChangedAsync(incident.GetIntOrNull("grid_id"), ct);
    }

    /// <summary>Uu tien cap phuong/xa; khong co thi lay cap gan nhat con lai trong cay.</summary>
    private async Task<IReadOnlyList<EscalationTarget>> NearestTargetsAsync(long incidentId, CancellationToken ct)
    {
        var commune = await spatial.FindEscalationTargetsAsync(incidentId, AdminLevelCodes.Commune, ct);
        if (commune.Count > 0) return commune;
        var all = await spatial.FindEscalationTargetsAsync(incidentId, null, ct);
        if (all.Count == 0) return all;
        var nearest = all.Max(t => t.LevelOrder);
        return all.Where(t => t.LevelOrder == nearest).ToList();
    }

    private static string EscalationText() => "mức cảnh báo";
}
