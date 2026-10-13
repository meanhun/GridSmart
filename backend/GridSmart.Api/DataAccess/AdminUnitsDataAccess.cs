using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_admin_units. unit_path do trigger tinh, khong nam trong Insert/Update.</summary>
public sealed class AdminUnitsDataAccess
{
    private const string Table = "tbl_admin_units";
    private static readonly string[] Columns =
    [
        "unit_code", "name", "unit_type_id", "parent_id", "boundary_wkt", "is_active",
        "effective_from", "effective_to", "merged_into_unit_id", "created_at", "updated_at",
    ];

    public async Task<int> InsertAsync(Row data) => (int)await TableCrud.InsertAsync(Table, Columns, data);

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data);

    public Task<int> DeleteAsync(int id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(int id) => TableCrud.ReadByIdAsync(Table, id);

    public Task<List<Row>> ReadTreeAsync(bool includeInactive) =>
        ProcExecutor.QueryAsync("SP_tbl_admin_units_ReadTree", ProcExecutor.P("@include_inactive", includeInactive));

    public Task<Row?> ReadDetailAsync(int id) =>
        ProcExecutor.QuerySingleAsync("SP_tbl_admin_units_ReadDetail", ProcExecutor.P("@id", id));

    public Task<List<Row>> ReadAncestorsAsync(int unitId) =>
        ProcExecutor.QueryAsync("SP_vw_admin_unit_ancestors_ReadByUnit", ProcExecutor.P("@unit_id", unitId));

    public Task<Row?> ReadByCodeAsync(string code) =>
        ProcExecutor.QuerySingleAsync("SP_tbl_admin_units_ReadByCode", ProcExecutor.P("@unit_code", code));

    public Task<List<Row>> ReadChildrenOfAsync(string parentIds) =>
        ProcExecutor.QueryAsync("SP_tbl_admin_units_ReadChildrenOf", ProcExecutor.P("@parent_ids", parentIds));
}
