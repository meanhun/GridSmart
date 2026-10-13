using Microsoft.AspNetCore.SignalR;

namespace GridSmart.Api.Hubs;

/// <summary>
/// Hub /hubs/events (BE-23). Nguoi dang nhap vao nhom user:{id} va unit:{id} theo pham vi
/// dashboard.view / incident.view; pham vi toan he thong vao unit:*. Trang tra cuu cong khai goi JoinTracking.
/// </summary>
public sealed class EventsHub(IPublicIncidentService publicIncidents) : Hub
{
    public static string UserGroup(int userId) => $"user:{userId}";
    public static string UnitGroup(int unitId) => $"unit:{unitId}";
    public const string GlobalGroup = "unit:*";
    public static string TrackGroup(string code) => $"track:{code.ToUpperInvariant()}";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated == true && int.TryParse(user.FindFirst(GridSmartClaims.Subject)?.Value, out var uid))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(uid));
            var scopes = ClaimScopes.Read(user).Where(s => s.Permission is Permissions.DashboardView or Permissions.IncidentView).ToList();
            foreach (var group in scopes.Select(s => s.IsGlobal ? GlobalGroup : UnitHierarchy.UnitIdFromPath(s.UnitPath) is { } id ? UnitGroup(id) : null)
                         .OfType<string>().Distinct())
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, group);
            }
        }
        await base.OnConnectedAsync();
    }

    /// <summary>Trang tra cuu: xac minh ma + so dien thoai roi moi cho nhan su kien.</summary>
    public async Task<bool> JoinTracking(string trackingCode, string phone)
    {
        if (!await publicIncidents.VerifyTrackingAsync(trackingCode, phone, Context.ConnectionAborted)) return false;
        await Groups.AddToGroupAsync(Context.ConnectionId, TrackGroup(trackingCode));
        return true;
    }
}
