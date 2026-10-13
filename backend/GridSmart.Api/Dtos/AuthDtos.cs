namespace GridSmart.Api.Dtos;

public sealed record LoginRequest(string Username, string Password);
public sealed record RegisterRequest(string Phone, string FullName, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ScopeDto(string Permission, int? UnitId, string? UnitPath, string? UnitName);
public sealed record MeDto(
    int Id, string? Username, string FullName, string? Phone, string? Email,
    IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions, IReadOnlyList<ScopeDto> Scopes);

/// <summary>Ket qua dang nhap. RefreshToken chi dung de dat cookie, khong tra trong body.</summary>
public sealed record AuthResult(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt, MeDto User);

/// <summary>Body tra ve khi dang nhap, refresh, dang ky.</summary>
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, MeDto User);
