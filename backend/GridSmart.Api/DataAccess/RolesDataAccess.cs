using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_roles.</summary>
public sealed class RolesDataAccess
{
    private const string Table = "tbl_roles";
    private static readonly string[] Columns = ["role_code", "role_name", "description", "is_system", "is_active", "created_at"];

    public async Task<int> InsertAsync(Row data) => (int)await TableCrud.InsertAsync(Table, Columns, data);

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data);

    public Task<int> DeleteAsync(int id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(int id) => TableCrud.ReadByIdAsync(Table, id);

    public Task<Row?> ReadByCodeAsync(string roleCode) =>
        ProcExecutor.QuerySingleAsync("SP_tbl_roles_ReadByCode", ProcExecutor.P("@role_code", roleCode));
}
