using GridSmart.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GridSmart.Api.Services;

/// <summary>Dang nhap, refresh token, dang ky cong dan, nap quyen theo pham vi (BE-06).</summary>
public sealed class AuthService(
    UsersDataAccess users,
    UserRolesDataAccess userRoles,
    RolesDataAccess roles,
    RefreshTokensDataAccess refreshTokens,
    IncidentsDataAccess incidents,
    ITokenService tokens,
    IPasswordHasher<object> hasher,
    IOptions<AuthOptions> authOptions,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock) : IAuthService
{
    private const string InvalidMessage = "Tên đăng nhập hoặc mật khẩu không đúng.";
    private const string InvalidRefreshMessage = "Phiên đăng nhập đã hết hạn.";

    private sealed record ScopeData(List<string> Roles, List<PermissionScope> Scopes, Dictionary<int, string?> UnitNames);

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default)
    {
        var now = Now();
        var username = request.Username.Trim();
        var user = await users.ReadByUsernameAsync(username);
        if (user is null || user.GetStringOrNull("password_hash") is null || !user.GetBool("is_active"))
            throw new DomainException(ErrorCodes.InvalidCredentials, InvalidMessage, 401);

        if (user.GetDateTimeOrNull("locked_until") is { } until && until > now)
            throw new DomainException(ErrorCodes.AccountLocked, $"Tài khoản tạm khóa đến {until:yyyy-MM-ddTHH:mm:ssZ} do đăng nhập sai nhiều lần.", 423);

        var verify = hasher.VerifyHashedPassword(user, user.GetString("password_hash"), request.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            var failed = user.GetByte("failed_login_count") + 1;
            if (failed >= authOptions.Value.MaxFailedLogins)
            {
                user["locked_until"] = now.AddMinutes(authOptions.Value.LockoutMinutes);
                failed = 0;
            }
            user["failed_login_count"] = (byte)failed;
            await users.UpdateAsync(user.Pick(Cols.User));
            throw new DomainException(ErrorCodes.InvalidCredentials, InvalidMessage, 401);
        }

        if (verify == PasswordVerificationResult.SuccessRehashNeeded)
            user["password_hash"] = hasher.HashPassword(user, request.Password);

        user["failed_login_count"] = (byte)0;
        user["locked_until"] = null;
        user["last_login_at"] = now;
        await users.UpdateAsync(user.Pick(Cols.User));
        return await IssueAsync(user, ip, null, ct);
    }

    public async Task<AuthResult> RefreshAsync(string? rawRefreshToken, string? ip, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rawRefreshToken))
            throw new DomainException(ErrorCodes.InvalidRefreshToken, InvalidRefreshMessage, 401);

        var now = Now();
        var token = await refreshTokens.ReadByHashAsync(tokens.Hash(rawRefreshToken))
                    ?? throw new DomainException(ErrorCodes.InvalidRefreshToken, InvalidRefreshMessage, 401);

        if (token.GetDateTimeOrNull("revoked_at") is not null)
        {
            // Token da xoay vong bi dung lai: co the bi danh cap, thu hoi toan bo phien cua nguoi dung
            if (token.GetLongOrNull("replaced_by_token_id") is not null)
            {
                await refreshTokens.RevokeByUserAsync(token.GetInt("user_id"), now);
                throw new DomainException(ErrorCodes.TokenReused, "Phát hiện phiên đăng nhập bất thường. Vui lòng đăng nhập lại.", 401);
            }
            throw new DomainException(ErrorCodes.InvalidRefreshToken, InvalidRefreshMessage, 401);
        }

        var userId = token.GetInt("user_id");
        if (token.GetDateTime("expires_at") <= now || !token.GetBool("user_is_active")
            || token.GetGuid("security_stamp") != token.GetGuid("user_security_stamp"))
        {
            var revoked = token.Pick(Cols.RefreshToken);
            revoked["revoked_at"] = now;
            await refreshTokens.UpdateAsync(revoked);
            throw new DomainException(ErrorCodes.InvalidRefreshToken, "Phiên đăng nhập đã hết hạn hoặc quyền đã thay đổi. Vui lòng đăng nhập lại.", 401);
        }

        var user = await users.ReadByIdAsync(userId)
                   ?? throw new DomainException(ErrorCodes.InvalidRefreshToken, InvalidRefreshMessage, 401);
        return await IssueAsync(user, ip, token, ct);
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rawRefreshToken)) return;
        var token = await refreshTokens.ReadByHashAsync(tokens.Hash(rawRefreshToken));
        if (token is not null && token.GetDateTimeOrNull("revoked_at") is null)
        {
            var revoked = token.Pick(Cols.RefreshToken);
            revoked["revoked_at"] = Now();
            await refreshTokens.UpdateAsync(revoked);
        }
    }

    public async Task<MeDto> GetMeAsync(int userId, CancellationToken ct = default)
    {
        var user = await users.ReadByIdAsync(userId)
                   ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy người dùng.");
        return BuildMe(user, await LoadAsync(userId));
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        PasswordPolicy.Ensure(request.NewPassword);
        var user = await users.ReadByIdAsync(userId)
                   ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy người dùng.");
        var currentHash = user.GetStringOrNull("password_hash");
        if (currentHash is null || hasher.VerifyHashedPassword(user, currentHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new DomainException(ErrorCodes.InvalidCredentials, "Mật khẩu hiện tại không đúng.", 400);

        var now = Now();
        user["password_hash"] = hasher.HashPassword(user, request.NewPassword);
        user["security_stamp"] = Guid.NewGuid();
        user["updated_at"] = now;

        await using var tx = await ProcTransaction.BeginAsync();
        await users.UpdateAsync(user.Pick(Cols.User));
        await refreshTokens.RevokeByUserAsync(userId, now);
        await tx.CommitAsync();
    }

    public async Task<AuthResult> RegisterCitizenAsync(RegisterRequest request, string? ip, CancellationToken ct = default)
    {
        var phone = PhoneNumber.NormalizeOrThrow(request.Phone)
                    ?? throw new DomainException(ErrorCodes.ValidationFailed, "Cần nhập số điện thoại.");
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new DomainException(ErrorCodes.ValidationFailed, "Cần nhập họ tên.");
        PasswordPolicy.Ensure(request.Password);

        if (await users.ReadByUsernameAsync(phone) is not null)
            throw DomainException.Conflict(ErrorCodes.Duplicate, "Số điện thoại đã được đăng ký.");

        var citizenRole = await roles.ReadByCodeAsync(RoleCodes.Citizen)
                          ?? throw new InvalidOperationException("Thiếu vai trò CITIZEN trong cơ sở dữ liệu.");
        var now = Now();
        var user = new Row
        {
            ["username"] = phone,
            ["password_hash"] = null,
            ["full_name"] = request.FullName.Trim(),
            ["phone"] = phone,
            ["email"] = null,
            ["is_active"] = true,
            ["security_stamp"] = Guid.NewGuid(),
            ["failed_login_count"] = (byte)0,
            ["locked_until"] = null,
            ["last_login_at"] = null,
            ["created_at"] = now,
            ["updated_at"] = null,
        };
        user["password_hash"] = hasher.HashPassword(user, request.Password);

        await using var tx = await ProcTransaction.BeginAsync();
        var userId = (int)await users.InsertAsync(user);
        user["id"] = userId;
        await userRoles.InsertAsync(new Row
        {
            ["user_id"] = userId,
            ["role_id"] = citizenRole.GetInt("id"),
            ["admin_unit_id"] = null,
            ["assigned_by_user_id"] = null,
            ["assigned_at"] = now,
            ["expires_at"] = null,
            ["is_active"] = true,
        });
        // Gan cac phan anh an danh truoc do cung so dien thoai cho tai khoan moi (cung giao dich)
        await incidents.LinkReporterByPhoneAsync(phone, userId);
        var result = await IssueAsync(user, ip, null, ct);
        await tx.CommitAsync();
        return result;
    }

    public async Task<(List<string> Roles, List<PermissionScope> Scopes)> LoadScopesAsync(int userId, CancellationToken ct = default)
    {
        var data = await LoadAsync(userId);
        return (data.Roles, data.Scopes);
    }

    /// <summary>Doc vai tro va quyen (proc SP_Auth_LoadScopes); da loc user/vai tro/vai tro active va chua het han.</summary>
    private async Task<ScopeData> LoadAsync(int userId)
    {
        var rows = await userRoles.LoadScopesAsync(userId);

        var roleCodes = rows.Select(r => r.GetStringOrNull("role_code")).OfType<string>()
            .Distinct().OrderBy(r => r).ToList();

        var scopes = rows
            .Where(r => r.GetStringOrNull("permission_code") is not null)
            .Select(r => new PermissionScope(
                r.GetString("permission_code"),
                r.GetIntOrNull("admin_unit_id") is null ? null : r.GetStringOrNull("unit_path")))
            .Distinct()
            .ToList();

        // Da co pham vi toan he thong cho mot quyen thi bo cac pham vi hep hon cua quyen do
        var globalPerms = scopes.Where(s => s.IsGlobal).Select(s => s.Permission).ToHashSet();
        scopes = scopes.Where(s => s.IsGlobal || !globalPerms.Contains(s.Permission))
            .OrderBy(s => s.Permission).ThenBy(s => s.UnitPath).ToList();

        var unitNames = new Dictionary<int, string?>();
        foreach (var r in rows)
        {
            if (r.GetIntOrNull("admin_unit_id") is { } unitId) unitNames.TryAdd(unitId, r.GetStringOrNull("unit_name"));
        }
        return new ScopeData(roleCodes, scopes, unitNames);
    }

    /// <summary>Cap token moi. rotateFrom la token cu (da doc bang ReadByHash) can xoay vong; null khi dang nhap moi.</summary>
    private async Task<AuthResult> IssueAsync(Row user, string? ip, Row? rotateFrom, CancellationToken ct)
    {
        var now = Now();
        var userId = user.GetInt("id");
        var data = await LoadAsync(userId);
        var securityStamp = user.GetGuid("security_stamp");
        var access = tokens.CreateAccessToken(userId, user.GetString("full_name"), securityStamp, data.Roles, data.Scopes);

        var raw = tokens.GenerateRefreshToken();
        var expiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays);
        var newTokenId = await refreshTokens.InsertAsync(new Row
        {
            ["user_id"] = userId,
            ["token_hash"] = tokens.Hash(raw),
            ["expires_at"] = expiresAt,
            ["created_at"] = now,
            ["created_by_ip"] = ip,
            ["revoked_at"] = null,
            ["replaced_by_token_id"] = null,
            ["security_stamp"] = securityStamp,
        });

        if (rotateFrom is not null)
        {
            var old = rotateFrom.Pick(Cols.RefreshToken);
            old["revoked_at"] = now;
            old["replaced_by_token_id"] = newTokenId;
            await refreshTokens.UpdateAsync(old);
        }

        return new AuthResult(access.Token, access.ExpiresAt, raw, expiresAt, BuildMe(user, data));
    }

    private static MeDto BuildMe(Row user, ScopeData data)
    {
        var scopeDtos = data.Scopes.Select(s =>
        {
            var id = UnitHierarchy.UnitIdFromPath(s.UnitPath);
            return new ScopeDto(s.Permission, id, s.UnitPath, id is { } i && data.UnitNames.TryGetValue(i, out var n) ? n : null);
        }).ToList();
        return new MeDto(user.GetInt("id"), user.GetStringOrNull("username"), user.GetString("full_name"),
            user.GetStringOrNull("phone"), user.GetStringOrNull("email"), data.Roles,
            data.Scopes.Select(s => s.Permission).Distinct().OrderBy(p => p).ToList(), scopeDtos);
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
