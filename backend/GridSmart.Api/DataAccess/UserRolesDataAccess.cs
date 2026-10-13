using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_user_roles (gan vai tro cho nguoi dung tai don vi) va nap pham vi quyen.</summary>
public sealed class UserRolesDataAccess
{
    private const string Table = "tbl_user_roles";
    private static readonly string[] Columns =
    [
        "user_id", "role_id", "admin_unit_id", "assigned_by_user_id", "assigned_at", "expires_at", "is_active",
    ];

    public async Task<int> InsertAsync(Row data) => (int)await TableCrud.InsertAsync(Table, Columns, data);

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data, "assigned_at");

    public Task<int> DeleteAsync(int id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(int id) => TableCrud.ReadByIdAsync(Table, id);

    public Task<List<Row>> ReadByUserAsync(int userId) =>
        ProcExecutor.QueryAsync("SP_tbl_user_roles_ReadByUser", ProcExecutor.P("@user_id", userId));

    public Task<List<Row>> ReadByUnitsAsync(string unitIds) =>
        ProcExecutor.QueryAsync("SP_tbl_user_roles_ReadByUnits", ProcExecutor.P("@unit_ids", unitIds));

    public Task<List<Row>> LoadScopesAsync(int userId) =>
        ProcExecutor.QueryAsync("SP_Auth_LoadScopes", ProcExecutor.P("@user_id", userId));
}
