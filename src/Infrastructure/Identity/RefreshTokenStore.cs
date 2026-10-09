using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Persistence;

namespace Saas.Infrastructure.Identity;

public sealed class RefreshTokenStore(AppDbContext db, IOptions<JwtOptions> options, IDateTimeProvider clock)
    : IRefreshTokenStore
{
    private readonly JwtOptions _o = options.Value;

    public async Task<(string RawToken, int Days)> IssueAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
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
        return (raw, _o.RefreshTokenDays);
    }

    public async Task<RotatedToken?> ValidateAndRotateAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        // Reuse detection: already-revoked token presented → theft signal → revoke all active tokens for user
        if (existing is not null && existing.RevokedAt is not null)
        {
            await RevokeAllAsync(existing.UserId, cancellationToken);
            return null;
        }

        // Token not found or expired
        if (existing is null || clock.UtcNow >= existing.ExpiresAt)
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

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return null;
        }

        return new RotatedToken(existing.UserId, existing.TenantId, newRaw, _o.RefreshTokenDays);
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.RevokedAt, now), cancellationToken);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        // Idempotent : token inconnu ou déjà révoqué → rien à faire.
        if (existing is null || existing.RevokedAt is not null)
        {
            return;
        }

        existing.RevokedAt = clock.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Déjà révoqué par une requête concurrente — succès idempotent.
        }
    }

    private static string Hash(string raw) => TokenHash.Sha256(raw);
}
