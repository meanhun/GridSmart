using GridSmart.Api.Data;

namespace GridSmart.Api.Services;

/// <summary>Quan ly vai tro va quyen (BE-08).</summary>
public sealed class RoleService(
    RolesDataAccess roles,
    RolePermissionsDataAccess rolePermissions,
    PermissionsDataAccess permissions,
    UserRolesDataAccess userRoles,
    UsersDataAccess users,
    TimeProvider clock) : IRoleService
{
    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct)
    {
        var rows = await roles.ReadAllAsync();
        var result = new List<RoleDto>(rows.Count);
        foreach (var r in rows.OrderBy(r => r.GetInt("id")))
        {
            var perms = await rolePermissions.ReadByRoleAsync(r.GetInt("id"));
            result.Add(new RoleDto(r.GetInt("id"), r.GetString("role_code"), r.GetString("role_name"), r.GetStringOrNull("description"),
                r.GetBool("is_system"), r.GetBool("is_active"),
                perms.Select(p => p.GetString("permission_code")).OrderBy(p => p).ToList()));
        }
        return result;
    }

    public async Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken ct)
    {
        var rows = await permissions.ReadAllAsync();
        return rows.OrderBy(p => p.GetString("module")).ThenBy(p => p.GetString("permission_code"))
            .Select(p => new PermissionDto(p.GetInt("id"), p.GetString("permission_code"), p.GetString("permission_name"), p.GetString("module")))
            .ToList();
    }

    public async Task<int> CreateAsync(RoleUpsertRequest request, CancellationToken ct)
    {
        Validate(request);
        var code = request.RoleCode.Trim().ToUpperInvariant();
        if (await roles.ReadByCodeAsync(code) is not null)
            throw DomainException.Conflict(ErrorCodes.Duplicate, "Mã vai trò đã tồn tại.");

        await using var tx = await ProcTransaction.BeginAsync();
        var roleId = (int)await roles.InsertAsync(new Row
        {
            ["role_code"] = code,
            ["role_name"] = request.RoleName.Trim(),
            ["description"] = request.Description,
            ["is_system"] = false,
            ["is_active"] = true,
            ["created_at"] = Now(),
        });
        if (request.Permissions is not null) await ApplyPermissionsAsync(roleId, request.Permissions);
        await tx.CommitAsync();
        return roleId;
    }

    public async Task UpdateAsync(int id, RoleUpsertRequest request, CancellationToken ct)
    {
        Validate(request);
        var role = await roles.ReadByIdAsync(id) ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy vai trò.");
        if (role.GetBool("is_system") && !string.Equals(role.GetString("role_code"), request.RoleCode.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new DomainException(ErrorCodes.ValidationFailed, "Không đổi mã của vai trò hệ thống.");

        role["role_code"] = request.RoleCode.Trim().ToUpperInvariant();
        role["role_name"] = request.RoleName.Trim();
        role["description"] = request.Description;

        await using var tx = await ProcTransaction.BeginAsync();
        await roles.UpdateAsync(role.Pick(Cols.Role));
        if (request.Permissions is not null) await ApplyPermissionsAsync(id, request.Permissions);
        await tx.CommitAsync();
    }

    public async Task SetPermissionsAsync(int id, IReadOnlyList<string> permissionCodes, CancellationToken ct)
    {
        _ = await roles.ReadByIdAsync(id) ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy vai trò.");
        await using var tx = await ProcTransaction.BeginAsync();
        await ApplyPermissionsAsync(id, permissionCodes);
        await tx.CommitAsync();
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var role = await roles.ReadByIdAsync(id) ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy vai trò.");
        if (role.GetBool("is_system")) throw DomainException.Conflict(ErrorCodes.Conflict, "Không thể xóa vai trò hệ thống.");
        // Chua co proc doc theo vai tro: loc tren SP_tbl_user_roles_ReadAll
        var inUse = (await userRoles.ReadAllAsync()).Any(ur => ur.GetInt("role_id") == id && ur.GetBool("is_active"));
        if (inUse)
            throw DomainException.Conflict(ErrorCodes.Conflict, "Vai trò đang được gán cho người dùng, hãy thu hồi trước.");
        role["is_active"] = false;
        await roles.UpdateAsync(role.Pick(Cols.Role));
    }

    /// <summary>Ghi lai bo quyen cua vai tro (proc phai nam trong ProcTransaction neu goi tu luong khac).</summary>
    private async Task ApplyPermissionsAsync(int roleId, IReadOnlyList<string> permissionCodes)
    {
        var known = (await permissions.ReadAllAsync())
            .ToDictionary(p => p.GetString("permission_code"), p => p.GetInt("id"), StringComparer.OrdinalIgnoreCase);
        var unknown = permissionCodes.Where(c => !known.ContainsKey(c)).ToList();
        if (unknown.Count > 0)
            throw new DomainException(ErrorCodes.ValidationFailed, $"Quyền không tồn tại: {string.Join(", ", unknown)}");

        var permissionIds = permissionCodes.Select(c => known[c]).Distinct().ToList();
        await rolePermissions.DeleteByRoleAsync(roleId);
        foreach (var permissionId in permissionIds)
            await rolePermissions.InsertAsync(roleId, permissionId);

        // Nguoi giu vai tro phai dang nhap lai de nhan quyen moi
        var holderIds = (await userRoles.ReadAllAsync())
            .Where(ur => ur.GetInt("role_id") == roleId && ur.GetBool("is_active"))
            .Select(ur => ur.GetInt("user_id"))
            .Distinct()
            .ToList();
        foreach (var userId in holderIds)
        {
            var user = await users.ReadByIdAsync(userId);
            if (user is null) continue;
            user["security_stamp"] = Guid.NewGuid();
            await users.UpdateAsync(user.Pick(Cols.User));
        }
    }

    private static void Validate(RoleUpsertRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.RoleCode) || r.RoleCode.Length > 30)
            throw new DomainException(ErrorCodes.ValidationFailed, "Mã vai trò bắt buộc, tối đa 30 ký tự.");
        if (string.IsNullOrWhiteSpace(r.RoleName) || r.RoleName.Length > 100)
            throw new DomainException(ErrorCodes.ValidationFailed, "Tên vai trò bắt buộc, tối đa 100 ký tự.");
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
