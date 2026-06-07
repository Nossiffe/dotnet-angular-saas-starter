using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Identity;

namespace Saas.Infrastructure.Identity;

public sealed class RefreshTokenStore(IAppDbContext db, IOptions<JwtOptions> options, IDateTimeProvider clock)
    : IRefreshTokenStore
{
    private readonly JwtOptions _o = options.Value;

    public async Task<string> IssueAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TenantId = tenantId,
            TokenHash = Hash(raw),
            ExpiresAt = clock.UtcNow.AddDays(_o.RefreshTokenDays),
        });
        await db.SaveChangesAsync(cancellationToken);
        return raw;
    }

    public async Task<RotatedToken?> ValidateAndRotateAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (existing is null || !existing.IsActive)
        {
            return null;
        }

        var newRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var replacement = new RefreshToken
        {
            UserId = existing.UserId,
            TenantId = existing.TenantId,
            TokenHash = Hash(newRaw),
            ExpiresAt = clock.UtcNow.AddDays(_o.RefreshTokenDays),
        };
        db.RefreshTokens.Add(replacement);

        existing.RevokedAt = clock.UtcNow;
        existing.ReplacedByTokenId = replacement.Id;

        await db.SaveChangesAsync(cancellationToken);
        return new RotatedToken(existing.UserId, existing.TenantId, newRaw);
    }

    private static string Hash(string raw)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
