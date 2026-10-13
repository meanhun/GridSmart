using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_permissions.</summary>
public sealed class PermissionsDataAccess
{
    private const string Table = "tbl_permissions";
    private static readonly string[] Columns = ["permission_code", "permission_name", "module"];

    public async Task<int> InsertAsync(Row data) => (int)await TableCrud.InsertAsync(Table, Columns, data);

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data);

    public Task<int> DeleteAsync(int id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(int id) => TableCrud.ReadByIdAsync(Table, id);
}
