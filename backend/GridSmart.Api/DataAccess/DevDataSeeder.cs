using GridSmart.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GridSmart.Api.DataAccess;

/// <summary>
/// BE-DB-02: thay password_hash = 'CHANGE_ME' cua tai khoan mau bang hash mat khau dev.
/// Chi chay khi DevSeed:DefaultPassword co gia tri. Khong dung EF Core.
/// </summary>
public sealed class DevDataSeeder(IServiceScopeFactory scopes, IOptions<DevSeedOptions> options, ILogger<DevDataSeeder> logger) : IHostedService
{
    public const string Placeholder = "CHANGE_ME";

    // Hash dung dinh dang V3 cua ASP.NET Identity, khong phu thuoc kieu user nen dung PasswordHasher<object>.
    private static readonly PasswordHasher<object> Hasher = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var password = options.Value.DefaultPassword;
        if (string.IsNullOrEmpty(password))
        {
            logger.LogWarning("DevSeed:DefaultPassword chua cau hinh, bo qua seeder");
            return;
        }

        // Khong de loi CSDL (chua bat SQL Server, sai ten instance, chua tao database) lam hong viec khoi dong API.
        // Seeder chi la tien ich cho may dev; loi duoc ghi log va API van chay.
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UsersDataAccess>();

            var all = await users.ReadAllAsync();
            var placeholders = all.Where(u => u.GetStringOrNull("password_hash") == Placeholder).ToList();
            foreach (var user in placeholders)
            {
                user["password_hash"] = Hasher.HashPassword(user, password);
                await users.UpdateAsync(user);
            }

            if (placeholders.Count > 0)
                logger.LogInformation("Da dat mat khau dev cho {Count} tai khoan mau", placeholders.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Khong dat duoc mat khau dev cho tai khoan mau. Kiem tra ConnectionStrings:Default va SQL Server, roi khoi dong lai API.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
