using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess;

/// <summary>tbl_refresh_tokens (khoa BIGINT).</summary>
public sealed class RefreshTokensDataAccess
{
    private const string Table = "tbl_refresh_tokens";
    private static readonly string[] Columns =
    [
        "user_id", "token_hash", "expires_at", "created_at", "created_by_ip", "revoked_at",
        "replaced_by_token_id", "security_stamp",
    ];

    public Task<long> InsertAsync(Row data) => TableCrud.InsertAsync(Table, Columns, data);

    public Task<int> UpdateAsync(Row data) => TableCrud.UpdateAsync(Table, Columns, data);

    public Task<int> DeleteAsync(long id) => TableCrud.DeleteAsync(Table, id);

    public Task<List<Row>> ReadAllAsync() => TableCrud.ReadAllAsync(Table);

    public Task<Row?> ReadByIdAsync(long id) => TableCrud.ReadByIdAsync(Table, id);

    public Task<Row?> ReadByHashAsync(string tokenHash) =>
        ProcExecutor.QuerySingleAsync("SP_tbl_refresh_tokens_ReadByHash", ProcExecutor.P("@token_hash", tokenHash));

    public Task<int> RevokeByUserAsync(int userId, DateTime revokedAt) =>
        ProcExecutor.ExecuteAsync("SP_tbl_refresh_tokens_RevokeByUser",
            ProcExecutor.P("@user_id", userId),
            ProcExecutor.P("@revoked_at", revokedAt));
}
