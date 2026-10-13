using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_grids va vw_grid_status (qua proc ReadStatus).</summary>
public sealed class GridsDataAccess
{
    private const string Table = "tbl_grids";
    private static readonly string[] Columns =
    [
        "grid_code", "name", "admin_unit_id", "boundary_wkt", "assigned_officer_id", "is_active", "created_at", "updated_at",
    ];

    public async Task<int> InsertAsync(Row data) => (int)await TableCrud.InsertAsync(Table, Columns, data);

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data);

    public Task<int> DeleteAsync(int id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(int id) => TableCrud.ReadByIdAsync(Table, id);

    public Task<Row?> ReadDetailAsync(int id) =>
        ProcExecutor.QuerySingleAsync("SP_tbl_grids_ReadDetail", ProcExecutor.P("@id", id));

    public Task<Row?> ReadByCodeAsync(string gridCode) =>
        ProcExecutor.QuerySingleAsync("SP_tbl_grids_ReadByCode", ProcExecutor.P("@grid_code", gridCode));

    public Task<List<Row>> ReadByUnitsAsync(string unitIds) =>
        ProcExecutor.QueryAsync("SP_tbl_grids_ReadByUnits", ProcExecutor.P("@unit_ids", unitIds));

    public Task<List<Row>> ReadStatusAsync(string? scopePaths, string? unitPath, int? gridId) =>
        ProcExecutor.QueryAsync("SP_tbl_grids_ReadStatus",
            ProcExecutor.P("@scope_paths", scopePaths),
            ProcExecutor.P("@unit_path", unitPath),
            ProcExecutor.P("@grid_id", gridId));
}
