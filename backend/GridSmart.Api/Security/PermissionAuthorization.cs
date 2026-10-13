using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace GridSmart.Api.Security;

/// <summary>
/// Lop 1 cua phan quyen (quyen chuc nang). Dat tren Controller hoac action:
/// <c>[HasPermission(Permissions.IncidentDispatch)]</c>. Lop 2 (pham vi don vi) kiem tra trong service.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute(PermissionPolicyProvider.Prefix + permission);

/// <summary>Doc cac claim "perm" (dang "incident.view@/1/2/") thanh danh sach pham vi.</summary>
public static class ClaimScopes
{
    public static IReadOnlyList<PermissionScope> Read(ClaimsPrincipal? principal) =>
        principal?.FindAll(GridSmartClaims.Permission).Select(c => PermissionScope.FromClaim(c.Value)).OfType<PermissionScope>().ToList() ?? [];
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Dat yeu cau khi nguoi dung co quyen o bat ky pham vi nao.</summary>
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (ClaimScopes.Read(context.User).Any(s => s.Permission == requirement.Permission)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>Sinh policy dong cho moi ten "perm:xxx", khong can khai bao truoc.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "perm:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal)) return await base.GetPolicyAsync(policyName);
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[Prefix.Length..]))
            .Build();
    }
}
