using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GridSmart.Api.Security;

/// <summary>Ten claim dung chung giua backend va frontend.</summary>
public static class GridSmartClaims
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Stamp = "stamp";
    public const string Role = "role";
    public const string Permission = "perm";
}

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public AccessToken CreateAccessToken(int userId, string fullName, Guid securityStamp, IEnumerable<string> roles, IEnumerable<PermissionScope> scopes)
    {
        var o = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(o.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(GridSmartClaims.Subject, userId.ToString()),
            new(GridSmartClaims.Name, fullName),
            new(GridSmartClaims.Stamp, securityStamp.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(roles.Select(r => new Claim(GridSmartClaims.Role, r)));
        claims.AddRange(scopes.Select(s => new Claim(GridSmartClaims.Permission, s.ToClaim())));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey));
        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, now, expires, new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string GenerateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));

    public string Hash(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
}
