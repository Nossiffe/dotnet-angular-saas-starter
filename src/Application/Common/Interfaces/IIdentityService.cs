namespace Saas.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<CreateUserResult> CreateUserAsync(string email, string password, CancellationToken cancellationToken);
    Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<AppUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Génère un token de confirmation d'email (Base64Url-encodé) pour le transport en lien.</summary>
    Task<string?> GenerateEmailConfirmationTokenAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Confirme l'email avec un token Base64Url-encodé. Renvoie false si l'user ou le token est invalide.</summary>
    Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken);

    /// <summary>Confirme directement l'email d'un user (sans transport de token), p.ex. invité acceptant via lien tokenisé. Renvoie false si l'user est introuvable.</summary>
    Task<bool> ConfirmEmailDirectlyAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Génère un token de reset de mot de passe. Renvoie null si l'email est inconnu (anti-énumération).</summary>
    Task<(Guid UserId, string Token)?> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken);

    /// <summary>Réinitialise le mot de passe avec un token Base64Url-encodé. Renvoie false si invalide.</summary>
    Task<bool> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken);
}
