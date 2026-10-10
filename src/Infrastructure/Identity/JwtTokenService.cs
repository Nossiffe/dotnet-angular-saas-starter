using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Identity;

public sealed class JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider clock) : ITokenService
{
    private readonly JwtOptions _o = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(
        Guid userId, string email, Guid tenantId, IEnumerable<string> roles, string tenantRole)
    {
        var now = clock.UtcNow;
        var expires = now.AddMinutes(_o.AccessTokenMinutes);

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()));
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Email, email));
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()));
        identity.AddClaim(new Claim("tenant", tenantId.ToString()));
        identity.AddClaim(new Claim("tenant_role", tenantRole));
        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _o.Issuer,
            Audience = _o.Audience,
            Subject = identity,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
