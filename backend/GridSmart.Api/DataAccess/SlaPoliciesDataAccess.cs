using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_sla_policies.</summary>
public sealed class SlaPoliciesDataAccess
{
    private const string Table = "tbl_sla_policies";
    private static readonly string[] Columns = ["priority", "sla_level", "sla_hours", "escalation_percent", "description"];

    public async Task<int> InsertAsync(Row data) => (int)await TableCrud.InsertAsync(Table, Columns, data);

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data);

    public Task<int> DeleteAsync(int id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(int id) => TableCrud.ReadByIdAsync(Table, id);
}
