namespace Saas.Infrastructure.Identity;

// Session de rafraîchissement persistée : un détail d'authentification, pas un concept métier.
public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required Guid UserId { get; set; }
    public required Guid TenantId { get; set; }   // tenant actif rattaché à ce token
    public required string TokenHash { get; set; } // SHA-256 du token brut, jamais le brut
    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    // Colonne système xmin de PostgreSQL : jeton de concurrence optimiste.
    public uint Version { get; set; }
}
