namespace GridSmart.Api.Interfaces;

/// <summary>Xac thuc va nap quyen (BE-06).</summary>
public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default);
    Task<AuthResult> RefreshAsync(string? rawRefreshToken, string? ip, CancellationToken ct = default);
    Task LogoutAsync(string? rawRefreshToken, CancellationToken ct = default);
    Task<MeDto> GetMeAsync(int userId, CancellationToken ct = default);
    Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task<AuthResult> RegisterCitizenAsync(RegisterRequest request, string? ip, CancellationToken ct = default);
    Task<(List<string> Roles, List<PermissionScope> Scopes)> LoadScopesAsync(int userId, CancellationToken ct = default);
}
