namespace GridSmart.Api.Interfaces;

/// <summary>Ghi tbl_notifications roi day qua SignalR toi nhom user:{id} (BE-22).</summary>
public interface INotificationService
{
    Task NotifyAsync(IEnumerable<int> userIds, NotificationType type, string title, string? content, long? incidentId, CancellationToken ct = default);
    Task<PagedResult<NotificationDto>> ListAsync(int userId, bool unreadOnly, int? page, int? pageSize, CancellationToken ct = default);
    Task<int> UnreadCountAsync(int userId, CancellationToken ct = default);
    Task MarkReadAsync(int userId, long id, CancellationToken ct = default);
    Task MarkAllReadAsync(int userId, CancellationToken ct = default);

    /// <summary>Nguoi co quyen trong pham vi chua don vi (dung de bao dieu phoi vien).</summary>
    Task<List<int>> FindUsersWithPermissionOverUnitAsync(string permission, int? unitId, CancellationToken ct = default);
}
