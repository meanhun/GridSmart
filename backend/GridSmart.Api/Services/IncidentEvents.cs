using GridSmart.Api.Common;
using GridSmart.Api.Data;
using GridSmart.Api.DataAccess;

namespace GridSmart.Api.Services;

/// <summary>Phat su kien realtime lien quan su co va o luoi.</summary>
public sealed class IncidentEvents(AdminUnitsDataAccess adminUnits, GridsDataAccess grids, IRealtimePublisher realtime)
{
    public async Task PublishAsync(Row incident, string eventName, object payload, CancellationToken ct, bool toTracking = false)
    {
        var units = await IncidentRows.AncestorIdsAsync(adminUnits, incident.GetIntOrNull("admin_unit_id"));
        await realtime.PublishToUnitsAsync(eventName, payload, units, ct);
        if (toTracking)
        {
            var trackingCode = incident.GetString("tracking_code");
            var status = IncidentRows.Status(incident);
            var publicPayload = new
            {
                trackingCode,
                status = IncidentText.StatusCode(status),
                statusText = IncidentText.PublicStatusText(status),
            };
            await realtime.PublishToTrackingAsync(trackingCode, eventName, publicPayload, ct);
        }
    }

    public Task UpdatedAsync(Row i, CancellationToken ct) =>
        PublishAsync(i, RealtimeEvents.IncidentUpdated,
            new
            {
                id = i.GetLong("id"),
                trackingCode = i.GetString("tracking_code"),
                status = IncidentText.StatusCode(IncidentRows.Status(i)),
                isOverdue = i.GetBool("is_overdue"),
                isManualReview = i.GetBool("is_manual_review"),
                updatedAt = i.GetDateTimeOrNull("updated_at") ?? i.GetDateTime("created_at"),
            },
            ct, toTracking: true);

    public async Task GridStatusChangedAsync(int? gridId, CancellationToken ct)
    {
        if (gridId is null) return;
        var s = (await grids.ReadStatusAsync(null, null, gridId)).FirstOrDefault();
        if (s is null) return;
        var units = await IncidentRows.AncestorIdsAsync(adminUnits, s.GetIntOrNull("admin_unit_id"));
        await realtime.PublishToUnitsAsync(RealtimeEvents.GridStatusChanged,
            new
            {
                gridId = s.GetInt("grid_id"),
                statusColor = s.GetString("status_color"),
                openCount = s.GetInt("open_incident_count"),
                overdueCount = s.GetInt("overdue_incident_count"),
            },
            units, ct);
    }
}
