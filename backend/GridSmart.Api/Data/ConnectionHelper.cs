using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace GridSmart.Api.Data;

/// <summary>Cung cap chuoi ket noi va mo ket noi SQL Server (Microsoft.Data.SqlClient).</summary>
public static class ConnectionHelper
{
    private static string? _connectionString;

    /// <summary>Doc ConnectionStrings:Default. Goi mot lan khi khoi dong ung dung.</summary>
    public static void SetConfiguration(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default.");
    }

    /// <summary>Mo mot ket noi moi, da o trang thai Open. Nguoi goi chiu trach nhiem dong/huy.</summary>
    public static async Task<SqlConnection> OpenAsync()
    {
        var conn = new SqlConnection(ConnectionString);
        try
        {
            await conn.OpenAsync();
            return conn;
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    private static string ConnectionString =>
        _connectionString ?? throw new InvalidOperationException("Chưa gọi ConnectionHelper.SetConfiguration.");
}
