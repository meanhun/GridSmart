using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>Bo loc danh sach su co (tham so cua SP_tbl_incidents_List).</summary>
public sealed record IncidentListFilter(
    int PageIndex,
    int PageSize,
    string? ScopePaths,
    string? StatusList,
    string? UnitPath,
    int? GridId,
    int? CategoryId,
    byte? Priority,
    bool? IsOverdue,
    bool? IsManualReview,
    int? AssignedOfficerId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Search,
    bool IncludeChildren);

/// <summary>tbl_incidents (khoa BIGINT).</summary>
public sealed class IncidentsDataAccess
{
    private const string Table = "tbl_incidents";
    private static readonly string[] Columns =
    [
        "tracking_code", "reporter_user_id", "reporter_phone", "description", "category_id", "ai_label",
        "ai_confidence", "is_manual_review", "location_point_wkt", "address_text", "grid_id", "admin_unit_id",
        "assigned_officer_id", "parent_incident_id", "duplicate_count", "status", "priority", "sla_policy_id",
        "sla_deadline", "accepted_at", "is_overdue", "escalated_at", "resolved_at", "created_at", "updated_at",
        "processed_at",
    ];

    public Task<long> InsertAsync(Row data) => TableCrud.InsertAsync(Table, Columns, data);

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data);

    public Task<int> DeleteAsync(long id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(long id) => TableCrud.ReadByIdAsync(Table, id);

    public Task<Row?> ReadByTrackingCodeAsync(string code) =>
        ProcExecutor.QuerySingleAsync("SP_tbl_incidents_ReadByTrackingCode", ProcExecutor.P("@tracking_code", code));

    public Task<Row?> ReadDetailAsync(long id) =>
        ProcExecutor.QuerySingleAsync("SP_tbl_incidents_ReadDetail", ProcExecutor.P("@id", id));

    public Task<List<Row>> ReadChildrenAsync(long parentId) =>
        ProcExecutor.QueryAsync("SP_tbl_incidents_ReadChildren", ProcExecutor.P("@parent_id", parentId));

    public Task<List<Row>> ReadOpenChildrenAsync(long parentId) =>
        ProcExecutor.QueryAsync("SP_tbl_incidents_ReadOpenChildren", ProcExecutor.P("@parent_id", parentId));

    public async Task<(List<Row> Items, int Total)> ListAsync(IncidentListFilter f)
    {
        var (index, size) = TableCrud.Page(f.PageIndex, f.PageSize);
        var sets = await ProcExecutor.QueryMultipleAsync("SP_tbl_incidents_List",
            ProcExecutor.P("@page_index", index),
            ProcExecutor.P("@page_size", size),
            ProcExecutor.P("@scope_paths", f.ScopePaths),
            ProcExecutor.P("@status_list", f.StatusList),
            ProcExecutor.P("@unit_path", f.UnitPath),
            ProcExecutor.P("@grid_id", f.GridId),
            ProcExecutor.P("@category_id", f.CategoryId),
            ProcExecutor.P("@priority", f.Priority),
            ProcExecutor.P("@is_overdue", f.IsOverdue),
            ProcExecutor.P("@is_manual_review", f.IsManualReview),
            ProcExecutor.P("@assigned_officer_id", f.AssignedOfficerId),
            ProcExecutor.P("@from_utc", f.FromUtc),
            ProcExecutor.P("@to_utc", f.ToUtc),
            ProcExecutor.P("@search", f.Search),
            ProcExecutor.P("@include_children", f.IncludeChildren));
        return (sets[0], TableCrud.TotalCount(sets));
    }

    /// <summary>Phan anh cua nguoi dung theo user_id hoac so dien thoai (toi da 200).</summary>
    public Task<List<Row>> ListByReporterAsync(int? userId, string? phone) =>
        ProcExecutor.QueryAsync("SP_tbl_incidents_ListByReporter",
            ProcExecutor.P("@user_id", userId),
            ProcExecutor.P("@phone", phone));

    /// <summary>So phan anh cua mot so dien thoai tu thoi diem sinceUtc.</summary>
    public async Task<int> CountRecentByPhoneAsync(string phone, DateTime sinceUtc)
    {
        var r = await ProcExecutor.QuerySingleAsync("SP_tbl_incidents_CountRecentByPhone",
            ProcExecutor.P("@phone", phone),
            ProcExecutor.P("@since_utc", sinceUtc));
        return r?.GetInt("total_count") ?? 0;
    }

    /// <summary>Gan cac phan anh an danh cung so dien thoai cho tai khoan userId; tra so dong duoc gan.</summary>
    public Task<int> LinkReporterByPhoneAsync(string phone, int userId) =>
        ProcExecutor.ExecuteAsync("SP_tbl_incidents_LinkReporterByPhone",
            ProcExecutor.P("@phone", phone),
            ProcExecutor.P("@user_id", userId));

    public Task<List<Row>> ReadPendingPipelineAsync(DateTime cutoffUtc, int take) =>
        ProcExecutor.QueryAsync("SP_tbl_incidents_ReadPendingPipeline",
            ProcExecutor.P("@cutoff_utc", cutoffUtc),
            ProcExecutor.P("@take", take));

    /// <summary>Danh dau leo thang; tra cac dong da cap nhat (UPDATE ... OUTPUT).</summary>
    public Task<List<Row>> MarkEscalatedAsync(DateTime nowUtc) =>
        ProcExecutor.QueryAsync("SP_tbl_incidents_MarkEscalated", ProcExecutor.P("@now_utc", nowUtc));

    /// <summary>Danh dau qua han; tra cac dong da cap nhat (UPDATE ... OUTPUT).</summary>
    public Task<List<Row>> MarkOverdueAsync(DateTime nowUtc) =>
        ProcExecutor.QueryAsync("SP_tbl_incidents_MarkOverdue", ProcExecutor.P("@now_utc", nowUtc));

    public Task<List<Row>> CountOpenByOfficersAsync(string officerIds) =>
        ProcExecutor.QueryAsync("SP_tbl_incidents_CountOpenByOfficers", ProcExecutor.P("@officer_ids", officerIds));

    /// <summary>Doi don vi hanh chinh cua su co tu fromUnitId sang toUnitId; thoi diem lay theo UTC hien tai.</summary>
    public Task<int> RepointAdminUnitAsync(int fromUnitId, int toUnitId) =>
        ProcExecutor.ExecuteAsync("SP_tbl_incidents_RepointAdminUnit",
            ProcExecutor.P("@from_unit_id", fromUnitId),
            ProcExecutor.P("@to_unit_id", toUnitId),
            ProcExecutor.P("@now_utc", DateTime.UtcNow));
}
