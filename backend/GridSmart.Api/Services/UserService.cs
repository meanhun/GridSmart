using GridSmart.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace GridSmart.Api.Services;

/// <summary>Quan ly nguoi dung va gan vai tro theo pham vi, chong leo quyen (BE-08).</summary>
public sealed class UserService(
    UsersDataAccess users,
    UserRolesDataAccess userRoles,
    RolesDataAccess roles,
    RolePermissionsDataAccess rolePermissions,
    AdminUnitsDataAccess adminUnits,
    RefreshTokensDataAccess refreshTokens,
    ICurrentUser currentUser,
    IScopeService scope,
    IPasswordHasher<object> hasher,
    TimeProvider clock) : IUserService
{
    private static readonly DateTime ManualLock = new(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc);
    private const string NotFoundVisible = "Không tìm thấy người dùng trong phạm vi của bạn.";
    private const string NotFoundManageable = "Không tìm thấy người dùng trong phạm vi quản lý của bạn.";

    public async Task<PagedResult<UserListItem>> ListAsync(string? q, string? roleCode, int? unitId, bool? locked, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, s) = Paging.Normalize(page, pageSize);
        var scopePaths = scope.ScopePaths(Permissions.UserView);
        if (scopePaths == "") return new PagedResult<UserListItem>([], p, s, 0);

        string? unitPath = null;
        if (unitId is { } uid)
            unitPath = (await adminUnits.ReadByIdAsync(uid))?.GetStringOrNull("unit_path") ?? "";

        var (items, total) = await users.ListAsync(p, s,
            string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            string.IsNullOrWhiteSpace(roleCode) ? null : roleCode.Trim(),
            unitPath, scopePaths, locked);

        return new PagedResult<UserListItem>(items.Select(ToListItem).ToList(), p, s, total);
    }

    public async Task<UserDetail> GetAsync(int id, CancellationToken ct)
    {
        var u = await LoadVisibleAsync(id, Permissions.UserView, NotFoundVisible);
        return new UserDetail(id, u.GetStringOrNull("username"), u.GetString("full_name"), u.GetStringOrNull("phone"), u.GetStringOrNull("email"),
            u.GetBool("is_active"), u.GetDateTimeOrNull("locked_until"), u.GetDateTimeOrNull("last_login_at"), u.GetDateTime("created_at"),
            await GetRolesInternalAsync(id));
    }

    public async Task<int> CreateAsync(UserCreateRequest request, CancellationToken ct)
    {
        if (!currentUser.HasPermission(Permissions.UserManage))
            throw DomainException.Forbidden(ErrorCodes.OutOfScope, "Bạn không có quyền tạo người dùng.");
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 50)
            throw new DomainException(ErrorCodes.ValidationFailed, "Tên đăng nhập bắt buộc, tối đa 50 ký tự.");
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new DomainException(ErrorCodes.ValidationFailed, "Cần nhập họ tên.");
        PasswordPolicy.Ensure(request.Password);
        var username = request.Username.Trim();
        if (await users.ReadByUsernameAsync(username) is not null)
            throw DomainException.Conflict(ErrorCodes.Duplicate, "Tên đăng nhập đã tồn tại.");

        var row = new Row
        {
            ["username"] = username,
            ["password_hash"] = null,
            ["full_name"] = request.FullName.Trim(),
            ["phone"] = PhoneNumber.NormalizeOrThrow(request.Phone),
            ["email"] = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            ["is_active"] = true,
            ["security_stamp"] = Guid.NewGuid(),
            ["failed_login_count"] = (byte)0,
            ["locked_until"] = null,
            ["last_login_at"] = null,
            ["created_at"] = Now(),
            ["updated_at"] = null,
        };
        row["password_hash"] = hasher.HashPassword(row, request.Password);
        return (int)await users.InsertAsync(row);
    }

    public async Task UpdateAsync(int id, UserUpdateRequest request, CancellationToken ct)
    {
        var user = await LoadVisibleAsync(id, Permissions.UserManage, NotFoundManageable);
        if (string.IsNullOrWhiteSpace(request.FullName)) throw new DomainException(ErrorCodes.ValidationFailed, "Cần nhập họ tên.");
        user["full_name"] = request.FullName.Trim();
        user["phone"] = PhoneNumber.NormalizeOrThrow(request.Phone);
        user["email"] = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (request.IsActive is { } active && active != user.GetBool("is_active"))
        {
            if (id == currentUser.UserId && !active) throw new DomainException(ErrorCodes.ValidationFailed, "Không thể tự vô hiệu hóa tài khoản của mình.");
            user["is_active"] = active;
            user["security_stamp"] = Guid.NewGuid();
        }
        user["updated_at"] = Now();
        await users.UpdateAsync(user.Pick(Cols.User));
    }

    public async Task LockAsync(int id, bool locked, CancellationToken ct)
    {
        var user = await LoadVisibleAsync(id, Permissions.UserManage, NotFoundManageable);
        if (locked && id == currentUser.UserId) throw new DomainException(ErrorCodes.ValidationFailed, "Không thể tự khóa tài khoản của mình.");
        var now = Now();
        DateTime? lockedUntil = locked ? ManualLock : null;
        user["locked_until"] = lockedUntil;
        user["failed_login_count"] = (byte)0;
        user["security_stamp"] = Guid.NewGuid();
        user["updated_at"] = now;

        await using var tx = await ProcTransaction.BeginAsync();
        await users.UpdateAsync(user.Pick(Cols.User));
        if (locked) await refreshTokens.RevokeByUserAsync(id, now);
        await tx.CommitAsync();
    }

    public async Task ResetPasswordAsync(int id, ResetPasswordRequest request, CancellationToken ct)
    {
        PasswordPolicy.Ensure(request.NewPassword);
        var user = await LoadVisibleAsync(id, Permissions.UserManage, NotFoundManageable);
        var now = Now();
        user["password_hash"] = hasher.HashPassword(user, request.NewPassword);
        user["security_stamp"] = Guid.NewGuid();
        user["failed_login_count"] = (byte)0;
        user["locked_until"] = null;
        user["updated_at"] = now;

        await using var tx = await ProcTransaction.BeginAsync();
        await users.UpdateAsync(user.Pick(Cols.User));
        await refreshTokens.RevokeByUserAsync(id, now);
        await tx.CommitAsync();
    }

    public async Task<IReadOnlyList<UserRoleDto>> GetRolesAsync(int id, CancellationToken ct)
    {
        await LoadVisibleAsync(id, Permissions.UserView, NotFoundVisible);
        return await GetRolesInternalAsync(id);
    }

    /// <summary>Chong leo quyen: chi gan trong pham vi user.manage cua minh, va vai tro khong vuot quyen cua minh tai don vi do.</summary>
    public async Task<int> AssignRoleAsync(int id, AssignRoleRequest request, CancellationToken ct)
    {
        var user = await users.ReadByIdAsync(id)
                   ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy người dùng.");
        var role = await roles.ReadByIdAsync(request.RoleId);
        if (role is null || !role.GetBool("is_active"))
            throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy vai trò.");
        var roleId = role.GetInt("id");

        string? unitPath = null;
        if (request.AdminUnitId is { } uid)
        {
            var unit = await adminUnits.ReadByIdAsync(uid);
            if (unit is null || !unit.GetBool("is_active"))
                throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị hành chính.");
            unitPath = unit.GetStringOrNull("unit_path")
                       ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị hành chính.");
        }
        if (!scope.CanAccess(Permissions.UserManage, unitPath))
            throw DomainException.Forbidden(ErrorCodes.PrivilegeEscalation, "Bạn không thể gán vai trò hoặc phạm vi vượt quyền của mình.");
        var rolePerms = await rolePermissions.ReadByRoleAsync(roleId);
        if (rolePerms.Any(rp => !scope.CanAccess(rp.GetString("permission_code"), unitPath)))
            throw DomainException.Forbidden(ErrorCodes.PrivilegeEscalation, "Bạn không thể gán vai trò hoặc phạm vi vượt quyền của mình.");
        if (request.ExpiresAt is { } exp && exp <= Now())
            throw new DomainException(ErrorCodes.ValidationFailed, "Hạn dùng phải ở tương lai.");

        var existing = (await userRoles.ReadByUserAsync(id))
            .FirstOrDefault(ur => ur.GetInt("role_id") == roleId && ur.GetIntOrNull("admin_unit_id") == request.AdminUnitId);
        if (existing is { } ex && ex.GetBool("is_active") && ex.GetDateTimeOrNull("expires_at") == request.ExpiresAt)
            throw DomainException.Conflict(ErrorCodes.Duplicate, "Người dùng đã có vai trò này tại đơn vị này.");

        var now = Now();
        await using var tx = await ProcTransaction.BeginAsync();
        int userRoleId;
        if (existing is null)
        {
            userRoleId = (int)await userRoles.InsertAsync(new Row
            {
                ["user_id"] = id,
                ["role_id"] = roleId,
                ["admin_unit_id"] = request.AdminUnitId,
                ["assigned_by_user_id"] = currentUser.UserId,
                ["assigned_at"] = now,
                ["expires_at"] = request.ExpiresAt,
                ["is_active"] = true,
            });
        }
        else
        {
            userRoleId = existing.GetInt("id");
            var ur = await userRoles.ReadByIdAsync(userRoleId)
                     ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy vai trò của người dùng.");
            ur["is_active"] = true;
            ur["expires_at"] = request.ExpiresAt;
            ur["assigned_at"] = now;
            ur["assigned_by_user_id"] = currentUser.UserId;
            await userRoles.UpdateAsync(ur.Pick(Cols.UserRole));
        }

        user["security_stamp"] = Guid.NewGuid();
        user["updated_at"] = now;
        await users.UpdateAsync(user.Pick(Cols.User));
        await tx.CommitAsync();
        return userRoleId;
    }

    public async Task RevokeRoleAsync(int id, int userRoleId, CancellationToken ct)
    {
        var assignment = (await userRoles.ReadByUserAsync(id)).FirstOrDefault(x => x.GetInt("id") == userRoleId)
                         ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy vai trò của người dùng.");
        if (!scope.CanAccess(Permissions.UserManage, assignment.GetStringOrNull("unit_path")))
            throw DomainException.Forbidden(ErrorCodes.PrivilegeEscalation, "Bạn không thể thu hồi vai trò ngoài phạm vi của mình.");
        if (id == currentUser.UserId && assignment.GetIntOrNull("admin_unit_id") is null)
            throw new DomainException(ErrorCodes.ValidationFailed, "Không thể tự thu hồi vai trò toàn hệ thống của mình.");

        var ur = await userRoles.ReadByIdAsync(userRoleId)
                 ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy vai trò của người dùng.");
        var user = await users.ReadByIdAsync(id)
                   ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy người dùng.");
        var now = Now();
        ur["is_active"] = false;
        user["security_stamp"] = Guid.NewGuid();
        user["updated_at"] = now;

        await using var tx = await ProcTransaction.BeginAsync();
        await userRoles.UpdateAsync(ur.Pick(Cols.UserRole));
        await users.UpdateAsync(user.Pick(Cols.User));
        await tx.CommitAsync();
    }

    /// <summary>
    /// Nguoi dung nhin thay: co pham vi toan he thong thi tat ca; neu khong thi nguoi co vai tro
    /// dang hoat dong (gan don vi) trong pham vi. Nem NotFound neu khong thay.
    /// </summary>
    private async Task<Row> LoadVisibleAsync(int id, string permission, string notFoundMessage)
    {
        var scopePaths = scope.ScopePaths(permission);
        if (scopePaths == "") throw DomainException.NotFound(ErrorCodes.NotFound, notFoundMessage);

        var user = await users.ReadByIdAsync(id)
                   ?? throw DomainException.NotFound(ErrorCodes.NotFound, notFoundMessage);
        if (scopePaths is null) return user;

        var assignments = await userRoles.ReadByUserAsync(id);
        var visible = assignments.Any(ur => ur.GetBool("is_active")
                                            && ur.GetIntOrNull("admin_unit_id") is not null
                                            && ScopeService.MatchesScopePaths(scopePaths, ur.GetStringOrNull("unit_path")));
        if (!visible) throw DomainException.NotFound(ErrorCodes.NotFound, notFoundMessage);
        return user;
    }

    private async Task<List<UserRoleDto>> GetRolesInternalAsync(int id)
    {
        var rows = await userRoles.ReadByUserAsync(id);
        return rows
            .OrderByDescending(r => r.GetBool("is_active")).ThenBy(r => r.GetString("role_code"))
            .Select(r => new UserRoleDto(r.GetInt("id"), r.GetInt("role_id"), r.GetString("role_code"), r.GetString("role_name"),
                r.GetIntOrNull("admin_unit_id"), r.GetStringOrNull("unit_name"), r.GetStringOrNull("unit_path"),
                r.GetIntOrNull("assigned_by_user_id"), r.GetStringOrNull("assigned_by_name"),
                r.GetDateTime("assigned_at"), r.GetDateTimeOrNull("expires_at"), r.GetBool("is_active")))
            .ToList();
    }

    private static UserListItem ToListItem(Row u) => new(
        u.GetInt("id"), u.GetStringOrNull("username"), u.GetString("full_name"), u.GetStringOrNull("phone"), u.GetStringOrNull("email"),
        u.GetBool("is_active"), u.GetDateTimeOrNull("locked_until"), u.GetDateTimeOrNull("last_login_at"),
        SplitRoleSummary(u.GetStringOrNull("role_summary")));

    /// <summary>role_summary dang 'CODE@ten don vi, CODE2@*'.</summary>
    private static IReadOnlyList<string> SplitRoleSummary(string? summary) =>
        string.IsNullOrWhiteSpace(summary)
            ? []
            : summary.Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
