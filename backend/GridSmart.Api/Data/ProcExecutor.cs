using System.Data;
using Microsoft.Data.SqlClient;

namespace GridSmart.Api.Data;

/// <summary>
/// Goi stored procedure bang SqlCommand (CommandType.StoredProcedure).
/// Neu luong async hien tai dang co ProcTransaction mo thi dung chung ket noi va giao dich do;
/// nguoc lai mo ket noi rieng va dong sau moi loi goi.
/// </summary>
public static class ProcExecutor
{
    /// <summary>Tao tham so. Gia tri null duoc gui la DBNull.</summary>
    public static SqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    /// <summary>Thuc thi proc, tra so dong anh huong (ExecuteNonQuery).</summary>
    public static Task<int> ExecuteAsync(string proc, params SqlParameter[] ps) =>
        Run(CommandType.StoredProcedure, proc, ps, cmd => cmd.ExecuteNonQueryAsync());

    /// <summary>Thuc thi proc co tham so @new_id OUTPUT, tra gia tri @new_id.</summary>
    public static async Task<long> InsertAsync(string proc, params SqlParameter[] ps)
    {
        var newId = new SqlParameter("@new_id", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        await Run(CommandType.StoredProcedure, proc, ps.Append(newId).ToArray(), cmd => cmd.ExecuteNonQueryAsync());
        if (newId.Value is null or DBNull)
            throw new InvalidOperationException($"Thủ tục {proc} không trả @new_id.");
        return Convert.ToInt64(newId.Value);
    }

    /// <summary>Thuc thi proc va doc result set dau tien thanh danh sach Row.</summary>
    public static Task<List<Row>> QueryAsync(string proc, params SqlParameter[] ps) =>
        Run(CommandType.StoredProcedure, proc, ps, async cmd =>
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            return await ReadRowsAsync(reader);
        });

    /// <summary>Doc mot dong dau tien cua result set dau tien; khong co dong thi tra null.</summary>
    public static async Task<Row?> QuerySingleAsync(string proc, params SqlParameter[] ps)
    {
        var rows = await QueryAsync(proc, ps);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Doc tat ca result set cua proc (vi du danh sach co phan trang: items + total_count).</summary>
    public static Task<List<List<Row>>> QueryMultipleAsync(string proc, params SqlParameter[] ps) =>
        Run(CommandType.StoredProcedure, proc, ps, async cmd =>
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            var sets = new List<List<Row>>();
            do
            {
                sets.Add(await ReadRowsAsync(reader));
            }
            while (await reader.NextResultAsync());
            return sets;
        });

    /// <summary>
    /// Lay khoa ung dung (sp_getapplock, pham vi Session) va giu den khi Dispose.
    /// Trong ProcTransaction thi khoa gan voi ket noi cua giao dich.
    /// Nem TimeoutException neu khong lay duoc khoa.
    /// </summary>
    public static async Task<IAsyncDisposable> AcquireAppLockAsync(string resource, TimeSpan timeout)
    {
        var owner = ProcTransaction.Active;
        SqlConnection? ownConnection = null;
        SqlConnection conn;
        if (owner is not null)
        {
            conn = owner.Connection;
        }
        else
        {
            ownConnection = await ConnectionHelper.OpenAsync();
            conn = ownConnection;
        }

        try
        {
            var transaction = owner?.Transaction;
            await using var cmd = new SqlCommand("sp_getapplock", conn, transaction) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.Add(new SqlParameter("@Resource", resource));
            cmd.Parameters.Add(new SqlParameter("@LockMode", "Exclusive"));
            cmd.Parameters.Add(new SqlParameter("@LockOwner", "Session"));
            cmd.Parameters.Add(new SqlParameter("@LockTimeout", (int)timeout.TotalMilliseconds));
            var ret = new SqlParameter("@ret", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };
            cmd.Parameters.Add(ret);
            await cmd.ExecuteNonQueryAsync();
            if (Convert.ToInt32(ret.Value) < 0)
                throw new TimeoutException($"Không lấy được khóa {resource}.");
        }
        catch
        {
            if (ownConnection is not null) await ownConnection.DisposeAsync();
            throw;
        }

        return new AppLock(resource, conn, owner, ownConnection);
    }

    /// <summary>Thuc thi lenh SQL van ban (khong phai proc) va tra gia tri cot dau tien. Dung noi bo cho ham scalar.</summary>
    internal static Task<object?> ScalarSqlAsync(string sql, params SqlParameter[] ps) =>
        Run(CommandType.Text, sql, ps, cmd => cmd.ExecuteScalarAsync());

    private static async Task<T> Run<T>(CommandType type, string commandText, SqlParameter[] ps, Func<SqlCommand, Task<T>> action)
    {
        var tx = ProcTransaction.Active;
        if (tx is not null)
        {
            return await Execute(tx.Connection, tx.Transaction, type, commandText, ps, action);
        }

        await using var conn = await ConnectionHelper.OpenAsync();
        return await Execute(conn, null, type, commandText, ps, action);
    }

    private static async Task<T> Execute<T>(SqlConnection conn, SqlTransaction? tx, CommandType type, string commandText, SqlParameter[] ps, Func<SqlCommand, Task<T>> action)
    {
        await using var cmd = new SqlCommand(commandText, conn, tx) { CommandType = type };
        if (ps.Length > 0) cmd.Parameters.AddRange(ps);
        return await action(cmd);
    }

    private static async Task<List<Row>> ReadRowsAsync(SqlDataReader reader)
    {
        var rows = new List<Row>();
        while (await reader.ReadAsync())
        {
            var row = new Row();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                // DATETIME2 trong database luu UTC: gan Kind = Utc de JSON co hau to Z
                if (value is DateTime dt && dt.Kind != DateTimeKind.Utc)
                    value = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                row[reader.GetName(i)] = value;
            }
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>Khoa ung dung da lay; Dispose se goi sp_releaseapplock.</summary>
    private sealed class AppLock(string resource, SqlConnection conn, ProcTransaction? owner, SqlConnection? ownConnection) : IAsyncDisposable
    {
        private int _released;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0) return;
            try
            {
                // Giao dich da dispose thi ket noi da dong, khoa da duoc giai phong cung luc.
                if (owner is { IsDisposed: true }) return;

                var transaction = owner is { IsOpen: true } ? owner.Transaction : null;
                await using var cmd = new SqlCommand("sp_releaseapplock", conn, transaction) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.Add(new SqlParameter("@Resource", resource));
                cmd.Parameters.Add(new SqlParameter("@LockOwner", "Session"));
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (ownConnection is not null) await ownConnection.DisposeAsync();
            }
        }
    }
}
