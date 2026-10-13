using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GridSmart.Api.Controllers;

/// <summary>BE-06: dang nhap, refresh, dang xuat, thong tin ca nhan, doi mat khau, dang ky cong dan.</summary>
[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public sealed class AuthController(IAuthService auth, ICurrentUser currentUser) : ControllerBase
{
    public const string RefreshCookie = "gs_refresh";

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [EndpointSummary("Đăng nhập. Refresh token đặt trong cookie HttpOnly.")]
    public async Task<LoginResponse> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ClientIp, ct);
        SetRefreshCookie(result);
        return ToResponse(result);
    }

    [HttpPost("refresh")]
    [EndpointSummary("Cấp access token mới từ cookie refresh token (xoay vòng).")]
    public async Task<LoginResponse> Refresh(CancellationToken ct)
    {
        try
        {
            var result = await auth.RefreshAsync(Request.Cookies[RefreshCookie], ClientIp, ct);
            SetRefreshCookie(result);
            return ToResponse(result);
        }
        catch (DomainException)
        {
            ClearRefreshCookie();
            throw;
        }
    }

    [HttpPost("logout")]
    [EndpointSummary("Đăng xuất, thu hồi refresh token.")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookie], ct);
        ClearRefreshCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [EndpointSummary("Thông tin người dùng, vai trò, quyền và phạm vi.")]
    public Task<MeDto> Me(CancellationToken ct) => auth.GetMeAsync(currentUser.UserId!.Value, ct);

    [HttpPost("change-password")]
    [Authorize]
    [EndpointSummary("Đổi mật khẩu. Mọi phiên khác bị đăng xuất.")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(currentUser.UserId!.Value, request, ct);
        ClearRefreshCookie();
        return NoContent();
    }

    [HttpPost("register")]
    [EnableRateLimiting("login")]
    [EndpointSummary("Công dân tự đăng ký bằng số điện thoại.")]
    public async Task<LoginResponse> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterCitizenAsync(request, ClientIp, ct);
        SetRefreshCookie(result);
        return ToResponse(result);
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private static LoginResponse ToResponse(AuthResult r) => new(r.AccessToken, r.AccessTokenExpiresAt, r.User);

    private void SetRefreshCookie(AuthResult result) =>
        Response.Cookies.Append(RefreshCookie, result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Expires = result.RefreshTokenExpiresAt,
        });

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = "/api/auth", Secure = true, SameSite = SameSiteMode.Strict, HttpOnly = true });
}
