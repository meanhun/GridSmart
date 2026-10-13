using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_incident_logs (khoa BIGINT).</summary>
public sealed class IncidentLogsDataAccess
{
    private const string Table = "tbl_incident_logs";
    private static readonly string[] Columns =
    [
        "incident_id", "action_by_user_id", "action_type", "action_note", "old_status", "new_status", "created_at",
    ];

    public Task<long> InsertAsync(Row data) => TableCrud.InsertAsync(Table, Columns, data);

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data);

    public Task<int> DeleteAsync(long id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(long id) => TableCrud.ReadByIdAsync(Table, id);

    public Task<List<Row>> ReadByIncidentAsync(long incidentId) =>
        ProcExecutor.QueryAsync("SP_tbl_incident_logs_ReadByIncident", ProcExecutor.P("@incident_id", incidentId));
}
