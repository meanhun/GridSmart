using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_users, cung cac proc xac thuc/phan quyen lien quan nguoi dung.</summary>
public sealed class UsersDataAccess
{
    private const string Table = "tbl_users";
    private static readonly string[] Columns =
    [
        "username", "password_hash", "full_name", "phone", "email", "is_active", "security_stamp",
        "failed_login_count", "locked_until", "last_login_at", "created_at", "updated_at",
    ];

    public async Task<int> InsertAsync(Row data) => (int)await TableCrud.InsertAsync(Table, Columns, data);

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data);

    public Task<int> DeleteAsync(int id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(int id) => TableCrud.ReadByIdAsync(Table, id);

    public Task<Row?> ReadByUsernameAsync(string username) =>
        ProcExecutor.QuerySingleAsync("SP_tbl_users_ReadByUsername", ProcExecutor.P("@username", username));

    public async Task<(List<Row> Items, int Total)> ListAsync(
        int pageIndex, int pageSize, string? search, string? roleCode, string? unitPath, string? scopePaths, bool? locked)
    {
        var (index, size) = TableCrud.Page(pageIndex, pageSize);
        var sets = await ProcExecutor.QueryMultipleAsync("SP_tbl_users_List",
            ProcExecutor.P("@page_index", index),
            ProcExecutor.P("@page_size", size),
            ProcExecutor.P("@search", search),
            ProcExecutor.P("@role_code", roleCode),
            ProcExecutor.P("@unit_path", unitPath),
            ProcExecutor.P("@scope_paths", scopePaths),
            ProcExecutor.P("@locked", locked));
        return (sets[0], TableCrud.TotalCount(sets));
    }

    public Task<List<Row>> FindWithPermissionAsync(string permissionCode, string? unitPath) =>
        ProcExecutor.QueryAsync("SP_Auth_FindUsersWithPermission",
            ProcExecutor.P("@permission_code", permissionCode),
            ProcExecutor.P("@unit_path", unitPath));

    public Task<List<Row>> FindOfficersForUnitAsync(string? unitPath) =>
        ProcExecutor.QueryAsync("SP_Auth_FindOfficersForUnit", ProcExecutor.P("@unit_path", unitPath));
}
