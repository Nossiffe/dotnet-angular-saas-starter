using Saas.Domain.Common;

namespace Saas.Domain.Identity;

public sealed class RefreshToken : BaseEntity
{
    public required Guid UserId { get; set; }
    public required Guid TenantId { get; set; }   // tenant actif rattaché à ce token
    public required string TokenHash { get; set; } // SHA-256 du token brut, jamais le brut
    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}
