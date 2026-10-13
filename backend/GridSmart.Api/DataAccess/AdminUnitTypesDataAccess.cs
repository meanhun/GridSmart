using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_admin_unit_types. Khoa id TINYINT khong identity nen Insert truyen @id.</summary>
public sealed class AdminUnitTypesDataAccess
{
    private const string Table = "tbl_admin_unit_types";
    private static readonly string[] Columns = ["type_code", "type_name", "level_id"];

    public async Task<int> InsertAsync(Row data) => (int)await TableCrud.InsertAsync(Table, Columns, data, keyColumn: "id");

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data);

    public Task<int> DeleteAsync(int id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(int id) => TableCrud.ReadByIdAsync(Table, id);

    public Task<List<Row>> ReadAllWithLevelAsync() => ProcExecutor.QueryAsync("SP_tbl_admin_unit_types_ReadAllWithLevel");
}
