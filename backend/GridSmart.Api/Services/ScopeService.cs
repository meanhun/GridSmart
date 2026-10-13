using Microsoft.Extensions.Logging;

namespace GridSmart.Api.Services;

/// <summary>Lop 2 cua phan quyen: kiem tra pham vi theo cay hanh chinh.</summary>
public sealed class ScopeService(ICurrentUser currentUser, ILogger<ScopeService> logger) : IScopeService
{
    public static bool PathMatches(string? scopePath, string? unitPath) =>
        scopePath is null || (unitPath is not null && unitPath.StartsWith(scopePath, StringComparison.Ordinal));

    /// <summary>Kiem tra unitPath co nam trong danh sach scopePaths ("a,b,c") hay khong; null = toan he thong.</summary>
    public static bool MatchesScopePaths(string? scopePaths, string? unitPath) =>
        scopePaths is null || (unitPath is not null &&
            scopePaths.Split(',', StringSplitOptions.RemoveEmptyEntries).Any(p => unitPath.StartsWith(p, StringComparison.Ordinal)));

    public bool CanAccess(string permission, string? unitPath) =>
        currentUser.Scopes.Any(s => s.Permission == permission && PathMatches(s.UnitPath, unitPath));

    public void EnsureAccess(string permission, string? unitPath, string resource)
    {
        if (CanAccess(permission, unitPath)) return;
        logger.LogWarning("Tu choi truy cap: user {UserId} quyen {Permission} doi tuong {Resource} path {UnitPath}",
            currentUser.UserId, permission, resource, unitPath);
        throw DomainException.Forbidden(ErrorCodes.OutOfScope, "Đối tượng nằm ngoài phạm vi quản lý của bạn.");
    }

    public ScopeFilter GetFilter(string permission)
    {
        var scopes = currentUser.Scopes.Where(s => s.Permission == permission).ToList();
        if (scopes.Any(s => s.IsGlobal)) return new ScopeFilter(true, []);
        return new ScopeFilter(false, scopes.Select(s => s.UnitPath!).Distinct().ToList());
    }

    public string? ScopePaths(string permission)
    {
        var scopes = currentUser.Scopes.Where(s => s.Permission == permission).ToList();
        if (scopes.Any(s => s.IsGlobal)) return null;
        return string.Join(",", scopes.Select(s => s.UnitPath!).Distinct());
    }
}
