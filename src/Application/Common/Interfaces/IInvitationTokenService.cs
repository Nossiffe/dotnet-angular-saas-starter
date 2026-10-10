namespace Saas.Application.Common.Interfaces;

/// <summary>Génère/valide les tokens d'invitation (raw envoyé dans le lien, hash SHA-256 stocké).</summary>
public interface IInvitationTokenService
{
    string GenerateRawToken();
    string HashToken(string rawToken);
    /// <summary>Faux si aucun hash n'est stocké : aucun lien n'est alors valable.</summary>
    bool VerifyToken(string rawToken, string? storedHash);
}
