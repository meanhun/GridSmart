using Microsoft.AspNetCore.SignalR;

namespace GridSmart.Api.Hubs;

/// <summary>Cai dat IRealtimePublisher bang SignalR: phat toi nhom don vi, nguoi dung, trang tra cuu.</summary>
public sealed class SignalRRealtimePublisher(IHubContext<EventsHub> hub) : IRealtimePublisher
{
    public Task PublishToUnitsAsync(string eventName, object payload, IEnumerable<int> unitIds, CancellationToken ct = default)
    {
        var groups = unitIds.Distinct().Select(EventsHub.UnitGroup).Append(EventsHub.GlobalGroup).ToList();
        return hub.Clients.Groups(groups).SendAsync(eventName, payload, ct);
    }

    public Task PublishToUserAsync(int userId, string eventName, object payload, CancellationToken ct = default) =>
        hub.Clients.Group(EventsHub.UserGroup(userId)).SendAsync(eventName, payload, ct);

    public Task PublishToTrackingAsync(string trackingCode, string eventName, object payload, CancellationToken ct = default) =>
        hub.Clients.Group(EventsHub.TrackGroup(trackingCode)).SendAsync(eventName, payload, ct);
}
