using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_notifications (khoa BIGINT).</summary>
public sealed class NotificationsDataAccess
{
    private const string Table = "tbl_notifications";
    private static readonly string[] Columns =
    [
        "user_id", "incident_id", "notification_type", "title", "content", "is_read", "created_at", "read_at",
    ];

    public Task<long> InsertAsync(Row data) => TableCrud.InsertAsync(Table, Columns, data);

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data);

public Task<List<Row>> ReadByIncidentAsync(long incidentId) =>
    ProcExecutor.QueryAsync("SP_tbl_notifications_ReadByIncident", ProcExecutor.P("@incident_id", incidentId));

    public Task<int> DeleteAsync(long id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(long id) => TableCrud.ReadByIdAsync(Table, id);

    public async Task<(List<Row> Items, int Total)> ListAsync(int userId, bool unreadOnly, int pageIndex, int pageSize)
    {
        var (index, size) = TableCrud.Page(pageIndex, pageSize);
        var sets = await ProcExecutor.QueryMultipleAsync("SP_tbl_notifications_List",
            ProcExecutor.P("@user_id", userId),
            ProcExecutor.P("@unread_only", unreadOnly),
            ProcExecutor.P("@page_index", index),
            ProcExecutor.P("@page_size", size));
        return (sets[0], TableCrud.TotalCount(sets));
    }

    public async Task<int> CountUnreadAsync(int userId)
    {
        var row = await ProcExecutor.QuerySingleAsync("SP_tbl_notifications_CountUnread", ProcExecutor.P("@user_id", userId));
        return row?.GetIntOrNull("unread_count") ?? 0;
    }

    public Task<int> MarkReadAsync(long id, int userId, DateTime readAt) =>
        ProcExecutor.ExecuteAsync("SP_tbl_notifications_MarkRead",
            ProcExecutor.P("@id", id),
            ProcExecutor.P("@user_id", userId),
            ProcExecutor.P("@read_at", readAt));

    public Task<int> MarkAllReadAsync(int userId, DateTime readAt) =>
        ProcExecutor.ExecuteAsync("SP_tbl_notifications_MarkAllRead",
            ProcExecutor.P("@user_id", userId),
            ProcExecutor.P("@read_at", readAt));
}
