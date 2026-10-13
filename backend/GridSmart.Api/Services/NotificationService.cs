using GridSmart.Api.Data;

namespace GridSmart.Api.Services;

/// <summary>Ghi tbl_notifications roi day qua SignalR toi nhom user:{id} (BE-22).</summary>
public sealed class NotificationService(
    NotificationsDataAccess notifications,
    UsersDataAccess users,
    AdminUnitsDataAccess adminUnits,
    IRealtimePublisher realtime,
    TimeProvider clock) : INotificationService
{
    public async Task NotifyAsync(IEnumerable<int> userIds, NotificationType type, string title, string? content, long? incidentId, CancellationToken ct = default)
    {
        var recipients = userIds.Distinct().ToList();
        if (recipients.Count == 0) return;

        var now = clock.GetUtcNow().UtcDateTime;
        var typeText = EnumText.UpperSnake(type);
        var created = new List<(long Id, int UserId)>(recipients.Count);

        await using (var tx = await ProcTransaction.BeginAsync())
        {
            foreach (var uid in recipients)
            {
                var id = await notifications.InsertAsync(new Row
                {
                    ["user_id"] = uid,
                    ["incident_id"] = incidentId,
                    ["notification_type"] = typeText,
                    ["title"] = title,
                    ["content"] = content,
                    ["is_read"] = false,
                    ["created_at"] = now,
                    ["read_at"] = null,
                });
                created.Add((id, uid));
            }
            await tx.CommitAsync();
        }

        foreach (var (id, uid) in created)
        {
            await realtime.PublishToUserAsync(uid, RealtimeEvents.NotificationCreated,
                new NotificationDto(id, typeText, title, content, incidentId, false, now), ct);
        }
    }

    public async Task<PagedResult<NotificationDto>> ListAsync(int userId, bool unreadOnly, int? page, int? pageSize, CancellationToken ct = default)
    {
        var (p, s) = Paging.Normalize(page, pageSize);
        var (items, total) = await notifications.ListAsync(userId, unreadOnly, p, s);
        return new PagedResult<NotificationDto>(
            items.Select(n => new NotificationDto(n.GetLong("id"), n.GetString("notification_type"), n.GetString("title"),
                n.GetStringOrNull("content"), n.GetLongOrNull("incident_id"), n.GetBool("is_read"), n.GetDateTime("created_at"))).ToList(),
            p, s, total);
    }

    public Task<int> UnreadCountAsync(int userId, CancellationToken ct = default) =>
        notifications.CountUnreadAsync(userId);

    public async Task MarkReadAsync(int userId, long id, CancellationToken ct = default)
    {
        var n = await notifications.ReadByIdAsync(id);
        if (n is null || n.GetInt("user_id") != userId)
            throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy thông báo.");
        if (!n.GetBool("is_read"))
            await notifications.MarkReadAsync(id, userId, clock.GetUtcNow().UtcDateTime);
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken ct = default)
    {
        await notifications.MarkAllReadAsync(userId, clock.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// Uu tien nguoi co quyen trong pham vi gan nhat voi don vi; neu khong co thi lay nguoi co quyen toan he thong.
    /// </summary>
    public async Task<List<int>> FindUsersWithPermissionOverUnitAsync(string permission, int? unitId, CancellationToken ct = default)
    {
        string? path = null;
        if (unitId is { } uid)
            path = (await adminUnits.ReadByIdAsync(uid))?.GetStringOrNull("unit_path");

        var candidates = await users.FindWithPermissionAsync(permission, path);

        if (path is not null)
        {
            var scoped = candidates
                .Where(c => c.GetStringOrNull("scope_path") is { } sp && path.StartsWith(sp, StringComparison.Ordinal))
                .Select(c => c.GetInt("user_id"))
                .Distinct()
                .ToList();
            if (scoped.Count > 0) return scoped;
        }

        return candidates
            .Where(c => c.GetStringOrNull("scope_path") is null)
            .Select(c => c.GetInt("user_id"))
            .Distinct()
            .ToList();
    }
}
