using Microsoft.Data.SqlClient;

namespace GridSmart.Api.Data;

/// <summary>
/// Giao dich dung chung cho moi loi goi ProcExecutor trong cung luong async (AsyncLocal).
/// Dung: <c>await using var tx = await ProcTransaction.BeginAsync(); ...; await tx.CommitAsync();</c>
/// Khong commit truoc khi Dispose thi rollback. Khong ho tro giao dich long nhau.
/// Luu y: khong dung chung giao dich giua cac tac vu chay song song (Task.Run, Parallel), vi SqlConnection khong chay dong thoi.
/// </summary>
public sealed class ProcTransaction : IAsyncDisposable
{
    // Gan gia tri dong bo trong BeginAsync (khong phai trong async method) de gia tri ton tai o ngu canh goi.
    private static readonly AsyncLocal<ProcTransaction?> Current = new();

    private SqlConnection? _connection;
    private SqlTransaction? _transaction;
    private bool _completed;   // da commit, rollback hoac dang dispose
    private bool _disposed;

    private ProcTransaction()
    {
    }

    /// <summary>Giao dich dang mo tren luong hien tai, hoac null.</summary>
    internal static ProcTransaction? Active
    {
        get
        {
            var tx = Current.Value;
            return tx is not null && tx.IsOpen ? tx : null;
        }
    }

    internal bool IsOpen => _connection is not null && !_completed;

    internal bool IsDisposed => _disposed;

    internal SqlConnection Connection =>
        _connection ?? throw new InvalidOperationException("Giao dịch chưa mở.");

    internal SqlTransaction Transaction =>
        _transaction ?? throw new InvalidOperationException("Giao dịch chưa mở.");

    /// <summary>Mo giao dich moi tren mot ket noi rieng va gan cho luong async hien tai.</summary>
    public static Task<ProcTransaction> BeginAsync()
    {
        if (Current.Value is { IsOpen: true })
            throw new InvalidOperationException("Đã có giao dịch đang mở trên luồng này. Không hỗ trợ giao dịch lồng nhau.");

        var tx = new ProcTransaction();
        Current.Value = tx;
        return tx.OpenAsync();
    }

    public async Task CommitAsync()
    {
        if (!IsOpen)
            throw new InvalidOperationException("Giao dịch không còn mở.");

        _completed = true;
        await Transaction.CommitAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        var wasOpen = IsOpen;
        _completed = true;
        if (_connection is null) return;

        try
        {
            if (wasOpen)
            {
                try
                {
                    await Transaction.RollbackAsync();
                }
                catch (Exception ex) when (ex is SqlException or InvalidOperationException)
                {
                    // Ket noi hong: SQL Server tu rollback khi dong ket noi ben duoi.
                }
            }
        }
        finally
        {
            _transaction?.Dispose();
            await _connection.DisposeAsync();
        }
    }

    private async Task<ProcTransaction> OpenAsync()
    {
        var conn = await ConnectionHelper.OpenAsync();
        try
        {
            _transaction = conn.BeginTransaction();
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }

        _connection = conn;
        return this;
    }
}
