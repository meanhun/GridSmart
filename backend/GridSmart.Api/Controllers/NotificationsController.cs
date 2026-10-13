using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GridSmart.Api.Controllers;

/// <summary>Thong bao cua nguoi dung dang nhap (BE-22).</summary>
[ApiController]
[Route("api/notifications")]
[Tags("Notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService notifications, ICurrentUser currentUser) : ControllerBase
{
    private int UserId => currentUser.UserId!.Value;

    [HttpGet]
    [EndpointSummary("Thông báo của người dùng, mới nhất trước.")]
    public Task<PagedResult<NotificationDto>> List(bool? unreadOnly, int? page, int? pageSize, CancellationToken ct) =>
        notifications.ListAsync(UserId, unreadOnly ?? false, page, pageSize, ct);

    [HttpGet("unread-count")]
    [EndpointSummary("Số thông báo chưa đọc.")]
    public async Task<UnreadCount> UnreadCount(CancellationToken ct) => new(await notifications.UnreadCountAsync(UserId, ct));

    [HttpPost("{id:long}/read")]
    [EndpointSummary("Đánh dấu một thông báo đã đọc.")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(UserId, id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    [EndpointSummary("Đánh dấu tất cả đã đọc.")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(UserId, ct);
        return NoContent();
    }
}
