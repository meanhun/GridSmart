using System.Security.Claims;

namespace GridSmart.Api.Security;

/// <summary>Nguoi dung cua request, doc tu claim JWT (sub, role, perm).</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private IReadOnlyList<PermissionScope>? _scopes;

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public int? UserId => int.TryParse(Principal?.FindFirstValue(GridSmartClaims.Subject), out var id) ? id : null;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public IReadOnlyList<string> Roles => Principal?.FindAll(GridSmartClaims.Role).Select(c => c.Value).ToList() ?? [];

    public IReadOnlyList<PermissionScope> Scopes => _scopes ??= ClaimScopes.Read(Principal);

    public bool HasPermission(string permission) => Scopes.Any(s => s.Permission == permission);
}
