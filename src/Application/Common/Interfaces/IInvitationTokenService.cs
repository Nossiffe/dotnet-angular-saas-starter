namespace Saas.Application.Common.Interfaces;

/// <summary>Génère/valide les tokens d'invitation (raw envoyé dans le lien, hash SHA-256 stocké).</summary>
public interface IInvitationTokenService
{
    string GenerateRawToken();
    string HashToken(string rawToken);
    bool VerifyToken(string rawToken, string storedHash);
}
