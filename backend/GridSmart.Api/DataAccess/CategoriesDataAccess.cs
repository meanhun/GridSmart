using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_categories.</summary>
public sealed class CategoriesDataAccess
{
    private const string Table = "tbl_categories";
    private static readonly string[] Columns = ["category_code", "category_name", "default_priority", "is_active"];

    public async Task<int> InsertAsync(Row data) => (int)await TableCrud.InsertAsync(Table, Columns, data);

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data);

    public Task<int> DeleteAsync(int id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(int id) => TableCrud.ReadByIdAsync(Table, id);
}
