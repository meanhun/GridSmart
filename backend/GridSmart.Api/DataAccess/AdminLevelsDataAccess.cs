using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_admin_levels. Khoa id TINYINT khong identity nen Insert truyen @id.</summary>
public sealed class AdminLevelsDataAccess
{
    private const string Table = "tbl_admin_levels";
    private static readonly string[] Columns = ["level_code", "level_name", "level_order"];

    public async Task<int> InsertAsync(Row data) => (int)await TableCrud.InsertAsync(Table, Columns, data, keyColumn: "id");

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data);

    public Task<int> DeleteAsync(int id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(int id) => TableCrud.ReadByIdAsync(Table, id);
}
