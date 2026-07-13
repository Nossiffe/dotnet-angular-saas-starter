namespace Saas.Application.Common.Interfaces;

public interface IRefreshTokenStore
{
    Task<(string RawToken, int Days)> IssueAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
    Task<RotatedToken?> ValidateAndRotateAsync(string rawToken, CancellationToken cancellationToken);

    /// <summary>Révoque un refresh token (logout). Idempotent : aucune erreur si déjà révoqué ou inconnu.</summary>
    Task RevokeAsync(string rawToken, CancellationToken cancellationToken);
}
