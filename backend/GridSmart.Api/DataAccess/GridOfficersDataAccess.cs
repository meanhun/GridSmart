using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_grid_officers, khoa kep (grid_id, user_id). Khong co Update, khong co OUTPUT khi Insert.</summary>
public sealed class GridOfficersDataAccess
{
    private const string Table = "tbl_grid_officers";

    public Task<int> InsertAsync(int gridId, int userId, bool isPrimary, DateTime assignedAt) =>
        ProcExecutor.ExecuteAsync($"SP_{Table}_Insert",
            ProcExecutor.P("@grid_id", gridId),
            ProcExecutor.P("@user_id", userId),
            ProcExecutor.P("@is_primary", isPrimary),
            ProcExecutor.P("@assigned_at", assignedAt));

    public Task<int> DeleteAsync(int gridId, int userId) =>
        ProcExecutor.ExecuteAsync($"SP_{Table}_Delete",
            ProcExecutor.P("@grid_id", gridId),
            ProcExecutor.P("@user_id", userId));

    public Task<List<Row>> ReadAllAsync() => ProcExecutor.QueryAsync($"SP_{Table}_ReadAll");

    public Task<List<Row>> ReadByGridAsync(int gridId) =>
        ProcExecutor.QueryAsync($"SP_{Table}_ReadByGrid", ProcExecutor.P("@grid_id", gridId));

    public Task<int> DeleteByGridAsync(int gridId) =>
        ProcExecutor.ExecuteAsync($"SP_{Table}_DeleteByGrid", ProcExecutor.P("@grid_id", gridId));
}
