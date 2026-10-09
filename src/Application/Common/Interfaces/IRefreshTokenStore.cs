namespace Saas.Application.Common.Interfaces;

public interface IRefreshTokenStore
{
    Task<(string RawToken, int Days)> IssueAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
    /// <summary>Utilisateur et tenant d'un refresh token actif (ni révoqué ni expiré), sans le consommer.</summary>
    Task<RefreshSession?> FindActiveAsync(string rawToken, CancellationToken cancellationToken);
    Task<RotatedToken?> ValidateAndRotateAsync(string rawToken, CancellationToken cancellationToken);

    /// <summary>Révoque un refresh token (logout). Idempotent : aucune erreur si déjà révoqué ou inconnu.</summary>
    Task RevokeAsync(string rawToken, CancellationToken cancellationToken);
    /// <summary>Révoque toutes les sessions actives d'un utilisateur (réutilisation détectée, mot de passe réinitialisé).</summary>
    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken);
    /// <summary>Révoque les sessions actives d'un utilisateur sur une organisation (il n'en est plus membre).</summary>
    Task RevokeTenantSessionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
}
