using Microsoft.Data.SqlClient;

namespace GridSmart.Api.Data;

/// <summary>
/// Sinh tham so va goi proc CRUD chuan cho mot bang (dung noi bo boi cac lop DataAccess).
/// Quy uoc: proc SP_&lt;bang&gt;_Insert/_Update/_Delete/_ReadAll/_ReadById; tham so = ten cot (co @).
/// </summary>
internal static class TableCrud
{
    /// <summary>
    /// Insert: truyen moi cot trong danh sach; cot thieu trong Row -> DBNull.
    /// keyColumn chi truyen khi khoa khong phai identity (vd. tbl_admin_levels); khi do proc nhan @id.
    /// </summary>
    public static Task<long> InsertAsync(string table, IReadOnlyList<string> columns, Row data, string? keyColumn = null) =>
        ProcExecutor.InsertAsync($"SP_{table}_Insert", BuildParams(data, columns, keyColumn));

    /// <summary>
    /// Update: ghi de toan bo cot, tru created_at va cac cot readOnly (proc Update khong nhan cac cot nay);
    /// data phai co "id"; cot thieu -> DBNull.
    /// </summary>
    public static Task<int> UpdateAsync(string table, IReadOnlyList<string> columns, Row data, params string[] readOnly)
    {
        var updatable = columns.Where(c => c != "created_at" && !readOnly.Contains(c)).ToList();
        return ProcExecutor.ExecuteAsync($"SP_{table}_Update", BuildParams(data, updatable, "id"));
    }

    public static Task<int> DeleteAsync(string table, object id) =>
        ProcExecutor.ExecuteAsync($"SP_{table}_Delete", ProcExecutor.P("@id", id));

    public static Task<List<Row>> ReadAllAsync(string table) =>
        ProcExecutor.QueryAsync($"SP_{table}_ReadAll");

    public static Task<Row?> ReadByIdAsync(string table, object id) =>
        ProcExecutor.QuerySingleAsync($"SP_{table}_ReadById", ProcExecutor.P("@id", id));

    /// <summary>Chuan hoa phan trang: page_index tu 1; page_size mac dinh 20, toi da 200.</summary>
    public static (int Index, int Size) Page(int pageIndex, int pageSize) =>
        (Math.Max(1, pageIndex), pageSize <= 0 ? 20 : Math.Min(pageSize, 200));

    /// <summary>Lay total_count tu result set thu hai cua proc phan trang.</summary>
    public static int TotalCount(List<List<Row>> sets) =>
        sets.Count > 1 && sets[1].Count > 0 ? sets[1][0].GetInt("total_count") : 0;

    private static SqlParameter[] BuildParams(Row data, IEnumerable<string> columns, string? keyColumn)
    {
        var list = new List<SqlParameter>();
        if (keyColumn is not null)
            list.Add(ProcExecutor.P("@" + keyColumn, Value(data, keyColumn)));
        foreach (var column in columns)
            list.Add(ProcExecutor.P("@" + column, Value(data, column)));
        return list.ToArray();
    }

    private static object? Value(Row data, string key) => data.TryGetValue(key, out var v) ? v : null;
}
