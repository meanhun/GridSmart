using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_role_permissions, khoa kep (role_id, permission_id). Khong co Update, khong co OUTPUT khi Insert.</summary>
public sealed class RolePermissionsDataAccess
{
    private const string Table = "tbl_role_permissions";

    public Task<int> InsertAsync(int roleId, int permissionId) =>
        ProcExecutor.ExecuteAsync($"SP_{Table}_Insert",
            ProcExecutor.P("@role_id", roleId),
            ProcExecutor.P("@permission_id", permissionId));

    public Task<int> DeleteAsync(int roleId, int permissionId) =>
        ProcExecutor.ExecuteAsync($"SP_{Table}_Delete",
            ProcExecutor.P("@role_id", roleId),
            ProcExecutor.P("@permission_id", permissionId));

    public Task<List<Row>> ReadAllAsync() => ProcExecutor.QueryAsync($"SP_{Table}_ReadAll");

    public Task<List<Row>> ReadByRoleAsync(int roleId) =>
        ProcExecutor.QueryAsync($"SP_{Table}_ReadByRole", ProcExecutor.P("@role_id", roleId));

    public Task<int> DeleteByRoleAsync(int roleId) =>
        ProcExecutor.ExecuteAsync($"SP_{Table}_DeleteByRole", ProcExecutor.P("@role_id", roleId));
}
